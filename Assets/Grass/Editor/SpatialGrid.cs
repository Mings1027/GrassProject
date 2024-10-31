using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid
{
    private readonly Dictionary<long, HashSet<Vector3Int>> _grid;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    public SpatialGrid(Bounds bounds, float cellSize)
    {
        _cellSize = cellSize;
        _origin = bounds.min;
        _grid = new Dictionary<long, HashSet<Vector3Int>>();
    }

    public static long GetKey(int x, int y, int z)
    {
        // 좌표를 하나의 long 값으로 인코딩
        return ((long)x & 0x1FFFFF) |
               (((long)y & 0x1FFFFF) << 21) |
               (((long)z & 0x1FFFFF) << 42);
    }

    public void AddObject(Vector3 position)
    {
        var cell = WorldToCell(position);
        var key = GetKey(cell.x, cell.y, cell.z);

        if (!_grid.TryGetValue(key, out var cellSet))
        {
            cellSet = new HashSet<Vector3Int>();
            _grid[key] = cellSet;
        }

        cellSet.Add(cell);
    }

    public bool CanPlaceGrass(Vector3 position, float radius)
    {
        var cell = WorldToCell(position);

        // 주변 셀 체크
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        for (var z = -1; z <= 1; z++)
        {
            var checkCell = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
            var key = GetKey(checkCell.x, checkCell.y, checkCell.z);

            if (_grid.TryGetValue(key, out var cellSet))
            {
                foreach (var occupiedCell in cellSet)
                {
                    if (Vector3.Distance(CellToWorld(occupiedCell), position) < radius)
                        return false;
                }
            }
        }

        return true;
    }

    public void RemoveObject(Vector3 position)
    {
        var cell = WorldToCell(position);
        var key = GetKey(cell.x, cell.y, cell.z);

        if (_grid.TryGetValue(key, out var cellSet))
        {
            cellSet.Remove(cell);
            if (cellSet.Count == 0)
            {
                _grid.Remove(key);
            }
        }
    }

    // 전체 초기화 메서드 추가
    public void Clear()
    {
        _grid.Clear();
    }

    // 그리드 재구성 메서드 추가
    public void Rebuild(List<GrassData> grassDataList)
    {
        Clear();
        foreach (var grass in grassDataList)
        {
            AddObject(grass.position);
        }
    }

    public Vector3Int WorldToCell(Vector3 position)
    {
        return Vector3Int.FloorToInt((position - _origin) / _cellSize);
    }

    private Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(cell.x * _cellSize, cell.y * _cellSize, cell.z * _cellSize) + _origin;
    }
}