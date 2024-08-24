using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PoolObjectControl
{
    public class VfxPoolObject : MonoBehaviour
    {
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            DisableObject().Forget();
        }

        private async UniTaskVoid DisableObject()
        {
            var maxDurationTime = _particleSystem.main.duration;
            _particleSystem.Play();

            await UniTask.Delay(TimeSpan.FromSeconds(maxDurationTime),
                cancellationToken: this.GetCancellationTokenOnDestroy());
            gameObject.SetActive(false);
        }
    }
}