using EventBusSystem.Scripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassInteractor : MonoBehaviour, InteractorData
{
    public Vector3 Position => transform.position;
    public float Radius => radius;

    [Range(0, 100)] public float radius = 1f;

    private void OnEnable()
    {
        var addedEvent = new InteractorAddedEvent { data = this };
        EventBus<InteractorAddedEvent>.Raise(addedEvent);
    }

    private void OnDisable()
    {
        var removeEvent = new InteractorRemovedEvent { data = this };
        EventBus<InteractorRemovedEvent>.Raise(removeEvent);
    }

#if UNITY_EDITOR

    [SerializeField] private bool drawGizmos;
    
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}