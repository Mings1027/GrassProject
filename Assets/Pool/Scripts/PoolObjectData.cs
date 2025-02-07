using UnityEngine;

namespace Pool
{
    [CreateAssetMenu(fileName = "PoolObjectData", menuName = "Pool/PoolObjectData")]
    public class PoolObjectData : ScriptableObject
    {
        public PoolObjectKey poolObjectKey;
        public GameObject prefab;
        public byte initSize;
 
    }
}