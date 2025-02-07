using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Grass.GrassScripts
{
    public class Test : MonoBehaviour
    {
        [SerializeField] private GrassSeasonManager grassSeasonManager;
        [SerializeField] private GrassSeasonZone zone;
        [SerializeField] private float cycleDuration;
        [SerializeField] private float nextSeasonDuration;
        [SerializeField] private float seasonValue;

        private async Task Start()
        {
            zone.PlayCycle();
            zone.PauseTransition();
            await zone.PlayCycleAsync(cycleDuration);
            await zone.PlayNextSeasonAsync(nextSeasonDuration);
            zone.SetSeasonValue(seasonValue);
        }
    }
}