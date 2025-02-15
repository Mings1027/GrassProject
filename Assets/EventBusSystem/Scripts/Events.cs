using UnityEngine;

namespace EventBusSystem.Scripts
{
    public interface IEvent { }

    public interface IRequest : IEvent { }

    public interface IResponse : IEvent { }

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

    public struct GrassColorRequest : IRequest
    {
        public Vector3 position;
        public Color defaultColor;
    }

    public struct GrassColorResponse : IResponse
    {
        public Color resultColor;
    }
}