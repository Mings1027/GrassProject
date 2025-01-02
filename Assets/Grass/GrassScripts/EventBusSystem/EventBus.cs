using System.Collections.Generic;
using System.Diagnostics;
using Grass.GrassScripts.EventBusSystem;
using Debug = UnityEngine.Debug;

public static class EventBus<T> where T : IEvent
{
    private static readonly HashSet<IEventBinding<T>> Bindings = new();
    private static readonly HashSet<IEventBinding<T>> snapshots = new();

#if UNITY_EDITOR
    private static bool isDebugEnabled = false;
    private static int eventRaiseCount = 0;

    public static void EnableDebugMode(bool enable) => isDebugEnabled = enable;

    private static bool IsDebugging => isDebugEnabled || EventBusDebug.IsGlobalDebugEnabled;
#endif

    public static void Register(EventBinding<T> binding)
    {
#if UNITY_EDITOR
        if (binding == null)
        {
            Debug.LogError($"Cannot register null binding for {typeof(T).Name}");
            return;
        }

        if (IsDebugging)
            Debug.Log($"[EventBus:{typeof(T).Name}] Registered new binding");
#endif
        Bindings.Add(binding);
    }

    public static void Deregister(EventBinding<T> binding)
    {
#if UNITY_EDITOR
        if (binding == null)
        {
            Debug.LogError($"Cannot deregister null binding for {typeof(T).Name}");
            return;
        }

        if (IsDebugging)
            Debug.Log($"[EventBus:{typeof(T).Name}] Deregistered binding");
#endif
        Bindings.Remove(binding);
    }

    public static void Raise(T @event)
    {
#if UNITY_EDITOR
        if (IsDebugging)
        {
            eventRaiseCount++;
            var timer = Stopwatch.StartNew();
#endif

            snapshots.Clear();
            snapshots.UnionWith(Bindings);

            foreach (var binding in snapshots)
            {
                binding.OnEvent?.Invoke(@event);
                binding.OnEventNoArgs?.Invoke();
            }

#if UNITY_EDITOR
            if (IsDebugging)
            {
                timer.Stop();
                Debug.Log($"[EventBus:{typeof(T).Name}] Event completed in {timer.ElapsedMilliseconds}ms " +
                          $"(Total raised: {eventRaiseCount}, Active listeners: {Bindings.Count})");
            }
        }
        else
        {
#endif
            snapshots.Clear();
            snapshots.UnionWith(Bindings);

            foreach (var binding in snapshots)
            {
                binding.OnEvent?.Invoke(@event);
                binding.OnEventNoArgs?.Invoke();
            }
#if UNITY_EDITOR
        }
#endif
    }

    public static void Raise(ref T @event)
    {
#if UNITY_EDITOR
        if (IsDebugging)
        {
            eventRaiseCount++;
            var timer = Stopwatch.StartNew();
#endif

            snapshots.Clear();
            snapshots.UnionWith(Bindings);

            foreach (var binding in snapshots)
            {
                binding.OnRefEvent?.Invoke(ref @event);
            }

#if UNITY_EDITOR
            if (IsDebugging)
            {
                timer.Stop();
                Debug.Log($"[EventBus:{typeof(T).Name}] Ref event completed in {timer.ElapsedMilliseconds}ms " +
                          $"(Total raised: {eventRaiseCount}, Active listeners: {Bindings.Count})");
            }
        }
        else
        {
#endif
            snapshots.Clear();
            snapshots.UnionWith(Bindings);

            foreach (var binding in snapshots)
            {
                binding.OnRefEvent?.Invoke(ref @event);
            }
#if UNITY_EDITOR
        }
#endif
    }

    private static void Clear()
    {
#if UNITY_EDITOR
        if (IsDebugging)
            Debug.Log($"[EventBus:{typeof(T).Name}] Cleared {Bindings.Count} bindings");
#endif
        Bindings.Clear();
        snapshots.Clear();
    }

#if UNITY_EDITOR
    public static int GetActiveBindingsCount() => Bindings.Count;
    public static int GetTotalEventsRaised() => eventRaiseCount;
#endif
}