using UnityEngine;

namespace Grass.Editor
{
    public class GrassAddPainter
    {
        private Vector3 _lastPosition = Vector3.zero;

        private GrassComputeScript _grassCompute;
        private SpatialGrid _spatialGrid;

        public GrassAddPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            Init(grassCompute, spatialGrid);
        }

        private void Init(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public void AddGrass(Vector3 hitPos, GrassToolSettingSo toolSettings, out int addedCount)
        {
            addedCount = 0;
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var normalLimit = toolSettings.NormalLimit;

            var startPos = hitPos + Vector3.up * 3f;
            var distanceMoved = Vector3.Distance(_lastPosition, startPos);

            if (distanceMoved >= brushSize * 0.5f)
            {
                for (var i = 0; i < density; i++)
                {
                    var randomPoint = Random.insideUnitCircle * brushSize;
                    var randomPos = new Vector3(startPos.x + randomPoint.x, startPos.y, startPos.z + randomPoint.y);

                    if (Physics.Raycast(randomPos + Vector3.up * 3f, Vector3.down, out var hit, float.MaxValue))
                    {
                        var hitLayer = hit.collider.gameObject.layer;
                        if (((1 << hitLayer) & paintMaskValue) != 0)
                        {
                            if (hit.normal.y <= 1 + normalLimit && hit.normal.y >= 1 - normalLimit)
                            {
                                var newData = CreateGrassData(hit.point, hit.normal, toolSettings);
                                _grassCompute.GrassDataList.Add(newData);
                                addedCount++;
                            }
                        }
                    }
                }

                if (addedCount > 0)
                {
                    _grassCompute.ResetFaster();
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

        public void Clear()
        {
            _lastPosition = Vector3.zero;
        }
    }
}