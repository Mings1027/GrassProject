using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private GrassSeasonManager grassSeasonManager;
    [SerializeField] private GrassSeasonZone[] seasonEffectVolume;

    private void Start()
    {
        SetSeason().Forget();
    }

    private async UniTask SetSeason()
    {
        while (!destroyCancellationToken.IsCancellationRequested)
        {
            for (int i = 0; i < seasonEffectVolume.Length; i++)
            {
                seasonEffectVolume[i]
                    .SetSeasonValueOverTime(seasonEffectVolume[i].MinRange, seasonEffectVolume[i].MaxRange, 2);
                await UniTask.Delay(2000, cancellationToken: destroyCancellationToken);
            }

            await UniTask.Delay(1000, cancellationToken: destroyCancellationToken);
            grassSeasonManager.SetGlobalSeasonValueOverTime(grassSeasonManager.GlobalMaxRange, 2);
        }
    }
}