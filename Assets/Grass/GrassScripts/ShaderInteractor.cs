using System;
using UnityEngine;

public class ShaderInteractor : MonoBehaviour
{
    public float radius = 1f;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}