using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Settings", menuName = "Utility/GrassSettings")]
public class GrassSettingSO : ScriptableObject
{
    public ComputeShader shaderToUse;

    public Material materialToUse;

    // Blade
    [Header("Blade")] public float randomHeightMin;
    public float RandomHeightMinLimit => 0f;
    public float randomHeightMax = 1f;
    public float RandomHeightMaxLimit => 5f;

    public float bladeRadius = 0.3f;
    public float MinBladeRadius => 0f;
    public float MaxBladeRadius => 2f;

    public float bladeForward = 1f;
    public float MinBladeForward => 0f;
    public float MaxBladeForward => 2f;

    public float bladeCurve = 2f;
    public float MinBladeCurve => 0f;
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
    public int MaxBladesPerVertex => 10;

    public int segmentsPerBlade = 3;
    public int MinSegmentsPerBlade => 1;
    public int MaxSegmentsPerBlade => 4;

    // Interactor
    [Header("Interactor Strength")] public float interactorStrength = 1;

    // Material
    [Header("Material")] public Color topTint = new(1, 1, 1);
    public Color bottomTint = new(0, 0, 1);

    [Header("LOD/ Culling")] public bool drawBounds;
    public float minFadeDistance = 40;
    public float maxFadeDistance = 125;
    public int cullingTreeDepth = 1;

    [Header("Particles")] public GameObject cuttingParticles;

    [Header("Other")] public UnityEngine.Rendering.ShadowCastingMode castShadow;
}