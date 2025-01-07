using UnityEngine;

public interface IEvent { }

public interface InteractorData
{
    Vector3 Position { get; }
    float Radius { get; }
    int ID { get; }
}

public struct InteractorAddedEvent : IEvent
{
    public InteractorData data;
}

public struct InteractorRemovedEvent : IEvent
{
    public InteractorData data;
}