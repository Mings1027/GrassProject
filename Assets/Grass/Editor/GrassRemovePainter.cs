using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassRemovePainter : BasePainter
    {
        private Vector3 _lastBrushPosition;
        private readonly List<int> _grassIndicesToDelete = new();
        private const float MinRemoveDistanceFactor = 0.25f;
        private float _currentBrushRadiusSqr;

        public GrassRemovePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void RemoveGrass(Ray mousePointRay, float radius)
        {
            var grassList = grassCompute.GrassDataList;
            if (grassList == null || grassList.Count == 0)
                return;

            if (!Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance))
                return;

            var hitPoint = hit.point;
            _currentBrushRadiusSqr = radius * radius;
            var minMoveSqr = _currentBrushRadiusSqr * MinRemoveDistanceFactor;

            if (GrassPainterHelper.SqrDistance(hitPoint, _lastBrushPosition) < minMoveSqr)
                return;

            _lastBrushPosition = hitPoint;
            sharedIndices.Clear();
            _grassIndicesToDelete.Clear();

            spatialGrid.GetObjectsInRadius(hitPoint, radius, sharedIndices);

            if (sharedIndices.Count == 0) return;

            FindGrassWithinBrushRadius(hitPoint, grassList);

            if (_grassIndicesToDelete.Count > 0)
            {
                ExecuteGrassRemoval(grassList);
            }
        }

        private void FindGrassWithinBrushRadius(Vector3 hitPoint, List<GrassData> grassList)
        {
            var currentGrassCount = grassList.Count;

            foreach (var index in sharedIndices)
            {
                if (index >= 0 && index < currentGrassCount)
                {
                    var grassPosition = grassList[index].position;
                    if (GrassPainterHelper.SqrDistance(grassPosition, hitPoint) <= _currentBrushRadiusSqr)
                    {
                        _grassIndicesToDelete.Add(index);
                    }
                }
            }
        }

        private void ExecuteGrassRemoval(List<GrassData> grassList)
        {
            _grassIndicesToDelete.Sort((a, b) => b.CompareTo(a));
            var lastIndex = grassList.Count - 1;
            
            // 삭제될 위치들만 spatialGrid에서 제거
            foreach (var index in _grassIndicesToDelete)
            {
                if (index >= 0 && index < grassList.Count)
                {
                    spatialGrid.RemoveObject(grassList[index].position, index);
                }
            }
            
            foreach (var index in _grassIndicesToDelete)
            {
                if (index < lastIndex && index >= 0)
                {
                    var lastGrass = grassList[lastIndex];

                    // 마지막 요소의 이전 위치에서 제거
                    spatialGrid.RemoveObject(lastGrass.position, lastIndex);
            
                    // 스왑
                    grassList[index] = lastGrass;
            
                    // 새 위치에 추가
                    spatialGrid.AddObject(lastGrass.position, index);

                    lastIndex--;
                }
            }

            var removeCount = Mathf.Min(_grassIndicesToDelete.Count, grassList.Count);
            if (removeCount > 0)
            {
                grassList.RemoveRange(grassList.Count - removeCount, removeCount);
                grassCompute.GrassDataList = grassList;
                grassCompute.ResetFaster();
            }
        }
        
        public override void Clear()
        {
            base.Clear();
            _lastBrushPosition = Vector3.zero;
            _grassIndicesToDelete.Clear();
        }
    }
}