using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Settings", menuName = "Utility/GrassSettings")]
public class SoGrassSettings : ScriptableObject
{
    public ComputeShader shaderToUse;

    public Material materialToUse;

    // Blade
    [Header("Blade")]
    [Range(0, 5)] public float grassRandomHeightMin = 0.0f;
    [Range(0, 5)] public float grassRandomHeightMax = 0.0f;
    [Range(0, 1)] public float bladeRadius = 0.2f;
    [Range(0, 1)] public float bladeForwardAmount = 0.38f;
    [Range(1, 5)] public float bladeCurveAmount = 2;
    [Range(0, 1)] public float bottomWidth = 0.1f;

    public float minWidth = 0.01f;
    public float minHeight = 0.01f;
    public float maxWidth = 1f;
    public float maxHeight = 1f;

    // Wind
    [Header("Wind")]
    public float windSpeed = 10;
    public float windStrength = 0.05f;

    //Grass
    [Header("Grass")]
    [Range(1, 8)] public int allowedBladesPerVertex = 4;
    [Range(1, 5)] public int allowedSegmentsPerBlade = 4;

    // Interactor
    [Header("Interactor Strength")]
    public float affectStrength = 1;

    // Material
    [Header("Material")]
    public Color topTint = new(1, 1, 1);
    public Color bottomTint = new(0, 0, 1);

    [Header("LOD/ Culling")]
    public bool drawBounds;
    public float minFadeDistance = 40;
    public float maxDrawDistance = 125;
    public int cullingTreeDepth = 1;

    [Header("Particles")]
    public GameObject cuttingParticles;

    [Header("Other")]
    public UnityEngine.Rendering.ShadowCastingMode castShadow;
}