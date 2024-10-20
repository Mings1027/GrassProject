using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public class SpatialGrid
    {
        private readonly HashSet<Vector3Int>[,,] _grid;
        private readonly float _cellSize;
        private readonly Vector3 _origin;

        public SpatialGrid(Bounds bounds, float cellSize)
        {
            _cellSize = cellSize;
            _origin = bounds.min;

            var size = bounds.size;
            var xSize = Mathf.CeilToInt(size.x / cellSize);
            var ySize = Mathf.CeilToInt(size.y / cellSize);
            var zSize = Mathf.CeilToInt(size.z / cellSize);

            _grid = new HashSet<Vector3Int>[xSize, ySize, zSize];

            for (var x = 0; x < xSize; x++)
            for (var y = 0; y < ySize; y++)
            for (var z = 0; z < zSize; z++)
                _grid[x, y, z] = new HashSet<Vector3Int>();
        }

        public void AddObject(Vector3 position)
        {
            var cell = WorldToCell(position);
            var index = GetIndex(cell);
            _grid[index.x, index.y, index.z].Add(cell);
        }

        public bool CanPlaceGrass(Vector3 position, float radius)
        {
            var cell = WorldToCell(position);
            var index = GetIndex(cell);

            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                var checkIndex = new Vector3Int(index.x + x, index.y + y, index.z + z);
                if (IsValidIndex(checkIndex))
                {
                    foreach (var occupiedCell in _grid[checkIndex.x, checkIndex.y, checkIndex.z])
                    {
                        if (Vector3.Distance(CellToWorld(occupiedCell), position) < radius)
                            return false;
                    }
                }
            }

            return true;
        }

        private Vector3Int WorldToCell(Vector3 position)
        {
            return Vector3Int.FloorToInt((position - _origin) / _cellSize);
        }

        private Vector3 CellToWorld(Vector3Int cell)
        {
            return new Vector3(cell.x * _cellSize, cell.y * _cellSize, cell.z * _cellSize) + _origin;
        }

        private Vector3Int GetIndex(Vector3Int cell)
        {
            return new Vector3Int(
                Mathf.Clamp(cell.x, 0, _grid.GetLength(0) - 1),
                Mathf.Clamp(cell.y, 0, _grid.GetLength(1) - 1),
                Mathf.Clamp(cell.z, 0, _grid.GetLength(2) - 1)
            );
        }

        private bool IsValidIndex(Vector3Int index)
        {
            return index.x >= 0 && index.x < _grid.GetLength(0) &&
                   index.y >= 0 && index.y < _grid.GetLength(1) &&
                   index.z >= 0 && index.z < _grid.GetLength(2);
        }
    }
}