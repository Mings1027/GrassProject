using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grass.GrassScripts
{
    public class QuadTree
    {
        private Bounds _bounds;
        private QuadTree[] _children;
        private readonly List<Transform> _objectsHeld = new();

        public Bounds Bounds => _bounds;
        public QuadTree[] Children => _children;
        public bool HasObjects => _objectsHeld.Count > 0;

        public QuadTree(Bounds bounds, int depth)
        {
            _bounds = bounds;

            if (depth <= 0)
            {
                _children = Array.Empty<QuadTree>();
                return;
            }

            _children = CreateChildren(bounds, depth);
        }

        private QuadTree[] CreateChildren(Bounds parentBounds, int depth)
        {
            var isEvenDepth = depth % 2 == 0;
            var childCount = isEvenDepth ? 4 : 8;
            var children = new QuadTree[childCount];

            var quarterSize = parentBounds.size / 4.0f;
            var childBounds = CalculateChildBounds(parentBounds, isEvenDepth);

            for (var i = 0; i < childCount; i++)
            {
                var childCenter = CalculateChildCenter(parentBounds.center, quarterSize, i, isEvenDepth);
                children[i] = new QuadTree(new Bounds(childCenter, childBounds), depth - 1);
            }

            return children;
        }

        private Vector3 CalculateChildBounds(Bounds parentBounds, bool isEvenDepth)
        {
            var childBounds = parentBounds.size / 2.0f;

            if (isEvenDepth)
            {
                childBounds.y = parentBounds.size.y;
            }

            return childBounds;
        }

        private Vector3 CalculateChildCenter(Vector3 parentCenter, Vector3 size, int index, bool isEvenDepth)
        {
            var x = parentCenter.x + ((index & 1) == 0 ? -size.x : size.x);
            var z = parentCenter.z + ((index & 2) == 0 ? -size.z : size.z);
            var y = isEvenDepth ? parentCenter.y : parentCenter.y + (index < 4 ? -size.y : size.y);

            return new Vector3(x, y, z);
        }

        public bool InsertObject(Vector3 position, Transform obj)
        {
            if (_bounds.Contains(position))
            {
                if (_children.Length != 0)
                {
                    for (var i = 0; i < _children.Length; i++)
                    {
                        if (_children[i] != null && _children[i].InsertObject(position, obj))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    _objectsHeld.Add(obj);
                    return true;
                }
            }

            return false;
        }

        public bool ClearEmpty()
        {
            if (_children.Length > 0)
            {
                var allChildrenEmpty = true;
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null)
                    {
                        if (_children[i].ClearEmpty())
                        {
                            _children[i] = null;
                        }
                        else
                        {
                            allChildrenEmpty = false;
                        }
                    }
                }

                if (allChildrenEmpty)
                {
                    return _objectsHeld.Count == 0;
                }
            }

            return _objectsHeld.Count == 0 && _children.Length == 0;
        }

        public void GetNearbyObjects(Vector3 point, List<Transform> objectList, float radius)
        {
            var expandedBounds = _bounds;
            expandedBounds.Expand(radius * 2);

            if (!expandedBounds.Contains(point))
            {
                return;
            }

            if (_children.Length == 0)
            {
                foreach (var obj in _objectsHeld)
                {
                    float sqrDist = (obj.position - point).sqrMagnitude;
                    if (sqrDist <= radius * radius)
                    {
                        objectList.Add(obj);
                    }
                }
            }
            else
            {
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null && _children[i]._bounds.SqrDistance(point) <= radius * radius)
                    {
                        _children[i].GetNearbyObjects(point, objectList, radius);
                    }
                }
            }
        }

        public bool RemoveObject(Vector3 position, Transform obj)
        {
            if (!_bounds.Contains(position))
            {
                return false;
            }

            if (_children.Length == 0)
            {
                return _objectsHeld.Remove(obj);
            }

            for (var i = 0; i < _children.Length; i++)
            {
                if (_children[i] != null && _children[i].RemoveObject(position, obj))
                {
                    return true;
                }
            }

            return false;
        }
    }
}