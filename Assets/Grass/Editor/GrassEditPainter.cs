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
        private List<GrassData> _grassData;
        private GrassTileSystem _grassTileSystem;

        private struct EditWorkData
        {
            public Vector2 adjustSize;
            public Color adjustColor;
            public Vector3 colorRange;
            public float brushSizeSqr;
            public float brushFalloffSizeSqr;
            public Vector3 hitPosition;
            public bool editColor;
            public bool editSize;
        }

        public GrassEditPainter(GrassComputeScript grassCompute, List<GrassData> grassData, GrassTileSystem sharedTileSystem)
        {
            Init(grassCompute, grassData, sharedTileSystem);
        }

        public void Init(GrassComputeScript grassCompute, List<GrassData> grassData, GrassTileSystem sharedTileSystem)
        {
            _grassCompute = grassCompute;
            _grassData = grassData;
            _grassTileSystem = sharedTileSystem;
        }

        public void EditGrass(Ray mouseRay, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            if (!Physics.Raycast(mouseRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            var workData = CreateWorkData(hit, toolSettings, editOption);
            var indices = _grassTileSystem.GetNearbyIndices(workData.hitPosition, toolSettings.BrushSize);

            ProcessGrassBatch(indices, workData, toolSettings);
        }

        private EditWorkData CreateWorkData(RaycastHit hit, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            return new EditWorkData
            {
                hitPosition = hit.point,
                brushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize,
                brushFalloffSizeSqr = toolSettings.BrushFalloffSize * toolSettings.BrushFalloffSize,
                adjustSize = new Vector2(toolSettings.AdjustWidth, toolSettings.AdjustHeight),
                adjustColor = toolSettings.BrushColor,
                colorRange = new Vector3(toolSettings.RangeR, toolSettings.RangeG, toolSettings.RangeB),
                editColor = editOption is EditOption.EditColors or EditOption.Both,
                editSize = editOption is EditOption.EditWidthHeight or EditOption.Both
            };
        }

        private void ProcessGrassBatch(List<int> indices, EditWorkData workData, GrassToolSettingSo toolSettings)
        {
            _changedIndices.Clear();
            _changedData.Clear();

            const int batchSize = 1024;
            using var positions = new NativeArray<Vector3>(batchSize, Allocator.Temp);
            using var distancesSqr = new NativeArray<float>(batchSize, Allocator.Temp);

            for (int i = 0; i < indices.Count; i += batchSize)
            {
                var currentBatchSize = Mathf.Min(batchSize, indices.Count - i);
                ProcessBatch(indices, i, currentBatchSize, positions, distancesSqr, workData, toolSettings);
            }

            if (_changedIndices.Count > 0)
            {
                _grassCompute.UpdateGrassData(_changedIndices, _changedData);
            }
        }

        private void ProcessBatch(List<int> indices, int startIndex, int batchSize,
                                  NativeArray<Vector3> positions, NativeArray<float> distancesSqr,
                                  EditWorkData workData, GrassToolSettingSo toolSettings)
        {
            // 배치로 위치 데이터 수집
            for (int j = 0; j < batchSize; j++)
            {
                var grassIndex = indices[startIndex + j];
                positions[j] = _grassData[grassIndex].position;
            }

            // 거리 계산을 배치로 처리
            for (int j = 0; j < batchSize; j++)
            {
                distancesSqr[j] = (workData.hitPosition - positions[j]).sqrMagnitude;
            }

            // 배치 처리된 데이터로 업데이트
            for (int j = 0; j < batchSize; j++)
            {
                var grassIndex = indices[startIndex + j];
                ProcessGrassInstance(grassIndex, distancesSqr[j], workData, toolSettings);
            }
        }

        private void ProcessGrassInstance(int grassIndex, float distSqr, in EditWorkData workData,
                                          GrassToolSettingSo toolSettings)
        {
            if (distSqr > workData.brushSizeSqr)
                return;

            var currentData = _grassData[grassIndex];
            var targetData = currentData;

            if (workData.editColor)
                targetData.color = CalculateNewColor(workData.adjustColor, workData.colorRange);

            if (workData.editSize)
                targetData.widthHeight = CalculateNewSize(currentData.widthHeight, workData.adjustSize, toolSettings);

            var changeSpeed = CalculateChangeSpeed(distSqr, workData.brushFalloffSizeSqr, toolSettings);
            UpdateGrassData(grassIndex, currentData, targetData, changeSpeed);
        }

        private Vector3 CalculateNewColor(Color adjustColor, Vector3 colorRange)
        {
            var newCol = new Color(
                adjustColor.r + Random.value * colorRange.x,
                adjustColor.g + Random.value * colorRange.y,
                adjustColor.b + Random.value * colorRange.z
            );
            return new Vector3(newCol.r, newCol.g, newCol.b);
        }

        private Vector2 CalculateNewSize(Vector2 currentSize, Vector2 adjustSize, GrassToolSettingSo toolSettings)
        {
            return new Vector2(
                Mathf.Clamp(currentSize.x + adjustSize.x, 0, toolSettings.AdjustWidthMax),
                Mathf.Clamp(currentSize.y + adjustSize.y, 0, toolSettings.AdjustHeightMax)
            );
        }

        private float CalculateChangeSpeed(float distSqr, float brushFalloffSizeSqr, GrassToolSettingSo toolSettings)
        {
            if (distSqr <= brushFalloffSizeSqr)
                return 1f;

            var distanceRatio = Mathf.Sqrt(distSqr) / toolSettings.BrushSize;
            var falloffRatio = (distanceRatio - toolSettings.BrushFalloffSize) / (1 - toolSettings.BrushFalloffSize);
            return (1 - falloffRatio) * toolSettings.FalloffOuterSpeed;
        }

        private void UpdateGrassData(int grassIndex, GrassData currentData, GrassData targetData, float changeSpeed)
        {
            _cumulativeChanges.TryAdd(grassIndex, 0f);
            _cumulativeChanges[grassIndex] =
                Mathf.Clamp01(_cumulativeChanges[grassIndex] + changeSpeed * Time.deltaTime);

            var t = _cumulativeChanges[grassIndex];
            var newData = new GrassData
            {
                position = currentData.position,
                normal = currentData.normal,
                color = Vector3.Lerp(currentData.color, targetData.color, t),
                widthHeight = Vector2.Lerp(currentData.widthHeight, targetData.widthHeight, t)
            };

            _grassData[grassIndex] = newData;
            _changedIndices.Add(grassIndex);
            _changedData.Add(newData);
        }

        public void ClearCumulativeChanges()
        {
            _cumulativeChanges.Clear();
        }
    }
}