using UnityEngine;

namespace PoolControl
{
    [DisallowMultipleComponent]
    public class PoolObject : MonoBehaviour
    {
        public PoolObjectKey poolObjKey { get; set; }

        private void OnDisable()
        {
            PoolObjectManager.ReturnToPool(gameObject, poolObjKey);
        }
    }
}