using System;
using System.Collections.Generic;
using UnityEngine;

public class CullingTreeNode
{
    private Bounds _bounds;
    private readonly CullingTreeNode[] _children;
    private readonly List<int> _grassIDHeld = new();

    public CullingTreeNode(Bounds bounds, int depth)
    {
        _bounds = bounds;

        if (depth > 0)
        {
            var size = _bounds.size / 4.0f;
            var childSize = _bounds.size / 2.0f;
            var center = _bounds.center;

            var isEvenDepth = depth % 2 == 0;
            var childCount = isEvenDepth ? 4 : 8;

            _children = new CullingTreeNode[childCount];

            if (isEvenDepth)
            {
                childSize.y = _bounds.size.y;
            }

            for (var i = 0; i < childCount; i++)
            {
                var childCenter = CalculateChildCenter(center, size, i, isEvenDepth);
                _children[i] = new CullingTreeNode(new Bounds(childCenter, childSize), depth - 1);
            }
        }
        else
        {
            _children = Array.Empty<CullingTreeNode>();
        }
    }

    private Vector3 CalculateChildCenter(Vector3 parentCenter, Vector3 size, int index, bool isEvenDepth)
    {
        var x = parentCenter.x + ((index & 1) == 0 ? -size.x : size.x);
        var z = (index & 2) == 0 ? -size.z : size.z;
        float y;

        if (isEvenDepth)
        {
            y = parentCenter.y;
        }
        else
        {
            y = parentCenter.y + (index < 4 ? -size.y : size.y);
        }

        z += parentCenter.z;

        return new Vector3(x, y, z);
    }

    public void RetrieveLeaves(Plane[] frustum, List<Bounds> list, List<int> visibleIDList)
    {
        if (GeometryUtility.TestPlanesAABB(frustum, _bounds))
        {
            if (_children.Length == 0)
            {
                if (_grassIDHeld.Count > 0)
                {
                    list.Add(_bounds);
                    visibleIDList.AddRange(_grassIDHeld);
                }
            }
            else
            {
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null)
                    {
                        _children[i].RetrieveLeaves(frustum, list, visibleIDList);
                    }
                }
            }
        }
    }

    public bool FindLeaf(Vector3 point, int index)
    {
        if (_bounds.Contains(point))
        {
            if (_children.Length != 0)
            {
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null && _children[i].FindLeaf(point, index))
                    {
                        return true;
                    }
                }
            }
            else
            {
                _grassIDHeld.Add(index);
                return true;
            }
        }

        return false;
    }

    public void RetrieveAllLeaves(List<CullingTreeNode> target)
    {
        if (_children.Length == 0)
        {
            target.Add(this);
        }
        else
        {
            for (var i = 0; i < _children.Length; i++)
            {
                if (_children[i] != null)
                {
                    _children[i].RetrieveAllLeaves(target);
                }
            }
        }
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
                return _grassIDHeld.Count == 0;
            }
        }

        return _grassIDHeld.Count == 0 && _children.Length == 0;
    }

    public void ReturnLeafList(Vector3 point, List<int> grassList, float radius)
    {
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return;
        }

        if (_children.Length == 0)
        {
            grassList.AddRange(_grassIDHeld);
        }
        else
        {
            for (var i = 0; i < _children.Length; i++)
            {
                if (_children[i] != null && _children[i]._bounds.SqrDistance(point) <= radius * radius)
                {
                    _children[i].ReturnLeafList(point, grassList, radius);
                }
            }
        }
    }
}