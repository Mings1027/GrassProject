using Grass.GrassScripts;
using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    [Range(0, 100)] public float radius = 1f;

    private void OnEnable()
    {
        GrassEventManager.TriggerEvent(GrassEvent.AddInteractor, this);
    }

    private void OnDisable()
    {
        GrassEventManager.TriggerEvent(GrassEvent.RemoveInteractor, this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}