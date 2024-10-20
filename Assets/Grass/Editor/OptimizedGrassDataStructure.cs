using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public class OptimizedGrassDataStructure
    {
        private readonly Dictionary<int, GrassData> _grassDataDict = new();
        private readonly SpatialHashGrid _spatialHashGrid;
        private int _nextId;

        public OptimizedGrassDataStructure(Bounds bounds, float cellSize)
        {
            _spatialHashGrid = new SpatialHashGrid(bounds, cellSize);
        }

        public int AddGrass(GrassData grassData)
        {
            var id = _nextId++;
            _grassDataDict[id] = grassData;
            _spatialHashGrid.Insert(id, grassData.position);
            return id;
        }

        public void RemoveGrass(Vector3 position, float radius)
        {
            var nearbyIds = _spatialHashGrid.QueryRadius(position, radius);
            var radiusSquared = radius * radius;

            foreach (var id in nearbyIds)
            {
                if (_grassDataDict.TryGetValue(id, out var grassData))
                {
                    if (Vector3.SqrMagnitude(grassData.position - position) <= radiusSquared)
                    {
                        _grassDataDict.Remove(id);
                        _spatialHashGrid.Remove(id, grassData.position);
                    }
                }
            }
        }

        public List<GrassData> GetAllGrassData()
        {
            return new List<GrassData>(_grassDataDict.Values);
        }

        public void GetGrassIndicesInRadius(Vector3 position, float radius, List<int> result)
        {
            result.Clear(); // 기존 내용을 비웁니다
            var nearbyIds = _spatialHashGrid.QueryRadius(position, radius);

            for (var i = 0; i < nearbyIds.Count; i++)
            {
                var id = nearbyIds[i];
                if (_grassDataDict.TryGetValue(id, out var grassData))
                {
                    if (Vector3.Distance(grassData.position, position) <= radius)
                    {
                        result.Add(id);
                    }
                }
            }
        }

        public bool TryGetGrassData(int id, out GrassData grassData)
        {
            return _grassDataDict.TryGetValue(id, out grassData);
        }

        public void UpdateGrass(int id, GrassData newData)
        {
            if (_grassDataDict.ContainsKey(id))
            {
                _grassDataDict[id] = newData;
                _spatialHashGrid.Remove(id, _grassDataDict[id].position);
                _spatialHashGrid.Insert(id, newData.position);
            }
        }

        public void UpdateMultipleGrass(List<GrassData> updatedGrassData)
        {
            // 기존 데이터를 모두 제거
            _grassDataDict.Clear();
            _spatialHashGrid.Clear();

            // 업데이트된 데이터로 다시 채우기
            for (var index = 0; index < updatedGrassData.Count; index++)
            {
                var grassData = updatedGrassData[index];
                AddGrass(grassData);
            }
        }
    }

    public class SpatialHashGrid
    {
        private readonly Dictionary<long, HashSet<int>> _cells = new();
        private readonly float _cellSize;
        private readonly Vector3 _minBounds;

        public SpatialHashGrid(Bounds bounds, float cellSize)
        {
            _cellSize = cellSize;
            _minBounds = bounds.min;
        }

        private long PositionToKey(Vector3 position)
        {
            var x = Mathf.FloorToInt((position.x - _minBounds.x) / _cellSize);
            var y = Mathf.FloorToInt((position.y - _minBounds.y) / _cellSize);
            var z = Mathf.FloorToInt((position.z - _minBounds.z) / _cellSize);
            return (long)x << 42 | (long)y << 21 | (uint)z;
        }

        public void Insert(int id, Vector3 position)
        {
            var key = PositionToKey(position);
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = new HashSet<int>();
                _cells[key] = cell;
            }

            cell.Add(id);
        }

        public void Remove(int id, Vector3 position)
        {
            var key = PositionToKey(position);
            if (_cells.TryGetValue(key, out var cell))
            {
                cell.Remove(id);
                if (cell.Count == 0)
                {
                    _cells.Remove(key);
                }
            }
        }

        public void Clear()
        {
            _cells.Clear();
        }

        public List<int> QueryRadius(Vector3 position, float radius)
        {
            var result = new List<int>();
            var cellRadius = Mathf.CeilToInt(radius / _cellSize);
            var centerCell = Vector3Int.FloorToInt((position - _minBounds) / _cellSize);

            for (var x = -cellRadius; x <= cellRadius; x++)
            {
                for (var y = -cellRadius; y <= cellRadius; y++)
                {
                    for (var z = -cellRadius; z <= cellRadius; z++)
                    {
                        var offset = new Vector3Int(x, y, z);
                        var currentCell = centerCell + offset;
                        var key = (long)currentCell.x << 42 | (long)currentCell.y << 21 | (uint)currentCell.z;

                        if (_cells.TryGetValue(key, out var cell))
                        {
                            result.AddRange(cell);
                        }
                    }
                }
            }

            return result;
        }
    }

    public class BatchGrassRemoval
    {
        private readonly List<int> _grassIdsToRemove = new();
        private int _currentIndex;
        private const int BatchSize = 1000; // 한 프레임에 처리할 잔디의 수

        public void MarkGrassForRemoval(int id)
        {
            if (!_grassIdsToRemove.Contains(id))
            {
                _grassIdsToRemove.Add(id);
            }
        }

        public void StartBatchRemoval(OptimizedGrassDataStructure grassDataStructure, System.Action onComplete)
        {
            EditorApplication.update += BatchRemove;
            return;

            void BatchRemove()
            {
                var endIndex = Mathf.Min(_currentIndex + BatchSize, _grassIdsToRemove.Count);
                for (var i = _currentIndex; i < endIndex; i++)
                {
                    var idToRemove = _grassIdsToRemove[i];
                    if (grassDataStructure.TryGetGrassData(idToRemove, out var grassData))
                    {
                        grassDataStructure.RemoveGrass(grassData.position, 0.01f); // 매우 작은 반경으로 특정 잔디만 제거
                    }
                }

                _currentIndex = endIndex;

                if (_currentIndex >= _grassIdsToRemove.Count)
                {
                    EditorApplication.update -= BatchRemove;
                    _grassIdsToRemove.Clear();
                    _currentIndex = 0;
                    EditorUtility.ClearProgressBar();
                    onComplete?.Invoke();
                }
                else
                {
                    // 진행 상황을 표시
                    EditorUtility.DisplayProgressBar("Removing Grass",
                        $"Processed {_currentIndex} out of {_grassIdsToRemove.Count}",
                        (float)_currentIndex / _grassIdsToRemove.Count);
                }
            }
        }
    }

    public class ObjectProgress
    {
        // public string objectName;
        public float progress;
        public string progressMessage;
    }
}