using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SpatialGrid
{
    private readonly Dictionary<long, HashSet<int>> _grid = new();
    private float _cellSize;
    private readonly Vector3 _origin;

    // 임시 리스트를 재사용하기 위한 필드
    private readonly HashSet<long> _tempCellKeys = new();

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

    // 특정 셀의 모든 오브젝트 인덱스를 가져오는 메서드
    public void GetObjectsInCell(long key, List<int> results)
    {
        if (_grid.TryGetValue(key, out var cellSet))
        {
            results.AddRange(cellSet);
        }
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
                cellSet.Clear();
            }
        }
    }

    public void GetObjectsInRadius(Vector3 position, float radius, List<int> results)
    {
        results.Clear();
        _tempCellKeys.Clear();

        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToCell(position);
        var radiusSqr = radius * radius;

        // 셀 대각선의 제곱 길이를 미리 계산
        var cellDiagonalSqr = _cellSize * _cellSize * 3;

        // 예상되는 셀 수만큼 용량 미리 할당
        var estimatedCells = (cellRadius * 2 + 1) * (cellRadius * 2 + 1) * (cellRadius * 2 + 1);
        _tempCellKeys.EnsureCapacity(estimatedCells);

        for (var x = -cellRadius; x <= cellRadius; x++)
        {
            // x 거리의 제곱을 미리 계산
            var xOffset = x * _cellSize;
            var xDistSqr = xOffset * xOffset;

            for (var y = -cellRadius; y <= cellRadius; y++)
            {
                // xy 평면상의 거리 제곱을 미리 계산
                var yOffset = y * _cellSize;
                var xyDistSqr = xDistSqr + (yOffset * yOffset);

                // 현재 xy 위치가 이미 반경을 벗어났다면 z축 검사 스킵
                if (xyDistSqr - cellDiagonalSqr > radiusSqr) continue;

                for (var z = -cellRadius; z <= cellRadius; z++)
                {
                    // 최종 거리 제곱 계산
                    var zOffset = z * _cellSize;
                    var totalDistSqr = xyDistSqr + (zOffset * zOffset);

                    // 셀의 대각선 길이를 고려한 거리 체크
                    if (totalDistSqr - cellDiagonalSqr > radiusSqr) continue;

                    var checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z + z);
                    var key = GetKey(checkCell.x, checkCell.y, checkCell.z);

                    // 이미 처리한 셀은 스킵
                    if (!_tempCellKeys.Add(key)) continue;

                    if (_grid.TryGetValue(key, out var indices))
                    {
                        results.AddRange(indices);
                    }
                }
            }
        }
    }

    public async UniTask GetObjectsInRadiusAsync(Vector3 position, float radius, List<int> results)
    {
        results.Clear();
        _tempCellKeys.Clear();

        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToCell(position);
        var radiusSqr = radius * radius;

        var estimatedCells = (cellRadius * 2 + 1) * (cellRadius * 2 + 1) * (cellRadius * 2 + 1);
        _tempCellKeys.EnsureCapacity(estimatedCells);

        var cellDiagonalSqr = _cellSize * _cellSize * 3;

        for (var x = -cellRadius; x <= cellRadius; x++)
        {
            var xOffset = x * _cellSize;
            var xDistSqr = xOffset * xOffset;

            for (var y = -cellRadius; y <= cellRadius; y++)
            {
                var yOffset = y * _cellSize;
                var xyDistSqr = xDistSqr + (yOffset * yOffset);

                if (xyDistSqr - cellDiagonalSqr > radiusSqr) continue;

                for (var z = -cellRadius; z <= cellRadius; z++)
                {
                    var zOffset = z * _cellSize;
                    var totalDistSqr = xyDistSqr + (zOffset * zOffset);

                    if (totalDistSqr - cellDiagonalSqr > radiusSqr) continue;

                    var checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z + z);
                    var key = GetKey(checkCell.x, checkCell.y, checkCell.z);

                    if (!_tempCellKeys.Add(key)) continue;

                    if (_grid.TryGetValue(key, out var indices))
                    {
                        results.AddRange(indices);
                    }
                }
            }

            // 진행 상황 업데이트 (x 좌표 기준으로)
            if (x % 2 == 0) // 모든 x값에 대해 하면 너무 자주 업데이트될 수 있으므로
            {
                await UniTask.Yield(); // UI 업데이트 기회 제공
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
            cellSet.Clear();
        }

        _grid.Clear();
        _tempCellKeys.Clear();

        _grid.Clear();
        _tempCellKeys.Clear();
    }
}