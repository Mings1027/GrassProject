#if UNITY_EDITOR
using UnityEditor;

namespace EventBusSystem.Scripts
{
    public static class EventBusDebug
    {
        public static string EventBusDebugEnableLog = "EventBusDebug_EnableLog";
        public static bool EnableLog { get; private set; }

        private const string EventBusSpecificLogPrefix = "EventBusDebug_Log_";

        static EventBusDebug()
        {
            EnableLog = EditorPrefs.GetBool(EventBusDebugEnableLog, false);
        }

        public static void SetLogEnabled(bool enable)
        {
            EnableLog = enable;
            EditorPrefs.SetBool(EventBusDebugEnableLog, enable);
        }

        private static string GetEventSpecificLogKey(string eventTypeName)
        {
            return $"{EventBusSpecificLogPrefix}{eventTypeName}";
        }

        public static bool GetEventSpecificLogEnabled(string eventTypeName)
        {
            return EditorPrefs.GetBool(GetEventSpecificLogKey(eventTypeName), false);
        }

        public static void SetEventSpecificLogEnabled(string eventTypeName, bool enable)
        {
            EditorPrefs.SetBool(GetEventSpecificLogKey(eventTypeName), enable);
        }
    }
}
#endif