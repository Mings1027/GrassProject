namespace Grass.GrassScripts
{
    [System.Flags]
    public enum KeyBinding
    {
        None = 0,
        LeftControl = 1 << 0,
        RightControl = 1 << 1,
        LeftAlt = 1 << 2,
        RightAlt = 1 << 3,
        LeftShift = 1 << 4,
        RightShift = 1 << 5,
        LeftCommand = 1 << 6,
        RightCommand = 1 << 7
    }

    public enum MouseButton
    {
        LeftMouse,
        RightMouse,
        MiddleMouse
    }
}