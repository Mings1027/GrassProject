using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

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
    private readonly Stack<List<int>> _listPool = new();
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

    private List<int> GetListFromPool()
    {
        if (_listPool.Count > 0)
            return _listPool.Pop();
        return new List<int>(32); // 일반적인 셀당 잔디 수를 초기 용량으로 설정
    }

    private void ReturnListToPool(List<int> list)
    {
        list.Clear();
        _listPool.Push(list);
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
        results.Clear();
        _tempResults.Clear();

        var minCell = WorldToGrid(point - Vector3.one * radius);
        var maxCell = WorldToGrid(point + Vector3.one * radius);

        minCell.x = Mathf.Max(0, minCell.x);
        minCell.y = Mathf.Max(0, minCell.y);
        minCell.z = Mathf.Max(0, minCell.z);
        maxCell.x = Mathf.Min(_gridDimensions.x - 1, maxCell.x);
        maxCell.y = Mathf.Min(_gridDimensions.y - 1, maxCell.y);
        maxCell.z = Mathf.Min(_gridDimensions.z - 1, maxCell.z);

        var radiusSqr = radius * radius;
        var extendedRadiusSqr = radiusSqr + _cellSize * _cellSize;

        // 셀 순회 최적화를 위한 캐시된 값들
        var pointX = point.x;
        var pointY = point.y;
        var pointZ = point.z;

        for (var z = minCell.z; z <= maxCell.z; z++)
        {
            var zOffset = z * _cellSize;
            var zDistSqr = (zOffset - pointZ) * (zOffset - pointZ);

            for (var y = minCell.y; y <= maxCell.y; y++)
            {
                var yOffset = y * _cellSize;
                var yzDistSqr = zDistSqr + (yOffset - pointY) * (yOffset - pointY);

                if (yzDistSqr > extendedRadiusSqr) continue;

                for (var x = minCell.x; x <= maxCell.x; x++)
                {
                    var cellIndex = GetCellIndex(new Vector3Int(x, y, z));
                    if (_cellOccupancy[cellIndex] == 0) continue;

                    var xOffset = x * _cellSize;
                    var totalDistSqr = yzDistSqr + (xOffset - pointX) * (xOffset - pointX);

                    if (totalDistSqr > extendedRadiusSqr) continue;

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

        // 결과를 한 번에 복사
        results.AddRange(_tempResults);
    }

    public void GetVisibleGrass(Plane[] frustumPlanes, List<int> visibleGrassIds)
    {
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
    }

    public void Cleanup()
    {
        for (var i = 0; i < _grassPerCell.Length; i++)
        {
            ReturnListToPool(_grassPerCell[i]);
            _cellOccupancy[i] = 0;
        }

        _grassPositions.Clear();
        _visibleGrassIds.Clear();
        
        while (_listPool.Count > 0)
            _listPool.Pop().Clear();

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
                UnityEditor.Handles.Label(_cellCenters[i], _cellOccupancy[i].ToString());
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