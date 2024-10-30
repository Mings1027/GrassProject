using System;
using System.Collections.Generic;

namespace Grass.GrassScripts
{
    public enum GrassEvent
    {
        InteractorAdded,
        InteractorRemoved,
        TotalGrassCount,
        VisibleGrassCount,
    }

    public static class GrassEventManager
    {
        private static readonly Dictionary<GrassEvent, Delegate> EventDictionary = new();

        public static void AddEvent(GrassEvent grassEvent, Action action) => AddEventInternal(grassEvent, action);

        public static void AddEvent<T>(GrassEvent grassEvent, Action<T> action) => AddEventInternal(grassEvent, action);

        public static void AddEvent<T1, T2>(GrassEvent grassEvent, Action<T1, T2> action) =>
            AddEventInternal(grassEvent, action);

        public static void AddEvent<T1, T2, T3>(GrassEvent grassEvent, Action<T1, T2, T3> action) =>
            AddEventInternal(grassEvent, action);

        private static void AddEventInternal(GrassEvent grassEvent, Delegate action)
        {
            if (EventDictionary.TryAdd(grassEvent, action)) return;
            EventDictionary[grassEvent] = Delegate.Combine(EventDictionary[grassEvent], action);
        }

        public static void RemoveEvent(GrassEvent grassEvent, Action action) => RemoveEventInternal(grassEvent, action);

        public static void RemoveEvent<T>(GrassEvent grassEvent, Action<T> action) =>
            RemoveEventInternal(grassEvent, action);

        public static void RemoveEvent<T1, T2>(GrassEvent grassEvent, Action<T1, T2> action) =>
            RemoveEventInternal(grassEvent, action);

        public static void RemoveEvent<T1, T2, T3>(GrassEvent grassEvent, Action<T1, T2, T3> action) =>
            RemoveEventInternal(grassEvent, action);

        private static void RemoveEventInternal(GrassEvent grassEvent, Delegate action)
        {
            if (EventDictionary.ContainsKey(grassEvent))
            {
                EventDictionary[grassEvent] = Delegate.Remove(EventDictionary[grassEvent], action);
            }
        }

        public static void TriggerEvent(GrassEvent grassEvent)
        {
            if (EventDictionary.TryGetValue(grassEvent, out var action))
            {
                (action as Action)?.Invoke();
            }
        }

        public static void TriggerEvent<T>(GrassEvent grassEvent, T arg)
        {
            if (EventDictionary.TryGetValue(grassEvent, out var action))
            {
                (action as Action<T>)?.Invoke(arg);
            }
        }

        public static void TriggerEvent<T1, T2>(GrassEvent grassEvent, T1 arg1, T2 arg2)
        {
            if (EventDictionary.TryGetValue(grassEvent, out var action))
            {
                (action as Action<T1, T2>)?.Invoke(arg1, arg2);
            }
        }

        public static void TriggerEvent<T1, T2, T3>(GrassEvent grassEvent, T1 arg1, T2 arg2, T3 arg3)
        {
            if (EventDictionary.TryGetValue(grassEvent, out var action))
            {
                (action as Action<T1, T2, T3>)?.Invoke(arg1, arg2, arg3);
            }
        }
    }

    public static class GrassFuncManager
    {
        private static readonly Dictionary<GrassEvent, Delegate> FuncDictionary = new();

        public static void AddEvent<T>(GrassEvent grassEvent, Func<T> func)
        {
            if (FuncDictionary.TryAdd(grassEvent, func)) return;
            FuncDictionary[grassEvent] = Delegate.Combine(FuncDictionary[grassEvent], func);
        }

        public static void RemoveEvent<T>(GrassEvent grassEvent, Func<T> func)
        {
            if (FuncDictionary.ContainsKey(grassEvent))
            {
                FuncDictionary[grassEvent] = Delegate.Remove(FuncDictionary[grassEvent], func);
            }
        }

        public static T TriggerEvent<T>(GrassEvent grassEvent)
        {
            if (FuncDictionary.TryGetValue(grassEvent, out var func))
            {
                if (func is Func<T> typedFunc)
                {
                    return typedFunc();
                }
            }

            return default;
        }
    }
}