using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassRepositionPainter : BasePainter
    {
        private readonly List<int> _changedIndices = new();

        public GrassRepositionPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void RepositionGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            if (!Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance,
                    toolSettings.PaintMask.value))
                return;

            var hitPoint = hit.point;
            var brushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize;

            sharedIndices.Clear();
            _changedIndices.Clear();

            var extendRadius = Mathf.Sqrt(brushSizeSqr + toolSettings.BrushHeight * toolSettings.BrushHeight);
            spatialGrid.GetObjectsInRadius(hitPoint, extendRadius, sharedIndices);

            var grassList = grassCompute.GrassDataList;

            ProcessGrassBatch(grassList, hitPoint, brushSizeSqr, toolSettings);

            if (_changedIndices.Count > 0)
            {
                var (startIndex, count) = GetModifiedRange();
                grassCompute.UpdateGrassDataFaster(startIndex, count);
            }
        }

        private void ProcessGrassBatch(List<GrassData> grassList, Vector3 hitPoint, float brushSizeSqr,
                                       GrassToolSettingSo toolSettings)
        {
            var cylinderBottom = hitPoint;
            var cylinderTop = hitPoint + Vector3.up * toolSettings.BrushHeight;

            for (var i = 0; i < sharedIndices.Count; i++)
            {
                var index = sharedIndices[i];
                var grassData = grassList[index];

                var xzDistance = new Vector2(
                    grassData.position.x - hitPoint.x,
                    grassData.position.z - hitPoint.z).sqrMagnitude;

                if (xzDistance <= brushSizeSqr)
                {
                    var height = grassData.position.y;
                    if (height >= cylinderBottom.y && height <= cylinderTop.y)
                    {
                        // 지면에 재배치하기 위한 레이캐스트
                        var ray = new Ray(
                            new Vector3(grassData.position.x, cylinderTop.y, grassData.position.z), 
                            Vector3.down);
                            
                        if (Physics.Raycast(ray, out var groundHit, toolSettings.BrushHeight * 2f, toolSettings.PaintMask))
                        {
                            var newData = grassData;
                            newData.position = groundHit.point;
                            newData.normal = groundHit.normal;

                            // 공간 그리드 업데이트
                            spatialGrid.RemoveObject(grassData.position, index);
                            spatialGrid.AddObject(groundHit.point, index);

                            grassList[index] = newData;
                            _changedIndices.Add(index);
                        }
                    }
                } 
            }
        }

        private (int startIndex, int count) GetModifiedRange()
        {
            if (_changedIndices.Count == 0)
                return (-1, 0);

            // 이미 정렬된 상태의 인덱스들에서 범위 계산
            _changedIndices.Sort();
            int minIndex = _changedIndices[0];
            int maxIndex = _changedIndices[_changedIndices.Count - 1];

            return (minIndex, maxIndex - minIndex + 1);
        }

        public override void Clear()
        {
            base.Clear();
            _changedIndices.Clear();
        }
    }
}