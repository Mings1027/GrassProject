using System.Collections.Generic;
using Grass.Editor;
using UnityEngine;

public class GrassReprojectPainter
{
    private const int BATCH_SIZE = 1024;
    private readonly List<int> _changedIndices = new(10000);
    private readonly List<GrassData> _changedData = new(10000);

    private GrassComputeScript _grassCompute;
    private List<GrassData> _grassData;
    private GrassTileSystem _grassTileSystem;

    public GrassReprojectPainter(GrassComputeScript grassCompute, List<GrassData> grassData, GrassTileSystem grassTileSystem)
    {
        Init(grassCompute, grassData, grassTileSystem);
    }

    public void Init(GrassComputeScript grassCompute, List<GrassData> grassData, GrassTileSystem grassTileSystem)
    {
        _grassCompute = grassCompute;
        _grassData = grassData;
        _grassTileSystem = grassTileSystem;

        _changedIndices.Clear();
        _changedData.Clear();
    }

    public void ReprojectGrass(Ray mousePointRay, LayerMask paintMask, float brushSize, float offset)
    {
        if (!Physics.Raycast(mousePointRay, out var hit, float.MaxValue, paintMask))
            return;

        var indices = _grassTileSystem.GetNearbyIndices(hit.point, brushSize);
        var brushSizeSqr = brushSize * brushSize;

        _changedIndices.Clear();
        _changedData.Clear();

        // 배치 처리
        for (int i = 0; i < indices.Count; i += BATCH_SIZE)
        {
            var currentBatchSize = Mathf.Min(BATCH_SIZE, indices.Count - i);
            ProcessBatch(indices, i, currentBatchSize, hit.point, brushSizeSqr, offset, paintMask);
        }

        if (_changedIndices.Count > 0)
        {
            _grassCompute.UpdateGrassData(_changedIndices, _changedData);
            _grassCompute.ResetFaster();
        }
    }

    private void ProcessBatch(List<int> indices, int startIndex, int batchSize,
                              Vector3 hitPoint, float brushSizeSqr, float offset,
                              LayerMask paintMask)
    {
        for (int j = 0; j < batchSize; j++)
        {
            var grassIndex = indices[startIndex + j];
            var pos = _grassData[grassIndex].position;

            if ((hitPoint - pos).sqrMagnitude <= brushSizeSqr)
            {
                var meshPoint = new Vector3(pos.x, pos.y + offset, pos.z);
                if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f, paintMask))
                {
                    var newData = _grassData[grassIndex];
                    newData.position = hitInfo.point;
                    _grassData[grassIndex] = newData;

                    _changedIndices.Add(grassIndex);
                    _changedData.Add(newData);
                }
            }
        }
    }

    public void Clear()
    {
        _grassTileSystem.ClearCache();
        _changedIndices.Clear();
        _changedData.Clear();
    }
}