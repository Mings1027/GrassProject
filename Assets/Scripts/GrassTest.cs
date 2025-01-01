using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;

public class GrassTest : MonoBehaviour
{
    private GrassComputeScript _grassComputeScript;
    [SerializeField] private float removeRadius = 1;
    [SerializeField] private GrassSeasonZone seasonZone;

    private void Start()
    {
        _grassComputeScript = FindAnyObjectByType<GrassComputeScript>();
        SeasonTest();
    }

    private async void SeasonTest()
    {
        await Task.Delay(3000);
        Debug.Log(seasonZone.CurrentSeason);
        
        await seasonZone.StartSeasonTransition();
        Debug.Log(seasonZone.CurrentSeason);
        await Task.Delay(1000);

        await seasonZone.StartSeasonTransition(false, 4);
        Debug.Log(seasonZone.CurrentSeason);
        await Task.Delay(1000);

        await seasonZone.StartSeasonTransition(true);
        Debug.Log(seasonZone.CurrentSeason);
        await Task.Delay(1000);

        await seasonZone.StartSeasonTransition(true, 4);
        Debug.Log(seasonZone.CurrentSeason);
        await Task.Delay(1000);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, removeRadius);
    }

    [ContextMenu("RemoveGrassTest")]
    private void RemoveGrassTest()
    {
        _grassComputeScript.RemoveGrassInRadius(transform.position, removeRadius);
    }
}