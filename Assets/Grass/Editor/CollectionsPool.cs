using System.Collections.Generic;
using UnityEngine;

public static class CollectionsPool
{
    private static readonly Stack<List<int>> ListPool = new();
    private static readonly Stack<HashSet<int>> HashSetPool = new();
    private const int InitialPoolSize = 10;

    private static class DictionaryPool<TKey, TValue>
    {
        private static readonly Stack<Dictionary<TKey, TValue>> Pool = new();

        static DictionaryPool()
        {
            for (var i = 0; i < InitialPoolSize; i++)
            {
                Pool.Push(new Dictionary<TKey, TValue>());
            }
        }

        public static Dictionary<TKey, TValue> Get(int capacity = 1000)
        {
            return Pool.Count > 0 ? Pool.Pop() : new Dictionary<TKey, TValue>(capacity);
        }

        public static void Return(Dictionary<TKey, TValue> dictionary)
        {
            dictionary.Clear();
            if (Pool.Count < InitialPoolSize)
            {
                Pool.Push(dictionary);
            }
        }
    }

    static CollectionsPool()
    {
        // 풀 초기화
        for (var i = 0; i < InitialPoolSize; i++)
        {
            ListPool.Push(new List<int>(1000));
            HashSetPool.Push(new HashSet<int>(1000));
        }
    }

    public static List<int> GetList(int capacity = 1000)
    {
        return ListPool.Count > 0 ? ListPool.Pop() : new List<int>(capacity);
    }

    public static void ReturnList(List<int> list)
    {
        if (list == null) return;
        list.Clear();
        if (ListPool.Count < InitialPoolSize)
            ListPool.Push(list);
    }

    public static HashSet<int> GetHashSet(int capacity = 1000)
    {
        return HashSetPool.Count > 0 ? HashSetPool.Pop() : new HashSet<int>(capacity);
    }

    public static void ReturnHashSet(HashSet<int> set)
    {
        if(set == null) return;
        set.Clear();
        if (HashSetPool.Count < InitialPoolSize)
            HashSetPool.Push(set);
    }

    public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(int capacity = 1000)
    {
        return DictionaryPool<TKey, TValue>.Get(capacity);
    }

    public static void ReturnDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
    {
        if(dictionary == null) return;
        DictionaryPool<TKey, TValue>.Return(dictionary);
    }
    
    // 최적화된 거리 계산 (제곱근 계산 제거)
    public static float SqrDistance(Vector3 a, Vector3 b)
    {
        var dx = a.x - b.x;
        var dy = a.y - b.y;
        var dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }
}