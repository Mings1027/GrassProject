using EventBusSystem.Scripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassInteractor : MonoBehaviour
{
    [Range(0, 100)] public float radius = 1f;

    private void OnEnable()
    {
        EventBus.Raise(new InteractorAddEvent(), this);
    }

    private void OnDisable()
    {
        EventBus.Raise(new InteractorRemoveEvent(), this);
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