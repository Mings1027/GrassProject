using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Grass.Editor;
using UnityEngine;

public class GrassRemovalOperation
{
    private readonly GrassPainterWindow _window;
    private readonly GrassComputeScript _grassCompute;
    private readonly SpatialGrid _spatialGrid;
    private readonly LayerMask _layerMask;
    private readonly List<int> _tempList = new();
    private const float CollisionRadius = 0.1f;

    public GrassRemovalOperation(GrassPainterWindow window, GrassComputeScript grassCompute, SpatialGrid spatialGrid,
                                 LayerMask layerMask)
    {
        _window = window;
        _grassCompute = grassCompute;
        _spatialGrid = spatialGrid;
        _layerMask = layerMask;
    }

    public async UniTask RemoveGrassFromObjects(GameObject[] selectedObjects)
    {
        var grassToRemove = new HashSet<int>();
        var totalGrassToCheck = 0;
        var currentProgress = 0;

        // First pass - get total grass count to check
        foreach (var obj in selectedObjects)
        {
            var bounds = GetObjectBounds(obj);
            if (!bounds.HasValue) continue;

            _tempList.Clear();
            _spatialGrid.GetObjectsInBounds(bounds.Value, _tempList);
            totalGrassToCheck += _tempList.Count;
        }

        // Second pass - actual removal with continuous progress
        foreach (var obj in selectedObjects)
        {
            var bounds = GetObjectBounds(obj);
            if (!bounds.HasValue) continue;

            foreach (var grassIndex in _tempList)
            {
                var grassPosition = _grassCompute.GrassDataList[grassIndex].position;
                if (bounds.Value.Contains(grassPosition) &&
                    Physics.CheckSphere(grassPosition, CollisionRadius, _layerMask))
                {
                    grassToRemove.Add(grassIndex);
                }

                currentProgress++;
                await _window.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass");
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

    private Bounds? GetObjectBounds(GameObject obj)
    {
        if (obj.TryGetComponent<Terrain>(out var terrain))
        {
            return new Bounds(terrain.transform.position + terrain.terrainData.size / 2, terrain.terrainData.size);
        }

        if (obj.TryGetComponent<Renderer>(out var renderer))
        {
            var bounds = renderer.bounds;
            // bounds.Expand(BOUNDS_EXPANSION);
            return bounds;
        }

        return null;
    }
}