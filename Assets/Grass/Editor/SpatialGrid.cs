using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public class SpatialGrid
{
    private HashSet<Vector3Int>[,,] grid;
    private float cellSize;
    private Vector3 origin;

    public SpatialGrid(Bounds bounds, float cellSize)
    {
        this.cellSize = cellSize;
        this.origin = bounds.min;
        
        Vector3 size = bounds.size;
        int xSize = Mathf.CeilToInt(size.x / cellSize);
        int ySize = Mathf.CeilToInt(size.y / cellSize);
        int zSize = Mathf.CeilToInt(size.z / cellSize);
        
        grid = new HashSet<Vector3Int>[xSize, ySize, zSize];
        
        for (int x = 0; x < xSize; x++)
            for (int y = 0; y < ySize; y++)
                for (int z = 0; z < zSize; z++)
                    grid[x, y, z] = new HashSet<Vector3Int>();
    }

    public void AddObject(Vector3 position)
    {
        Vector3Int cell = WorldToCell(position);
        Vector3Int index = GetIndex(cell);
        grid[index.x, index.y, index.z].Add(cell);
    }

    public bool CanPlaceGrass(Vector3 position, float radius)
    {
        Vector3Int cell = WorldToCell(position);
        Vector3Int index = GetIndex(cell);
        
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    Vector3Int checkIndex = new Vector3Int(index.x + x, index.y + y, index.z + z);
                    if (IsValidIndex(checkIndex))
                    {
                        foreach (Vector3Int occupiedCell in grid[checkIndex.x, checkIndex.y, checkIndex.z])
                        {
                            if (Vector3.Distance(CellToWorld(occupiedCell), position) < radius)
                                return false;
                        }
                    }
                }
        
        return true;
    }

    private Vector3Int WorldToCell(Vector3 position)
    {
        return Vector3Int.FloorToInt((position - origin) / cellSize);
    }

    private Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize) + origin;
    }

    private Vector3Int GetIndex(Vector3Int cell)
    {
        return new Vector3Int(
            Mathf.Clamp(cell.x, 0, grid.GetLength(0) - 1),
            Mathf.Clamp(cell.y, 0, grid.GetLength(1) - 1),
            Mathf.Clamp(cell.z, 0, grid.GetLength(2) - 1)
        );
    }

    private bool IsValidIndex(Vector3Int index)
    {
        return index.x >= 0 && index.x < grid.GetLength(0) &&
               index.y >= 0 && index.y < grid.GetLength(1) &&
               index.z >= 0 && index.z < grid.GetLength(2);
    }
}
}