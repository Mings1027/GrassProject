using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassAddPainter : BasePainter
    {
        private Vector3 _lastPosition = Vector3.zero;

        public GrassAddPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void AddGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var normalLimit = toolSettings.NormalLimit;

            if (Physics.Raycast(mousePointRay, out var hit, 100))
            {
                var distanceMoved = Vector3.Distance(_lastPosition, hit.point);
                if (distanceMoved >= brushSize * 0.5f)
                {
                    var grassAdded = false;
                    for (int i = 0; i < density; i++)
                    {
                        var randomPoint = Random.insideUnitCircle * brushSize;
                        var randomRayOrigin = mousePointRay.origin;
                        randomRayOrigin.x += randomPoint.x;
                        randomRayOrigin.z += randomPoint.y;

                        var ray = new Ray(randomRayOrigin, mousePointRay.direction);
                        if (Physics.Raycast(ray, out var hit2, 100))
                        {
                            var hitLayer = hit2.collider.gameObject.layer;
                            if (((1 << hitLayer) & paintMaskValue) != 0 &&
                                hit2.normal.y <= 1 + normalLimit &&
                                hit2.normal.y >= 1 - normalLimit)
                            {
                                var newData = CreateGrassData(hit2.point, hit2.normal, toolSettings);
                                var newIndex = grassCompute.GrassDataList.Count;
                                grassCompute.GrassDataList.Add(newData);
                                spatialGrid.AddObject(hit2.point, newIndex);

                                grassAdded = true;
                            }
                        }
                    }

                    if (grassAdded) grassCompute.ResetFaster();
                    _lastPosition = hit.point;
                }
            }
        }

        private GrassData CreateGrassData(Vector3 grassPosition, Vector3 grassNormal, GrassToolSettingSo toolSettings)
        {
            return new GrassData
            {
                color = GetRandomColor(toolSettings),
                position = grassPosition,
                widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight),
                normal = grassNormal
            };
        }

        private Vector3 GetRandomColor(GrassToolSettingSo toolSettings)
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

        public override void Clear()
        {
            base.Clear();
            _lastPosition = Vector3.zero;
        }
    }
}