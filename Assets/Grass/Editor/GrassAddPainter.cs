using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassAddPainter : BasePainter
    {
        private Vector3 _lastPosition = Vector3.zero;
        private readonly List<int> _nearbyGrassIds = new();

        public GrassAddPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void AddGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var paintBlockMaskValue = toolSettings.PaintBlockMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var maxFadeDistance = grassCompute.GrassSetting.maxFadeDistance;

            if (Physics.Raycast(mousePointRay, out var hit, maxFadeDistance))
            {
                var hitLayer = hit.collider.gameObject.layer;
                if (hitLayer.Matches(paintBlockMaskValue)) return;
                if (hitLayer.NotMatches(paintMaskValue)) return;

                var distanceMoved = Vector3.Distance(_lastPosition, hit.point);
                if (distanceMoved >= brushSize * 0.5f)
                {
                    var grassAdded = false;
                    var hitNormal = hit.normal;
                    var rayDirection = mousePointRay.direction;

                    var right = Vector3.Cross(hitNormal, rayDirection).normalized;
                    var forward = Vector3.Cross(right, hitNormal).normalized;

                    var maxAttempts = density * 2;
                    var successfulPlacements = 0;

                    for (int attempt = 0; attempt < maxAttempts && successfulPlacements < density; attempt++)
                    {
                        var angle = Random.Range(0f, 2f * Mathf.PI);
                        var radius = Mathf.Sqrt(Random.Range(0f, 1f)) * brushSize;

                        var randomOffset = right * (radius * Mathf.Cos(angle)) +
                                           forward * (radius * Mathf.Sin(angle));
                        var randomPoint = hit.point + randomOffset;

                        var cameraToPointRay = new Ray(mousePointRay.origin,
                            (randomPoint - mousePointRay.origin).normalized);
                        
                        if (Physics.Raycast(cameraToPointRay, out var cameraHit, maxFadeDistance))
                        {
                            if (cameraHit.collider.gameObject.layer.Matches(paintBlockMaskValue))
                                continue;

                            var newRay = new Ray(randomPoint - rayDirection * brushSize, rayDirection);

                            if (Physics.Raycast(newRay, out var hit2, maxFadeDistance))
                            {
                                if (hit2.collider.gameObject.layer.Matches(paintMaskValue))
                                {
                                    var surfaceAngle = toolSettings.allowUndersideGrass
                                        ? Mathf.Acos(Mathf.Abs(hit2.normal.y)) * Mathf.Rad2Deg // 위아래 모두 허용
                                        : Mathf.Acos(hit2.normal.y) * Mathf.Rad2Deg; // 위쪽만 허용

                                    if (surfaceAngle <= toolSettings.NormalLimit * 90.01f)
                                    {
                                        _nearbyGrassIds.Clear();
                                        spatialGrid.GetObjectsInRadius(hit2.point, toolSettings.GrassSpacing,
                                            _nearbyGrassIds);

                                        var tooClose = false;
                                        foreach (var nearbyId in _nearbyGrassIds)
                                        {
                                            var existingGrass = grassCompute.GrassDataList[nearbyId];
                                            if (Vector3.Distance(existingGrass.position, hit2.point) <
                                                toolSettings.GrassSpacing)
                                            {
                                                tooClose = true;
                                                break;
                                            }
                                        }

                                        if (!tooClose)
                                        {
                                            var newData = CreateGrassData(hit2.point, hit2.normal, toolSettings);
                                            var newIndex = grassCompute.GrassDataList.Count;

                                            grassCompute.GrassDataList.Add(newData);
                                            spatialGrid.AddObject(hit2.point, newIndex);

                                            grassCompute.AddNewGrassToCullingTree(newData.position, newIndex);

                                            grassAdded = true;
                                            successfulPlacements++;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (grassAdded)
                    {
                        grassCompute.ResetFaster();
                    }

                    _lastPosition = hit.point;
                }
            }
        }

        private static GrassData CreateGrassData(Vector3 grassPosition, Vector3 grassNormal, GrassToolSettingSo toolSettings)
        {
            return new GrassData
            {
                color = GrassEditorHelper.GetRandomColor(toolSettings),
                position = grassPosition,
                widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight),
                normal = grassNormal
            };
        }

        public override void Clear()
        {
            base.Clear();
            _lastPosition = Vector3.zero;
        }
    }
}