using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Grass.Editor;
using UnityEngine;

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
        var objectBoundsList = new List<Bounds>();

        // First pass - collect bounds and calculate total grass to check
        foreach (var meshFilter in meshFilters)
        {
            var bounds = GetObjectBounds(meshFilter);
            if (!bounds.HasValue) continue;

            objectBoundsList.Add(bounds.Value);

            _tempList.Clear();
            _spatialGrid.GetObjectsInBounds(bounds.Value, _tempList);
            totalGrassToCheck += _tempList.Count;
        }

        // Second pass - actual removal with progress tracking
        var currentProgress = 0;
        foreach (var bounds in objectBoundsList)
        {
            _tempList.Clear();
            _spatialGrid.GetObjectsInBounds(bounds, _tempList);

            foreach (var grassIndex in _tempList)
            {
                // Skip if already marked for removal
                if (grassToRemove.Contains(grassIndex))
                {
                    currentProgress++;
                    continue;
                }

                var grassPosition = _grassCompute.GrassDataList[grassIndex].position;
                if (bounds.Contains(grassPosition))
                {
                    grassToRemove.Add(grassIndex);
                }

                currentProgress++;
                await _tool.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass");
            }
        }

        if (grassToRemove.Count > 0)
        {
            var list = new List<GrassData>();
            for (int index = 0; index < _grassCompute.GrassDataList.Count; index++)
            {
                if (grassToRemove.Contains(index)) continue;
                list.Add(_grassCompute.GrassDataList[index]);
            }

            _grassCompute.GrassDataList = list;
        }
    }

    private Bounds? GetObjectBounds(MeshFilter obj)
    {
        if (obj.TryGetComponent<Renderer>(out var renderer))
        {
            var bounds = renderer.bounds;
            // bounds.Expand(BOUNDS_EXPANSION);
            return bounds;
        }

        return null;
    }
}