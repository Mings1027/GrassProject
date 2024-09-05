using System;
using UnityEngine;

public class ShaderInteractor : MonoBehaviour
{
    public float radius = 1f;

    private void Start()
    {
        FindFirstObjectByType<GrassComputeScript>().SetInteractors(this);
    }

    private void OnDisable()
    {
        FindFirstObjectByType<GrassComputeScript>().RemoveInteractor(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}