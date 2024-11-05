using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassAddPainter : BasePainter
    {
        private Vector3 _lastPosition = Vector3.zero;
        private const float MinGrassSpacing = 0.1f; // 최소 잔디 간격

        public GrassAddPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void AddGrass(Vector3 hitPos, GrassToolSettingSo toolSettings)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var normalLimit = toolSettings.NormalLimit;

            var startPos = hitPos + Vector3.up * 3f;
            var distanceMoved = Vector3.Distance(_lastPosition, startPos);

            if (distanceMoved >= brushSize * 0.5f)
            {
                var grassAdded = false;
                sharedIndices.Clear();
                for (var i = 0; i < density; i++)
                {
                    var randomPoint = Random.insideUnitCircle * brushSize;
                    var randomPos = new Vector3(startPos.x + randomPoint.x, startPos.y, startPos.z + randomPoint.y);

                    if (Physics.Raycast(randomPos + Vector3.up * toolSettings.BrushHeight, Vector3.down, out var hit,
                            float.MaxValue))
                    {
                        var hitLayer = hit.collider.gameObject.layer;
                        if (((1 << hitLayer) & paintMaskValue) != 0 &&
                            hit.normal.y <= 1 + normalLimit &&
                            hit.normal.y >= 1 - normalLimit)
                        {
                            // 주변 잔디 체크
                            spatialGrid.GetObjectsInRadius(hit.point, MinGrassSpacing, sharedIndices);

                            var newData = CreateGrassData(hit.point, hit.normal, toolSettings);
                            var newIndex = grassCompute.GrassDataList.Count;

                            grassCompute.GrassDataList.Add(newData);
                            spatialGrid.AddObject(hit.point, newIndex);

                            grassAdded = true;
                        }
                    }
                }

                if (grassAdded)
                {
                    grassCompute.ResetFaster();
                }

                _lastPosition = startPos;
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
            _lastPosition = Vector3.zero;
        }
    }
}