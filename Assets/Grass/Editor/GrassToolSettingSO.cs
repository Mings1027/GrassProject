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

    [Header("Terrain Layer Settings")]
    [SerializeField] private List<bool> layerEnabled = new List<bool>();
    [SerializeField] private List<float> heightFading = new List<float>();

    [Header("Vertex Color Settings")]
    [SerializeField] private VertexColorSetting vertexColorSettings;
    [SerializeField] private VertexColorSetting vertexFade;

    [Header("Grass Size Settings")]
    [SerializeField] private float grassWidth = 0.1f;
    [SerializeField] private float grassHeight = 1f;
    [SerializeField] private float adjustWidth = 0f;
    [SerializeField] private float adjustHeight = 0f;
    [SerializeField] private float adjustWidthMax = 2f;
    [SerializeField] private float adjustHeightMax = 2f;

    [Header("Color Settings")]
    [SerializeField] private float rangeR;
    [SerializeField] private float rangeG;
    [SerializeField] private float rangeB;
    [SerializeField] private Color brushColor = Color.white;

    [Header("Paint Layer Settings")]
    [SerializeField] private LayerMask paintMask = 1;
    [SerializeField] private LayerMask paintBlockMask = 0;

    [Header("Brush Settings")]
    [SerializeField] private float brushSize = 4f;
    [SerializeField] private float brushTransitionSpeed = 0.5f;
    [SerializeField] private float normalLimit = 1f;
    public bool allowUndersideGrass;
    [SerializeField] private int density = 1;
    [SerializeField] private float grassSpacing = 1f;
    [SerializeField] private float brushHeight = 1f;

    [Header("Generation Settings")]
    [SerializeField] private int generateGrassCount = 100000;

#if UNITY_EDITOR
    [Header("Input Settings")]
    public MouseButton grassMouseButton;
    public KeyType brushSizeShortcut;
    public KeyType brushHeightShortcut;
#endif
    // Constants
    public float MinSizeWidth => 0.01f;
    public float MaxSizeWidth => 2f;
    public float MinSizeHeight => 0.01f;
    public float MaxSizeHeight => 3f;
    public float MinAdjust => -1f;
    public float MaxAdjust => 1f;
    public float MinBrushSize => 0.1f;
    public float MaxBrushSize => 1000f;
    public int MaxDensity => 1000;
    public int MinDensity => 1;
    public float MaxNormalLimit => 1f;
    public float MinNormalLimit => 0f;
    public int MaxGrassAmountToGenerate => 1000000;
    public int MinGrassAmountToGenerate => 0;

    // Properties
    public List<bool> LayerEnabled
    {
        get => layerEnabled;
        set => layerEnabled = value;
    }
    public List<float> HeightFading
    {
        get => heightFading;
        set => heightFading = value;
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
    public Color BrushColor
    {
        get => brushColor;
        set => brushColor = value;
    }
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
        set => brushHeight = Mathf.Clamp(value, MinBrushSize, MaxBrushSize);
    }
    public float BrushTransitionSpeed
    {
        get => brushTransitionSpeed;
        set => brushTransitionSpeed = Mathf.Clamp01(value);
    }
    public int Density
    {
        get => density;
        set => density = Mathf.Clamp(value, MinDensity, MaxDensity);
    }
    public float GrassSpacing
    {
        get => grassSpacing;
        set => grassSpacing = Mathf.Clamp(value, 0.1f, 1f);
    }
    public float NormalLimit
    {
        get => normalLimit;
        set => normalLimit = Mathf.Clamp(value, MinNormalLimit, MaxNormalLimit);
    }
    public int GenerateGrassCount
    {
        get => generateGrassCount;
        set => generateGrassCount = Mathf.Clamp(value, MinGrassAmountToGenerate, MaxGrassAmountToGenerate);
    }

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
        BrushTransitionSpeed = brushTransitionSpeed;
        Density = density;
        NormalLimit = normalLimit;
        GenerateGrassCount = generateGrassCount;
        RangeR = rangeR;
        RangeG = rangeG;
        RangeB = rangeB;
    }

    public void CreateNewLayers(int layerCount)
    {
        Debug.Log($"Setting up initial tool settings for {layerCount} layers");
        layerEnabled = new List<bool>();
        heightFading = new List<float>();

        for (int i = 0; i < layerCount; i++)
        {
            layerEnabled.Add(true);
            heightFading.Add(1f);
        }
    }

    public void UpdateLayerCount(int newLayerCount)
    {
        // Add new layers if needed
        while (layerEnabled.Count < newLayerCount)
        {
            layerEnabled.Add(true);
            heightFading.Add(1f);
        }

        // Remove excess layers if needed
        if (layerEnabled.Count > newLayerCount)
        {
            layerEnabled.RemoveRange(newLayerCount, layerEnabled.Count - newLayerCount);
            heightFading.RemoveRange(newLayerCount, heightFading.Count - newLayerCount);
        }
    }
}