using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

namespace EventBusSystem.Scripts
{
    public static class EventBus<T> where T : IEvent
    {
        private static readonly HashSet<IEventBinding<T>> Bindings = new();
        private static readonly HashSet<IEventBinding<T>> Snapshots = new();

#if UNITY_EDITOR
        private static bool _localLogEnabled;
        private static int _eventRaiseCount;

        public static void SetLogEnabled(bool enable) => _localLogEnabled = enable;

        private static bool IsEnableLog => _localLogEnabled || EventBusDebug.EnableLog;
#endif

        public static void Register(EventBinding<T> binding)
        {
#if UNITY_EDITOR
            if (binding == null)
            {
                Debug.LogError($"Cannot register null binding for {typeof(T).Name}");
                return;
            }

            if (IsEnableLog)
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

            if (IsEnableLog)
                Debug.Log($"[EventBus:{typeof(T).Name}] Deregistered binding");
#endif
            Bindings.Remove(binding);
        }

        public static void Raise(T @event)
        {
#if UNITY_EDITOR
            _eventRaiseCount++;
            var timer = IsEnableLog ? Stopwatch.StartNew() : null;
#endif
            Snapshots.Clear();
            Snapshots.UnionWith(Bindings);

            foreach (var binding in Snapshots)
            {
                binding.OnEvent?.Invoke(@event);
                binding.OnEventNoArgs?.Invoke();
            }

#if UNITY_EDITOR
            if (IsEnableLog && timer != null)
            {
                timer.Stop();
                Debug.Log($"[EventBus:{typeof(T).Name}] Event completed in {timer.ElapsedMilliseconds}ms " +
                          $"(Total raised: {_eventRaiseCount}, Active listeners: {Bindings.Count})");
            }
#endif
        }

        private static void Clear()
        {
#if UNITY_EDITOR
            if (IsEnableLog)
                Debug.Log($"[EventBus:{typeof(T).Name}] Cleared {Bindings.Count} bindings");
#endif
            Bindings.Clear();
            Snapshots.Clear();
        }

#if UNITY_EDITOR
        public static int GetActiveBindingsCount() => Bindings.Count;
        public static int GetTotalEventsRaised() => _eventRaiseCount;

        public static List<string> GetRegisteredList()
        {
            var methodNames = new List<string>();

            foreach (var binding in Bindings)
            {
                // Delegate 처리를 위한 헬퍼 함수
                void ProcessDelegate(Delegate d)
                {
                    var method = d.Method;
                    var declaringType = method.DeclaringType?.Name ?? "Unknown";
                    var methodName = method.Name;

                    // 컴파일러 생성 메서드 필터링
                    bool isCompilerGenerated =
                        methodName.Contains("<") || // 람다, 로컬 함수 등
                        methodName.Contains("__") || // 컴파일러 생성 백업 필드
                        methodName.StartsWith("get_") || // 자동 구현 프로퍼티 getter
                        methodName.StartsWith("set_") || // 자동 구현 프로퍼티 setter
                        methodName == ".ctor" || // 생성자
                        methodName == ".cctor" || // 정적 생성자
                        method.DeclaringType?.Name.Contains("<>") == true; // 컴파일러 생성 타입

                    if (!isCompilerGenerated)
                    {
                        methodNames.Add($"{declaringType}.{methodName}");
                    }
                }

                // Action<T> 델리게이트 체크
                if (binding.OnEvent != null)
                {
                    foreach (var d in binding.OnEvent.GetInvocationList())
                    {
                        ProcessDelegate(d);
                    }
                }

                // Action 델리게이트 체크
                if (binding.OnEventNoArgs != null)
                {
                    foreach (var d in binding.OnEventNoArgs.GetInvocationList())
                    {
                        ProcessDelegate(d);
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
            }

            return count;
        }
#endif
    }
}