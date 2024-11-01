using System.Collections.Generic;
using UnityEngine;

public static class PainterUtils
{
    private static readonly Vector3 UpOffset = new(0, 3f, 0);
    private static readonly Vector3 Down = Vector3.down;

    // 재사용 가능한 메모리 풀
    private static readonly Stack<List<int>> ListPool = new();
    private static readonly Stack<HashSet<int>> HashSetPool = new();
    private const int InitialPoolSize = 10;

    static PainterUtils()
    {
        // 풀 초기화
        for (int i = 0; i < InitialPoolSize; i++)
        {
            ListPool.Push(new List<int>(1000));
            HashSetPool.Push(new HashSet<int>(1000));
        }
    }

    public static List<int> GetList()
    {
        return ListPool.Count > 0 ? ListPool.Pop() : new List<int>(1000);
    }

    public static void ReturnList(List<int> list)
    {
        list.Clear();
        if (ListPool.Count < InitialPoolSize)
            ListPool.Push(list);
    }

    public static HashSet<int> GetHashSet()
    {
        return HashSetPool.Count > 0 ? HashSetPool.Pop() : new HashSet<int>(1000);
    }

    public static void ReturnHashSet(HashSet<int> set)
    {
        set.Clear();
        if (HashSetPool.Count < InitialPoolSize)
            HashSetPool.Push(set);
    }

    public static bool TryGetValidPoint(Vector3 startPos, LayerMask paintMask, float normalLimit, out RaycastHit hit)
    {
        if (Physics.Raycast(startPos + UpOffset, Down, out hit, float.MaxValue, paintMask))
        {
            return hit.normal.y <= 1 + normalLimit && hit.normal.y >= 1 - normalLimit;
        }
        return false;
    }

    // 최적화된 거리 계산 (제곱근 계산 제거)
    public static float SqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        float dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }
}