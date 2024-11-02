using System.Collections.Generic;
using Grass.Editor;
using UnityEngine;

public sealed class GrassRemovePainter : BasePainter
{
    private List<GrassData> _grassList;

    private Vector3 _lastRemovePosition;

    private readonly List<int> _indicesToRemove;
    private readonly HashSet<int> _validIndices; // 유효한 인덱스 추적용
    private const float MinRemoveDistanceFactor = 0.25f;
    private float _currentRadiusSqr;

    public GrassRemovePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute, spatialGrid)
    {
        _indicesToRemove = CollectionsPool.GetList(100);
        _validIndices = CollectionsPool.GetHashSet(100);
        _grassList = grassCompute.GrassDataList;
    }
    
    public void RemoveGrass(Vector3 hitPoint, float radius)
    {
        if (_grassList == null || _grassList.Count == 0)
            return;

        _currentRadiusSqr = radius * radius;
        float minMoveSqr = _currentRadiusSqr * MinRemoveDistanceFactor;

        if (CollectionsPool.SqrDistance(hitPoint, _lastRemovePosition) < minMoveSqr)
            return;

        _lastRemovePosition = hitPoint;
        PrepareForRemoval();

        // SpatialGrid 검색 시간 측정
        spatialGrid.GetObjectsInRadius(hitPoint, radius, sharedIndices);

        if (sharedIndices.Count == 0) return;

        ProcessInBatches(sharedIndices, (start, end) =>
            CollectValidIndices(start, end, hitPoint));

        if (_indicesToRemove.Count > 0)
        {
            RemoveGrassItems();
        }
    }

    private void PrepareForRemoval()
    {
        sharedIndices.Clear();
        _indicesToRemove.Clear();
        _validIndices.Clear();
    }

    private void CollectValidIndices(int start, int end, Vector3 hitPoint)
    {
        int currentGrassCount = _grassList.Count;
        var indices = sharedIndices;

        for (int i = start; i < end; i++)
        {
            int index = indices[i];
            if (index >= 0 && index < currentGrassCount)
            {
                var grassPosition = _grassList[index].position;
                if (CollectionsPool.SqrDistance(grassPosition, hitPoint) <= _currentRadiusSqr)
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
        foreach (var index in _indicesToRemove)
        {
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
        CollectionsPool.ReturnHashSet(_validIndices);
    }
}