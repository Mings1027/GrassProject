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

    [Header("Input Setting")] public KeyBinding paintKey;
    public MouseButton paintButton;

    [Header("Paint Layer Settings")] [SerializeField]
    private LayerMask paintMask = 1;
    [SerializeField] private LayerMask paintBlockMask = 0;

    [Header("Brush Settings")] [SerializeField]
    private float brushSize = 4f;
    [SerializeField] private float brushHeight = 2f;
    [SerializeField] private float falloffOuterSpeed;
    [SerializeField] private int density = 1;
    [SerializeField] private float normalLimit = 1f;
    [SerializeField] private Color brushColor = Color.white;

    [Header("Grass Size Settings")] [SerializeField]
    private float grassWidth = 0.1f;
    [SerializeField] private float grassHeight = 1f;

    [Header("Adjustment Settings")] [SerializeField]
    private float adjustWidth = 0f;
    [SerializeField] private float adjustHeight = 0f;
    [SerializeField] private float adjustWidthMax = 2f;
    [SerializeField] private float adjustHeightMax = 2f;
    [SerializeField] private float reprojectOffset = 1f;

    [Header("Color Settings")] [SerializeField]
    private float rangeR;
    [SerializeField] private float rangeG;
    [SerializeField] private float rangeB;

    [Header("Generation Settings")] [SerializeField]
    private int grassAmountToGenerate = 100000;
    [SerializeField] private float generationDensity = 1f;

    [Header("Terrain Layer Settings")] [SerializeField]
    private float[] layerBlocking = new float[8];

    [SerializeField] private bool[] layerFading = new bool[8];

    [Header("Vertex Color Settings")] [SerializeField]
    private VertexColorSetting vertexColorSettings;
    [SerializeField] private VertexColorSetting vertexFade;

    #region Constants
    public float MinSizeWidth => 0.01f;
    public float MaxSizeWidth => 2f;
    public float MinSizeHeight => 0.01f;
    public float MaxSizeHeight => 2f;
    public float MinAdjust => -1f;
    public float MaxAdjust => 1f;
    public float MinBrushSize => 0.1f;
    public float MaxBrushSize => 50f;
    public float MinBrushHeight => 0.1f;
    public float MaxBrushHeight => 50f;
    public int MaxDensity => 1000;
    public int MinDensity => 1;
    public float MaxNormalLimit => 1f;
    public float MinNormalLimit => 0f;
    public int MaxGrassAmountToGenerate => 100000;
    public int MinGrassAmountToGenerate => 0;
    public float MaxGenerationDensity => 10f;
    public float MinGenerationDensity => 0.01f;
    #endregion

    #region Properties
    public LayerMask PaintMask
    {
        get => paintMask;
        set => paintMask = value;
    }

    public LayerMask PaintBlockMask
    {
        get => paintBlockMask;
        set => paintBlockMask = value;
    }

    public float BrushSize
    {
        get => brushSize;
        set => brushSize = Mathf.Clamp(value, MinBrushSize, MaxBrushSize);
    }

    public float BrushHeight
    {
        get => brushHeight;
        set => brushHeight = Mathf.Clamp(value, MinBrushHeight, MaxBrushHeight);
    }

    public float FalloffOuterSpeed
    {
        get => falloffOuterSpeed;
        set => falloffOuterSpeed = Mathf.Clamp01(value);
    }

    public int Density
    {
        get => density;
        set => density = Mathf.Clamp(value, MinDensity, MaxDensity);
    }

    public float NormalLimit
    {
        get => normalLimit;
        set => normalLimit = Mathf.Clamp(value, MinNormalLimit, MaxNormalLimit);
    }

    public Color BrushColor
    {
        get => brushColor;
        set => brushColor = value;
    }

    public float GrassWidth
    {
        get => grassWidth;
        set => grassWidth = Mathf.Clamp(value, MinSizeWidth, MaxSizeWidth);
    }

    public float GrassHeight
    {
        get => grassHeight;
        set => grassHeight = Mathf.Clamp(value, MinSizeHeight, MaxSizeHeight);
    }

    public float AdjustWidth
    {
        get => adjustWidth;
        set => adjustWidth = Mathf.Clamp(value, MinAdjust, MaxAdjust);
    }

    public float AdjustHeight
    {
        get => adjustHeight;
        set => adjustHeight = Mathf.Clamp(value, MinAdjust, MaxAdjust);
    }

    public float AdjustWidthMax
    {
        get => adjustWidthMax;
        set => adjustWidthMax = Mathf.Max(0.01f, value);
    }

    public float AdjustHeightMax
    {
        get => adjustHeightMax;
        set => adjustHeightMax = Mathf.Max(0.01f, value);
    }

    public float ReprojectOffset
    {
        get => reprojectOffset;
        set => reprojectOffset = value;
    }

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

    public int GrassAmountToGenerate
    {
        get => grassAmountToGenerate;
        set => grassAmountToGenerate = Mathf.Clamp(value, MinGrassAmountToGenerate, MaxGrassAmountToGenerate);
    }

    public float GenerationDensity
    {
        get => generationDensity;
        set => generationDensity = Mathf.Clamp(value, MinGenerationDensity, MaxGenerationDensity);
    }

    public float[] LayerBlocking
    {
        get => layerBlocking;
        set => layerBlocking = value;
    }

    public bool[] LayerFading
    {
        get => layerFading;
        set => layerFading = value;
    }

    public VertexColorSetting VertexColorSettings
    {
        get => vertexColorSettings;
        set => vertexColorSettings = value;
    }

    public VertexColorSetting VertexFade
    {
        get => vertexFade;
        set => vertexFade = value;
    }
    #endregion

    public List<string> GetPaintMaskLayerNames()
    {
        List<string> layerNames = new List<string>();

        for (int i = 0; i < 32; i++)
        {
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

    private void OnValidate()
    {
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