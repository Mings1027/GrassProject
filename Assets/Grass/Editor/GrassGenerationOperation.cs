using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassGenerationOperation
    {
        private const int BatchSize = 10000;
        private readonly GrassPainterWindow _window;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;
        private readonly System.Random _random;

        public GrassGenerationOperation(GrassPainterWindow window, GrassComputeScript grassCompute,
                                        GrassToolSettingSo toolSettings)
        {
            _window = window;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
            _random = new System.Random(Environment.TickCount);
        }

        public async UniTask GenerateGrass(GameObject[] selectedObjects)
        {
            var meshObjects = new List<(MeshFilter mesh, Matrix4x4 matrix, Vector3 up)>();
            var terrains = new List<(Terrain terrain, TerrainData data, Vector3 size, Vector3 position)>();
            var totalArea = 0f;

            foreach (var obj in selectedObjects)
            {
                if (((1 << obj.layer) & _toolSettings.PaintMask.value) == 0)
                {
                    Debug.LogWarning($"Layer mismatch for {obj.name}. Check paint mask settings.");
                    continue;
                }

                if (obj.TryGetComponent(out MeshFilter mesh))
                {
                    var area = CalculateMeshArea(mesh.sharedMesh);
                    totalArea += area;
                    meshObjects.Add((mesh, mesh.transform.localToWorldMatrix, mesh.transform.up));
                }
                else if (obj.TryGetComponent(out Terrain terrain))
                {
                    var data = terrain.terrainData;
                    totalArea += data.size.x * data.size.z;
                    terrains.Add((terrain, data, data.size, terrain.transform.position));
                }
            }

            var pointsPerUnit = _toolSettings.GenerateGrassCount / totalArea;
            var allTasks = new List<UniTask<List<GrassData>>>();

            // 메시 오브젝트 병렬 처리
            foreach (var (mesh, matrix, up) in meshObjects)
            {
                var meshArea = CalculateMeshArea(mesh.sharedMesh);
                var pointCount = Mathf.RoundToInt(meshArea * pointsPerUnit);
                allTasks.Add(GeneratePointsForMesh(mesh, matrix, up, pointCount));
            }

            // 터레인 병렬 처리
            foreach (var (terrain, data, size, position) in terrains)
            {
                var terrainArea = size.x * size.z;
                var pointCount = Mathf.RoundToInt(terrainArea * pointsPerUnit);
                allTasks.Add(GeneratePointsForTerrain(terrain, data, size, position, pointCount));
            }

            // 모든 태스크를 동시에 실행하고 결과 수집
            var results = await UniTask.WhenAll(allTasks);
            var allGrassData = results.SelectMany(x => x).ToList();

            // 배치 처리로 데이터 추가
            for (int i = 0; i < allGrassData.Count; i += BatchSize)
            {
                var batchSize = Math.Min(BatchSize, allGrassData.Count - i);
                _grassCompute.GrassDataList.AddRange(allGrassData.GetRange(i, batchSize));
                await _window.UpdateProgress(i + batchSize, allGrassData.Count,
                    $"Adding grass data ({i + batchSize}/{allGrassData.Count})");
            }
        }

        private static float CalculateMeshArea(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var area = 0f;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v1 = vertices[triangles[i]];
                var v2 = vertices[triangles[i + 1]];
                var v3 = vertices[triangles[i + 2]];
                area += Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;
            }

            return area;
        }

        private async UniTask<List<GrassData>> GeneratePointsForMesh(
            MeshFilter mesh, Matrix4x4 localToWorld, Vector3 objectUp, int pointCount)
        {
            var points = new List<GrassData>();
            var sharedMesh = mesh.sharedMesh;
            var vertices = sharedMesh.vertices;
            var normals = sharedMesh.normals;
            var triangles = sharedMesh.triangles;
            var colors = sharedMesh.colors;
            var hasColors = colors != null && colors.Length > 0;

            var triangleCount = triangles.Length / 3;
            var safeTriangleCount = triangleCount - 1;

            for (int i = 0; i < pointCount; i++)
            {
                await _window.UpdateProgress(i, pointCount,
                    $"Processing points on '{mesh.name}' ({i}/{pointCount})");

                // 안전한 삼각형 인덱스 선택
                var triangleIndex = Mathf.Min(
                    Mathf.FloorToInt((float)_random.NextDouble() * triangleCount),
                    safeTriangleCount
                );
                var triIndex = triangleIndex * 3;

                // 인덱스 범위 체크
                if (triIndex + 2 >= triangles.Length ||
                    triangles[triIndex] >= vertices.Length ||
                    triangles[triIndex + 1] >= vertices.Length ||
                    triangles[triIndex + 2] >= vertices.Length)
                {
                    continue;
                }

                var baryCoords = GrassEditorHelper.GetRandomBarycentricCoordWithSeed(_random);

                var localPos = baryCoords.x * vertices[triangles[triIndex]] +
                               baryCoords.y * vertices[triangles[triIndex + 1]] +
                               baryCoords.z * vertices[triangles[triIndex + 2]];

                var normal = baryCoords.x * normals[triangles[triIndex]] +
                             baryCoords.y * normals[triangles[triIndex + 1]] +
                             baryCoords.z * normals[triangles[triIndex + 2]];

                if (hasColors && ShouldSkipDueToVertexColor(colors, triangles, triIndex, baryCoords))
                    continue;

                var worldPos = localToWorld.MultiplyPoint3x4(localPos);
                var worldNormal = localToWorld.MultiplyVector(normal).normalized;

                if (IsValidGrassPoint(worldPos, worldNormal, objectUp))
                {
                    points.Add(new GrassData
                    {
                        position = worldPos,
                        normal = worldNormal,
                        color = GetRandomColor(),
                        widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight)
                    });
                }
            }

            return points;
        }

        private async UniTask<List<GrassData>> GeneratePointsForTerrain(
            Terrain terrain, TerrainData data, Vector3 size, Vector3 position, int pointCount)
        {
            var points = new List<GrassData>();
            var layers = data.terrainLayers;
            var splatMapWidth = data.alphamapWidth;
            var splatMapHeight = data.alphamapHeight;
            var splatmapData = data.GetAlphamaps(0, 0, splatMapWidth, splatMapHeight);

            for (int i = 0; i < pointCount; i++)
            {
                if (i % 1000 == 0)
                {
                    await _window.UpdateProgress(i, pointCount,
                        $"Processing points on terrain '{terrain.name}' ({i}/{pointCount})");
                }

                var randomPoint = new Vector3(
                    (float)(_random.NextDouble() * size.x),
                    0,
                    (float)(_random.NextDouble() * size.z)
                );

                var worldPoint = terrain.transform.TransformPoint(randomPoint);
                worldPoint.y = terrain.SampleHeight(worldPoint);

                if (!IsValidTerrainPoint(worldPoint, randomPoint, size, splatmapData,
                        splatMapWidth, splatMapHeight, layers.Length))
                    continue;

                var normal = data.GetInterpolatedNormal(
                    randomPoint.x / size.x,
                    randomPoint.z / size.z
                );

                points.Add(new GrassData
                {
                    position = worldPoint,
                    normal = normal,
                    color = GetRandomColor(),
                    widthHeight = new Vector2(
                        _toolSettings.GrassWidth,
                        _toolSettings.GrassHeight * GetHeightFade(splatmapData, randomPoint, size,
                            splatMapWidth, splatMapHeight, layers.Length)
                    )
                });
            }

            return points;
        }

        private bool ShouldSkipDueToVertexColor(Color[] colors, int[] triangles, int triIndex, Vector3 baryCoords)
        {
            var interpolatedColor = baryCoords.x * colors[triangles[triIndex]] +
                                    baryCoords.y * colors[triangles[triIndex + 1]] +
                                    baryCoords.z * colors[triangles[triIndex + 2]];

            return _toolSettings.VertexColorSettings switch
            {
                GrassToolSettingSo.VertexColorSetting.Red => interpolatedColor.r > 0.5f,
                GrassToolSettingSo.VertexColorSetting.Green => interpolatedColor.g > 0.5f,
                GrassToolSettingSo.VertexColorSetting.Blue => interpolatedColor.b > 0.5f,
                _ => false
            };
        }

        private bool IsValidGrassPoint(Vector3 worldPos, Vector3 worldNormal, Vector3 objectUp)
        {
            if (Physics.CheckSphere(worldPos, 0.1f, _toolSettings.PaintBlockMask))
                return false;

            var normalDot = Mathf.Abs(Vector3.Dot(worldNormal, objectUp));
            return normalDot >= 1 - _toolSettings.NormalLimit;
        }

        private bool IsValidTerrainPoint(Vector3 worldPoint, Vector3 randomPoint,
                                         Vector3 size, float[,,] splatmapData, int width, int height, int layerCount)
        {
            if (Physics.CheckSphere(worldPoint, 0.1f, _toolSettings.PaintBlockMask))
                return false;

            var splatX = Mathf.FloorToInt((randomPoint.x / size.x) * (width - 1));
            var splatZ = Mathf.FloorToInt((randomPoint.z / size.z) * (height - 1));

            var totalDensity = 0f;
            var totalWeight = 0f;
            var shouldSkip = true;

            for (var j = 0; j < layerCount; j++)
            {
                if (_toolSettings.LayerBlocking[j] <= 0f || _toolSettings.HeightFading[j] <= 0f)
                    continue;

                var layerStrength = splatmapData[splatZ, splatX, j];
                shouldSkip = false;
                totalDensity += _toolSettings.LayerBlocking[j] * layerStrength;
                totalWeight += layerStrength;
            }

            if (shouldSkip || totalWeight <= 0)
                return false;

            var averageDensity = totalDensity / totalWeight;
            return (float)_random.NextDouble() <= averageDensity;
        }

        private float GetHeightFade(float[,,] splatmapData, Vector3 point, Vector3 size,
                                    int width, int height, int layerCount)
        {
            var splatX = Mathf.FloorToInt((point.x / size.x) * (width - 1));
            var splatZ = Mathf.FloorToInt((point.z / size.z) * (height - 1));
            var totalFade = 0f;
            var totalWeight = 0f;

            for (var j = 0; j < layerCount; j++)
            {
                var layerStrength = splatmapData[splatZ, splatX, j];
                totalFade += layerStrength * _toolSettings.HeightFading[j];
                totalWeight += layerStrength;
            }

            return totalWeight > 0 ? totalFade / totalWeight : 1f;
        }

        private Vector3 GetRandomColor()
        {
            var baseColor = _toolSettings.BrushColor;
            var newRandomCol = new Color(
                baseColor.r + (float)_random.NextDouble() * _toolSettings.RangeR,
                baseColor.g + (float)_random.NextDouble() * _toolSettings.RangeG,
                baseColor.b + (float)_random.NextDouble() * _toolSettings.RangeB,
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
        }
    }
}