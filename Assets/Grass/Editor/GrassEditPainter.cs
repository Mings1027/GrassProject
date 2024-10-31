using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassEditPainter
    {
        private readonly Dictionary<int, float> _cumulativeChanges = new();
        private readonly List<int> _changedIndices = new(10000);
        private readonly List<GrassData> _changedData = new(10000);

        private GrassComputeScript _grassCompute;
        private SpatialGrid _spatialGrid;

        private struct EditWorkData
        {
            public Vector2 adjustSize;
            public Color adjustColor;
            public Vector3 colorRange;
            public float brushSizeSqr;
            public Vector3 hitPosition;
            public bool editColor;
            public bool editSize;
            public float changeSpeed;
            public float radius;  // 추가
        }

        public GrassEditPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            Init(grassCompute, spatialGrid);
        }

        public void Init(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public void EditGrass(Ray mouseRay, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            if (!Physics.Raycast(mouseRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            var workData = CreateWorkData(hit, toolSettings, editOption);
        
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

            ProcessGrassBatch(indices, workData);
        }

        public void Clear()
        {
            _cumulativeChanges.Clear();
            _changedIndices.Clear();
            _changedData.Clear();
            // _spatialGrid.Rebuild(_grassCompute.GrassDataList);
        }

        private EditWorkData CreateWorkData(RaycastHit hit, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            return new EditWorkData
            {
                hitPosition = hit.point,
                brushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize,
                adjustSize = new Vector2(toolSettings.AdjustWidth, toolSettings.AdjustHeight),
                adjustColor = toolSettings.BrushColor,
                colorRange = new Vector3(toolSettings.RangeR, toolSettings.RangeG, toolSettings.RangeB),
                editColor = editOption is EditOption.EditColors or EditOption.Both,
                editSize = editOption is EditOption.EditWidthHeight or EditOption.Both,
                changeSpeed = toolSettings.FalloffOuterSpeed,
            };
        }

        private void ProcessGrassBatch(List<int> indices, EditWorkData workData)
        {
            _changedIndices.Clear();
            _changedData.Clear();

            const int batchSize = 1024;
            using var positions = new NativeArray<Vector3>(batchSize, Allocator.Temp);
            using var distancesSqr = new NativeArray<float>(batchSize, Allocator.Temp);

            for (int i = 0; i < indices.Count; i += batchSize)
            {
                var currentBatchSize = Mathf.Min(batchSize, indices.Count - i);
                ProcessBatch(indices, i, currentBatchSize, positions, distancesSqr, workData);
            }

            if (_changedIndices.Count > 0)
            {
                _grassCompute.ResetFaster();

                // SpatialGrid 재구성

                // _spatialGrid.Clear();
                // foreach (var grass in _grassCompute.GrassDataList)
                // {
                //     _spatialGrid.AddObject(grass.position);
                // }
            }
        }
        private void ProcessBatch(List<int> indices, int startIndex, int batchSize,
                                  NativeArray<Vector3> positions, NativeArray<float> distancesSqr,
                                  EditWorkData workData)
        {
            for (int j = 0; j < batchSize; j++)
            {
                var grassIndex = indices[startIndex + j];
                positions[j] = _grassCompute.GrassDataList[grassIndex].position;
            }

            for (int j = 0; j < batchSize; j++)
            {
                distancesSqr[j] = (workData.hitPosition - positions[j]).sqrMagnitude;
            }

            for (int j = 0; j < batchSize; j++)
            {
                var grassIndex = indices[startIndex + j];
                ProcessGrassInstance(grassIndex, distancesSqr[j], workData);
            }
        }

        private void ProcessGrassInstance(int grassIndex, float distSqr, in EditWorkData workData)
        {
            if (distSqr > workData.brushSizeSqr)
                return;

            // Calculate linear falloff based on distance
            var falloff = 1f - (distSqr / workData.brushSizeSqr);

            var currentData = _grassCompute.GrassDataList[grassIndex];
            var targetData = currentData;

            // Initialize or update cumulative change
            _cumulativeChanges.TryAdd(grassIndex, 0f);
            _cumulativeChanges[grassIndex] = Mathf.Clamp01(
                _cumulativeChanges[grassIndex] + Time.deltaTime * falloff * workData.changeSpeed);

            var t = _cumulativeChanges[grassIndex];

            if (workData.editColor)
            {
                var targetColor = CalculateNewColor(workData.adjustColor, workData.colorRange);
                targetData.color = targetColor;
            }

            if (workData.editSize)
            {
                targetData.widthHeight = CalculateNewSize(currentData.widthHeight, workData.adjustSize);
            }

            // Create interpolated data
            var newData = new GrassData
            {
                position = currentData.position,
                normal = currentData.normal,
                color = workData.editColor ? Vector3.Lerp(currentData.color, targetData.color, t) : currentData.color,
                widthHeight = workData.editSize
                    ? Vector2.Lerp(currentData.widthHeight, targetData.widthHeight, t)
                    : currentData.widthHeight
            };

            _grassCompute.GrassDataList[grassIndex] = newData;
            _changedIndices.Add(grassIndex);
            _changedData.Add(newData);
        }

        private Vector3 CalculateNewColor(Color adjustColor, Vector3 colorRange)
        {
            return new Vector3(
                adjustColor.r + Random.value * colorRange.x,
                adjustColor.g + Random.value * colorRange.y,
                adjustColor.b + Random.value * colorRange.z
            );
        }

        private Vector2 CalculateNewSize(Vector2 currentSize, Vector2 adjustSize)
        {
            return new Vector2(
                currentSize.x + adjustSize.x,
                currentSize.y + adjustSize.y
            );
        }
 
    }
}