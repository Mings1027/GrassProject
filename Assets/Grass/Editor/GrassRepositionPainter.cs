using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassRepositionPainter : BasePainter
    {
        private readonly List<int> _changedIndices;
        private const float MaxRayHeight = 1000f; // 충분히 높은 위치에서 레이를 쏘기 위한 상수

        public GrassRepositionPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid)
        {
            _changedIndices = CollectionsPool.GetList<int>();
        }

        public void RepositionGrass(Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            if (!Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance,
                    toolSettings.PaintMask.value))
                return;

            var hitPoint = hit.point;
            var brushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize;

            sharedIndices.Clear();
            _changedIndices.Clear();

            spatialGrid.GetObjectsInRadius(hitPoint, toolSettings.BrushSize, sharedIndices);

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
            for (var i = 0; i < sharedIndices.Count; i++)
            {
                var index = sharedIndices[i];
                var grassData = grassList[index];
                var start = new Vector3(
                    grassData.position.x, MaxRayHeight, grassData.position.z);
                var ray = new Ray(start, Vector3.down * MaxRayHeight);
                if (Physics.Raycast(ray, out var hit, MaxRayHeight, toolSettings.PaintMask.value))
                {
                    var distanceSqr = GrassPainterHelper.SqrDistance(grassData.position, hitPoint);
                    if (distanceSqr <= brushSizeSqr)
                    {
                        var newData = grassData;
                        newData.position = hit.point;
                        newData.normal = hit.normal;

                        spatialGrid.RemoveObject(grassData.position, index);
                        spatialGrid.AddObject(hit.point, index);

                        grassList[index] = newData;
                        _changedIndices.Add(index);
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
            CollectionsPool.ReturnList(_changedIndices);
        }
    }
}