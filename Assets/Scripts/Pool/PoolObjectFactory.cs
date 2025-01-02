using System.Collections.Generic;
using UnityEngine;

namespace Pool
{
    public class PoolObjectFactory
    {
        private readonly Dictionary<PoolObjectKey, GameObject> _prefabTable = new();

        public void InitFactory(PoolObjectData[] poolObjectDataList)
        {
            foreach (var poolObjectData in poolObjectDataList)
            {
                _prefabTable.Add(poolObjectData.poolObjectKey, poolObjectData.prefab);
            }
        }

        public GameObject CreatePoolObject(PoolObjectKey poolObjectKey)
        {
            var prefab = _prefabTable[poolObjectKey];
            if (prefab.TryGetComponent(out IPoolObject poolObject))
            {
                poolObject.poolObjectKey = poolObjectKey;
            }

            var obj = Object.Instantiate(prefab);
            obj.SetActive(false);
            return obj;
        }
    }
}