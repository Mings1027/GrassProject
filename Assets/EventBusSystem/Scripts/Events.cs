using UnityEngine;

public interface IEvent { }

public delegate void RefEventHandler<T>(ref T args) where T : IEvent;

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

public struct GrassColorEvent : IEvent
{
    public Vector3 position;
    public Color grassColor;
}