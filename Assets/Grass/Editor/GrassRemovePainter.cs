using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassRemovePainter : BasePainter
    {
        private List<GrassData> _grassList;
        private Vector3 _lastBrushPosition;
        private readonly List<int> _grassIndicesToDelete;
        private const float MinRemoveDistanceFactor = 0.25f;
        private float _currentBrushRadiusSqr;

        public GrassRemovePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid)
        {
            _grassIndicesToDelete = new List<int>();
            _grassList = grassCompute.GrassDataList;
        }

        public void RemoveGrass(Vector3 hitPoint, float radius)
        {
            if (_grassList == null || _grassList.Count == 0)
                return;

            _currentBrushRadiusSqr = radius * radius;
            var minMoveSqr = _currentBrushRadiusSqr * MinRemoveDistanceFactor;

            if (GrassPainterHelper.SqrDistance(hitPoint, _lastBrushPosition) < minMoveSqr)
                return;

            _lastBrushPosition = hitPoint;
            sharedIndices.Clear();
            _grassIndicesToDelete.Clear();

            spatialGrid.GetObjectsInRadius(hitPoint, radius, sharedIndices);

            if (sharedIndices.Count == 0) return;

            FindGrassWithinBrushRadius(hitPoint);

            if (_grassIndicesToDelete.Count > 0)
            {
                ExecuteGrassRemoval();
            }
        }

        private void FindGrassWithinBrushRadius(Vector3 hitPoint)
        {
            var currentGrassCount = _grassList.Count;

            for (var i = 0; i < sharedIndices.Count; i++)
            {
                var index = sharedIndices[i];
                if (index >= 0 && index < currentGrassCount)
                {
                    var grassPosition = _grassList[index].position;
                    if (GrassPainterHelper.SqrDistance(grassPosition, hitPoint) <= _currentBrushRadiusSqr)
                    {
                        _grassIndicesToDelete.Add(index);
                    }
                }
            }
        }

        private void ExecuteGrassRemoval()
        {
            _grassIndicesToDelete.Sort((a, b) => b.CompareTo(a));
            var lastIndex = _grassList.Count - 1;
            foreach (var index in _grassIndicesToDelete)
            {
                if (index < lastIndex && index >= 0)
                {
                    SwapLastIndexToDeleteIndex(index, lastIndex);
                    lastIndex--;
                }
            }

            var removeCount = Mathf.Min(_grassIndicesToDelete.Count, _grassList.Count);
            if (removeCount > 0)
            {
                _grassList.RemoveRange(_grassList.Count - removeCount, removeCount);
                grassCompute.GrassDataList = _grassList;
                grassCompute.ResetFaster();
            }
        }

        private void SwapLastIndexToDeleteIndex(int deleteIndex, int lastIndex)
        {
            var lastGrass = _grassList[lastIndex];
            var currentGrass = _grassList[deleteIndex];

            // 1. 현재 위치의 잔디를 Grid에서 제거
            spatialGrid.RemoveObject(currentGrass.position, deleteIndex);

            // 2. 마지막 잔디를 현재 위치로 이동
            _grassList[deleteIndex] = lastGrass;
            spatialGrid.AddObject(lastGrass.position, deleteIndex);

            // 3. Grid에서 마지막 위치의 잔디 제거
            spatialGrid.RemoveObject(lastGrass.position, lastIndex);
        }

        public override void Clear()
        {
            base.Clear();
            _lastBrushPosition = Vector3.zero;
            _grassIndicesToDelete.Clear();
        }
    }
}