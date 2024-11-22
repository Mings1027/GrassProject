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

            for (var i = 0; i < sharedIndices.Count; i++)
            {
                var index = sharedIndices[i];
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
            foreach (var index in _grassIndicesToDelete)
            {
                if (index < lastIndex && index >= 0)
                {
                    SwapLastIndexToDeleteIndex(index, lastIndex, grassList);

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

        private void SwapLastIndexToDeleteIndex(int deleteIndex, int lastIndex, List<GrassData> grassList)
        {
            var lastGrass = grassList[lastIndex];
            var currentGrass = grassList[deleteIndex];

            spatialGrid.RemoveObject(currentGrass.position, deleteIndex);
            spatialGrid.RemoveObject(lastGrass.position, lastIndex);

            grassList[deleteIndex] = lastGrass;

            spatialGrid.AddObject(lastGrass.position, deleteIndex);
        }

        public override void Clear()
        {
            base.Clear();
            _lastBrushPosition = Vector3.zero;
            _grassIndicesToDelete.Clear();
        }
    }
}