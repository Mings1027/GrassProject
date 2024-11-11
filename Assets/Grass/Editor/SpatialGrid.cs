using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid
{
    private readonly Dictionary<long, HashSet<int>> _grid = CollectionsPool.GetDictionary<long, HashSet<int>>();
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    // 임시 리스트를 재사용하기 위한 필드
    private readonly HashSet<long> _tempCellKeys = CollectionsPool.GetHashSet<long>();

    public float CellSize => _cellSize;

    public SpatialGrid(Bounds bounds, float cellSize)
    {
        _cellSize = cellSize;
        _origin = bounds.min;
    }

    public static long GetKey(int x, int y, int z)
    {
        const int bits = 21;
        const long mask = ((long)1 << bits) - 1;
        return (x & mask) | ((y & mask) << bits) | ((z & mask) << bits * 2);
    }

    public bool HasAnyObject(long key)
    {
        return _grid.TryGetValue(key, out var cellSet) && cellSet.Count > 0;
    }

    public void AddObject(Vector3 position, int index)
    {
        var cell = WorldToCell(position);
        var key = GetKey(cell.x, cell.y, cell.z);

        if (!_grid.TryGetValue(key, out var cellSet))
        {
            cellSet = CollectionsPool.GetHashSet<int>(1);
            _grid[key] = cellSet;
        }

        cellSet.Add(index);
    }

    public void RemoveObject(Vector3 position, int index)
    {
        var cell = WorldToCell(position);
        var key = GetKey(cell.x, cell.y, cell.z);

        if (_grid.TryGetValue(key, out var cellSet))
        {
            cellSet.Remove(index);
            if (cellSet.Count == 0)
            {
                _grid.Remove(key);
                CollectionsPool.ReturnHashSet(cellSet);
            }
        }
    }

    public void GetObjectsInRadius(Vector3 position, float radius, List<int> results)
    {
        results.Clear();
        _tempCellKeys.Clear();

        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToCell(position);

        for (var x = -cellRadius; x <= cellRadius; x++)
        for (var y = -cellRadius; y <= cellRadius; y++)
        for (var z = -cellRadius; z <= cellRadius; z++)
        {
            var checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z + z);
            var key = GetKey(checkCell.x, checkCell.y, checkCell.z);

            // 이미 처리한 셀은 건너뛰기
            if (!_tempCellKeys.Add(key)) continue;

            if (_grid.TryGetValue(key, out var indices))
            {
                results.AddRange(indices);
            }
        }
    }

    public Vector3Int WorldToCell(Vector3 position)
    {
        var relativePosition = position - _origin;
        return new Vector3Int(
            Mathf.FloorToInt(relativePosition.x / _cellSize),
            Mathf.FloorToInt(relativePosition.y / _cellSize),
            Mathf.FloorToInt(relativePosition.z / _cellSize)
        );
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(
            cell.x * _cellSize + _origin.x,
            cell.y * _cellSize + _origin.y,
            cell.z * _cellSize + _origin.z
        );
    }

    public void Clear()
    {
        foreach (var cellSet in _grid.Values)
        {
            CollectionsPool.ReturnHashSet(cellSet);
        }

        _grid.Clear();
        _tempCellKeys.Clear();

        CollectionsPool.ReturnDictionary(_grid);
        CollectionsPool.ReturnHashSet(_tempCellKeys);
    }
}