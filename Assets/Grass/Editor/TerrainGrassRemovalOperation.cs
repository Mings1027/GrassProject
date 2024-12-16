using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Grass.Editor;
using UnityEngine;

public class TerrainGrassRemovalOperation
{
    private readonly GrassPainterTool _tool;
    private readonly GrassComputeScript _grassCompute;
    private readonly SpatialGrid _spatialGrid;
    private readonly List<int> _tempList = new();

    public TerrainGrassRemovalOperation(GrassPainterTool tool, GrassComputeScript grassCompute, SpatialGrid spatialGrid)
    {
        _tool = tool;
        _grassCompute = grassCompute;
        _spatialGrid = spatialGrid;
    }

    public async UniTask RemoveGrass(List<Terrain> terrains)
    {
        var grassToRemove = new HashSet<int>();
        var totalGrassToCheck = 0;

        // First pass - collect all grass in terrain bounds
        foreach (var terrain in terrains)
        {
            var bounds = GetTerrainBounds(terrain);
            _tempList.Clear();
            _spatialGrid.GetObjectsInBounds(bounds, _tempList);
            totalGrassToCheck += _tempList.Count;
        }

        // Second pass - remove grass
        var currentProgress = 0;
        foreach (var terrain in terrains)
        {
            var bounds = GetTerrainBounds(terrain);
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
                if (bounds.Contains(grassPosition))
                {
                    grassToRemove.Add(grassIndex);
                }

                currentProgress++;
                await _tool.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass from terrains");
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

    private Bounds GetTerrainBounds(Terrain terrain)
    {
        var size = terrain.terrainData.size;
        return new Bounds(
            terrain.transform.position + size / 2f,
            size
        );
    }
}