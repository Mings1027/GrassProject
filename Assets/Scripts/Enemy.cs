using Grass.GrassScripts.EventBusSystem;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private EventBinding<TargetingEvent> _testEventBinding;
    private EventBinding<InteractorAddedEvent> _interactorAddedEventBinding;
    private EventBinding<InteractorRemovedEvent> _interactorRemovedEventBinding;
    private EventBinding<GrassColorEvent> _grassColorEventBinding;

    [SerializeField] private int atk;
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetDir;
    [SerializeField] private float speed;

    private void OnEnable()
    {
        _testEventBinding = new EventBinding<TargetingEvent>(TargetingEvent);
        _testEventBinding.Add(NonTargetingEvent);
        _interactorAddedEventBinding = new EventBinding<InteractorAddedEvent>(InteractorAddedEvent);
        _interactorRemovedEventBinding = new EventBinding<InteractorRemovedEvent>(InteractorRemovedEvent);
        _grassColorEventBinding = new EventBinding<GrassColorEvent>(GrassColorEvent);

        EventBus<TargetingEvent>.Register(_testEventBinding);
        EventBus<InteractorAddedEvent>.Register(_interactorAddedEventBinding);
        EventBus<InteractorRemovedEvent>.Register(_interactorRemovedEventBinding);
        EventBus<GrassColorEvent>.Register(_grassColorEventBinding);
    }

    private void OnDisable()
    {
        EventBus<TargetingEvent>.Deregister(_testEventBinding);
        EventBus<InteractorAddedEvent>.Deregister(_interactorAddedEventBinding);
        EventBus<InteractorRemovedEvent>.Deregister(_interactorRemovedEventBinding);
        EventBus<GrassColorEvent>.Deregister(_grassColorEventBinding);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, targetDir);
    }

    private void TargetingEvent(TargetingEvent targetingEvent)
    {
        atk = targetingEvent.atk;
        target = targetingEvent.target;
        targetDir = (targetingEvent.target.position - transform.position);
        speed = targetingEvent.speed;
    }

    private void InteractorAddedEvent(InteractorAddedEvent interactorAddedEvent) { }
    private void InteractorRemovedEvent(InteractorRemovedEvent interactorRemovedEvent) { }
    private void GrassColorEvent(GrassColorEvent grassColorEvent) { }
    private void NonTargetingEvent() { }
}