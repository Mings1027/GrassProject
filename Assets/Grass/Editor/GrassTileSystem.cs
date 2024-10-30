using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassTileSystem
    {
        private ConcurrentDictionary<Vector2Int, ConcurrentBag<int>> _tiles; // Thread-safe 컬렉션으로 변경
        private float _tileSize;

        private Vector3 _lastQueryPosition;
        private float _lastQueryRadius;
        private List<int> _cachedIndices;
        private bool _hasCachedQuery;

        public GrassTileSystem(List<GrassData> grassData, float brushSize)
        {
            InitTileSystem(grassData, brushSize).Forget();
        }

        private async UniTask InitTileSystem(List<GrassData> grassData, float brushSize)
        {
            _tileSize = brushSize * 5f;
            _tiles = new ConcurrentDictionary<Vector2Int, ConcurrentBag<int>>();
            _cachedIndices = new List<int>(1000);

            const int batchSize = 10000; // 각 작업자가 처리할 데이터 크기
            var totalBatches = (grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask[totalBatches];

            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var start = batchIndex * batchSize;
                var end = Mathf.Min(start + batchSize, grassData.Count);

                tasks[batchIndex] = UniTask.RunOnThreadPool(() =>
                {
                    for (var i = start; i < end; i++)
                    {
                        var tilePos = GetTilePosition(grassData[i].position);
                        var bag = _tiles.GetOrAdd(tilePos, _ => new ConcurrentBag<int>());
                        bag.Add(i);
                    }
                });
            }

            await UniTask.WhenAll(tasks);

            // ConcurrentBag을 List로 변환
            var regularDict = new Dictionary<Vector2Int, List<int>>();
            foreach (var kvp in _tiles)
            {
                regularDict[kvp.Key] = kvp.Value.ToList();
            }

            _tiles = null;
            _tiles = new ConcurrentDictionary<Vector2Int, ConcurrentBag<int>>(
                regularDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ConcurrentBag<int>(kvp.Value)
                ));
        }


        private Vector2Int GetTilePosition(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / _tileSize),
                Mathf.FloorToInt(worldPosition.z / _tileSize)
            );
        }

        public List<int> GetNearbyIndices(Vector3 worldPosition, float radius)
        {
            if (_hasCachedQuery &&
                Vector3.Distance(_lastQueryPosition, worldPosition) < _tileSize * 0.5f &&
                Mathf.Approximately(_lastQueryRadius, radius))
            {
                return _cachedIndices;
            }

            _cachedIndices.Clear();

            var centralTile = GetTilePosition(worldPosition);

            var tilesRadius = Mathf.CeilToInt(radius / _tileSize);

            for (var x = -tilesRadius; x <= tilesRadius; x++)
            {
                for (var z = -tilesRadius; z <= tilesRadius; z++)
                {
                    var neighborTile = new Vector2Int(centralTile.x + x, centralTile.y + z);
                    if (_tiles.TryGetValue(neighborTile, out var indices))
                    {
                        _cachedIndices.AddRange(indices);
                    }
                }
            }

            _lastQueryPosition = worldPosition;
            _lastQueryRadius = radius;
            _hasCachedQuery = true;

            return _cachedIndices;
        }

        public async UniTask UpdateTileSystem(List<GrassData> grassData)
        {
            ClearCache();
            _tiles.Clear();
            const int batchSize = 10000;
            var totalBatches = (grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask[totalBatches];

            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var start = batchIndex * batchSize;
                var end = Mathf.Min(start + batchSize, grassData.Count);

                tasks[batchIndex] = UniTask.RunOnThreadPool(() =>
                {
                    for (var i = start; i < end; i++)
                    {
                        var tilePos = GetTilePosition(grassData[i].position);
                        var bag = _tiles.GetOrAdd(tilePos, _ => new ConcurrentBag<int>());
                        bag.Add(i);
                    }
                });
            }

            await UniTask.WhenAll(tasks);
        }

        public void ClearCache()
        {
            _hasCachedQuery = false;
            _cachedIndices.Clear();
        }
    }
}