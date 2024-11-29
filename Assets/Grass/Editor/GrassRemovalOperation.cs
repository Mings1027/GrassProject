using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassRemovalOperation
    {
        private readonly GrassPainterWindow _window;
        private readonly GrassComputeScript _grassCompute;
        private readonly SpatialGrid _spatialGrid;
        private readonly List<GrassData> _grassData;

        public GrassRemovalOperation(GrassPainterWindow window, GrassComputeScript grassCompute,
                                     SpatialGrid spatialGrid)
        {
            _window = window;
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
            _grassData = grassCompute.GrassDataList;
        }

        public async UniTask RemoveGrassFromObjects(GameObject[] selectedObjects)
        {
            var grassPositionsToRemove = await FindGrassToRemove(selectedObjects);
            if (grassPositionsToRemove.Count == 0)
            {
                Debug.Log("No grass to remove");
                return;
            }

            await RemoveGrassAtPositions(grassPositionsToRemove);
        }

        private async UniTask<HashSet<Vector3>> FindGrassToRemove(GameObject[] selectedObjects)
        {
            var positionsToRemove = new HashSet<Vector3>();
            var tempIndices = new List<int>();

            for (var i = 0; i < selectedObjects.Length; i++)
            {
                var obj = selectedObjects[i];
                await ProcessSingleObject(obj, i, selectedObjects.Length, tempIndices, positionsToRemove);
            }

            return positionsToRemove;
        }

        private async UniTask ProcessSingleObject(GameObject obj, int currentIndex,
                                                  int totalObjects, List<int> tempIndices,
                                                  HashSet<Vector3> positionsToRemove)
        {
            var bound = GrassPainterHelper.GetObjectBounds(obj);
            if (!bound.HasValue) return;

            await CollectGrassPositionsInBounds(bound.Value, tempIndices, positionsToRemove);
            await UpdateRemovalProgress(currentIndex, totalObjects, $"Scanning grass on '{obj.name}'");
        }

        private async UniTask CollectGrassPositionsInBounds(Bounds bounds, List<int> tempIndices,
                                                            HashSet<Vector3> positionsToRemove)
        {
            tempIndices.Clear();

            var expandedRadius = bounds.extents.magnitude * 1.1f;
            await _spatialGrid.GetObjectsInRadiusAsync(bounds.center, expandedRadius, tempIndices);

            foreach (var index in tempIndices)
            {
                if (index < _grassData.Count)
                {
                    positionsToRemove.Add(_grassData[index].position);
                }
            }
        }

        private async UniTask RemoveGrassAtPositions(HashSet<Vector3> positionsToRemove)
        {
            const int progressUpdateInterval = 1000; // 진행상태 업데이트 간격
            var remainingGrass = new List<GrassData>();
            var removedCount = 0;

            // 병렬 처리를 위해 데이터를 청크로 나누어 처리
            await UniTask.RunOnThreadPool(() =>
            {
                for (var i = 0; i < _grassData.Count; i++)
                {
                    var grassData = _grassData[i];
                    if (!positionsToRemove.Contains(grassData.position))
                    {
                        remainingGrass.Add(grassData);
                    }
                    else
                    {
                        removedCount++;
                    }

                    // progressUpdateInterval마다 진행상태 업데이트
                    if (i % progressUpdateInterval == 0)
                    {
                        UpdateRemovalProgress(i, _grassData.Count, "Removing Grass").Forget();
                    }
                }
            });

            // 최종 진행상태 업데이트
            await UpdateRemovalProgress(_grassData.Count, _grassData.Count, "Removing Grass");

            _grassCompute.GrassDataList = remainingGrass;
            _window.InitSpatialGrid();

            Debug.Log($"Removed {removedCount} grass instances in total");
        }

        private async UniTask UpdateRemovalProgress(int current, int total, string message)
        {
            await _window.UpdateProgress(current, total, message);
        }
    }
}