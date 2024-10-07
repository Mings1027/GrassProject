using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Tool Settings", menuName = "Utility/GrassToolSettings")]
public class GrassToolSettingSO : ScriptableObject
{
    public enum VertexColorSetting
    {
        None,
        Red,
        Blue,
        Green
    }

    [Header("Terrain Layer Settings")] public float[] layerBlocking;

    public bool[] layerFading;

    [Header("Vertex Color Settings")] public VertexColorSetting vertexColorSettings;

    public VertexColorSetting vertexFade;

    // length/width
    public float sizeWidth;
    public float MinSizeWidth => 0.01f;
    public float MaxSizeWidth => 2f;

    public float sizeHeight;
    public float MinSizeHeight => 0.01f;
    public float MaxSizeHeight => 2f;

    // length/width adjustments
    public float AdjustWidth { get; set; }
    public float AdjustHeight { get; set; }
    public float MinAdjust => -1;
    public float MaxAdjust => 1;

    public float adjustWidthMax = 2f;
    public float adjustHeightMax = 2f;

    // reproject settings
    public float reprojectOffset = 1f;

    // color settings
    public float rangeR, rangeG, rangeB;
    public Color adjustedColor = Color.white;

    // brush settings
    public LayerMask paintMask = 1; // 풀을 그릴 위치를 설정하기 위한 레이어
    public LayerMask paintBlockMask = 0; // 풀을 그리지 않을 영역을 설정하기 위한 레이어

    public float brushSize = 4f;
    public float MinBrushSize => 0.1f;
    public float MaxBrushSize => 50f;

    public float brushFalloffSize = 0.8f;
    public float flow;
    public int density = 1;
    public float normalLimit = 1;
    public int rayDensity = 1;
    public int grassAmountToGenerate = 100000;
    public float generationDensity = 1;

    public void CreateNewLayers()
    {
        Debug.Log("Setting up initial tool settings");
        layerBlocking = new float[8];
        for (int i = 0; i < layerBlocking.Length; i++)
        {
            layerBlocking[i] = 1;
        }

        layerFading = new bool[8];
        layerFading[0] = true;
    }
}