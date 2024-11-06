using System.Collections.Generic;

public static class CollectionsPool
{
    private const int InitialPoolSize = 10;

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new();

        static ListPool()
        {
            for (var i = 0; i < InitialPoolSize; i++)
            {
                Pool.Push(new List<T>(100));
            }
        }

        public static List<T> Get(int capacity)
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>(capacity);
        }

        public static List<T> Get(IEnumerable<T> collection)
        {
            var list = Pool.Count > 0 ? Pool.Pop() : new List<T>();
            list.AddRange(collection);
            return list;
        }

        public static void Return(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            if (Pool.Count < InitialPoolSize)
            {
                Pool.Push(list);
            }
        }
    }

    private static class HashSetPool<T>
    {
        private static readonly Stack<HashSet<T>> Pool = new();

        static HashSetPool()
        {
            for (var i = 0; i < InitialPoolSize; i++)
            {
                Pool.Push(new HashSet<T>(100));
            }
        }

        public static HashSet<T> Get(int capacity = 100)
        {
            return Pool.Count > 0 ? Pool.Pop() : new HashSet<T>(capacity);
        }

        public static HashSet<T> Get(IEnumerable<T> collection)
        {
            var set = Pool.Count > 0 ? Pool.Pop() : new HashSet<T>();
            foreach (var item in collection)
            {
                set.Add(item);
            }

            return set;
        }

        public static void Return(HashSet<T> set)
        {
            if (set == null) return;
            set.Clear();
            if (Pool.Count < InitialPoolSize)
            {
                Pool.Push(set);
            }
        }
    }

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

        public static Dictionary<TKey, TValue> Get(int capacity = 100)
        {
            return Pool.Count > 0 ? Pool.Pop() : new Dictionary<TKey, TValue>(capacity);
        }

        public static Dictionary<TKey, TValue> Get(IDictionary<TKey, TValue> dictionary)
        {
            var dict = Pool.Count > 0 ? Pool.Pop() : new Dictionary<TKey, TValue>();
            foreach (var kvp in dictionary)
            {
                dict.Add(kvp.Key, kvp.Value);
            }

            return dict;
        }

        public static Dictionary<TKey, TValue> Get(IEnumerable<KeyValuePair<TKey, TValue>> collection)
        {
            var dict = Pool.Count > 0 ? Pool.Pop() : new Dictionary<TKey, TValue>();
            foreach (var kvp in collection)
            {
                dict.Add(kvp.Key, kvp.Value);
            }

            return dict;
        }

        public static void Return(Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) return;
            dictionary.Clear();
            if (Pool.Count < InitialPoolSize)
            {
                Pool.Push(dictionary);
            }
        }
    }

    public static List<T> GetList<T>(int capacity = 100)
    {
        return ListPool<T>.Get(capacity);
    }

    public static List<T> GetList<T>(IEnumerable<T> collection)
    {
        return ListPool<T>.Get(collection);
    }

    public static void ReturnList<T>(List<T> list)
    {
        ListPool<T>.Return(list);
    }

    public static HashSet<T> GetHashSet<T>(int capacity = 100)
    {
        return HashSetPool<T>.Get(capacity);
    }

    public static HashSet<T> GetHashSet<T>(IEnumerable<T> collection)
    {
        return HashSetPool<T>.Get(collection);
    }

    public static void ReturnHashSet<T>(HashSet<T> set)
    {
        HashSetPool<T>.Return(set);
    }

    public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(int capacity = 100)
    {
        return DictionaryPool<TKey, TValue>.Get(capacity);
    }

    public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
    {
        return DictionaryPool<TKey, TValue>.Get(dictionary);
    }

    public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        return DictionaryPool<TKey, TValue>.Get(collection);
    }

    public static void ReturnDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
    {
        DictionaryPool<TKey, TValue>.Return(dictionary);
    }
}