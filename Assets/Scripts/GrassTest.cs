using System;
using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;

public class GrassTest : MonoBehaviour
{
    private GrassComputeScript _grassComputeScript;
    [SerializeField] private float removeRadius = 1;

    private void Start()
    {
        _grassComputeScript = FindAnyObjectByType<GrassComputeScript>();
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