using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Grass.Editor;
using UnityEngine;

public class GrassRemovalOperation
{
    private readonly GrassPainterWindow _window;
    private readonly GrassComputeScript _grassCompute;
    private readonly LayerMask _layerMask;
    private const float COLLISION_RADIUS = 0.1f;
    private const float BOUNDS_EXPANSION = 0.1f;

    public GrassRemovalOperation(GrassPainterWindow window, GrassComputeScript grassCompute, LayerMask layerMask)
    {
        _window = window;
        _grassCompute = grassCompute;
        _layerMask = layerMask;
    }

    public async UniTask RemoveGrassFromObjects(GameObject[] selectedObjects)
    {
        var totalOperations = selectedObjects.Length * _grassCompute.GrassDataList.Count;
        var currentOperation = 0;
        var grassToRemove = new HashSet<int>();

        foreach (var obj in selectedObjects)
        {
            var bounds = GetObjectBounds(obj);
            if (!bounds.HasValue) continue;

            for (int i = 0; i < _grassCompute.GrassDataList.Count; i++)
            {
                var grass = _grassCompute.GrassDataList[i];

                if (bounds.Value.Contains(grass.position) && Physics.CheckSphere(grass.position, COLLISION_RADIUS, _layerMask))
                {
                    grassToRemove.Add(i);
                }

                currentOperation++;
                await _window.UpdateProgress(currentOperation, totalOperations, "Removing grass");
            }
        }

        _grassCompute.GrassDataList = _grassCompute.GrassDataList
            .Where((_, i) => !grassToRemove.Contains(i))
            .ToList();
    }

    private Bounds? GetObjectBounds(GameObject obj)
    {
        if (obj.TryGetComponent<Terrain>(out var terrain))
        {
            return new Bounds(
                terrain.transform.position + terrain.terrainData.size / 2,
                terrain.terrainData.size
            );
        }
        
        if (obj.TryGetComponent<Renderer>(out var renderer))
        {
            var bounds = renderer.bounds;
            bounds.Expand(BOUNDS_EXPANSION);
            return bounds;
        }

        return null;
    }
}