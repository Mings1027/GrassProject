using System.Collections.Generic;
using Grass.Editor;
using UnityEngine;

public sealed class GrassReprojectPainter : BasePainter
{
    private readonly List<int> _changedIndices;

    public GrassReprojectPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
        spatialGrid)
    {
        _changedIndices = CollectionsPool.GetList(100);
    }

    public void ReprojectGrass(Ray mousePointRay, LayerMask paintMask, float brushSize, float offset)
    {
        if (!Physics.Raycast(mousePointRay, out var hit, float.MaxValue, paintMask))
            return;

        var hitPoint = hit.point;
        var brushSizeSqr = brushSize * brushSize;

        // SpatialGrid를 사용하여 브러시 영역 내의 잔디 인덱스들을 가져옴
        sharedIndices.Clear();
        _changedIndices.Clear();
        spatialGrid.GetObjectsInRadius(hitPoint, brushSize, sharedIndices);

        var grassList = grassCompute.GrassDataList;

        // 배치 처리 적용
        ProcessInBatches(sharedIndices, (start, end) =>
            ProcessGrassBatch(start, end, grassList, hitPoint, brushSizeSqr, paintMask, offset));

        if (_changedIndices.Count > 0)
        {
            grassCompute.UpdateGrassDataFaster();
        }
    }

    private void ProcessGrassBatch(int startIdx, int endIdx, List<GrassData> grassList,
                                   Vector3 hitPoint, float brushSizeSqr, LayerMask paintMask, float offset)
    {
        for (int i = startIdx; i < endIdx; i++)
        {
            int index = sharedIndices[i];
            var grassData = grassList[index];
            float distanceSqr = CollectionsPool.SqrDistance(grassData.position, hitPoint);

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
                    _changedIndices.Add(index);
                }
            }
        }
    }

    public override void Clear()
    {
        base.Clear();
        CollectionsPool.ReturnList(_changedIndices);
    }
}