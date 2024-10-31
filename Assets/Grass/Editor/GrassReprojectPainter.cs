using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class GrassReprojectPainter
{
    private readonly List<int> _changedIndices = new(10000);

    private GrassComputeScript _grassCompute;
    private SpatialGrid _spatialGrid;

    private struct EditWorkData
    {
        public float brushSizeSqr;
        public Vector3 hitPosition;
        public float offset;
    }

    public GrassReprojectPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
    {
        Init(grassCompute, spatialGrid);
    }

    public void Init(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
    {
        _grassCompute = grassCompute;
        _spatialGrid = spatialGrid;
    }

    public void ReprojectGrass(Ray mousePointRay, LayerMask paintMask, float brushSize, float offset)
    {
        if (!Physics.Raycast(mousePointRay, out var hit, float.MaxValue, paintMask))
            return;

        var workData = CreateWorkData(hit, brushSize, offset);
        
        // 브러시 영역 내의 모든 풀 검사
        var grassList = _grassCompute.GrassDataList;
        var indices = new List<int>();
        
        for (int i = 0; i < grassList.Count; i++)
        {
            var distSqr = (grassList[i].position - workData.hitPosition).sqrMagnitude;
            if (distSqr <= workData.brushSizeSqr)
            {
                indices.Add(i);
            }
        }

        ProcessGrassBatch(indices, workData, paintMask);
    }

    private EditWorkData CreateWorkData(RaycastHit hit, float brushSize, float offset)
    {
        return new EditWorkData
        {
            hitPosition = hit.point,
            brushSizeSqr = brushSize * brushSize,
            offset = offset
        };
    }

    private void ProcessGrassBatch(List<int> indices, EditWorkData workData, LayerMask paintMask)
    {
        _changedIndices.Clear();

        const int batchSize = 1024;
        using var positions = new NativeArray<Vector3>(batchSize, Allocator.Temp);
        using var distancesSqr = new NativeArray<float>(batchSize, Allocator.Temp);

        for (int i = 0; i < indices.Count; i += batchSize)
        {
            var currentBatchSize = Mathf.Min(batchSize, indices.Count - i);
            ProcessBatch(indices, i, currentBatchSize, positions, distancesSqr, workData, paintMask);
        }

        if (_changedIndices.Count > 0)
        {
            _grassCompute.ResetFaster();
 
        }
    }

    private void ProcessBatch(List<int> indices, int startIndex, int batchSize,
                              NativeArray<Vector3> positions, NativeArray<float> distancesSqr,
                              EditWorkData workData, LayerMask paintMask)
    {
        // Collect position data in batch
        for (int j = 0; j < batchSize; j++)
        {
            var grassIndex = indices[startIndex + j];
            positions[j] = _grassCompute.GrassDataList[grassIndex].position;
        }

        // Process distances in batch
        for (int j = 0; j < batchSize; j++)
        {
            distancesSqr[j] = (workData.hitPosition - positions[j]).sqrMagnitude;
        }

        // Update with batch processed data
        for (int j = 0; j < batchSize; j++)
        {
            var grassIndex = indices[startIndex + j];
            ProcessGrassInstance(grassIndex, distancesSqr[j], workData, paintMask);
        }
    }

    private void ProcessGrassInstance(int grassIndex, float distSqr, in EditWorkData workData, LayerMask paintMask)
    {
        if (distSqr > workData.brushSizeSqr)
            return;

        var currentData = _grassCompute.GrassDataList[grassIndex];
        var meshPoint = new Vector3(currentData.position.x, currentData.position.y + workData.offset,
            currentData.position.z);

        if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f, paintMask))
        {
            var newData = currentData;
            newData.position = hitInfo.point;
            newData.normal = hitInfo.normal;

            _grassCompute.GrassDataList[grassIndex] = newData;
            _changedIndices.Add(grassIndex);
        }
    }

    public void Clear()
    {
        _changedIndices.Clear();
    }
}