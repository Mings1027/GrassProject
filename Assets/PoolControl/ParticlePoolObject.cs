using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PoolControl
{
    public class ParticlePoolObject : PoolObject
    {
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            AwaitParticleCountZero().Forget();
        }

        private async UniTaskVoid AwaitParticleCountZero()
        {
            var main = _particleSystem.main;
            var particleTime = main.startLifetime.constant;
            await UniTask.Delay(TimeSpan.FromSeconds(particleTime),
                cancellationToken: this.GetCancellationTokenOnDestroy());

            gameObject.SetActive(false);
        }
    }
}