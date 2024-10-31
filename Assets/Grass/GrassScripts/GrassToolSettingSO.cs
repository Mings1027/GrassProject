using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Tool Settings", menuName = "Utility/GrassToolSettings")]
public class GrassToolSettingSo : ScriptableObject
{
    public enum VertexColorSetting
    {
        None,
        Red,
        Blue,
        Green
    }

    [Header("Terrain Layer Settings")] [SerializeField]
    private float[] layerBlocking = new float[8];

    public float[] LayerBlocking
    {
        get => layerBlocking;
        set => layerBlocking = value;
    }

    [SerializeField] private bool[] layerFading = new bool[8];

    public bool[] LayerFading
    {
        get => layerFading;
        set => layerFading = value;
    }

    [Header("Vertex Color Settings")] [SerializeField]
    private VertexColorSetting vertexColorSettings;

    public VertexColorSetting VertexColorSettings
    {
        get => vertexColorSettings;
        set => vertexColorSettings = value;
    }

    [SerializeField] private VertexColorSetting vertexFade;

    public VertexColorSetting VertexFade
    {
        get => vertexFade;
        set => vertexFade = value;
    }

    [SerializeField] private float grassWidth = 0.1f;

    public float GrassWidth
    {
        get => grassWidth;
        set => grassWidth = Mathf.Clamp(value, MinSizeWidth, MaxSizeWidth);
    }

    [SerializeField] private float grassHeight = 1f;

    public float GrassHeight
    {
        get => grassHeight;
        set => grassHeight = Mathf.Clamp(value, MinSizeHeight, MaxSizeHeight);
    }

    [SerializeField] private float adjustWidth = 0f;

    public float AdjustWidth
    {
        get => adjustWidth;
        set => adjustWidth = Mathf.Clamp(value, MinAdjust, MaxAdjust);
    }

    [SerializeField] private float adjustHeight = 0f;

    public float AdjustHeight
    {
        get => adjustHeight;
        set => adjustHeight = Mathf.Clamp(value, MinAdjust, MaxAdjust);
    }

    [SerializeField] private float adjustWidthMax = 2f;

    public float AdjustWidthMax
    {
        get => adjustWidthMax;
        set => adjustWidthMax = Mathf.Max(0.01f, value);
    }

    [SerializeField] private float adjustHeightMax = 2f;

    public float AdjustHeightMax
    {
        get => adjustHeightMax;
        set => adjustHeightMax = Mathf.Max(0.01f, value);
    }

    [SerializeField] private float reprojectOffset = 1f;

    public float ReprojectOffset
    {
        get => reprojectOffset;
        set => reprojectOffset = value;
    }

    [SerializeField] private float rangeR;

    [SerializeField] private float rangeG;

    [SerializeField] private float rangeB;

    public float RangeR
    {
        get => rangeR;
        set => rangeR = Mathf.Clamp01(value);
    }

    public float RangeG
    {
        get => rangeG;
        set => rangeG = Mathf.Clamp01(value);
    }

    public float RangeB
    {
        get => rangeB;
        set => rangeB = Mathf.Clamp01(value);
    }

    [SerializeField] private Color brushColor = Color.white;

    public Color BrushColor
    {
        get => brushColor;
        set => brushColor = value;
    }

    [SerializeField] private LayerMask paintMask = 1;

    public LayerMask PaintMask
    {
        get => paintMask;
        set => paintMask = value;
    }

    public List<string> GetPaintMaskLayerNames()
    {
        List<string> layerNames = new List<string>();

        for (int i = 0; i < 32; i++)
        {
            // Check if the layer is active in the LayerMask
            if ((PaintMask.value & (1 << i)) != 0)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layerNames.Add(layerName);
                }
            }
        }

        return layerNames;
    }

    [SerializeField] private LayerMask paintBlockMask = 0;

    public LayerMask PaintBlockMask
    {
        get => paintBlockMask;
        set => paintBlockMask = value;
    }

    [SerializeField] private float brushSize = 4f;

    public float BrushSize
    {
        get => brushSize;
        set => brushSize = Mathf.Clamp(value, MinBrushSize, MaxBrushSize);
    }

    [SerializeField] private float brushFalloffSize = 0.8f;

  

    [SerializeField] private float falloffOuterSpeed;

    public float FalloffOuterSpeed
    {
        get => falloffOuterSpeed;
        set => falloffOuterSpeed = Mathf.Clamp01(value);
    }

    [SerializeField] private int density = 1;

    public int Density
    {
        get => density;
        set => density = Mathf.Clamp(value, MinDensity, MaxDensity);
    }

    [SerializeField] private float normalLimit = 1f;

    public float NormalLimit
    {
        get => normalLimit;
        set => normalLimit = Mathf.Clamp(value, MinNormalLimit, MaxNormalLimit);
    }

    [SerializeField] private int grassAmountToGenerate = 100000;

    public int GrassAmountToGenerate
    {
        get => grassAmountToGenerate;
        set => grassAmountToGenerate = Mathf.Clamp(value, MinGrassAmountToGenerate, MaxGrassAmountToGenerate);
    }

    [SerializeField] private float generationDensity = 1f;

    public float GenerationDensity
    {
        get => generationDensity;
        set => generationDensity = Mathf.Clamp(value, MinGenerationDensity, MaxGenerationDensity);
    }

    public KeyBinding paintKey;
    public MouseButton paintButton;

    // Constants
    public float MinSizeWidth => 0.01f;
    public float MaxSizeWidth => 2f;
    public float MinSizeHeight => 0.01f;
    public float MaxSizeHeight => 2f;
    public float MinAdjust => -1f;
    public float MaxAdjust => 1f;
    public float MinBrushSize => 0.1f;
    public float MaxBrushSize => 50f; 
    public int MaxDensity => 100;
    public int MinDensity => 1;
    public float MaxNormalLimit => 1f;
    public float MinNormalLimit => 0f;
    public int MaxGrassAmountToGenerate => 100000;
    public int MinGrassAmountToGenerate => 0;
    public float MaxGenerationDensity => 10f;
    public float MinGenerationDensity => 0.01f;

    private void OnValidate()
    {
        // 값이 변경될 때 범위를 확인하고 조정합니다.
        GrassWidth = grassWidth;
        GrassHeight = grassHeight;
        AdjustWidth = adjustWidth;
        AdjustHeight = adjustHeight;
        BrushSize = brushSize;
        FalloffOuterSpeed = falloffOuterSpeed;
        Density = density;
        NormalLimit = normalLimit;
        GrassAmountToGenerate = grassAmountToGenerate;
        GenerationDensity = generationDensity;
        RangeR = rangeR;
        RangeG = rangeG;
        RangeB = rangeB;
    }

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