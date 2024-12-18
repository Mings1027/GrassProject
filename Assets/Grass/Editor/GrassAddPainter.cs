using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassAddPainter : BasePainter
    {
        private Vector3 _lastPosition = Vector3.zero;
        private readonly List<int> _nearbyGrassIds = new List<int>();

        public GrassAddPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void AddGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;

            if (Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance))
            {
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
                        var randomOrigin = hit.point + randomOffset;

                        var newRay = new Ray(randomOrigin - rayDirection * brushSize, rayDirection);

                        // 랜덤 포인트 시각화 (흰색)
                        // Debug.DrawLine(randomOrigin + Vector3.up, randomOrigin - Vector3.up, Color.white, 2f);

                        if (Physics.Raycast(newRay, out var hit2, grassCompute.GrassSetting.maxFadeDistance))
                        {
                            var hitLayer = hit2.collider.gameObject.layer;
                            if (((1 << hitLayer) & paintMaskValue) != 0)
                            {
                                var surfaceAngle = toolSettings.allowUndersideGrass 
                                    ? Mathf.Acos(Mathf.Abs(hit2.normal.y)) * Mathf.Rad2Deg  // 위아래 모두 허용
                                    : Mathf.Acos(hit2.normal.y) * Mathf.Rad2Deg;           // 위쪽만 허용
                                
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

                                        grassAdded = true;
                                        successfulPlacements++;
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

        private GrassData CreateGrassData(Vector3 grassPosition, Vector3 grassNormal, GrassToolSettingSo toolSettings)
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