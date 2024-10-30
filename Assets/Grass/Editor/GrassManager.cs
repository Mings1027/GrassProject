using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassManager
    {
        private readonly Dictionary<int, GrassData> _grassDict;
        private readonly QuadTree<int> _spatialIndex;
        private readonly float _cellSize;
        private int _nextId;

        public GrassManager(Bounds worldBounds, float cellSize = 1.0f)
        {
            _grassDict = new Dictionary<int, GrassData>();
            _cellSize = cellSize;
            _spatialIndex = new QuadTree<int>(worldBounds, 8); // Max 8 items per node before splitting
            _nextId = 0;
        }

        // 잔디 추가
        public int AddGrass(GrassData grassData)
        {
            var id = _nextId++;
            _grassDict[id] = grassData;
            _spatialIndex.Insert(id, new Bounds(grassData.position, Vector3.one * _cellSize));
            return id;
        }

        // 잔디 제거
        public bool RemoveGrass(int id)
        {
            if (_grassDict.TryGetValue(id, out var grassData))
            {
                _spatialIndex.Remove(id, new Bounds(grassData.position, Vector3.one * _cellSize));
                return _grassDict.Remove(id);
            }

            return false;
        }

        // 특정 영역 내의 잔디 ID 가져오기
        public List<int> GetGrassInArea(Vector3 center, float radius)
        {
            var searchBounds = new Bounds(center, Vector3.one * (radius * 2));
            return _spatialIndex.Query(searchBounds);
        }

        // 잔디 데이터 업데이트
        public void UpdateGrass(int id, GrassData newData)
        {
            if (_grassDict.TryGetValue(id, out var oldData))
            {
                _spatialIndex.Remove(id, new Bounds(oldData.position, Vector3.one * _cellSize));
                _grassDict[id] = newData;
                _spatialIndex.Insert(id, new Bounds(newData.position, Vector3.one * _cellSize));
            }
        }

        // QuadTree 내부 클래스
        private class QuadTree<T>
        {
            private class Node
            {
                public Bounds Bounds;
                public readonly List<(T item, Bounds bounds)> Items;
                public Node[] Children;
                public bool IsLeaf => Children == null;

                public Node(Bounds bounds)
                {
                    Bounds = bounds;
                    Items = new List<(T, Bounds)>();
                }
            }

            private readonly Node _root;
            private readonly int _maxItems;

            public QuadTree(Bounds bounds, int maxItems)
            {
                _root = new Node(bounds);
                _maxItems = maxItems;
            }

            public void Insert(T item, Bounds bounds)
            {
                Insert(_root, item, bounds);
            }

            private void Insert(Node node, T item, Bounds bounds)
            {
                if (!node.Bounds.Intersects(bounds))
                    return;

                if (node.IsLeaf)
                {
                    node.Items.Add((item, bounds));
                    if (node.Items.Count > _maxItems)
                        Split(node);
                }
                else
                {
                    foreach (var child in node.Children)
                        Insert(child, item, bounds);
                }
            }

            private void Split(Node node)
            {
                var size = node.Bounds.size * 0.5f;
                var center = node.Bounds.center;

                node.Children = new Node[4];
                node.Children[0] = new Node(new Bounds(center + new Vector3(-size.x * 0.5f, 0, -size.z * 0.5f), size));
                node.Children[1] = new Node(new Bounds(center + new Vector3(size.x * 0.5f, 0, -size.z * 0.5f), size));
                node.Children[2] = new Node(new Bounds(center + new Vector3(-size.x * 0.5f, 0, size.z * 0.5f), size));
                node.Children[3] = new Node(new Bounds(center + new Vector3(size.x * 0.5f, 0, size.z * 0.5f), size));

                var oldItems = node.Items;
                node.Items.Clear();

                foreach (var item in oldItems)
                {
                    foreach (var child in node.Children)
                        Insert(child, item.item, item.bounds);
                }
            }

            public bool Remove(T item, Bounds bounds)
            {
                return Remove(_root, item, bounds);
            }

            private bool Remove(Node node, T item, Bounds bounds)
            {
                if (!node.Bounds.Intersects(bounds))
                    return false;

                if (node.IsLeaf)
                {
                    for (var i = 0; i < node.Items.Count; i++)
                    {
                        if (node.Items[i].item.Equals(item))
                        {
                            node.Items.RemoveAt(i);
                            return true;
                        }
                    }
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        if (Remove(child, item, bounds))
                            return true;
                    }
                }

                return false;
            }

            public List<T> Query(Bounds bounds)
            {
                var result = new List<T>();
                Query(_root, bounds, result);
                return result;
            }

            private void Query(Node node, Bounds bounds, List<T> result)
            {
                if (!node.Bounds.Intersects(bounds))
                    return;

                if (node.IsLeaf)
                {
                    foreach (var item in node.Items)
                    {
                        if (bounds.Intersects(item.bounds))
                            result.Add(item.item);
                    }
                }
                else
                {
                    foreach (var child in node.Children)
                        Query(child, bounds, result);
                }
            }
        }

        // 전체 잔디 데이터를 List로 변환
        public List<GrassData> ToList()
        {
            return new List<GrassData>(_grassDict.Values);
        }

        // Dictionary에서 특정 잔디 데이터 가져오기
        public GrassData GetGrassData(int id)
        {
            return _grassDict.TryGetValue(id, out var data) ? data : default;
        }

        // 배치 업데이트 처리
        public void BatchUpdate(List<(int id, GrassData data)> updates)
        {
            foreach (var (id, data) in updates)
            {
                UpdateGrass(id, data);
            }
        }
    }
}