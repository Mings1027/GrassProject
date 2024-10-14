using Grass.GrassScripts;
using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    public float radius = 1f;

    private void OnEnable()
    {
        GrassEventManager<GrassInteractor>.TriggerEvent(GrassEvent.InteractorAdded, this);
    }

    private void OnDisable()
    {
        GrassEventManager<GrassInteractor>.TriggerEvent(GrassEvent.InteractorRemoved, this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}