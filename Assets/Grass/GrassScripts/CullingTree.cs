using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// GrassComputeScript의 GrassData 리스트원소 중 카메라 영역 내에 위치한 잔디의 인덱스만 visibleIDList에 저장해준다.
/// 이를 가지고 Compute Shader에 넘겨 visibleIDList원소를 인덱스로 하는 GrassData 리스트만 GPU 연산을 한다. 
/// </summary>
public class CullingTree
{
    private Bounds _bounds; // 현재 노드의 영역
    private CullingTree[] _children; // 자식 노드들
    private readonly List<int> _grassIDList = new(); // 현재 노드에 포함된 잔디 ID들

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
            // CreateChild(depth);
            // CreateQuadChild(depth);
            CreateQctChild(depth);
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
            // 4분할일 때 상대적 위치 (x, z)
            // 좌하단, 우하단, 좌상단, 우상단 순서
            Vector2[] positions = new Vector2[4]
            {
                new Vector2(-1, -1), // 좌하단
                new Vector2(1, -1), // 우하단
                new Vector2(-1, 1), // 좌상단
                new Vector2(1, 1) // 우상단
            };

            childSize.y = _bounds.size.y; // y축은 유지

            for (int i = 0; i < positions.Length; i++)
            {
                var childCenter = center + new Vector3(
                    positions[i].x * size.x,
                    0,
                    positions[i].y * size.z
                );
                _children[i] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
            }
        }
        else
        {
            // 8분할일 때 상대적 위치 (x, y, z)
            Vector3[] positions = new Vector3[8]
            {
                new Vector3(-1, -1, -1), // 좌하단 뒤
                new Vector3(1, -1, -1), // 우하단 뒤
                new Vector3(-1, 1, -1), // 좌상단 뒤
                new Vector3(1, 1, -1), // 우상단 뒤
                new Vector3(-1, -1, 1), // 좌하단 앞
                new Vector3(1, -1, 1), // 우하단 앞
                new Vector3(-1, 1, 1), // 좌상단 앞
                new Vector3(1, 1, 1) // 우상단 앞
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var childCenter = center + new Vector3(
                    positions[i].x * size.x,
                    positions[i].y * size.y,
                    positions[i].z * size.z
                );
                _children[i] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
            }
        }
    }

    private void CreateQuadChild(int depth)
    {
        var quarterSize = _bounds.size / 4.0f;
        var childSize = _bounds.size / 2.0f;
        var center = _bounds.center;

        _children = new CullingTree[4];

        // 미리 정의된 상대적 위치 배열 (x, z)
        Vector2[] positions = new Vector2[4]
        {
            new Vector2(-1, -1), // 좌하단
            new Vector2(1, -1), // 우하단
            new Vector2(-1, 1), // 좌상단
            new Vector2(1, 1) // 우상단
        };

        for (int i = 0; i < 4; i++)
        {
            var childCenter = center + new Vector3(
                positions[i].x * quarterSize.x,
                0,
                positions[i].y * quarterSize.z
            );

            var childBounds = new Bounds(
                childCenter,
                new Vector3(childSize.x, _bounds.size.y, childSize.z)
            );

            _children[i] = new CullingTree(childBounds, depth - 1);
        }
    }

    private void CreateQctChild(int depth)
    {
        var childSize = _bounds.size / 2.0f;
        var quarterSize = _bounds.size / 4.0f;
        var center = _bounds.center;

        _children = new CullingTree[8];

        var positions = new Vector3[8]
        {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            new Vector3(-1, 1, 1),
            new Vector3(1, 1, 1),
        };
        for (var i = 0; i < positions.Length; i++)
        {
            var childCenter = center + new Vector3(
                positions[i].x * quarterSize.x,
                positions[i].y * quarterSize.y,
                positions[i].z * quarterSize.z);

            _children[i] = new CullingTree(new Bounds(childCenter, childSize), depth - 1);
        }
    }

    /// <summary>
    /// GrassData 리스트원소를 순회하며 리프노드 영역 안에 있는 것들을 찾아 grassIDList에 저장
    /// </summary>
    /// <param name="grassDataList"></param>
    /// <param name="visibleIDList"></param>
    public void InsertGrassData(List<GrassData> grassDataList, List<int> visibleIDList)
    {
        visibleIDList.Clear();
        for (int i = 0; i < grassDataList.Count; i++)
        {
            if (FindLeaf(grassDataList[i].position, i))
            {
                visibleIDList.Add(i);
            }
        }
    }

    /// <summary>
    /// 주어진 위치의 리프 노드를 찾아 잔디 ID추가
    /// </summary>
    /// <param name="point">잔디 위치</param>
    /// <param name="index">잔디 ID</param>
    /// <returns></returns>
    private bool FindLeaf(Vector3 point, int index)
    {
        if (!_bounds.Contains(point)) return false; // 영역 체크

        if (_children.Length == 0) // 리프 노드인지 확인
        {
            _grassIDList.Add(index); // 잔디 ID 저장
            return true;
        }

        foreach (var child in _children) // 자식 노드 탐색
        {
            if (child != null && child.FindLeaf(point, index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 카메라 프러스텀과 겹치는 영역의 잔디 ID만 효율적으로 수집
    /// 1. 노드의 경계와 프러스텀이 교차하는지 확인
    /// 2. 교차한다면 :
    ///     - 리프 노드의 경우 : 노드에 저장된 모든 잔디 ID를 visibleIDList에 추가
    ///     - 아닐 경우 : 모든 자식 노드를 재귀 검사
    /// 3. 교차하지 않으면 : 해당 노드 및 모든 하위 트리 스킵
    /// </summary>
    /// <param name="frustum">카메라 프러스텀 평면들</param>
    /// <param name="visibleIDList">보이는 잔디 ID들을 저장할 리스트</param>
    public void FrustumCull(Plane[] frustum, List<int> visibleIDList)
    {
        Profiler.BeginSample("FrustumCull With Recursion");
#if UNITY_EDITOR
        _boundsList.Clear();
#endif
        if (GeometryUtility.TestPlanesAABB(frustum, _bounds)) // 프러스텀과 현재 노드 영역이 겹치는지
        {
            if (_children.Length == 0) // 리프 노드면 포함된 잔디들을 결과에 추가
            {
                if (_grassIDList.Count > 0)
                {
                    visibleIDList.AddRange(_grassIDList);
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
                        _children[i].FrustumCull(frustum, visibleIDList);
#if UNITY_EDITOR
                        _boundsList.AddRange(_children[i]._boundsList);
#endif
                    }
                }
            }
        }

        Profiler.EndSample();
    }

    private static readonly Stack<CullingTree> _nodeProcessStack = new();

    public void FrustumCullTest(Plane[] frustum, List<int> visibleIDList)
    {
        Profiler.BeginSample("FrustumCull With Stack");
        _boundsList.Clear();
        _nodeProcessStack.Clear();
        _nodeProcessStack.Push(this);

        while (_nodeProcessStack.Count > 0)
        {
            var currentNode = _nodeProcessStack.Pop();

            if (GeometryUtility.TestPlanesAABB(frustum, currentNode._bounds))
            {
                if (currentNode._children.Length == 0)
                {
                    if (currentNode._grassIDList.Count > 0)
                    {
                        visibleIDList.AddRange(currentNode._grassIDList);
#if UNITY_EDITOR
                        _boundsList.Add(currentNode._bounds);
#endif
                    }
                }
                else
                {
                    for (int i = currentNode._children.Length - 1; i >= 0; i--)
                    {
                        if (currentNode._children[i] != null)
                        {
                            _nodeProcessStack.Push(currentNode._children[i]);
                        }
                    }
                }
            }
        }

        Profiler.EndSample();
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
        Profiler.BeginSample("GetObjectsInRadius With Recursion");
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return;
        }

        if (_children.Length == 0) // 리프 노드면 모든 ID 추가
        {
            resultList.AddRange(_grassIDList);
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

        Profiler.EndSample();
    }

    public void GetObjectsInRadiusTest(List<int> resultList, Vector3 point, float radius)
    {
        Profiler.BeginSample("GetObjectsInRadius With Stack");
        var expandedBounds = _bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return;
        }

        var squaredRadius = radius * radius;

        _nodeProcessStack.Clear();
        _nodeProcessStack.Push(this);

        while (_nodeProcessStack.Count > 0)
        {
            var currentNode = _nodeProcessStack.Pop();
            if (currentNode._bounds.SqrDistance(point) <= squaredRadius)
            {
                if (currentNode._children.Length == 0)
                {
                    resultList.AddRange(currentNode._grassIDList);
                }
                else
                {
                    for (int i = 0; i < currentNode._children.Length; i++)
                    {
                        if (currentNode._children[i] != null)
                        {
                            _nodeProcessStack.Push(currentNode._children[i]);
                        }
                    }
                }
            }
        }

        Profiler.EndSample();
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
                return _grassIDList.Count == 0;
            }
        }

        return _grassIDList.Count == 0 && _children.Length == 0;
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
            return _grassIDList.Remove(index);
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

    private void DrawNodeBounds(CullingTree rootNode)
    {
        if (rootNode == null) return;
        _nodeProcessStack.Clear();
        _nodeProcessStack.Push(rootNode);

        while (_nodeProcessStack.Count > 0)
        {
            var node = _nodeProcessStack.Pop();
            if (node == null) continue;

            var center = node._bounds.center + new Vector3(0, 0.01f, 0);
            if (node._children.Length == 0)
            {
                if (node._grassIDList.Count > 0)
                {
                    Gizmos.color = new Color(0, 1, 0, 0.3f);
                    Gizmos.DrawWireCube(center, node._bounds.size);

                    Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.1f);
                    Gizmos.DrawCube(center, node._bounds.size);
                }
            }
            else
            {
                Gizmos.color = new Color(1, 1, 1, 0.3f);
                Gizmos.DrawWireCube(center, node._bounds.size);

                for (int i = 0; i < node._children.Length; i++)
                {
                    if (node._children[i] != null)
                    {
                        _nodeProcessStack.Push(node._children[i]);
                    }
                }
            }
        }
    }

#endif
}