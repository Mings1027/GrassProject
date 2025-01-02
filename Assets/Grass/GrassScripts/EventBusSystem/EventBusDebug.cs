#if UNITY_EDITOR
public static class EventBusDebug
{
    public static bool IsGlobalDebugEnabled { get; private set; }

    public static void EnableGlobalDebugMode(bool enable)
    {
        IsGlobalDebugEnabled = enable;
    }
}
#endif