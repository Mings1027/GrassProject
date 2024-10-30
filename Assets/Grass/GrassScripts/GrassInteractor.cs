using Grass.GrassScripts;
using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    [Range(0, 10)] public float radius = 1f;

    private void OnEnable()
    {
        GrassEventManager.TriggerEvent(GrassEvent.InteractorAdded, this);
    }

    private void OnDisable()
    {
        GrassEventManager.TriggerEvent(GrassEvent.InteractorRemoved, this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}