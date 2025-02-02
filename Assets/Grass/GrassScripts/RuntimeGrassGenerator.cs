using System.Collections.Generic;
using UnityEngine;

public class RuntimeGrassGenerator : MonoBehaviour
{
    [SerializeField] private GrassComputeScript grassCompute;
    [SerializeField] private GrassToolSettingSo toolSettings;
    [SerializeField] private GameObject[] targetObjects;

    private class GenerationData
    {
        public float totalArea;
        public int targetGrassCount;
        public List<GrassPlacementData> validPositions;

        public float[,,] splatmapData;
        public TerrainData terrainData;
        public int alphamapWidth;
        public int alphamapHeight;
        public Vector3 terrainPosition;
        public Vector3 terrainSize;

        // Mesh specific data
        public List<Triangle> triangles;
    }

    private struct Triangle
    {
        public readonly Vector3[] vertices;
        public readonly Vector3 normal;
        public readonly float area;

        public Triangle(Vector3[] vertices, Vector3 normal, float area)
        {
            this.vertices = vertices;
            this.normal = normal;
            this.area = area;
        }
    }

    private struct GrassPlacementData
    {
        public Vector3 position;
        public Vector3 normal;
        public float widthScale;
        public float heightScale;
    }

    private void Awake()
    {
        if (targetObjects == null || targetObjects.Length == 0 || !grassCompute || !toolSettings)
        {
            Debug.LogWarning("Required components are missing!");
            return;
        }

        ClearGrass();
        GenerateGrass();
        grassCompute.Reset();
    }

    [ContextMenu("Preview Grass")]
    private void PreviewGrass()
    {
        ClearGrass();
        GenerateGrass();
        grassCompute.Reset();
    }

    private void ClearGrass()
    {
        if (grassCompute.GrassDataList.Count <= 0) return;

        grassCompute.GrassDataList = new List<GrassData>();
    }

