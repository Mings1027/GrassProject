using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;

public class OptimizedGrassCulling
{
    private readonly int[] _cellOccupancy;
    private readonly List<int>[] _grassPerCell;
    private readonly Vector3Int _gridDimensions;
    private readonly float _cellSize;
    private readonly Bounds _worldBounds;
    private readonly HashSet<int> _visibleGrassIds = new();
    private readonly Dictionary<int, Vector3> _grassPositions = new();

    // 캐시 추가
    private readonly Vector3[] _cellCenters;
    private readonly Bounds[] _cellBounds;
    private readonly List<int> _tempResults = new();

#if UNITY_EDITOR
    private int _totalOccupiedCells;
    private int _totalGrassCount;
#endif

    public OptimizedGrassCulling(Bounds worldBounds, float cellSize)
    {
        _worldBounds = worldBounds;
        _cellSize = cellSize;

        _gridDimensions = new Vector3Int(
            Mathf.CeilToInt(worldBounds.size.x / cellSize),
            Mathf.CeilToInt(worldBounds.size.y / cellSize),
            Mathf.CeilToInt(worldBounds.size.z / cellSize)
        );

        var totalCells = _gridDimensions.x * _gridDimensions.y * _gridDimensions.z;
        _cellOccupancy = new int[totalCells];
        _grassPerCell = new List<int>[totalCells];
        _cellCenters = new Vector3[totalCells];
        _cellBounds = new Bounds[totalCells];

        // 셀 중심점과 바운드를 미리 계산
        for (var i = 0; i < totalCells; i++)
        {
            _grassPerCell[i] = new List<int>();

            var x = i % _gridDimensions.x;
            var y = (i / _gridDimensions.x) % _gridDimensions.y;
            var z = i / (_gridDimensions.x * _gridDimensions.y);

            var cellMin = _worldBounds.min + new Vector3(x * cellSize, y * cellSize, z * cellSize);
            _cellCenters[i] = cellMin + Vector3.one * (cellSize * 0.5f);
            _cellBounds[i] = new Bounds(_cellCenters[i], Vector3.one * cellSize);
        }
    }

    private Vector3Int WorldToGrid(Vector3 worldPosition)
    {
        var relative = worldPosition - _worldBounds.min;
        return new Vector3Int(
            Mathf.FloorToInt(relative.x / _cellSize),
            Mathf.FloorToInt(relative.y / _cellSize),
            Mathf.FloorToInt(relative.z / _cellSize)
        );
    }

    private int GetCellIndex(Vector3Int gridPosition)
    {
        return gridPosition.x +
               gridPosition.y * _gridDimensions.x +
               gridPosition.z * _gridDimensions.x * _gridDimensions.y;
    }

    public void AddGrass(List<GrassData> grassDataList)
    {
        for (var i = 0; i < grassDataList.Count; i++)
        {
            var grassPosition = grassDataList[i].position;
            _grassPositions[i] = grassPosition;

            var gridPos = WorldToGrid(grassPosition);
            var cellIndex = GetCellIndex(gridPos);

            if (cellIndex >= 0 && cellIndex < _grassPerCell.Length)
            {
                _grassPerCell[cellIndex].Add(i);
                _cellOccupancy[cellIndex]++;

#if UNITY_EDITOR
                if (_cellOccupancy[cellIndex] == 1)
                {
                    _totalOccupiedCells++;
                }

                _totalGrassCount++;
#endif
            }
        }
    }

    public void GetObjectsInRadius(Vector3 point, float radius, List<int> results)
    {
        Profiler.BeginSample("GetObjectsInRadius");
        results.Clear();
        _tempResults.Clear();

        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToGrid(point);
        var radiusSqr = radius * radius;

        // 셀 대각선의 제곱 길이를 미리 계산
        var cellDiagonalSqr = _cellSize * _cellSize * 3;

        // 캐시된 값들
        var pointX = point.x;
        var pointY = point.y;
        var pointZ = point.z;

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
                    var cellIndex = GetCellIndex(checkCell);

                    if (cellIndex >= 0 && cellIndex < _grassPerCell.Length && _cellOccupancy[cellIndex] > 0)
                    {
                        foreach (var grassId in _grassPerCell[cellIndex])
                        {
                            var grassPosition = _grassPositions[grassId];
                            var dx = grassPosition.x - pointX;
                            var dy = grassPosition.y - pointY;
                            var dz = grassPosition.z - pointZ;
                            if (dx * dx + dy * dy + dz * dz <= radiusSqr)
                            {
                                _tempResults.Add(grassId);
                            }
                        }
                    }
                }
            }
        }

        // 결과를 한 번에 복사
        results.AddRange(_tempResults);
        Profiler.EndSample();
    }

    public void GetVisibleGrass(Plane[] frustumPlanes, List<int> visibleGrassIds)
    {
        Profiler.BeginSample("GetVisibleGrass");
        visibleGrassIds.Clear();
        _visibleGrassIds.Clear();

        // 바운딩 볼륨 계층 구조를 사용하여 컬링 최적화
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, _worldBounds))
            return;

        var totalCells = _gridDimensions.x * _gridDimensions.y * _gridDimensions.z;

        for (var i = 0; i < totalCells; i++)
        {
            if (_cellOccupancy[i] == 0) continue;

            if (GeometryUtility.TestPlanesAABB(frustumPlanes, _cellBounds[i]))
            {
                _visibleGrassIds.UnionWith(_grassPerCell[i]);
            }
        }

        visibleGrassIds.AddRange(_visibleGrassIds);
        Profiler.EndSample();
    }

    public void Cleanup()
    {
        for (var i = 0; i < _grassPerCell.Length; i++)
        {
            _grassPerCell[i].Clear();
            _cellOccupancy[i] = 0;
        }

        _grassPositions.Clear();
        _visibleGrassIds.Clear();

#if UNITY_EDITOR
        _totalOccupiedCells = 0;
        _totalGrassCount = 0;
#endif
    }

#if UNITY_EDITOR
    public void DrawVisibleGrassGizmos(Plane[] frustumPlanes)
    {
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, _worldBounds))
            return;

        var totalCells = _gridDimensions.x * _gridDimensions.y * _gridDimensions.z;

        for (var i = 0; i < totalCells; i++)
        {
            if (_cellOccupancy[i] == 0) continue;

            if (GeometryUtility.TestPlanesAABB(frustumPlanes, _cellBounds[i]))
            {
                Gizmos.color = new Color(0, 1, 0);
                Gizmos.DrawWireCube(_cellCenters[i], Vector3.one * _cellSize);
                // UnityEditor.Handles.Label(_cellCenters[i], _cellOccupancy[i].ToString());
            }
        }

        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireCube(_worldBounds.center, _worldBounds.size);
    }

    public string GetDebugStats()
    {
        return $"Grid Stats:\n" +
               $"Dimensions: {_gridDimensions}\n" +
               $"Cell Size: {_cellSize}\n" +
               $"Total Cells: {_cellOccupancy.Length}\n" +
               $"Occupied Cells: {_totalOccupiedCells}\n" +
               $"Total Grass: {_totalGrassCount}";
    }
#endif
}