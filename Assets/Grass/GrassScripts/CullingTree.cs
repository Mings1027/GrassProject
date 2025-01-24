using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class CullingTree
{
    private Bounds _bounds; // 현재 노드의 영역
    private CullingTree[] _children; // 자식 노드들
    private readonly List<int> _grassIDHeld = new(); // 현재 노드에 포함된 잔디 ID들

#if UNITY_EDITOR
    private readonly List<Bounds> _boundsList = new();
#endif

    /// <summary>
    /// 트리 생성, 재귀 공간 분할
    /// 짝수 깊이는 xz평면으로 4분할, 홀수는 xyz축으로 8분할
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="depth"></param>
    public CullingTree(Bounds bounds, int depth)
    {
        _bounds = bounds;

        if (depth > 0)
        {
            // 프로파일링 해보고 4분할만 하든 4,8 번갈아 하든 선택하면 될듯함.
            CreateChild(depth);
            // CreateChildEight(depth);
        }
        else
        {
            _children = Array.Empty<CullingTree>();
        }
    }

    private void CreateChild(int depth)
    {
        var size = _bounds.size / 4.0f;
        var childSize = _bounds.size / 2.0f;
        var center = _bounds.center;

        var isEvenDepth = depth % 2 == 0;
        var childCount = isEvenDepth ? 4 : 8;

        _children = new CullingTree[childCount];

        if (isEvenDepth)
        {
            childSize.y = _bounds.size.y;
        }

        for (var index = 0; index < childCount; index++)
        {
            var x = (index & 1) == 0 ? -size.x : size.x;
            var y = isEvenDepth ? 0 : index < 4 ? -size.y : size.y;
            var z = (index & 2) == 0 ? -size.z : size.z;
            var childCenter = new Vector3(x, y, z) + center;
            _children[index] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
        }
    }

    private void CreateChildEight(int depth)
    {
        var childSize = _bounds.size / 2.0f;
        var center = _bounds.center;

        _children = new CullingTree[8];

        for (var i = 0; i < 8; i++)
        {
            var x = (i & 1) == 0 ? -childSize.x : childSize.x;
            var y = (i & 2) == 0 ? -childSize.y : childSize.y;
            var z = (i & 4) == 0 ? -childSize.z : childSize.z;

            var childCenter = new Vector3(x / 2, y / 2, z / 2) + center;
            _children[i] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
        }
    }

    /// <summary>
    /// 카메라 프러스텀 내에 있는 잔디 찾아서 반환
    /// </summary>
    /// <param name="frustum">카메라 프러스텀 평면들</param>
    /// <param name="visibleIDList">보이는 잔디 ID들을 저장할 리스트</param>
    public void GetVisibleObjectsInFrustum(Plane[] frustum, List<int> visibleIDList)
    {
#if UNITY_EDITOR
        _boundsList.Clear();
#endif
        if (GeometryUtility.TestPlanesAABB(frustum, _bounds)) // 프러스텀과 현재 노드 영역이 겹치는지
        {
            if (_children.Length == 0) // 리프 노드면 포함된 잔디들을 결과에 추가
            {
                if (_grassIDHeld.Count > 0)
                {
                    visibleIDList.AddRange(_grassIDHeld);
#if UNITY_EDITOR
                    _boundsList.Add(_bounds);
#endif
                }
            }
            else // 내부 노드면 자식들 재귀 호출
            {
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null)
                    {
                        _children[i].GetVisibleObjectsInFrustum(frustum, visibleIDList);
#if UNITY_EDITOR
                        _boundsList.AddRange(_children[i]._boundsList);
#endif
                    }
                }
            }
        }
    }

    /// <summary>
    /// 주어진 위치의 리프 노드를 찾아 잔디 ID추가
    /// </summary>
    /// <param name="point">잔디 위치</param>
    /// <param name="index">잔디 ID</param>
    /// <returns></returns>
    public bool GetClosestNode(Vector3 point, int index)
    {
        if (!_bounds.Contains(point)) return false;
        
        if (_children.Length == 0) // 내부 노드면 자식 검사
        {
            _grassIDHeld.Add(index);
            return true;
        }

        foreach (var child in _children)
        {
            if (child != null && child.GetClosestNode(point, index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 모든 리프 노드들 수집
    /// 트리 전체 구조순화 혹은 디버깅 때 사용
    /// </summary>
    /// <param name="target">결과 저장 리스트</param>
    public void GetAllNodes(List<CullingTree> target)
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
                    _children[i].GetAllNodes(target);
                }
            }
        }
    }

    /// <summary>
    /// 빈 노드들 정리
    /// 잔디 제거, 이동된 후 불필요한 노드들 제거
    /// </summary>
    /// <returns>현재 노드가 비어있는지</returns>
    public bool ClearEmptyNodes()
    {
        if (_children.Length > 0)
        {
            var allChildrenEmpty = true;
            for (var i = 0; i < _children.Length; i++)
            {
                if (_children[i] != null)
                {
                    if (_children[i].ClearEmptyNodes())
                    {
                        _children[i] = null; // 빈 자식 노드 제거
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

    /// <summary>
    /// 특정 위치를 중심으로 지정된 반경 내의 잔디 ID 반환
    /// 잔디 전체 순회하지 않고 필요한 영역의 잔디만 효율적으로 찾음
    /// </summary>
    /// <param name="resultList"></param>
    /// <param name="point"></param>
    /// <param name="radius"></param>
    public void GetObjectsInRadius(List<int> resultList, Vector3 point, float radius)
    {
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return;
        }

        if (_children.Length == 0) // 리프 노드면 모든 ID 추가
        {
            resultList.AddRange(_grassIDHeld);
        }
        else // 내부 노드면 반경 내에 있는 자식들 재귀 호출
        {
            for (var i = 0; i < _children.Length; i++)
            {
                if (_children[i] != null && _children[i]._bounds.SqrDistance(point) <= radius * radius)
                {
                    _children[i].GetObjectsInRadius(resultList, point, radius);
                }
            }
        }
    }

    /// <summary>
    /// 특정 위치 잔디 ID 제거
    /// 잔디 제거, 다른 위치로 이동될 때 사용
    /// </summary>
    /// <param name="point">제거할 잔디 위치</param>
    /// <param name="index">제거할 잔디 ID</param>
    /// <returns></returns>
    public bool RemoveObject(Vector3 point, int index)
    {
        if (!_bounds.Contains(point))
        {
            return false;
        }

        if (_children.Length == 0)
        {
            return _grassIDHeld.Remove(index);
        }

        for (var i = 0; i < _children.Length; i++)
        {
            if (_children[i] != null && _children[i].RemoveObject(point, index))
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public void DrawBounds()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        for (int i = 0; i < _boundsList.Count; i++)
        {
            Gizmos.DrawWireCube(_boundsList[i].center, _boundsList[i].size);
        }

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireCube(_bounds.center, _bounds.size);
    }

    public void DrawAllBounds()
    {
        DrawNodeBounds(this);
    }

    private void DrawNodeBounds(CullingTree node)
    {
        if (node == null) return;
        var center = node._bounds.center + new Vector3(0, 0.01f, 0);
        if (node._children.Length == 0) // 리프 노드
        {
            if (node._grassIDHeld.Count > 0) // 잔디가 있는 리프 노드만
            {
                // 리프 노드는 초록색으로 표시
                Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.3f);
                Gizmos.DrawWireCube(center, node._bounds.size);

                // 내부를 반투명하게 채움
                Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.1f);
                Gizmos.DrawCube(center, node._bounds.size);
            }
        }
        else // 내부 노드
        {
            // 흰색으로 표시
            Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.3f);
            Gizmos.DrawWireCube(center, node._bounds.size);
        }

        // 자식 노드들 재귀적으로 그리기
        foreach (var child in node._children)
        {
            if (child != null)
            {
                DrawNodeBounds(child);
            }
        }
    }

#endif
}