    private void GenerateGrass()
    {
        var generationDataList = new List<GenerationData>();
        var totalArea = 0f;

        // Collect mesh data
        foreach (var obj in targetObjects)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                var meshData = CollectMeshData(meshFilter);
                if (meshData == null) continue;

                generationDataList.Add(meshData);
                totalArea += meshData.totalArea;
            }
            else if (obj.TryGetComponent(out Terrain terrain))
            {
                var terrainData = CollectTerrainData(terrain);
                if (terrainData == null) continue;

                generationDataList.Add(terrainData);
                totalArea += terrainData.totalArea;
            }
        }

        if (generationDataList.Count == 0)
        {
            Debug.LogWarning("No valid data found for grass generation!");
            return;
        }

        // Calculate total target grass count and allocate
        var maxGrassForArea = Mathf.FloorToInt(totalArea / (toolSettings.GrassSpacing * toolSettings.GrassSpacing));
        var totalTargetGrassCount = Mathf.Min(toolSettings.GenerateGrassCount, maxGrassForArea);

        // Distribute grass count based on area ratios
        foreach (var data in generationDataList)
        {
            data.targetGrassCount = Mathf.FloorToInt(data.totalArea / totalArea * totalTargetGrassCount);
        }

        // Generate grass positions for each surface
        foreach (var data in generationDataList)
        {
            if (data.triangles != null)
            {
                GenerateMeshGrassPositions(data);
            }
            else if (data.terrainData != null)
            {
                GenerateTerrainGrassPositions(data);
            }
        }

        // Collect and apply all grass data
        var allGrassData = new List<GrassData>();

        foreach (var data in generationDataList)
        {
            foreach (var placement in data.validPositions)
            {
                var grassData = new GrassData
                {
                    position = placement.position,
                    normal = placement.normal,
                    color = GetRandomColor(),
                    widthHeight = new Vector2(
                        toolSettings.GrassWidth * placement.widthScale,
                        toolSettings.GrassHeight * placement.heightScale
                    )
                };

                allGrassData.Add(grassData);
            }
        }

        if (allGrassData.Count > 0)
        {
            grassCompute.GrassDataList.AddRange(allGrassData);
        }
    }

    private GenerationData CollectMeshData(MeshFilter meshFilter)
    {
        var data = new GenerationData
        {
            triangles = new List<Triangle>(),
            validPositions = new List<GrassPlacementData>()
        };

        var mesh = meshFilter.sharedMesh;
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var normals = mesh.normals;
        var meshFilterTransform = meshFilter.transform;

        for (var i = 0; i < triangles.Length; i += 3)
        {
            var v1 = meshFilterTransform.TransformPoint(vertices[triangles[i]]);
            var v2 = meshFilterTransform.TransformPoint(vertices[triangles[i + 1]]);
            var v3 = meshFilterTransform.TransformPoint(vertices[triangles[i + 2]]);

            var normal = meshFilterTransform.TransformDirection(
                (normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f
            ).normalized;

            var area = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;

            var surfaceAngle = toolSettings.allowUndersideGrass
                ? Mathf.Acos(Mathf.Abs(normal.y)) * Mathf.Rad2Deg
                : Mathf.Acos(normal.y) * Mathf.Rad2Deg;

            if (surfaceAngle <= toolSettings.NormalLimit * 90.01f)
            {
                var triangle = new Triangle
                (
                    new[] { v1, v2, v3 }, normal, area
                );
                data.triangles.Add(triangle);
                data.totalArea += area;
            }
        }

        return data.totalArea > 0 ? data : null;
    }

    private GenerationData CollectTerrainData(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null) return null;

        var terrainData = terrain.terrainData;
        var alphamapWidth = terrainData.alphamapWidth;
        var alphamapHeight = terrainData.alphamapHeight;

        return new GenerationData
        {
            terrainData = terrainData,
            splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight),
            alphamapWidth = alphamapWidth,
            alphamapHeight = alphamapHeight,
            terrainPosition = terrain.transform.position,
            terrainSize = terrainData.size,
            totalArea = terrainData.size.x * terrainData.size.z,
            validPositions = new List<GrassPlacementData>()
        };
    }

    private void GenerateMeshGrassPositions(GenerationData data)
    {
        var spacing = toolSettings.GrassSpacing;
        var attempts = 0;
        var maxAttempts = data.targetGrassCount * 2;

        var bounds = new Bounds(data.triangles[0].vertices[0], Vector3.zero);
        var objectGrid = new SpatialGrid(bounds, spacing);

        while (data.validPositions.Count < data.targetGrassCount && attempts < maxAttempts)
        {
            attempts++;

            var randomValue = Random.value * data.totalArea;
            var currentSum = 0f;
            var selectedIndex = 0;

            for (var i = 0; i < data.triangles.Count; i++)
            {
                currentSum += data.triangles[i].area;
                if (currentSum >= randomValue)
                {
                    selectedIndex = i;
                    break;
                }
            }

            var selectedTriangle = data.triangles[selectedIndex];
            var randomPoint = GetRandomPointInTriangle(
                selectedTriangle.vertices[0],
                selectedTriangle.vertices[1],
                selectedTriangle.vertices[2]
            );

            var tempPositionIds = new List<int>();
            objectGrid.GetObjectsInRadius(randomPoint, spacing, tempPositionIds);

            if (tempPositionIds.Count > 0) continue;

            if (Physics.CheckSphere(randomPoint, 0.01f, toolSettings.PaintBlockMask))
                continue;

            var placementData = new GrassPlacementData
            {
                position = randomPoint,
                normal = selectedTriangle.normal,
                widthScale = 1f,
                heightScale = 1f
            };

            data.validPositions.Add(placementData);
            objectGrid.AddObject(randomPoint, data.validPositions.Count - 1);
        }
    }

    private void GenerateTerrainGrassPositions(GenerationData data)
    {
        var spacing = toolSettings.GrassSpacing;
        var attempts = 0;
        var maxAttempts = data.targetGrassCount * 2;

        var bounds = new Bounds(data.terrainPosition, data.terrainSize);
        var objectGrid = new SpatialGrid(bounds, spacing);

        while (data.validPositions.Count < data.targetGrassCount && attempts < maxAttempts)
        {
            attempts++;

            var randomX = Random.value * data.terrainSize.x;
            var randomZ = Random.value * data.terrainSize.z;

            var normX = randomX / data.terrainSize.x;
            var normZ = randomZ / data.terrainSize.z;

            var mapX = Mathf.FloorToInt(normX * (data.alphamapWidth - 1));
            var mapZ = Mathf.FloorToInt(normZ * (data.alphamapHeight - 1));

            var finalWidthScale = 1f;
            var finalHeightScale = 1f;
            var isLayerEnabled = true;

            if (data.splatmapData != null && data.splatmapData.GetLength(2) > 0)
            {
                var layerCount = data.splatmapData.GetLength(2);
                var dominantLayer = -1;
                var maxWeight = 0f;

                for (var layer = 0; layer < layerCount; layer++)
                {
                    var weight = data.splatmapData[mapZ, mapX, layer];
                    if (weight > maxWeight)
                    {
                        maxWeight = weight;
                        dominantLayer = layer;
                    }
                }

                if (dominantLayer >= 0)
                {
                    isLayerEnabled = toolSettings.LayerEnabled[dominantLayer];
                    finalWidthScale = toolSettings.WidthFading[dominantLayer];
                    finalHeightScale = toolSettings.HeightFading[dominantLayer];
                }
            }

            if (!isLayerEnabled || finalHeightScale <= 0) continue;

            var worldPos = data.terrainPosition + new Vector3(randomX, 0, randomZ);
            worldPos.y = data.terrainData.GetHeight(
                Mathf.RoundToInt(normX * data.terrainData.heightmapResolution),
                Mathf.RoundToInt(normZ * data.terrainData.heightmapResolution)
            );

            var tempLocalPositionIds = new List<int>();
            objectGrid.GetObjectsInRadius(worldPos, spacing, tempLocalPositionIds);

            if (tempLocalPositionIds.Count > 0) continue;

            var normal = data.terrainData.GetInterpolatedNormal(normX, normZ);
            var surfaceAngle = Mathf.Acos(normal.y) * Mathf.Rad2Deg;
            if (surfaceAngle > toolSettings.NormalLimit * 90.01f) continue;

            if (Physics.CheckSphere(worldPos, 0.01f, toolSettings.PaintBlockMask))
                continue;

            var placementData = new GrassPlacementData
            {
                position = worldPos,
                normal = normal,
                widthScale = finalWidthScale,
                heightScale = finalHeightScale
            };

            data.validPositions.Add(placementData);
            objectGrid.AddObject(worldPos, data.validPositions.Count - 1);
        }
    }

    private Vector3 GetRandomPointInTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        var r1 = Mathf.Sqrt(Random.value);
        var r2 = Random.value;
        var m1 = 1 - r1;
        var m2 = r1 * (1 - r2);
        var m3 = r1 * r2;
        return v1 * m1 + v2 * m2 + v3 * m3;
    }

    private Vector3 GetRandomColor()
    {
        var baseColor = toolSettings.BrushColor;
        var newRandomCol = new Color(
            baseColor.r + Random.Range(0, toolSettings.RangeR),
            baseColor.g + Random.Range(0, toolSettings.RangeG),
            baseColor.b + Random.Range(0, toolSettings.RangeB),
            1
        );
        return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
    }
}