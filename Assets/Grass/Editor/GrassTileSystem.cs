using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassTileSystem
    {
        private readonly Dictionary<Vector2Int, List<int>> _tiles;
        private readonly float _tileSize;

        public GrassTileSystem(List<GrassData> grassData, float brushSize)
        {
            _tileSize = brushSize * 1.5f; // 타일 크기를 브러시 크기의 1.5배로 설정
            _tiles = new Dictionary<Vector2Int, List<int>>();

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

        public List<int> GetGrassIndicesInAndAroundTile(Vector3 worldPosition, float radius)
        {
            var centralTile = GetTilePosition(worldPosition);
            var result = new List<int>();

            var tilesRadius = Mathf.CeilToInt(radius / _tileSize);

            for (var x = -tilesRadius; x <= tilesRadius; x++)
            {
                for (var z = -tilesRadius; z <= tilesRadius; z++)
                {
                    var neighborTile = new Vector2Int(centralTile.x + x, centralTile.y + z);
                    if (_tiles.TryGetValue(neighborTile, out var indices))
                    {
                        result.AddRange(indices);
                    }
                }
            }

            return result;
        }

        public void UpdateTileSystem(List<GrassData> grassData)
        {
            // 기존 타일 데이터를 모두 초기화
            _tiles.Clear();

            // 모든 잔디 데이터를 순회하며 타일에 재할당
            for (int i = 0; i < grassData.Count; i++)
            {
                Vector2Int tilePos = GetTilePosition(grassData[i].position);

                if (!_tiles.ContainsKey(tilePos))
                {
                    _tiles[tilePos] = new List<int>();
                }

                _tiles[tilePos].Add(i);
            }
        }

        public float GetTileSize()
        {
            return _tileSize;
        }
    }
}