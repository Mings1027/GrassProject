#if UNITY_EDITOR
namespace EventBusSystem.Scripts
{
    public static class EventBusDebug
    {
        public static string EventBusDebugEnableLog = "EventBusDebug_EnableLog";
        public static bool EnableLog { get; private set; }

        public static void SetLogEnabled(bool enable)
        {
            EnableLog = enable;
        }
    }
}
#endif