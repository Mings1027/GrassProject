using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid
{
    private readonly Dictionary<long, HashSet<int>> _grid;
    private readonly float _cellSize;
    private readonly Vector3 _origin;
    private readonly Bounds _bounds;
    private readonly Dictionary<long, Bounds> _cellBounds = new();

    public float CellSize => _cellSize;
    public Bounds Bounds => _bounds;

    public SpatialGrid(Bounds bounds, float cellSize)
    {
        _cellSize = cellSize;
        _origin = bounds.min;
        _bounds = bounds;
        _grid = new Dictionary<long, HashSet<int>>();
    }

    public static long GetKey(int x, int y, int z)
    {
        return ((long)x & 0x1FFFFF) | 
               (((long)y & 0x1FFFFF) << 21) | 
               (((long)z & 0x1FFFFF) << 42);
    }

    public void AddObject(Vector3 position, int index)
    {
        var cell = WorldToCell(position);
        var key = GetKey(cell.x, cell.y, cell.z);

        if (!_grid.TryGetValue(key, out var cellSet))
        {
            cellSet = new HashSet<int>();
            _grid[key] = cellSet;
        }

        cellSet.Add(index);

        // 셀의 경계를 캐시
        if (!_cellBounds.ContainsKey(key))
        {
            var cellCenter = CellToWorld(cell) + new Vector3(_cellSize * 0.5f, _cellSize * 0.5f, _cellSize * 0.5f);
            _cellBounds[key] = new Bounds(cellCenter, new Vector3(_cellSize, _cellSize, _cellSize));
        }
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
                _cellBounds.Remove(key);
            }
        }
    }

    public void GetObjectsInRadius(Vector3 position, float radius, List<int> results)
    {
        results.Clear();
        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToCell(position);
        var radiusSqr = radius * radius;

        for (var x = -cellRadius; x <= cellRadius; x++)
        for (var y = -cellRadius; y <= cellRadius; y++)
        for (var z = -cellRadius; z <= cellRadius; z++)
        {
            var checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z + z);
            var key = GetKey(checkCell.x, checkCell.y, checkCell.z);

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
        _grid.Clear();
        _cellBounds.Clear();
    }
    
}