using System;
using UnityEngine;

namespace Grass.GrassScripts.EventBusSystem
{
    public interface IEventBinding<T> where T : IEvent
    {
        Action<T> OnEvent { get; set; }
        Action OnEventNoArgs { get; set; }
        RefEventHandler<T> OnRefEvent { get; set; }
    }

    public class EventBinding<T> : IEventBinding<T> where T : IEvent
    {
        private Action<T> _onEvent = _ => { };
        private Action _onEventNoArgs = () => { };
        private RefEventHandler<T> _onRefEvent = delegate { };

        Action<T> IEventBinding<T>.OnEvent
        {
            get => _onEvent;
            set => _onEvent = value;
        }

        Action IEventBinding<T>.OnEventNoArgs
        {
            get => _onEventNoArgs;
            set => _onEventNoArgs = value;
        }

        RefEventHandler<T> IEventBinding<T>.OnRefEvent
        {
            get => _onRefEvent;
            set => _onRefEvent = value;
        }

        public EventBinding(Action<T> onEvent) => _onEvent = onEvent;
        public EventBinding(Action onEventNoArgs) => _onEventNoArgs = onEventNoArgs;
        public EventBinding(RefEventHandler<T> onRefEvent) => _onRefEvent = onRefEvent;

        public void Add(Action<T> onEvent) => _onEvent += onEvent;
        public void Remove(Action<T> onEvent) => _onEvent -= onEvent;

        public void Add(Action onEvent) => _onEventNoArgs += onEvent;
        public void Remove(Action onEvent) => _onEventNoArgs -= onEvent;

        public void Add(RefEventHandler<T> onRefEvent) => _onRefEvent += onRefEvent;
        public void Remove(RefEventHandler<T> onRefEvent) => _onRefEvent -= onRefEvent;
    }
}