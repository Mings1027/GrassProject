using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassRemovalOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly SpatialGrid _spatialGrid;
        private readonly List<int> _tempList = new();

        public GrassRemovalOperation(GrassPainterTool tool, GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public async UniTask RemoveGrass(List<MeshFilter> meshFilters)
        {
            var grassToRemove = new HashSet<int>();
            var totalGrassToCheck = 0;
            var originalLayers = new Dictionary<MeshFilter, int>();

            try
            {
                // First pass - setup temporary layer and calculate total grass
                const int tempLayer = 31; // 임시로 사용할 레이어
                foreach (var meshFilter in meshFilters)
                {
                    if (!meshFilter.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                
                    var bounds = renderer.bounds;
                    bounds.Expand(0.1f);
                
                    _tempList.Clear();
                    _spatialGrid.GetObjectsInBounds(bounds, _tempList);
                    totalGrassToCheck += _tempList.Count;

                    // 원래 레이어 저장하고 임시 레이어로 변경
                    originalLayers[meshFilter] = meshFilter.gameObject.layer;
                    meshFilter.gameObject.layer = tempLayer;
                }

                // Second pass - check grass positions
                var currentProgress = 0;
                var checkMask = 1 << tempLayer;

                foreach (var meshFilter in meshFilters)
                {
                    if (!meshFilter.TryGetComponent<MeshRenderer>(out var renderer)) continue;

                    var bounds = renderer.bounds;
                    bounds.Expand(0.1f);

                    _tempList.Clear();
                    _spatialGrid.GetObjectsInBounds(bounds, _tempList);

                    foreach (var grassIndex in _tempList)
                    {
                        if (grassToRemove.Contains(grassIndex))
                        {
                            currentProgress++;
                            continue;
                        }

                        var grassPosition = _grassCompute.GrassDataList[grassIndex].position;
                        if (Physics.CheckSphere(grassPosition, 0.05f, checkMask))
                        {
                            grassToRemove.Add(grassIndex);
                        }

                        currentProgress++;
                        await _tool.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass from meshes");
                    }
                }

                if (grassToRemove.Count > 0)
                {
                    var updatedList = new List<GrassData>();
                    for (var index = 0; index < _grassCompute.GrassDataList.Count; index++)
                    {
                        if (!grassToRemove.Contains(index))
                        {
                            updatedList.Add(_grassCompute.GrassDataList[index]);
                        }
                    }

                    _grassCompute.GrassDataList = updatedList;
                }
            }
            finally
            {
                // 원래 레이어로 복원
                foreach (var (meshFilter, originalLayer) in originalLayers)
                {
                    if (meshFilter != null)
                    {
                        meshFilter.gameObject.layer = originalLayer;
                    }
                }
            }
        }
    }
}