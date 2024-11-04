using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassRemovePainter : BasePainter
    {
        private List<GrassData> _grassList;

        private Vector3 _lastRemovePosition;

        private readonly List<int> _indicesToRemove;
        private const float MinRemoveDistanceFactor = 0.25f;
        private float _currentRadiusSqr;

        public GrassRemovePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid)
        {
            _indicesToRemove = CollectionsPool.GetList<int>(100);
            _grassList = grassCompute.GrassDataList;
        }

        public void RemoveGrass(Vector3 hitPoint, float radius)
        {
            if (_grassList == null || _grassList.Count == 0)
                return;

            _currentRadiusSqr = radius * radius;
            var minMoveSqr = _currentRadiusSqr * MinRemoveDistanceFactor;

            if (GrassPainterHelper.SqrDistance(hitPoint, _lastRemovePosition) < minMoveSqr)
                return;

            _lastRemovePosition = hitPoint;
            sharedIndices.Clear();
            _indicesToRemove.Clear();

            spatialGrid.GetObjectsInRadius(hitPoint, radius, sharedIndices);

            if (sharedIndices.Count == 0) return;

            CollectIndicesToRemove(hitPoint);

            if (_indicesToRemove.Count > 0)
            {
                RemoveGrassItems();
            }
        }

        private void CollectIndicesToRemove(Vector3 hitPoint)
        {
            var currentGrassCount = _grassList.Count;

            var count = sharedIndices.Count;
            for (int i = 0; i < count; i++)
            {
                var index = sharedIndices[i];
                if (index >= 0 && index < currentGrassCount)
                {
                    var grassPosition = _grassList[index].position;
                    if (GrassPainterHelper.SqrDistance(grassPosition, hitPoint) <= _currentRadiusSqr)
                    {
                        _indicesToRemove.Add(index);
                    }
                }
            }
        }

        private void RemoveGrassItems()
        {
            _indicesToRemove.Sort((a, b) => b.CompareTo(a));
            var lastIndex = _grassList.Count - 1;
            for (var i = 0; i < _indicesToRemove.Count; i++)
            {
                var index = _indicesToRemove[i];
                if (index < lastIndex && index >= 0)
                {
                    SwapAndRemoveGrass(index, lastIndex);
                    lastIndex--;
                }
            }

            var removeCount = Mathf.Min(_indicesToRemove.Count, _grassList.Count);
            if (removeCount > 0)
            {
                _grassList.RemoveRange(_grassList.Count - removeCount, removeCount);
                grassCompute.GrassDataList = _grassList;
                grassCompute.ResetFaster();
            }
        }

        private void SwapAndRemoveGrass(int index, int lastIndex)
        {
            var lastGrass = _grassList[lastIndex];
            var currentGrass = _grassList[index];

            // 1. 현재 위치의 잔디를 Grid에서 제거
            spatialGrid.RemoveObject(currentGrass.position, index);

            // 2. 마지막 잔디를 현재 위치로 이동
            _grassList[index] = lastGrass;
            spatialGrid.AddObject(lastGrass.position, index);

            // 3. Grid에서 마지막 위치의 잔디 제거
            spatialGrid.RemoveObject(lastGrass.position, lastIndex);
        }

        public override void Clear()
        {
            base.Clear();
            _lastRemovePosition = Vector3.zero;
            CollectionsPool.ReturnList(_indicesToRemove);
        }
    }
}