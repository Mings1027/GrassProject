using System.Collections.Generic;
using System.Linq;
using Editor;
using Grass.Editor;
using UnityEngine;

public sealed class GrassRemovePainter : BasePainter
{
    private Vector3 _lastBrushPosition;
    private readonly HashSet<int> _pendingDeletes = new();
    private const float MinRemoveDistanceFactor = 0.25f;
    private float _currentBrushRadiusSqr;

    public GrassRemovePainter(GrassCompute grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
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

        if (CustomEditorHelper.SqrDistance(hitPoint, _lastBrushPosition) < minMoveSqr)
            return;

        _lastBrushPosition = hitPoint;
        sharedIndices.Clear();

        spatialGrid.GetObjectsInRadius(hitPoint, radius, sharedIndices);
        
        // 반경 내의 잔디를 찾아서 컷 버퍼 업데이트
        var cutIDs = grassCompute.GetCutBuffer();
        foreach (var index in sharedIndices)
        {
            if (index >= 0 && index < grassList.Count)
            {
                var grassPosition = grassList[index].position;
                if (CustomEditorHelper.SqrDistance(grassPosition, hitPoint) <= _currentBrushRadiusSqr)
                {
                    // 해당 잔디를 완전히 잘린 상태로 표시 (높이를 0으로 설정)
                    cutIDs[index] = 0f;
                    _pendingDeletes.Add(index);
                }
            }
        }
        
        // 컷 버퍼 업데이트
        grassCompute.SetCutBuffer(cutIDs);
    }

    private void ExecuteRemoval()
    {
        if (_pendingDeletes.Count == 0) return;

        var grassList = grassCompute.GrassDataList;
        var deleteList = _pendingDeletes.OrderByDescending(x => x).ToList();
        var lastIndex = grassList.Count - 1;

        foreach (var index in deleteList)
        {
            if (index < lastIndex && index >= 0)
            {
                var lastGrass = grassList[lastIndex];
                spatialGrid.RemoveObject(lastGrass.position, lastIndex);
                spatialGrid.RemoveObject(grassList[index].position, index);
                grassList[index] = lastGrass;
                spatialGrid.AddObject(lastGrass.position, index);
                lastIndex--;
            }
        }

        var removeCount = Mathf.Min(_pendingDeletes.Count, grassList.Count);
        if (removeCount > 0)
        {
            grassList.RemoveRange(grassList.Count - removeCount, removeCount);
            grassCompute.GrassDataList = grassList;
        }

        _pendingDeletes.Clear();
    }

    public override void Clear()
    {
        base.Clear();
        _lastBrushPosition = Vector3.zero;
        ExecuteRemoval();
    }
}