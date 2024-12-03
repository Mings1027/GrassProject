using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SharedVariable.Scripts
{
    public class ExampleTest : MonoBehaviour
    {
        [SerializeField] private IntVariable health;

        private void Start()
        {
            DamageDelay().Forget();
        }

        private async UniTask DamageDelay()
        {
            while (health.Value > 0)
            {
                await UniTask.Delay(1000);
                health.Value--;
            }
        }
    }
}