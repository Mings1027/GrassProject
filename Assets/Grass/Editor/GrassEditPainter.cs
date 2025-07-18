using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public sealed class GrassEditPainter : BasePainter
    {
        private readonly Dictionary<int, float> _cumulativeChanges = new();
        private readonly Dictionary<int, GrassData> _modifiedGrassData = new();
        private readonly HashSet<int> _processedIndices = new();

        private float _currentBrushSizeSqr;
        private Vector3 _currentHitPoint;

        public GrassEditPainter(GrassCompute grassCompute, SpatialGrid spatialGrid) : base(grassCompute,
            spatialGrid) { }

        public void EditGrass(Ray mousePointRay, GrassToolSettingSo toolSettings, EditOption editOption)
        {
            if (!Physics.Raycast(mousePointRay, out var hit, grassCompute.GrassSetting.maxFadeDistance)) return;

            sharedIndices.Clear();
            _processedIndices.Clear();
            _modifiedGrassData.Clear();

            // 자주 사용되는 값들을 미리 계산
            _currentHitPoint = hit.point;
            _currentBrushSizeSqr = toolSettings.BrushSize * toolSettings.BrushSize;

            // 범위 내의 잔디 인덱스들 가져오기
            spatialGrid.GetObjectsInRadius(hit.point, toolSettings.BrushSize, sharedIndices);

            ProcessGrassBatch(grassCompute.GrassDataList, toolSettings, editOption);

            // 변경된 데이터가 있을 경우에만 업데이트
            if (_modifiedGrassData.Count > 0)
            {
                ApplyModifications(grassCompute.GrassDataList);
                var (startIndex, count) = GetModifiedRange();
                grassCompute.UpdateGrassDataFaster(startIndex, count);
            }
        }

        private (int startIndex, int count) GetModifiedRange()
        {
            if (_modifiedGrassData.Count == 0)
                return (-1, 0);

            var minIndex = int.MaxValue;
            var maxIndex = int.MinValue;

            foreach (var index in _modifiedGrassData.Keys)
            {
                minIndex = Mathf.Min(minIndex, index);
                maxIndex = Mathf.Max(maxIndex, index);
            }

            return (minIndex, maxIndex - minIndex + 1);
        }

        private void ProcessGrassBatch(List<GrassData> grassDataList, GrassToolSettingSo toolSettings,
                                       EditOption editOption)
        {
            for (var i = 0; i < sharedIndices.Count; i++)
            {
                var index = sharedIndices[i];

                if (index < 0 || index >= grassDataList.Count)
                    continue;

                // 이미 처리된 인덱스는 건너뛰기
                if (_processedIndices.Contains(index)) continue;

                var grassData = grassDataList[index];
                var distanceSqr = (grassData.position - _currentHitPoint).sqrMagnitude;

                if (distanceSqr <= _currentBrushSizeSqr)
                {
                    _cumulativeChanges.TryAdd(index, 0f);

                    var currentValue = _cumulativeChanges[index];
                    var speed = Mathf.Pow(toolSettings.BrushTransitionSpeed, 2);
                    _cumulativeChanges[index] = Mathf.Clamp01(currentValue + speed);

                    if (editOption is EditOption.EditColors or EditOption.Both)
                    {
                        ProcessColorEdit(ref grassData, index, toolSettings);
                    }

                    if (editOption == EditOption.EditWidthHeight)
                    {
                        if (toolSettings.SetGrassSizeImmediately)
                        {
                            InstantSizeChange(ref grassData, index, toolSettings);
                        }
                        else
                        {
                            ProcessWidthHeightEdit(ref grassData, index, toolSettings);
                        }
                    }
                    else if (editOption == EditOption.Both)
                    {
                        ProcessWidthHeightEdit(ref grassData, index, toolSettings);
                    }

                    _modifiedGrassData[index] = grassData;
                    _processedIndices.Add(index);
                }
            }
        }

        private void ProcessColorEdit(ref GrassData grassData, int index, GrassToolSettingSo toolSettings)
        {
            var targetColor = CalculateNewColor(
                toolSettings.BrushColor,
                toolSettings.RangeR,
                toolSettings.RangeG,
                toolSettings.RangeB
            );
            var t = _cumulativeChanges[index];
            grassData.color = Vector3.Lerp(grassData.color, targetColor, t);
        }

        private void ProcessWidthHeightEdit(ref GrassData grassData, int index, GrassToolSettingSo toolSettings)
        {
            var targetSize = new Vector2(
                grassData.widthHeight.x + toolSettings.AdjustWidth,
                grassData.widthHeight.y + toolSettings.AdjustHeight
            );

            var t = _cumulativeChanges[index];
            grassData.widthHeight = Vector2.Lerp(grassData.widthHeight, targetSize, t);
        }

        private void InstantSizeChange(ref GrassData grassData, int index, GrassToolSettingSo toolSettings)
        {
            grassData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
            _modifiedGrassData[index] = grassData;
            _processedIndices.Add(index);
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
            _modifiedGrassData.Clear();
            _processedIndices.Clear();
        }
    }
}