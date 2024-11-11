using System;
using System.Collections.Generic;
using UnityEngine;

public class CullingTree
{
    private Bounds _bounds; // 현재 노드의 영역
    private readonly CullingTree[] _children; // 자식 노드들
    private readonly List<int> _grassIDHeld = new(); // 현재 노드에 포함된 잔디 ID들

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

            for (var i = 0; i < childCount; i++)
            {
                var childCenter = CalculateChildCenter(center, size, i, isEvenDepth);
                _children[i] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
            }
        }
        else
        {
            _children = Array.Empty<CullingTree>();
        }
    }

    public Bounds GetBounds()
    {
        return _bounds;
    }

    /// <summary>
    /// 자식 노드의 중심점 계산
    /// </summary>
    /// <param name="parentCenter"></param>
    /// <param name="size"></param>
    /// <param name="index"></param>
    /// <param name="isEvenDepth"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 카메라 프러스텀 내에 있는 잔디 찾아서 반환
    /// </summary>
    /// <param name="frustum">카메라 프러스텀 평면들</param>
    /// <param name="list">결과 영역들을 저장할 리스트</param>
    /// <param name="visibleIDList">보이는 잔디 ID들을 저장할 리스트</param>
    public void RetrieveLeaves(Plane[] frustum, List<Bounds> list, List<int> visibleIDList)
    {
        if (GeometryUtility.TestPlanesAABB(frustum, _bounds)) // 프러스텀과 현재 노드 영역이 겹치는지
        {
            if (_children.Length == 0) // 리프 노드면 포함된 잔디들을 결과에 추가
            {
                if (_grassIDHeld.Count > 0)
                {
                    list.Add(_bounds);
                    visibleIDList.AddRange(_grassIDHeld);
                }
            }
            else // 내부 노드면 자식들 재귀 호출
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

    /// <summary>
    /// 주어진 위치의 리프 노드를 찾아 잔디 ID추가
    /// </summary>
    /// <param name="point">잔디 위치</param>
    /// <param name="index">잔디 ID</param>
    /// <returns></returns>
    public bool FindLeaf(Vector3 point, int index)
    {
        if (_bounds.Contains(point))
        {
            if (_children.Length != 0) // 내부 노드면 자식 검사
            {
                for (var i = 0; i < _children.Length; i++)
                {
                    if (_children[i] != null && _children[i].FindLeaf(point, index))
                    {
                        return true;
                    }
                }
            }
            else // 리프 노드면 ID 추가
            {
                _grassIDHeld.Add(index);
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
    public void RetrieveAllLeaves(List<CullingTree> target)
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

    /// <summary>
    /// 빈 노드들 정리
    /// 잔디 제거, 이동된 후 불필요한 노드들 제거
    /// </summary>
    /// <returns>현재 노드가 비어있는지</returns>
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
    /// <param name="point"></param>
    /// <param name="grassList"></param>
    /// <param name="radius"></param>
    public void ReturnLeafList(Vector3 point, List<int> grassList, float radius)
    {
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return;
        }

        if (_children.Length == 0) // 리프 노드면 모든 ID 추가
        {
            grassList.AddRange(_grassIDHeld);
        }
        else // 내부 노드면 반경 내에 있는 자식들 재귀 호출
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

    /// <summary>
    /// 특정 위치 잔디 ID 제거
    /// 잔디 제거, 다른 위치로 이동될 때 사용
    /// </summary>
    /// <param name="point">제거할 잔디 위치</param>
    /// <param name="index">제거할 잔디 ID</param>
    /// <returns></returns>
    public bool Remove(Vector3 point, int index)
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
            if (_children[i] != null && _children[i].Remove(point, index))
            {
                return true;
            }
        }

        return false;
    }
}