using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Grass.Editor
{
    public class GrassTileSystem
    {
        private Dictionary<Vector2Int, List<int>> _tiles;
        private float _tileSize;

        private Vector3 _lastQueryPosition;
        private float _lastQueryRadius;
        private List<int> _cachedIndices;
        private bool _hasCachedQuery;

        public GrassTileSystem(List<GrassData> grassData, float brushSize)
        {
            InitTileSystem(grassData, brushSize);
        }

        private void InitTileSystem(List<GrassData> grassData, float brushSize)
        {
            _tileSize = brushSize * 5f;
            _tiles = new Dictionary<Vector2Int, List<int>>();
            _cachedIndices = new List<int>(1000);

            for (var i = 0; i < grassData.Count; i++)
            {
                var tilePos = GetTilePosition(grassData[i].position);
                if (!_tiles.ContainsKey(tilePos))
                {
                    _tiles[tilePos] = new List<int>();
                }
                _tiles[tilePos].Add(i);
            }
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

        public void UpdateTileSystem(List<GrassData> grassData)
        {
            ClearCache();
            _tiles.Clear();

            for (var i = 0; i < grassData.Count; i++)
            {
                var tilePos = GetTilePosition(grassData[i].position);
                if (!_tiles.ContainsKey(tilePos))
                {
                    _tiles[tilePos] = new List<int>();
                }
                _tiles[tilePos].Add(i);
            }
        }

        public void ClearCache()
        {
            _hasCachedQuery = false;
            _cachedIndices.Clear();
        }
    }
}