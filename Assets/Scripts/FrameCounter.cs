using UnityEngine;

public class FrameCounter : MonoBehaviour
{
    private float _deltaTime;
    private int _frameCount;
    
    [SerializeField] private int size = 25;
    [SerializeField] private Color color = Color.red;
    [SerializeField] private int frameSkip = 5; // 몇 프레임마다 업데이트할지 설정

    private void Update()
    {
        _frameCount++;

        // 설정한 프레임마다 업데이트
        if (_frameCount >= frameSkip)
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _frameCount = 0; // 카운터 초기화
        }
    }

    private void OnGUI()
    {
        var style = new GUIStyle();

        var rect = new Rect(30, 30, Screen.width, Screen.height);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = size;
        style.normal.textColor = color;

        var ms = _deltaTime * 1000f;
        var fps = 1.0f / _deltaTime;
        var text = $"{fps:0.} FPS ({ms:0.0} ms)";

        GUI.Label(rect, text, style);
    }
}