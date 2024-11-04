using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid
{
    private readonly Dictionary<long, HashSet<int>> _grid;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    private readonly Queue<HashSet<int>> _hashSetPool;

    private const int PoolSize = 100;

    // 임시 리스트를 재사용하기 위한 필드
    private readonly HashSet<long> _tempCellKeys = new();
    
    public int TotalObjectCount
    {
        get
        {
            var count = 0;
            foreach (var cell in _grid.Values)
            {
                count += cell.Count;
            }

            return count;
        }
    }

    public float CellSize => _cellSize;

    public SpatialGrid(Bounds bounds, float cellSize)
    {
        _cellSize = cellSize;
        _origin = bounds.min;
        _grid = new Dictionary<long, HashSet<int>>(1000);
        _hashSetPool = new Queue<HashSet<int>>(PoolSize);
        InitializePool();
    }

    private void InitializePool()
    {
        for (var i = 0; i < PoolSize; i++)
        {
            _hashSetPool.Enqueue(new HashSet<int>(50));
        }
    }

    private HashSet<int> GetHashSet()
    {
        if (_hashSetPool.Count > 0)
        {
            return _hashSetPool.Dequeue();
        }

        return new HashSet<int>(50);
    }

    private void ReturnHashSet(HashSet<int> hashSet)
    {
        hashSet.Clear();
        if (_hashSetPool.Count < PoolSize)
        {
            _hashSetPool.Enqueue(hashSet);
        }
    }

    public static long GetKey(int x, int y, int z)
    {
        // 비트 마스크를 상수로 정의
        const long mask = 0x1FFFFF;
        return (x & mask) | ((y & mask) << 21) | ((z & mask) << 42);
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
            cellSet = GetHashSet();
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
                ReturnHashSet(cellSet);
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
            ReturnHashSet(cellSet);
        }

        _grid.Clear();
        _tempCellKeys.Clear();
    }
}