using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassRemovePainter
    {
        private readonly HashSet<Vector3> _grassMarkedForRemoval = new();
        private readonly List<Vector3> _removalPositions = new();
        private Vector3 _lastRemovePos;
        private bool _isDragging;

        private List<GrassData> _grassData;
        private GrassTileSystem _grassTileSystem;

        public bool HasMarkedGrass => _grassMarkedForRemoval.Count > 0;
        public bool IsDragging => _isDragging;
        public IReadOnlyList<Vector3> RemovalPositions => _removalPositions;

        public GrassRemovePainter(List<GrassData> grassData)
        {
            Init(grassData);
        }

        public void Init(List<GrassData> grassData)
        {
            _grassData = grassData;
            _grassTileSystem ??= new GrassTileSystem(_grassData, 1);
        }

        public void MarkGrassForRemoval(Vector3 hitPoint, float radius)
        {
            var indices = _grassTileSystem.GetNearbyIndices(hitPoint, radius);

            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index < _grassData.Count)
                {
                    if (Vector3.SqrMagnitude(_grassData[index].position - hitPoint) <= radius * radius)
                    {
                        _grassMarkedForRemoval.Add(_grassData[index].position);

                        var halfBrushSize = radius * 0.5f;
                        if (Vector3.SqrMagnitude(hitPoint - _lastRemovePos) >= halfBrushSize * halfBrushSize)
                        {
                            _removalPositions.Add(hitPoint);
                            _lastRemovePos = hitPoint;
                        }
                    }
                }
            }

            _isDragging = true;
        }

        public async UniTask RemoveMarkedGrass(CancellationToken cancellationToken,
                                               Action<float, string> progressCallback)
        {
            if (_grassMarkedForRemoval.Count <= 0) return;

            const int batchSize = 10000;
            var totalBatches = (_grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask<List<GrassData>>[totalBatches];
            var removedCounts = new int[totalBatches];
            var totalRemoveGrassCount = _grassMarkedForRemoval.Count;

            // 각 배치별로 병렬 처리
            for (var index = 0; index < totalBatches; index++)
            {
                var start = index * batchSize;
                var end = Mathf.Min(start + batchSize, _grassData.Count);
                var localBatchIndex = index;

                tasks[index] = UniTask.RunOnThreadPool(() =>
                {
                    var localGrassToKeep = new List<GrassData>();
                    var localRemovedCount = 0;

                    for (var i = start; i < end; i++)
                    {
                        if (!_grassMarkedForRemoval.Contains(_grassData[i].position))
                        {
                            localGrassToKeep.Add(_grassData[i]);
                        }
                        else
                        {
                            localRemovedCount++;
                        }
                    }

                    removedCounts[localBatchIndex] = localRemovedCount;
                    return localGrassToKeep;
                }, cancellationToken: cancellationToken);

                progressCallback((float)index / totalRemoveGrassCount,
                    $"Removing grass - {index}/{totalRemoveGrassCount}");
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            // 모든 배치의 결과를 수집하고 병합
            var results = await UniTask.WhenAll(tasks);
            var finalGrassToKeep = new List<GrassData>();

            for (var index = 0; index < results.Length; index++)
            {
                finalGrassToKeep.AddRange(results[index]);
                progressCallback((float)index / results.Length, $"Merging results - {index}/{results.Length}");
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            // 결과 적용
            _grassData.Clear();
            _grassData.AddRange(finalGrassToKeep);

            await _grassTileSystem.UpdateTileSystem(_grassData);

            Debug.Log($"Removed {totalRemoveGrassCount} grass instances in total");
        }

        public void Clear()
        {
            _grassMarkedForRemoval.Clear();
            _removalPositions.Clear();
            _isDragging = false;
        }
    }
}