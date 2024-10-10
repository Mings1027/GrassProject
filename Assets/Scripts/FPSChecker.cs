using UnityEngine;

public class FPSChecker : MonoBehaviour
{
    [Range(1, 100)] public int fFontSize;
    [Range(0, 1)] public float red;
    [Range(0, 1)] public float green;
    [Range(0, 1)] public float blue;
    [Range(0, 1)] public float xPosition = 0.0f; // X offset (0 is left, 1 is right)
    [Range(0, 1)] public float yPosition = 0.0f; // Y offset (0 is top, 1 is bottom)

    public int framesToSkip = 5;

    private int _frameCounter = 0;
    private float _deltaTime;
    private float _msec;
    private float _fps;

    public enum TargetFrameRate
    {
        NoLimit = 0,
        FPS30 = 30,
        FPS45 = 45,
        FPS60 = 60,
        FPS90 = 90,
        FPS120 = 120,
        FPS144 = 144
    }

    public TargetFrameRate targetFrameRate = TargetFrameRate.NoLimit;

    private void Awake()
    {
        ApplyTargetFrameRate();
    }

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

        _frameCounter++;
        if (_frameCounter >= framesToSkip)
        {
            _msec = _deltaTime * 1000.0f;
            _fps = 1.0f / _deltaTime;
            _frameCounter = 0;
        }
    }

    private void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        var style = new GUIStyle();
        var rect = new Rect(w * xPosition, h * yPosition, w,
            h * 0.02f); // Adjust position using xPosition and yPosition
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / fFontSize;
        style.normal.textColor = new Color(red, green, blue, 1.0f);
        var text = $"{_msec:0.0} ms ({_fps:0.} fps)";
        GUI.Label(rect, text, style);
    }

    private void ApplyTargetFrameRate()
    {
#if UNITY_EDITOR
        Application.targetFrameRate = -1;
#elif UNITY_IOS
        if (targetFrameRate == TargetFrameRate.NoLimit)
        {
            Application.targetFrameRate = -1; // -1 means no limit
        }
        else
        {
            Application.targetFrameRate = (int)targetFrameRate;
        }
#endif
    }
}