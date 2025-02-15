using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

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
        private static bool _logEnabled;

        public static void SetLogEnabled(bool enabled)
        {
            _logEnabled = enabled;
        }
#endif
        public static void Register(EventBinding<T> binding)
        {
#if UNITY_EDITOR
            if (binding == null)
            {
                Debug.LogError($"Cannot register null binding for {typeof(T).Name}");
                return;
            }

#endif
            Debug.Log($"Registering {typeof(T).Name}");
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

#endif
            Debug.Log($"Deregistering {typeof(T).Name}");
            Bindings.Remove(binding);
        }

        public static void Raise(T @event)
        {
#if UNITY_EDITOR
            if ((_logEnabled || EventBusDebug.GetEventSpecificLogEnabled(typeof(T).Name))
                && !typeof(IRequest).IsAssignableFrom(typeof(T))
                && !typeof(IResponse).IsAssignableFrom(typeof(T)))
            {
                var stackTrace = new StackTrace(true);
                var callerFrame = stackTrace.GetFrame(1); // Get the caller's stack frame
                if (callerFrame != null)
                {
                    var eventDetails = string.Join(", ",
                        @event.GetType()
                              .GetFields()
                              .Select(f => $"{f.Name}: {f.GetValue(@event)}"));

                    Debug.Log($"[EventBus] Raising event {typeof(T).Name} {{ {eventDetails} }}");
                }
            }
#endif
            Snapshots.Clear();
            Snapshots.UnionWith(Bindings);

            foreach (var binding in Snapshots)
            {
                binding.OnEvent?.Invoke(@event);
                binding.OnEventNoArgs?.Invoke();
            }
        }

        private static void Clear()
        {
            Bindings.Clear();
            Snapshots.Clear();
        }
    }

    public static class EventBusExtensions
    {
        private static readonly Dictionary<Guid, IEvent> ResponseMap = new();
        private static Guid _currentRequestId;

        public static TResponse Request<TRequest, TResponse>()
            where TRequest : IRequest, new() where TResponse : IResponse
        {
            return Request<TRequest, TResponse>(new TRequest());
        }

        public static TResponse Request<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest where TResponse : IResponse
        {
            _currentRequestId = Guid.NewGuid();

#if UNITY_EDITOR
            if (EventBusDebug.EnableLog || EventBusDebug.GetEventSpecificLogEnabled(typeof(TRequest).Name))
            {
                var requestDetails = string.Join(", ",
                    request.GetType()
                           .GetFields()
                           .Select(f => $"{f.Name}: {f.GetValue(request)}"));
                Debug.Log(
                    $"[EventBus] Request {typeof(TRequest).Name} {{ {requestDetails} }} (ID: {_currentRequestId.ToString().Substring(0, 8)})");
            }
#endif
            try
            {
                EventBus<TRequest>.Raise(request);
                if (ResponseMap.TryGetValue(_currentRequestId, out var response))
                {
                    var typedResponse = (TResponse)response;
#if UNITY_EDITOR
                    if (EventBusDebug.EnableLog || EventBusDebug.GetEventSpecificLogEnabled(typeof(TResponse).Name))
                    {
                        var responseDetails = string.Join(", ",
                            typedResponse.GetType()
                                         .GetFields()
                                         .Select(f => $"{f.Name}: {f.GetValue(typedResponse)}"));
                        Debug.Log(
                            $"[EventBus] Response {typeof(TResponse).Name} {{ {responseDetails} }} (ID: {_currentRequestId.ToString().Substring(0, 8)})");
                    }
#endif
                    return typedResponse;
                }

                throw new InvalidOperationException(
                    $"No response received for request {typeof(TRequest).Name} (ID: {_currentRequestId.ToString()[..8]})");
            }
            finally
            {
                ResponseMap.Remove(_currentRequestId);
            }
        }

        public static bool TryRequest<TRequest, TResponse>(TRequest request, out TResponse response)
            where TRequest : IRequest where TResponse : IResponse
        {
            _currentRequestId = Guid.NewGuid();

#if UNITY_EDITOR
            if (EventBusDebug.EnableLog || EventBusDebug.GetEventSpecificLogEnabled(typeof(TRequest).Name))
            {
                var requestDetails = string.Join(", ",
                    request.GetType()
                           .GetFields()
                           .Select(f => $"{f.Name}: {f.GetValue(request)}"));
                Debug.Log($"[EventBus] Request {typeof(TRequest).Name} {{ {requestDetails} }}");
            }
#endif

            try
            {
                EventBus<TRequest>.Raise(request);
                if (ResponseMap.TryGetValue(_currentRequestId, out var responseObj))
                {
                    response = (TResponse)responseObj;
#if UNITY_EDITOR
                    if (EventBusDebug.EnableLog || EventBusDebug.GetEventSpecificLogEnabled(typeof(TResponse).Name))
                    {
                        var response1 = response;
                        var responseDetails = string.Join(", ",
                            response.GetType()
                                    .GetFields()
                                    .Select(f => $"{f.Name}: {f.GetValue(response1)}"));
                        Debug.Log($"[EventBus] Response {typeof(TResponse).Name} {{ {responseDetails} }}");
                    }
#endif
                    return true;
                }

                response = default;
                return false;
            }
            finally
            {
                ResponseMap.Remove(_currentRequestId);
            }
        }

        public static void Response<TResponse>(TResponse response) where TResponse : IEvent
        {
            ResponseMap[_currentRequestId] = response;
        }

        internal static void ClearResponses()
        {
            ResponseMap.Clear();
        }
    }
}