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
        var timer = IsDebugging ? Stopwatch.StartNew() : null;
        if(IsDebugging) eventRaiseCount++;
#endif
        snapshots.Clear();
        snapshots.UnionWith(Bindings);

        foreach (var binding in snapshots)
        {
            binding.OnEvent?.Invoke(@event);
            binding.OnEventNoArgs?.Invoke();
        }

#if UNITY_EDITOR
        if (IsDebugging && timer != null)
        {
            timer.Stop();
            Debug.Log($"[EventBus:{typeof(T).Name}] Event completed in {timer.ElapsedMilliseconds}ms " +
                      $"(Total raised: {eventRaiseCount}, Active listeners: {Bindings.Count})");
        }
#endif
    }

    public static void Raise(ref T @event)
    {
#if UNITY_EDITOR
        var timer = IsDebugging ? Stopwatch.StartNew() : null;
        if(IsDebugging) eventRaiseCount++;
#endif
        snapshots.Clear();
        snapshots.UnionWith(Bindings);

        foreach (var binding in snapshots)
        {
            binding.OnRefEvent?.Invoke(ref @event);
        }

#if UNITY_EDITOR
        if (IsDebugging && timer != null)
        {
            timer.Stop();
            Debug.Log($"[EventBus:{typeof(T).Name}] Ref event completed in {timer.ElapsedMilliseconds}ms " +
                      $"(Total raised: {eventRaiseCount}, Active listeners: {Bindings.Count})");
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

    public static List<string> GetRegisteredList()
    {
        var methodNames = new List<string>();

        foreach (var binding in Bindings)
        {
            // Action<T> 델리게이트 체크
            if (binding.OnEvent != null)
            {
                foreach (var d in binding.OnEvent.GetInvocationList())
                {
                    var declaringType = d.Method.DeclaringType?.Name ?? "Unknown";
                    var methodName = d.Method.Name;
                
                    // 컴파일러 생성 메서드가 아닌 경우에만 추가
                    if (!methodName.Contains("<"))
                    {
                        methodNames.Add($"{declaringType}.{methodName}");
                    }
                    // methodNames.Add($"{d.Method.DeclaringType?.Name}.{d.Method.Name}");

                }
            }

            // Action 델리게이트 체크
            if (binding.OnEventNoArgs != null)
            {
                foreach (var d in binding.OnEventNoArgs.GetInvocationList())
                {
                    var declaringType = d.Method.DeclaringType?.Name ?? "Unknown";
                    var methodName = d.Method.Name;
                
                    // 컴파일러 생성 메서드가 아닌 경우에만 추가
                    if (!methodName.Contains("<"))
                    {
                        methodNames.Add($"{declaringType}.{methodName}");
                    }
                    // methodNames.Add($"{d.Method.DeclaringType?.Name}.{d.Method.Name}");
                }
            }

            // RefEventHandler<T> 델리게이트 체크
            if (binding.OnRefEvent != null)
            {
                foreach (var d in binding.OnRefEvent.GetInvocationList())
                {
                    var declaringType = d.Method.DeclaringType?.Name ?? "Unknown";
                    var methodName = d.Method.Name;
                
                    // 컴파일러 생성 메서드가 아닌 경우에만 추가
                    if (!methodName.Contains("<"))
                    {
                        methodNames.Add($"{declaringType}.{methodName}");
                    }
                    // methodNames.Add($"{d.Method.DeclaringType?.Name}.{d.Method.Name}");
                }
            }
        }

        return methodNames;
    }

    public static int GetRegisteredCount()
    {
        var count = 0;
        foreach (var binding in Bindings)
        {
            if (binding.OnEvent != null)
                count += binding.OnEvent.GetInvocationList().Length;
            if (binding.OnEventNoArgs != null)
                count += binding.OnEventNoArgs.GetInvocationList().Length;
            if (binding.OnRefEvent != null)
                count += binding.OnRefEvent.GetInvocationList().Length;
        }

        return count;
    }
#endif
}