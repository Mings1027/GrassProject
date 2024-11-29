using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Pool
{
    public class PoolObjectManager : MonoBehaviour
    {
        [SerializeField] private PoolObjectData[] poolObjectData;
        [SerializeField] private bool autoPoolCleaner;
        [SerializeField] private float autoCleanerDelay = 3f;
        [SerializeField] private byte poolMaxSize;

        private PoolObjectFactory _poolObjectFactor = new();
        private static PoolObjectManager _inst;
        private Dictionary<PoolObjectKey, Stack<GameObject>> _poolStackTable;

        private void Awake()
        {
            _inst = this;
            _poolStackTable = new Dictionary<PoolObjectKey, Stack<GameObject>>();
            _poolObjectFactor.InitFactory(poolObjectData);
            for (int i = 0; i < poolObjectData.Length; i++)
            {
                var pool = poolObjectData[i];
                _poolStackTable.Add(pool.poolObjectKey, new Stack<GameObject>());
                for (int j = 0; j < pool.initSize; j++)
                {
                    var obj = _poolObjectFactor.CreatePoolObject(pool.poolObjectKey);
#if UNITY_EDITOR
                    obj.transform.SetParent(transform);
#endif
                }
            }
        }

        private void Start()
        {
            if (autoPoolCleaner)
            {
                _ = PoolCleaner();
            }
        }

        public static void Get(PoolObjectKey poolObjectKey, Transform t) =>
            _inst.Spawn(poolObjectKey, t.position, t.rotation);

        public static T Get<T>(PoolObjectKey poolObjectKey, Transform t) where T : Component
        {
            var obj = _inst.Spawn(poolObjectKey, t.position, t.rotation);
            obj.TryGetComponent(out T component);
            return component;
        }

        public static T Get<T>(PoolObjectKey poolObjectKey, Vector3 position) where T : Component
        {
            var obj = _inst.Spawn(poolObjectKey, position, Quaternion.identity);
            obj.TryGetComponent(out T component);
            return component;
        }

        private GameObject Spawn(PoolObjectKey poolObjectKey, Vector3 position, Quaternion rotation)
        {
            var poolStack = _poolStackTable[poolObjectKey];
            if (poolStack.Count <= 0)
            {
                var obj = _poolObjectFactor.CreatePoolObject(poolObjectKey);
#if UNITY_EDITOR
                obj.transform.SetParent(transform);
#endif
            }

            var poolObj = poolStack.Pop();
            poolObj.transform.SetPositionAndRotation(position, rotation);
            poolObj.SetActive(true);
            return poolObj;
        }

        public static void ReturnToPool(GameObject obj, PoolObjectKey poolObjectKey)
        {
            if (!_inst._poolStackTable.TryGetValue(poolObjectKey, out var stack)) return;
            stack.Push(obj);
        }

        private static async UniTask PoolCleaner()
        {
            var cancellationToken = _inst.destroyCancellationToken;
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var poolStack in _inst._poolStackTable.Values)
                {
                    while (poolStack.Count > _inst.poolMaxSize)
                    {
                        Destroy(poolStack.Pop());
                        await UniTask.Delay(1000, cancellationToken: cancellationToken);
                    }
                }

                await UniTask.Delay(1000, cancellationToken: cancellationToken);
            }
        }
    }
}