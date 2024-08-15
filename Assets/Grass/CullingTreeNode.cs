using System.Collections.Generic;
using UnityEngine;

// CullingTreeNode 클래스는 공간 분할 트리의 노드를 나타내며, 주로 공간 내에 존재하는 객체들을 관리하기 위한 용도로 사용됩니다.
public class CullingTreeNode
{
    private Bounds _bounds; // 이 노드가 포함하는 공간의 경계
    private readonly List<CullingTreeNode> _children = new(); // 하위 노드 리스트
    private readonly List<int> _grassIDHeld = new(); // 이 노드가 포함하는 객체 ID 리스트

    // 생성자: 주어진 경계와 깊이를 사용하여 노드를 초기화합니다.
    public CullingTreeNode(Bounds bounds, int depth)
    {
        _children.Clear();
        _bounds = bounds;

        // 깊이가 0보다 클 경우 자식 노드를 생성합니다.
        if (depth > 0)
        {
            var size = _bounds.size;
            size /= 4.0f;
            var childSize = _bounds.size / 2.0f;
            var center = _bounds.center;

            // 깊이가 짝수인 경우 Y축으로 세분화를 하지 않습니다.
            // (만약 Y축 방향으로 많은 세분화가 필요하면 이 조건문을 삭제하세요)
            if (depth % 2 == 0)
            {
                childSize.y = _bounds.size.y;
                var topLeftSingle =
                    new Bounds(new Vector3(center.x - size.x, center.y, center.z - size.z), childSize);
                var bottomRightSingle =
                    new Bounds(new Vector3(center.x + size.x, center.y, center.z + size.z), childSize);
                var topRightSingle =
                    new Bounds(new Vector3(center.x - size.x, center.y, center.z + size.z), childSize);
                var bottomLeftSingle =
                    new Bounds(new Vector3(center.x + size.x, center.y, center.z - size.z), childSize);

                // 하위 노드를 추가합니다.
                _children.Add(new CullingTreeNode(topLeftSingle, depth - 1));
                _children.Add(new CullingTreeNode(bottomRightSingle, depth - 1));
                _children.Add(new CullingTreeNode(topRightSingle, depth - 1));
                _children.Add(new CullingTreeNode(bottomLeftSingle, depth - 1));
            }
            else
            {
                // 깊이가 홀수인 경우 8개의 하위 노드를 생성합니다.

                // 1층 레이어
                var topLeft = new Bounds(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z),
                    childSize);
                var bottomRight = new Bounds(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z),
                    childSize);
                var topRight = new Bounds(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z),
                    childSize);
                var bottomLeft = new Bounds(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z),
                    childSize);

                // 2층 레이어
                var topLeft2 = new Bounds(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z),
                    childSize);
                var bottomRight2 = new Bounds(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z),
                    childSize);
                var topRight2 = new Bounds(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z),
                    childSize);
                var bottomLeft2 = new Bounds(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z),
                    childSize);

                // 하위 노드를 추가합니다.
                _children.Add(new CullingTreeNode(topLeft, depth - 1));
                _children.Add(new CullingTreeNode(bottomRight, depth - 1));
                _children.Add(new CullingTreeNode(topRight, depth - 1));
                _children.Add(new CullingTreeNode(bottomLeft, depth - 1));

                _children.Add(new CullingTreeNode(topLeft2, depth - 1));
                _children.Add(new CullingTreeNode(bottomRight2, depth - 1));
                _children.Add(new CullingTreeNode(topRight2, depth - 1));
                _children.Add(new CullingTreeNode(bottomLeft2, depth - 1));
            }
        }
    }

    // 주어진 프러스텀(frustum)에 의해 가시성 여부를 판단하여 잎 노드를 리스트에 추가합니다.
    public void RetrieveLeaves(Plane[] frustum, List<Bounds> list, List<int> visibleIDList)
    {
        // 프러스텀과 경계가 교차하는지 검사합니다.
        if (GeometryUtility.TestPlanesAABB(frustum, _bounds))
        {
            // 하위 노드가 없는 경우 현재 노드를 리스트에 추가합니다.
            if (_children.Count == 0)
            {
                if (_grassIDHeld.Count > 0)
                {
                    list.Add(_bounds);
                    visibleIDList.AddRange(_grassIDHeld);
                }
            }
            // 하위 노드가 있으면 재귀적으로 하위 노드의 RetrieveLeaves를 호출합니다.
            else
            {
                for (var i = 0; i < _children.Count; i++)
                {
                    var child = _children[i];
                    child.RetrieveLeaves(frustum, list, visibleIDList);
                }
            }
        }
    }

    // 주어진 포인트가 포함된 잎 노드를 찾아서 객체 ID를 추가합니다.
    public bool FindLeaf(Vector3 point, int index)
    {
        // 경계가 주어진 포인트를 포함하는지 검사합니다.
        if (_bounds.Contains(point))
        {
            // 하위 노드가 있으면 재귀적으로 하위 노드의 FindLeaf를 호출합니다.
            if (_children.Count != 0)
            {
                for (var i = 0; i < _children.Count; i++)
                {
                    var child = _children[i];
                    if (child.FindLeaf(point, index))
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

    // 모든 잎 노드를 리스트에 추가합니다.
    public void RetrieveAllLeaves(List<CullingTreeNode> target)
    {
        // 하위 노드가 없으면 현재 노드를 리스트에 추가합니다.
        if (_children.Count == 0)
        {
            target.Add(this);
        }
        else
        {
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                child.RetrieveAllLeaves(target);
            }
        }
    }

    // 비어 있는 노드를 제거합니다.
    public bool ClearEmpty()
    {
        var delete = false;
        if (_children.Count > 0)
        {
            // 노드를 줄이기 위해 하위 노드를 확인합니다.
            var i = _children.Count - 1;
            while (i > 0)
            {
                if (_children[i].ClearEmpty())
                {
                    _children.RemoveAt(i);
                }
                i--;
            }
        }
        if (_grassIDHeld.Count == 0 && _children.Count == 0)
        {
            delete = true;
        }
        return delete;
    }

    // 특정 반경 내의 객체 ID 리스트를 반환합니다.
    public void ReturnLeafList(Vector3 point, List<int> grassList, float radius)
    {
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return; // 포인트가 경계 외부에 있습니다.
        }

        if (_children.Count == 0)
        {
            grassList.AddRange(_grassIDHeld);
        }
        else
        {
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                var expandedBoundsChild = child._bounds;
                expandedBoundsChild.Expand(radius * 2);
                if (expandedBoundsChild.Contains(point))
                {
                    child.ReturnLeafList(point, grassList, radius);
                }
            }
        }
    }
}