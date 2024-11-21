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

            if (Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance))
            {
                var distanceMoved = Vector3.Distance(_lastPosition, hit.point);
                if (distanceMoved >= brushSize * 0.5f)
                {
                    var grassAdded = false;
                    for (int i = 0; i < density; i++)
                    {
                        var randomPoint = Random.insideUnitSphere * brushSize;
                        var randomRayOrigin = mousePointRay.origin;
                        randomRayOrigin.x += randomPoint.x;
                        randomRayOrigin.z += randomPoint.z;

                        var ray = new Ray(randomRayOrigin, mousePointRay.direction);

                        if (Physics.Raycast(ray, out var hit2, grassCompute.GrassSetting.maxFadeDistance))
                        {
                            var hitLayer = hit2.collider.gameObject.layer;
                            if (((1 << hitLayer) & paintMaskValue) != 0)
                            {
                                var hitObj = hit2.collider.gameObject;
                                var objectUp = hitObj.transform.up;
                                var normalDot = Mathf.Abs(Vector3.Dot(hit2.normal, objectUp));
                                // normalDot 에 Abs를 제거하면 오브젝트가 회전했을 때 아래를 바라보는 면에는 잔디를 안그릴 수 있음 절댓값을 취했기 때문에 아래면도 그리고 있는것
                                if (normalDot >= 1 - normalLimit)
                                {
                                    var newData = CreateGrassData(hit2.point, hit2.normal, toolSettings);
                                    var newIndex = grassCompute.GrassDataList.Count;
                                    
                                    grassCompute.GrassDataList.Add(newData);
                                    spatialGrid.AddObject(hit2.point, newIndex);
                            
                                    grassAdded = true;
                                }
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