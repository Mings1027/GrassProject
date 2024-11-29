using Grass.GrassScripts;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Settings", menuName = "Utility/GrassSettings")]
public class GrassSettingSO : ScriptableObject
{
    public ComputeShader shaderToUse;

    public Material materialToUse;

    // Blade

    public float bladeRadius = 0.3f;
    public float MinBladeRadius => 0f;
    public float MaxBladeRadius => 2f;

    public float bladeForward = 1f;
    public float MinBladeForward => 0f;
    public float MaxBladeForward => 2f;

    public float bladeCurve = 2f;
    public float MinBladeCurve => 0.01f;
    public float MaxBladeCurve => 2f;

    public float bottomWidth = 0.2f;
    public float MinBottomWidth => 0f;
    public float MaxBottomWidth => 2f;

    public float minWidth = 0.01f;
    public float MinWidthLimit => 0.01f;
    public float maxWidth = 1f;
    public float MaxWidthLimit => 3f;

    public float minHeight = 0.01f;
    public float MinHeightLimit => 0.01f;
    public float maxHeight = 3f;
    public float MaxHeightLimit => 3f;

    public float randomHeightMin = 0.01f;
    public float randomHeightMax = 3f;
    // Wind
    [Header("Wind")] public float windSpeed = 0.05f;
    public float MinWindSpeed => -10f;
    public float MaxWindSpeed => 10f;

    public float windStrength = 0.3f;
    public float MinWindStrength => 0f;
    public float MaxWindStrength => 2f;

    //Grass
    [Header("Grass")] public int bladesPerVertex = 4;
    public int MinBladesPerVertex => 1;
    public int MaxBladesPerVertex => 30;

    public int segmentsPerBlade = 3;
    public int MinSegmentsPerBlade => 1;
    public int MaxSegmentsPerBlade => 3;

    // Interactor
    [Header("Interactor Strength")] public float interactorStrength = 1;

    // Material
    [Header("Material")] public Color topTint = new(1, 1, 1);
    public Color bottomTint = new(0, 0, 1);

    [Header("Season Settings")]
    public SeasonSettings winterSettings = new();
    public SeasonSettings springSettings = new();
    public SeasonSettings summerSettings = new();
    public SeasonSettings autumnSettings = new();

    [Header("Season Range")] public float seasonRangeMin = 0f;
    public float seasonRangeMax = 4f;

    [Header("LOD/ Culling")] public bool drawBounds;
    public float minFadeDistance = 40;
    public float maxFadeDistance = 125;
    public int cullingTreeDepth = 1;

    [Header("Particles")] public GameObject cuttingParticles;

    [Header("Other")] public UnityEngine.Rendering.ShadowCastingMode castShadow;
}