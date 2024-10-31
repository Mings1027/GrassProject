using System.Collections.Generic;
using UnityEngine;

public class GrassRemovePainter
{
    private Vector3 _lastRemovePos;
    private bool _isDragging;
    private readonly List<int> _indicesToRemove = new();

    private SpatialGrid _spatialGrid;
    private GrassComputeScript _grassCompute;

    public GrassRemovePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
    {
        Init(grassCompute, spatialGrid);
    }

    public void Init(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
    {
        _grassCompute = grassCompute;
        _spatialGrid = spatialGrid;
    }

    public void RemoveGrass(Vector3 hitPoint, float radius)
    {
        var radiusSqr = radius * radius;
        var removedCount = 0;

        var halfBrushSize = radius * 0.5f;
        if (Vector3.SqrMagnitude(hitPoint - _lastRemovePos) >= halfBrushSize * halfBrushSize)
        {
            _lastRemovePos = hitPoint;
        }

        _indicesToRemove.Clear();

        var grassList = _grassCompute.GrassDataList;
        // SpatialGrid를 사용하여 주변 영역 검사
        for (var i = grassList.Count - 1; i >= 0; i--)
        {
            var grassData = grassList[i];
            var distanceSqr = Vector3.SqrMagnitude(grassData.position - hitPoint);
            if (distanceSqr <= radiusSqr)
            {
                _indicesToRemove.Add(i);
                // _spatialGrid.RemoveObject(grassData.position);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            // 큰 인덱스부터 제거
            _indicesToRemove.Sort((a, b) => b.CompareTo(a));
 
            for (var i = 0; i < _indicesToRemove.Count; i++)
            {
                var index = _indicesToRemove[i];
                _grassCompute.GrassDataList.RemoveAt(index);
            }
            
            _grassCompute.ResetFaster();
        }

        _isDragging = true;
    }

    public void Clear()
    {
        _isDragging = false;
        _lastRemovePos = Vector3.zero;
        _indicesToRemove.Clear();
        // _spatialGrid.Rebuild(_grassCompute.GrassDataList);

    }
}