using Grass.GrassScripts.EventBusSystem;
using UnityEngine;

public class GrassInteractor : MonoBehaviour
{
    [Range(0, 100)] public float radius = 1f;

    private void OnEnable()
    {
        EventBus<InteractorAddedEvent>.Raise(new InteractorAddedEvent { Interactor = this });
    }

    private void OnDisable()
    {
        EventBus<InteractorRemovedEvent>.Raise(new InteractorRemovedEvent { Interactor = this });
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}