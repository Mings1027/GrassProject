using System;
using System.Collections.Generic;

namespace Grass.GrassScripts
{
    public enum GrassEvent
    {
        InteractorAdded,
        InteractorRemoved,
    }

    public static class GrassEventManager
    {
        private static readonly Dictionary<GrassEvent, Action> EventDictionary = new();

        public static void AddListener(GrassEvent eventType, Action action)
        {
            if (EventDictionary.TryAdd(eventType, action)) return;
            EventDictionary[eventType] += action;
        }


        public static void RemoveListener(GrassEvent eventType, Action listener)
        {
            if (EventDictionary.TryGetValue(eventType, out var existingAction))
            {
                EventDictionary[eventType] = existingAction - listener;
            }
        }

        public static void TriggerEvent(GrassEvent eventType)
        {
            if (EventDictionary.TryGetValue(eventType, out var action))
            {
                action?.Invoke();
            }
        }
    }
    
    public static class GrassEventManager<T>
    {
        private static readonly Dictionary<GrassEvent, Action<T>> EventDictionary = new();

        public static void AddListener(GrassEvent eventType, Action<T> action)
        {
            if (EventDictionary.TryAdd(eventType, action)) return;
            EventDictionary[eventType] += action;
        }


        public static void RemoveListener(GrassEvent eventType, Action<T> listener)
        {
            if (EventDictionary.TryGetValue(eventType, out var existingAction))
            {
                EventDictionary[eventType] = existingAction - listener;
            }
        }

        public static void TriggerEvent(GrassEvent eventType, T value)
        {
            if (EventDictionary.TryGetValue(eventType, out var action))
            {
                action?.Invoke(value);
            }
        }
    }
    
}