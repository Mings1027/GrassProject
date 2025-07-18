using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassAddPainter
    {
        private Vector3 _lastPosition = Vector3.zero;
        private readonly List<int> _nearbyGrassIds = new();
        private readonly GrassCompute _grassCompute;
        private readonly SpatialGrid _spatialGrid;

        public GrassAddPainter(GrassCompute grassCompute, SpatialGrid spatialGrid)
        {
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public void AddGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            if (!Physics.Raycast(mousePointRay, out var hit, _grassCompute.GrassSetting.maxFadeDistance)) return;

            var hitLayer = hit.collider.gameObject.layer;
            if (hitLayer.Matches(toolSettings.PaintBlockMask.value) ||
                hitLayer.NotMatches(toolSettings.PaintMask.value)) return;

            var distanceMoved = Vector3.Distance(_lastPosition, hit.point);
            if (distanceMoved < toolSettings.BrushSize * 0.5f) return;

            var grassAdded = PlaceGrassInGrid(hit, mousePointRay.direction, toolSettings);

            if (grassAdded)
            {
                _grassCompute.ResetFaster();
            }

            _lastPosition = hit.point;
        }

        private bool PlaceGrassInGrid(RaycastHit hit, Vector3 rayDirection, GrassToolSettingSo toolSettings)
        {
            var bounds = new Bounds(hit.point, Vector3.one * toolSettings.BrushSize * 2);
            var tempGrid = new SpatialGrid(bounds, toolSettings.GrassSpacing);

            // 기존 잔디 위치 추가
            _nearbyGrassIds.Clear();
            _spatialGrid.GetObjectsInRadius(hit.point, toolSettings.BrushSize, _nearbyGrassIds);
            foreach (var id in _nearbyGrassIds)
            {
                tempGrid.AddObject(_grassCompute.GrassDataList[id].position, id);
            }

            var hitNormal = hit.normal;
            var right = Vector3.Cross(hitNormal, rayDirection).normalized;
            var forward = Vector3.Cross(right, hitNormal).normalized;

            var circleArea = Mathf.PI * toolSettings.BrushSize * toolSettings.BrushSize;
            var spacingBetweenPoints = Mathf.Sqrt(circleArea / toolSettings.Density);
            var gridSize = Mathf.CeilToInt(toolSettings.BrushSize * 2 / spacingBetweenPoints);

            var grassAdded = false;
            var successfulPlacements = 0;

            for (var x = -gridSize / 2; x < gridSize / 2; x++)
            {
                for (var z = -gridSize / 2; z < gridSize / 2; z++)
                {
                    var randomOffset = new Vector3(
                        Random.Range(-spacingBetweenPoints / 4f, spacingBetweenPoints / 4f),
                        0,
                        Random.Range(-spacingBetweenPoints / 4f, spacingBetweenPoints / 4f)
                    );

                    var gridPos = hit.point + (right * x + forward * z) * spacingBetweenPoints + randomOffset;

                    if (Vector3.Distance(gridPos, hit.point) > toolSettings.BrushSize) continue;

                    var surfaceRay = new Ray(gridPos - rayDirection * toolSettings.BrushSize, rayDirection);

                    if (Physics.Raycast(surfaceRay, out var surfaceHit, _grassCompute.GrassSetting.maxFadeDistance))
                    {
                        if (surfaceHit.collider.gameObject.layer.NotMatches(toolSettings.PaintMask.value)) continue;

                        var surfaceAngle = toolSettings.allowUndersideGrass
                            ? Mathf.Acos(Mathf.Abs(surfaceHit.normal.y)) * Mathf.Rad2Deg
                            : Mathf.Acos(surfaceHit.normal.y) * Mathf.Rad2Deg;

                        if (surfaceAngle <= toolSettings.NormalLimit * 90.01f)
                        {
                            _nearbyGrassIds.Clear();
                            tempGrid.GetObjectsInRadius(surfaceHit.point, toolSettings.GrassSpacing, _nearbyGrassIds);

                            if (_nearbyGrassIds.Count == 0)
                            {
                                var newData = CreateGrassData(surfaceHit.point, surfaceHit.normal, toolSettings);
                                var newIndex = _grassCompute.GrassDataList.Count;

                                _grassCompute.GrassDataList.Add(newData);
                                _spatialGrid.AddObject(surfaceHit.point, newIndex);
                                tempGrid.AddObject(surfaceHit.point, newIndex);
                                
                                grassAdded = true;
                                successfulPlacements++;
                            }
                        }
                    }
                }
            }

            return grassAdded;
        }

        private static GrassData CreateGrassData(Vector3 grassPosition, Vector3 grassNormal,
                                                 GrassToolSettingSo toolSettings)
        {
            return new GrassData
            {
                color = GrassEditorHelper.GetRandomColor(toolSettings),
                position = grassPosition,
                widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight),
                normal = grassNormal
            };
        }

        public void Clear()
        {
            _lastPosition = Vector3.zero;
            _grassCompute.Reset();
        }
    }
}