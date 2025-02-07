using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Grass.Editor
{
    public class GrassRemovalOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly SpatialGrid _spatialGrid;
        private readonly List<int> _tempList = new();
        private readonly HashSet<int> _grassToRemove = new();

        public GrassRemovalOperation(GrassPainterTool tool, GrassComputeScript grassCompute,
                                     SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public async UniTask RemoveGrass(GameObject[] selectedObjects)
        {
            var meshObjects = new List<MeshFilter>();
            var terrainObjects = new List<Terrain>();

            foreach (var obj in selectedObjects)
            {
                if (obj.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    meshObjects.Add(meshFilter);
                }

                if (obj.TryGetComponent<Terrain>(out var terrain))
                {
                    terrainObjects.Add(terrain);
                }
            }

            _grassToRemove.Clear();
            var totalGrassToCheck = 0;
            var originalLayers = new Dictionary<MeshFilter, int>();

            try
            {
                // First pass - Calculate total grass to check and setup temporary layers
                const int tempLayer = 31;
                foreach (var meshFilter in meshObjects)
                {
                    if (!meshFilter.TryGetComponent<MeshRenderer>(out var renderer)) continue;

                    var bounds = renderer.bounds;
                    bounds.Expand(0.1f);

                    _tempList.Clear();
                    _spatialGrid.GetObjectsInBounds(bounds, _tempList);
                    totalGrassToCheck += _tempList.Count;

                    originalLayers[meshFilter] = meshFilter.gameObject.layer;
                    meshFilter.gameObject.layer = tempLayer;
                }

                foreach (var terrain in terrainObjects)
                {
                    var bounds = GetTerrainBounds(terrain);
                    _tempList.Clear();
                    _spatialGrid.GetObjectsInBounds(bounds, _tempList);
                    _grassToRemove.UnionWith(_tempList);
                    totalGrassToCheck += _tempList.Count;
                }

                // Process mesh filters
                var currentProgress = 0;
                var checkMask = 1 << tempLayer;

                foreach (var meshFilter in meshObjects)
                {
                    if (!meshFilter.TryGetComponent<MeshRenderer>(out var renderer)) continue;

                    var bounds = renderer.bounds;
                    bounds.Expand(0.1f);

                    _tempList.Clear();
                    _spatialGrid.GetObjectsInBounds(bounds, _tempList);

                    foreach (var grassIndex in _tempList)
                    {
                        if (_grassToRemove.Contains(grassIndex))
                        {
                            currentProgress++;
                            continue;
                        }

                        var grassPosition = _grassCompute.GrassDataList[grassIndex].position;
                        if (Physics.CheckSphere(grassPosition, 0.05f, checkMask))
                        {
                            _grassToRemove.Add(grassIndex);
                        }

                        currentProgress++;
                        await _tool.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass from objects");
                    }
                }

                // Update grass data
                if (_grassToRemove.Count > 0)
                {
                    var updatedList = new List<GrassData>();
                    for (var index = 0; index < _grassCompute.GrassDataList.Count; index++)
                    {
                        if (!_grassToRemove.Contains(index))
                        {
                            updatedList.Add(_grassCompute.GrassDataList[index]);
                        }
                    }

                    _grassCompute.GrassDataList = updatedList;
                }
            }
            finally
            {
                // Restore original layers
                foreach (var (meshFilter, originalLayer) in originalLayers)
                {
                    if (meshFilter != null)
                    {
                        meshFilter.gameObject.layer = originalLayer;
                    }
                }
            }
        }

        private static Bounds GetTerrainBounds(Terrain terrain)
        {
            var size = terrain.terrainData.size;
            return new Bounds(
                terrain.transform.position + size / 2f,
                size
            );
        }
    }
}