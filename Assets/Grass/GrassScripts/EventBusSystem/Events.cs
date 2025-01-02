using UnityEngine;

namespace Grass.GrassScripts.EventBusSystem
{
    public interface IEvent { }

    public delegate void RefEventHandler<T>(ref T args) where T : IEvent;

    public struct InteractorAddedEvent : IEvent
    {
        public GrassInteractor Interactor;
    }

    public struct InteractorRemovedEvent : IEvent
    {
        public GrassInteractor Interactor;
    }

    public struct GrassColorEvent : IEvent
    {
        public Vector3 position;
        public Color grassColor;
    }
}