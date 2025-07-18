using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Grass.Editor
{
    public class SyncGrassGenerationOperation
    {
        private readonly GrassCompute _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;
        private readonly SpatialGrid _spatialGrid;

        private struct GrassPlacementData
        {
            public Vector3 position;
            public Vector3 normal;
            public float widthScale;
            public float heightScale;
        }

        private struct TriangleData
        {
            public readonly Vector3[] vertices;
            public readonly Vector3 normal;
            public readonly float area;

            public TriangleData(Vector3[] verts, Vector3 norm, float a)
            {
                vertices = verts;
                normal = norm;
                area = a;
            }
        }

        private class GenerationData
        {
            public float totalArea;
            public int targetGrassCount;
            public readonly List<GrassPlacementData> validPositions = new();
            public float[,,] splatmapData;
            public TerrainData terrainData;
            public int alphamapWidth;
            public int alphamapHeight;
            public Vector3 terrainPosition;
            public Vector3 terrainSize;
            public readonly List<TriangleData> triangles = new();
        }

        public SyncGrassGenerationOperation(GrassCompute grassCompute,
                                            GrassToolSettingSo toolSettings, SpatialGrid spatialGrid)
        {
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
            _spatialGrid = spatialGrid;
        }

        public void GenerateGrass(GameObject[] selectedObjects)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Generating Grass", "Initializing...", 0f);

                var meshObjects = new List<MeshFilter>();
                var terrainObjects = new List<Terrain>();

                foreach (var obj in selectedObjects)
                {
                    if (obj.layer.NotMatches(_toolSettings.PaintMask.value)) continue;

                    if (obj.TryGetComponent<MeshFilter>(out var meshFilter))
                        meshObjects.Add(meshFilter);

                    if (obj.TryGetComponent<Terrain>(out var terrain))
                        terrainObjects.Add(terrain);
                }

                var spacing = _toolSettings.GrassSpacing;
                var existingGrassCount = _grassCompute.GrassDataList.Count;
                var generationDataList = new List<GenerationData>();
                var totalArea = 0f;

                // 메시 데이터 수집
                EditorUtility.DisplayProgressBar("Generating Grass", "Collecting mesh data...", 0.1f);
                foreach (var meshFilter in meshObjects)
                {
                    var meshData = CollectMeshData(meshFilter);
                    if (meshData == null) continue;

                    generationDataList.Add(meshData);
                    totalArea += meshData.totalArea;
                }

                // 지형 데이터 수집
                EditorUtility.DisplayProgressBar("Generating Grass", "Collecting terrain data...", 0.2f);
                foreach (var terrain in terrainObjects)
                {
                    var terrainData = CollectTerrainData(terrain);
                    if (terrainData == null) continue;

                    generationDataList.Add(terrainData);
                    totalArea += terrainData.totalArea;
                }

                if (generationDataList.Count == 0)
                {
                    Debug.LogWarning("No valid data found for grass generation!");
                    return;
                }

                var maxGrassForArea = Mathf.FloorToInt(totalArea / (spacing * spacing));
                var totalTargetGrassCount = Mathf.Min(_toolSettings.GenerateGrassCount, maxGrassForArea);

                foreach (var data in generationDataList)
                {
                    data.targetGrassCount = Mathf.FloorToInt(data.totalArea / totalArea * totalTargetGrassCount);
                }

                // 위치 생성
                EditorUtility.DisplayProgressBar("Generating Grass", "Calculating grass positions...", 0.3f);
                for (int i = 0; i < generationDataList.Count; i++)
                {
                    var progress = 0.3f + (0.5f * ((float)i / generationDataList.Count));
                    EditorUtility.DisplayProgressBar("Generating Grass",
                        $"Processing object {i + 1}/{generationDataList.Count}...", progress);
                    GenerateGrassPositions(generationDataList[i]);
                }

                // 최종 데이터 생성
                EditorUtility.DisplayProgressBar("Generating Grass", "Creating final grass data...", 0.9f);
                var currentIndex = existingGrassCount;
                var allGrassData = new List<GrassData>();

                foreach (var data in generationDataList)
                {
                    foreach (var placement in data.validPositions)
                    {
                        var grassData = new GrassData
                        {
                            position = placement.position,
                            normal = placement.normal,
                            color = GrassEditorHelper.GetRandomColor(_toolSettings),
                            widthHeight = new Vector2(
                                _toolSettings.GrassWidth * placement.widthScale,
                                _toolSettings.GrassHeight * placement.heightScale
                            )
                        };

                        allGrassData.Add(grassData);
                        _spatialGrid.AddObject(placement.position, currentIndex++);
                    }
                }

                EditorUtility.DisplayProgressBar("Generating Grass", "Finalizing...", 0.95f);
                _grassCompute.GrassDataList.AddRange(allGrassData);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private GenerationData CollectMeshData(MeshFilter meshFilter)
        {
            var data = new GenerationData();

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var normals = mesh.normals;
            var transform = meshFilter.transform;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v1 = transform.TransformPoint(vertices[triangles[i]]);
                var v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                var v3 = transform.TransformPoint(vertices[triangles[i + 2]]);

                var normal = transform.TransformDirection(
                    (normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f
                ).normalized;

                var area = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;

                var surfaceAngle = _toolSettings.allowUndersideGrass
                    ? Mathf.Acos(Mathf.Abs(normal.y)) * Mathf.Rad2Deg
                    : Mathf.Acos(normal.y) * Mathf.Rad2Deg;

                if (surfaceAngle <= _toolSettings.NormalLimit * 90.01f)
                {
                    var triangleData = new TriangleData(
                        new[] { v1, v2, v3 },
                        normal,
                        area
                    );

                    data.triangles.Add(triangleData);
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
                totalArea = terrainData.size.x * terrainData.size.z
            };
        }

        private void GenerateGrassPositions(GenerationData data)
        {
            if (data.triangles != null && data.triangles.Count > 0)
            {
                GenerateMeshGrassPositions(data);
            }
            else if (data.terrainData != null)
            {
                GenerateTerrainGrassPositions(data);
            }
        }

        private void GenerateMeshGrassPositions(GenerationData data)
        {
            var spacing = _toolSettings.GrassSpacing;
            var attempts = 0;
            var maxAttempts = data.targetGrassCount * 2;

            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            var objectGrid = new SpatialGrid(bounds, spacing);

            while (data.validPositions.Count < data.targetGrassCount && attempts < maxAttempts)
            {
                attempts++;

                var randomValue = Random.value * data.totalArea;
                var currentSum = 0f;
                var selectedIndex = 0;

                for (int i = 0; i < data.triangles.Count; i++)
                {
                    currentSum += data.triangles[i].area;
                    if (currentSum >= randomValue)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                var triangle = data.triangles[selectedIndex];
                var randomPoint =
                    GetRandomPointInTriangle(triangle.vertices[0], triangle.vertices[1], triangle.vertices[2]);

                var tempPositionIds = new List<int>();
                objectGrid.GetObjectsInRadius(randomPoint, spacing, tempPositionIds);

                if (tempPositionIds.Count > 0) continue;

                if (Physics.CheckSphere(randomPoint, 0.01f, _toolSettings.PaintBlockMask))
                    continue;

                var placementData = new GrassPlacementData
                {
                    position = randomPoint,
                    normal = triangle.normal,
                    widthScale = 1f,
                    heightScale = 1f
                };

                data.validPositions.Add(placementData);
                objectGrid.AddObject(randomPoint, data.validPositions.Count - 1);
            }
        }

        private void GenerateTerrainGrassPositions(GenerationData data)
        {
            var spacing = _toolSettings.GrassSpacing;
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
                        isLayerEnabled = _toolSettings.LayerEnabled[dominantLayer];
                        finalWidthScale = _toolSettings.WidthFading[dominantLayer];
                        finalHeightScale = _toolSettings.HeightFading[dominantLayer];
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
                if (surfaceAngle > _toolSettings.NormalLimit * 90.01f) continue;

                if (Physics.CheckSphere(worldPos, 0.01f, _toolSettings.PaintBlockMask))
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
            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;
            float m1 = 1 - r1;
            float m2 = r1 * (1 - r2);
            float m3 = r1 * r2;
            return v1 * m1 + v2 * m2 + v3 * m3;
        }
    }
}