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
            // Calculate valid objects that match the paint mask
            List<GameObject> list = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                if (((1 << obj.layer) & _toolSettings.PaintMask.value) != 0) list.Add(obj);
            }
            
            if (list.Count == 0) return;

            // Calculate grass points per object to match total requested count
            var grassPerObject = _toolSettings.GenerateGrassCount / list.Count;
            
            var allGrassPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();
            var currentPoint = 0;
            var totalPoints = _toolSettings.GenerateGrassCount;

            foreach (var obj in list)
            {
                var task = CreateGenerationTaskForObject(obj, currentPoint, totalPoints, grassPerObject);
                if (task.HasValue)
                {
                    var points = await task.Value;
                    allGrassPoints.AddRange(points);
                    currentPoint += grassPerObject;
                }
            }

            await CreateGrassFromPoints(allGrassPoints);
            _grassCompute.Reset();
        }

        private UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>?
            CreateGenerationTaskForObject(GameObject obj, int startPoint, int totalPoints, int pointsForThisObject)
        {
            if (obj.TryGetComponent(out MeshFilter mesh))
            {
                return CalculateValidPointsForMesh(mesh, pointsForThisObject, startPoint, totalPoints);
            }

            if (obj.TryGetComponent(out Terrain terrain))
            {
                return CalculateValidPointsForTerrain(terrain, pointsForThisObject, startPoint, totalPoints);
            }

            LogLayerMismatch(obj);
            return null;
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CalculateValidPointsForMesh(MeshFilter sourceMesh, int numPoints, int startPoint, int totalPoints)
        {
            var localToWorld = sourceMesh.transform.localToWorldMatrix;
            var objectUp = sourceMesh.transform.up;
            var sharedMesh = sourceMesh.sharedMesh;
            var vertices = sharedMesh.vertices;
            var normals = sharedMesh.normals;
            var triangles = sharedMesh.triangles;
            var colors = sharedMesh.colors;
            var hasColors = colors is { Length: > 0 };
            var tempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();
            var random = new System.Random(Environment.TickCount);

            for (var i = 0; i < numPoints; i++)
            {
                await _window.UpdateProgress(startPoint + i + 1, totalPoints,
                    $"Calculating grass positions on '{sourceMesh.name}'");

                var triIndex = random.Next(0, triangles.Length / 3) * 3;

                var baryCoords = GrassEditorHelper.GetRandomBarycentricCoordWithSeed(random);
                var localPos = baryCoords.x * vertices[triangles[triIndex]] +
                               baryCoords.y * vertices[triangles[triIndex + 1]] +
                               baryCoords.z * vertices[triangles[triIndex + 2]];

                var normal = baryCoords.x * normals[triangles[triIndex]] +
                             baryCoords.y * normals[triangles[triIndex + 1]] +
                             baryCoords.z * normals[triangles[triIndex + 2]];

                var shouldSkip = false;
                if (hasColors)
                {
                    var interpolatedColor = baryCoords.x * colors[triangles[triIndex]] +
                                            baryCoords.y * colors[triangles[triIndex + 1]] +
                                            baryCoords.z * colors[triangles[triIndex + 2]];

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

                var worldPos = localToWorld.MultiplyPoint3x4(localPos);
                var worldNormal = localToWorld.MultiplyVector(normal).normalized;

                var normalDot = Mathf.Abs(Vector3.Dot(worldNormal, objectUp));
                if (normalDot >= 1 - _toolSettings.NormalLimit)
                {
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

            var layerCount = terrainData.terrainLayers.Length;
            var splatMapWidth = terrainData.alphamapWidth;
            var splatMapHeight = terrainData.alphamapHeight;
            var splatmapData = terrainData.GetAlphamaps(0, 0, splatMapWidth, splatMapHeight);

            for (var i = 0; i < numPoints; i++)
            {
                await _window.UpdateProgress(startPoint + i + 1, totalPoints,
                    $"Calculating grass positions on '{terrain.name}'");

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
                    var splatX = Mathf.FloorToInt((randomPoint.x / terrainSize.x) * (splatMapWidth - 1));
                    var splatZ = Mathf.FloorToInt((randomPoint.z / terrainSize.z) * (splatMapHeight - 1));

                    var totalDensity = 0f;
                    var totalWeight = 0f;
                    var heightFade = 0f;
                    var shouldSkip = true;

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
    }
}