using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Grass.Editor
{
    public class GrassGenerationOperation
    {
        private readonly GrassPainterWindow _window;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;

        public GrassGenerationOperation(GrassPainterWindow window,
                                        GrassComputeScript grassCompute, GrassToolSettingSo toolSettings)
        {
            _window = window;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
        }

        public async UniTask GenerateGrass(GameObject[] selectedObjects)
        {
            var grassPoints = await CollectGrassGenerationPoints(selectedObjects);
            await CreateGrassFromPoints(grassPoints);
            _grassCompute.Reset();
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CollectGrassGenerationPoints(GameObject[] selectedObjects)
        {
            var totalRequestedPoints = CalculateTotalPoints(selectedObjects);
            var tasks = CreateGenerationTasks(selectedObjects, totalRequestedPoints);
            var results = await UniTask.WhenAll(tasks);

            return results.SelectMany(x => x).ToList();
        }

        private int CalculateTotalPoints(GameObject[] selectedObjects)
        {
            return selectedObjects.Sum(obj =>
                ((1 << obj.layer) & _toolSettings.PaintMask.value) != 0
                    ? _toolSettings.GenerateGrassCount
                    : 0);
        }

        private List<UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>>
            CreateGenerationTasks(GameObject[] selectedObjects, int totalPoints)
        {
            var tasks = new List<UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>>();
            var currentPoint = 0;

            foreach (var obj in selectedObjects)
            {
                if (((1 << obj.layer) & _toolSettings.PaintMask.value) == 0)
                {
                    LogLayerMismatch(obj);
                    continue;
                }

                var task = CreateGenerationTaskForObject(obj, currentPoint, totalPoints);
                if (task.HasValue)
                {
                    tasks.Add(task.Value);
                    currentPoint += _toolSettings.GenerateGrassCount;
                }
            }

            return tasks;
        }

        private UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>?
            CreateGenerationTaskForObject(GameObject obj, int startPoint, int totalPoints)
        {
            if (obj.TryGetComponent(out MeshFilter mesh))
            {
                return CalculateValidPointsForMesh(mesh, _toolSettings.GenerateGrassCount, startPoint, totalPoints);
            }

            if (obj.TryGetComponent(out Terrain terrain))
            {
                return CalculateValidPointsForTerrain(terrain, _toolSettings.GenerateGrassCount, startPoint,
                    totalPoints);
            }

            return null;
        }

        private async UniTask CreateGrassFromPoints(
            List<(Vector3 position, Vector3 normal, Vector2 widthHeight)> points)
        {
            var newGrassData = new List<GrassData>(points.Count);

            for (var i = 0; i < points.Count; i++)
            {
                await UpdateGenerationProgress(i, points.Count);
                newGrassData.Add(CreateGrassData(points[i]));
            }

            _grassCompute.GrassDataList.AddRange(newGrassData);
        }

        private GrassData CreateGrassData((Vector3 position, Vector3 normal, Vector2 widthHeight) point)
        {
            return new GrassData
            {
                position = point.position,
                normal = point.normal,
                color = GetRandomColor(),
                widthHeight = point.widthHeight
            };
        }

        private async UniTask UpdateGenerationProgress(int current, int total)
        {
            await _window.UpdateProgress(
                current + 1,
                total,
                $"Generating grass data ({current + 1:N0}/{total:N0} points)"
            );
        }

        private void LogLayerMismatch(GameObject obj)
        {
            var expectedLayers = string.Join(", ", _toolSettings.GetPaintMaskLayerNames());
            Debug.LogWarning(
                $"'{obj.name}' layer mismatch. Expected: '{expectedLayers}', Current: {LayerMask.LayerToName(obj.layer)}");
        }

        private Vector3 GetRandomColor()
        {
            var baseColor = _toolSettings.BrushColor;
            var newRandomCol = new Color(
                baseColor.r + Random.Range(0, _toolSettings.RangeR),
                baseColor.g + Random.Range(0, _toolSettings.RangeG),
                baseColor.b + Random.Range(0, _toolSettings.RangeB),
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CalculateValidPointsForMesh(MeshFilter sourceMesh, int numPoints, int startPoint, int totalPoints)
        {
            // 메인 스레드에서 필요한 데이터 미리 준비
            var localToWorld = sourceMesh.transform.localToWorldMatrix;
            var objectUp = sourceMesh.transform.up;
            var sharedMesh = sourceMesh.sharedMesh;
            var vertices = sharedMesh.vertices;
            var normals = sharedMesh.normals;
            var triangles = sharedMesh.triangles;
            var colors = sharedMesh.colors; // 버텍스 컬러 데이터 가져오기
            var hasColors = colors is { Length: > 0 };
            var tempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();
            var random = new System.Random(Environment.TickCount);

            for (var i = 0; i < numPoints; i++)
            {
                // 진행 상황 업데이트
                await _window.UpdateProgress(startPoint + i + 1, totalPoints,
                    $"Calculating grass positions on '{sourceMesh.name}'");

                // 랜덤한 삼각형 선택
                var triIndex = random.Next(0, triangles.Length / 3) * 3;

                // 삼각형의 랜덤한 점 계산
                var baryCoords = GrassPainterHelper.GetRandomBarycentricCoordWithSeed(random);
                var localPos = baryCoords.x * vertices[triangles[triIndex]] +
                               baryCoords.y * vertices[triangles[triIndex + 1]] +
                               baryCoords.z * vertices[triangles[triIndex + 2]];

                // 노말 계산
                var normal = baryCoords.x * normals[triangles[triIndex]] +
                             baryCoords.y * normals[triangles[triIndex + 1]] +
                             baryCoords.z * normals[triangles[triIndex + 2]];

                // 버텍스 컬러 보간 계산
                var shouldSkip = false;
                if (hasColors)
                {
                    var interpolatedColor = baryCoords.x * colors[triangles[triIndex]] +
                                            baryCoords.y * colors[triangles[triIndex + 1]] +
                                            baryCoords.z * colors[triangles[triIndex + 2]];

                    // VertexColorSettings에 따라 컬러 체크
                    switch (_toolSettings.VertexColorSettings)
                    {
                        case GrassToolSettingSo.VertexColorSetting.Red:
                            if (interpolatedColor.r > 0.5f) shouldSkip = true;
                            break;
                        case GrassToolSettingSo.VertexColorSetting.Green:
                            if (interpolatedColor.g > 0.5f) shouldSkip = true;
                            break;
                        case GrassToolSettingSo.VertexColorSetting.Blue:
                            if (interpolatedColor.b > 0.5f) shouldSkip = true;
                            break;
                    }
                }

                if (shouldSkip)
                    continue;

                // 월드 좌표로 변환
                var worldPos = localToWorld.MultiplyPoint3x4(localPos);
                var worldNormal = localToWorld.MultiplyVector(normal).normalized;

                // 경사도 체크
                var normalDot = Mathf.Abs(Vector3.Dot(worldNormal, objectUp));
                if (normalDot >= 1 - _toolSettings.NormalLimit)
                {
                    // 물리 체크
                    if (!Physics.CheckSphere(worldPos, 0.1f, _toolSettings.PaintBlockMask))
                    {
                        var widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight);
                        tempPoints.Add((worldPos, worldNormal, widthHeight));
                    }
                }
            }

            return tempPoints;
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CalculateValidPointsForTerrain(Terrain terrain, int numPoints, int startPoint, int totalPoints)
        {
            var terrainData = terrain.terrainData;
            var terrainSize = terrainData.size;
            var tempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();
            var random = new System.Random(Environment.TickCount);

            // 레이어 데이터 캐싱
            var layerCount = terrainData.terrainLayers.Length;
            var splatMapWidth = terrainData.alphamapWidth;
            var splatMapHeight = terrainData.alphamapHeight;
            var splatmapData = terrainData.GetAlphamaps(0, 0, splatMapWidth, splatMapHeight);

            for (var i = 0; i < numPoints; i++)
            {
                if (i % 100 == 0)
                {
                    await _window.UpdateProgress(startPoint + i + 1, totalPoints,
                        $"Calculating grass positions on '{terrain.name}'");
                }

                var randomPoint = new Vector3(
                    (float)(random.NextDouble() * terrainSize.x),
                    0,
                    (float)(random.NextDouble() * terrainSize.z)
                );

                var worldPoint = terrain.transform.TransformPoint(randomPoint);
                worldPoint.y = terrain.SampleHeight(worldPoint);

                if (Physics.CheckSphere(worldPoint, 0.1f, _toolSettings.PaintBlockMask))
                    continue;

                var normal = terrain.terrainData.GetInterpolatedNormal(
                    randomPoint.x / terrainSize.x,
                    randomPoint.z / terrainSize.z);

                if (normal.y > 1 - _toolSettings.NormalLimit || normal.y < 1 + _toolSettings.NormalLimit)
                {
                    // 스플랫맵 좌표 계산
                    var splatX = Mathf.FloorToInt((randomPoint.x / terrainSize.x) * (splatMapWidth - 1));
                    var splatZ = Mathf.FloorToInt((randomPoint.z / terrainSize.z) * (splatMapHeight - 1));

                    var totalDensity = 0f;
                    var totalWeight = 0f;
                    var heightFade = 0f;
                    var shouldSkip = true;

                    // 각 레이어별 계산
                    for (var j = 0; j < layerCount; j++)
                    {
                        if (_toolSettings.LayerBlocking[j] <= 0f || _toolSettings.HeightFading[j] <= 0f)
                            continue;

                        var layerStrength = splatmapData[splatZ, splatX, j];
                        shouldSkip = false;
                        totalDensity += _toolSettings.LayerBlocking[j] * layerStrength;
                        totalWeight += layerStrength;
                        heightFade += layerStrength * _toolSettings.HeightFading[j];
                    }

                    if (shouldSkip)
                        continue;

                    var averageDensity = totalWeight > 0 ? totalDensity / totalWeight : 0;
                    heightFade = totalWeight > 0 ? heightFade / totalWeight : 1f;

                    if ((float)random.NextDouble() <= averageDensity)
                    {
                        var widthHeight = new Vector2(
                            _toolSettings.GrassWidth,
                            _toolSettings.GrassHeight * heightFade
                        );
                        tempPoints.Add((worldPoint, normal, widthHeight));
                    }
                }
            }

            return tempPoints;
        }
    }
}