using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassReprojectPainter : BasePainter
    {
        private int _minChangedIndex;
        private int _maxChangedIndex;
        private bool _hasChanges;

        public GrassReprojectPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void ReprojectGrass(Ray mousePointRay, LayerMask paintMask, float brushSize, float offset)
        {
            if (!Physics.Raycast(mousePointRay, out var hit, float.MaxValue, paintMask))
                return;

            var hitPoint = hit.point;
            var brushSizeSqr = brushSize * brushSize;

            // SpatialGrid를 사용하여 브러시 영역 내의 잔디 인덱스들을 가져옴
            CollectionsPool.ReturnList(sharedIndices);
            spatialGrid.GetObjectsInRadius(hitPoint, brushSize, sharedIndices);

            _hasChanges = false;
            _minChangedIndex = int.MaxValue;
            _maxChangedIndex = int.MinValue;

            var grassList = grassCompute.GrassDataList;

            ProcessGrassBatch(grassList, hitPoint, brushSizeSqr, paintMask, offset);

            if (_hasChanges)
            {
                grassCompute.UpdateGrassDataFaster(_minChangedIndex, _maxChangedIndex - _minChangedIndex + 1);
            }
        }

        private void ProcessGrassBatch(List<GrassData> grassList, Vector3 hitPoint, float brushSizeSqr,
                                       LayerMask paintMask, float offset)
        {
            var count = sharedIndices.Count;
            for (int i = 0; i < count; i++)
            {
                int index = sharedIndices[i];
                var grassData = grassList[index];
                float distanceSqr = GrassPainterHelper.SqrDistance(grassData.position, hitPoint);

                if (distanceSqr <= brushSizeSqr)
                {
                    var meshPoint = new Vector3(
                        grassData.position.x,
                        grassData.position.y + offset,
                        grassData.position.z
                    );

                    if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f, paintMask))
                    {
                        var newData = grassData;
                        newData.position = hitInfo.point;
                        newData.normal = hitInfo.normal;

                        // SpatialGrid 업데이트
                        spatialGrid.RemoveObject(grassData.position, index);
                        spatialGrid.AddObject(hitInfo.point, index);

                        grassList[index] = newData;

                        _minChangedIndex = Mathf.Min(_minChangedIndex, index);
                        _maxChangedIndex = Mathf.Max(_maxChangedIndex, index);
                        _hasChanges = true;
                    }
                }
            }
        }
    }
}