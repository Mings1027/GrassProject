using UnityEngine;

namespace Pool
{
    [DisallowMultipleComponent]
    public abstract class PoolObject : MonoBehaviour, IPoolObject
    {
        public PoolObjectKey poolObjectKey { get; set; }
        
        public abstract void Use();

        private void OnEnable()
        {
            Use();
        }

        private void OnDisable()
        {
            PoolObjectManager.ReturnToPool(gameObject, poolObjectKey);
        }
    }
}