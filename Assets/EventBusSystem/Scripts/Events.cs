using System;
using UnityEngine;

public interface IEvent { }

public interface IInteractorData
{
    Vector3 Position { get; }
    float Radius { get; }
}

public struct InteractorAddedEvent : IEvent
{
    public IInteractorData data;
}

public struct InteractorRemovedEvent : IEvent
{
    public IInteractorData data;
}

public struct GrassColorEvent : IEvent
{
    public Vector3 position;
    public Color defaultColor;
}

public struct GrassColorResultEvent : IEvent
{
    public Color resultColor;
}