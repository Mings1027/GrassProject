using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassEditPainter : BasePainter
    {
        private readonly Dictionary<int, float> _cumulativeChanges = new();
        private readonly Dictionary<int, GrassData> _modifiedGrassData = new(BatchSize);
        private readonly HashSet<int> _processedIndices;

        private float _currentBrushSizeSqr;
        private Vector3 _currentHitPoint;
        private Vector3 _targetColor;
        private float _deltaTimeSpeed;

        public GrassEditPainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            Initialize(grassCompute, spatialGrid);
            _processedIndices = PainterUtils.GetHashSet();
        }

        public void EditGrass(Ray mouseRay, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            if (!Physics.Raycast(mouseRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            sharedIndices.Clear();
            _processedIndices.Clear();
            _modifiedGrassData.Clear();

            // 자주 사용되는 값들을 미리 계산
            _currentHitPoint = hit.point;
            _currentBrushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize;
            _deltaTimeSpeed = Time.deltaTime * toolSettings.FalloffOuterSpeed;

            if (editOption is EditOption.EditColors or EditOption.Both)
            {
                _targetColor = CalculateNewColor(
                    toolSettings.BrushColor,
                    toolSettings.RangeR,
                    toolSettings.RangeG,
                    toolSettings.RangeB
                );
            }

            // 범위 내의 잔디 인덱스들 가져오기
            _spatialGrid.GetObjectsInRadius(hit.point, toolSettings.BrushSize, sharedIndices);

            // 배치 처리
            ProcessInBatches(sharedIndices, (start, end) =>
                ProcessGrassBatch(start, end, _grassCompute.GrassDataList, toolSettings, editOption));

            // 변경된 데이터가 있을 경우에만 업데이트
            if (_modifiedGrassData.Count > 0)
            {
                ApplyModifications(_grassCompute.GrassDataList);
                _grassCompute.UpdateGrassDataFaster();
            }
        }

        private void ProcessGrassBatch(int startIdx, int endIdx, List<GrassData> grassDataList,
                                       GrassToolSettingSo toolSettings, EditOption editOption)
        {
            for (int i = startIdx; i < endIdx; i++)
            {
                int index = sharedIndices[i];

                // 이미 처리된 인덱스는 건너뛰기
                if (_processedIndices.Contains(index)) continue;

                var grassData = grassDataList[index];
                float distanceSqr = (grassData.position - _currentHitPoint).sqrMagnitude;

                if (distanceSqr <= _currentBrushSizeSqr)
                {
                    float distanceFalloff = 1f - (distanceSqr / _currentBrushSizeSqr);

                    if (editOption is EditOption.EditColors or EditOption.Both)
                    {
                        ProcessColorEdit(ref grassData, index, distanceFalloff);
                    }

                    if (editOption is EditOption.EditWidthHeight or EditOption.Both)
                    {
                        ProcessWidthHeightEdit(ref grassData, index, distanceFalloff, toolSettings);
                    }

                    _modifiedGrassData[index] = grassData;
                    _processedIndices.Add(index);
                }
            }
        }

        private void ProcessColorEdit(ref GrassData grassData, int index, float distanceFalloff)
        {
            _cumulativeChanges.TryAdd(index, 0f);

            _cumulativeChanges[index] = Mathf.Clamp01(
                _cumulativeChanges[index] + _deltaTimeSpeed * distanceFalloff
            );

            var t = _cumulativeChanges[index];
            grassData.color = Vector3.Lerp(grassData.color, _targetColor, t);
        }

        private void ProcessWidthHeightEdit(ref GrassData grassData, int index, float distanceFalloff,
                                            GrassToolSettingSo toolSettings)
        {
            _cumulativeChanges.TryAdd(index, 0f);

            _cumulativeChanges[index] = Mathf.Clamp01(
                _cumulativeChanges[index] + _deltaTimeSpeed * distanceFalloff
            );

            var targetSize = new Vector2(
                grassData.widthHeight.x + toolSettings.AdjustWidth,
                grassData.widthHeight.y + toolSettings.AdjustHeight
            );

            var t = _cumulativeChanges[index];
            grassData.widthHeight = Vector2.Lerp(grassData.widthHeight, targetSize, t);
        }

        private void ApplyModifications(List<GrassData> grassDataList)
        {
            foreach (var kvp in _modifiedGrassData)
            {
                grassDataList[kvp.Key] = kvp.Value;
            }
        }

        private Vector3 CalculateNewColor(Color brushColor, float rangeR, float rangeG, float rangeB)
        {
            return new Vector3(
                brushColor.r + Random.value * rangeR, // Red 값에 랜덤 변화
                brushColor.g + Random.value * rangeG, // Green 값에 랜덤 변화
                brushColor.b + Random.value * rangeB // Blue 값에 랜덤 변화
            );
        }

        public override void Clear()
        {
            base.Clear();
            _cumulativeChanges.Clear();
            if (_processedIndices != null)
            {
                PainterUtils.ReturnHashSet(_processedIndices);
            }
        }
    }
}