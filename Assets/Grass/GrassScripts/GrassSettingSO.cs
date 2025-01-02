using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Settings", menuName = "Grass/GrassSettings")]
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
    public float maxWidth = 1f;

    public float minHeight = 0.01f;
    public float maxHeight = 3f;

    public float randomHeightMin = 0.01f;
    public float randomHeightMax = 3f;

    // Wind
    [Header("Wind")]
    public float windSpeed = 0.05f;
    public float MinWindSpeed => -10f;
    public float MaxWindSpeed => 10f;

    public float windStrength = 0.3f;
    public float MinWindStrength => 0f;
    public float MaxWindStrength => 2f;

    [Range(0, 360)] [SerializeField]
    private float windDirection = 0f;
    public Vector2 windDirectionVector;
    public float WindDirection
    {
        get => windDirection;
        set
        {
            windDirection = value;
            var angleRad = windDirection * Mathf.Deg2Rad;
            windDirectionVector = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        }
    }

    [Header("Grass")]
    public int bladesPerVertex = 4;
    public int MinBladesPerVertex => 1;
    public int MaxBladesPerVertex => 30;

    public int segmentsPerBlade = 3;
    public int MinSegmentsPerBlade => 1;
    public int MaxSegmentsPerBlade => 3;

    // Interactor
    [Header("Interactor Strength")]
    public float interactorStrength = 1;

    // Material
    [Header("Tinting")]
    public Color topTint = new(1, 1, 1);
    public Color bottomTint = new(0, 0, 1);

    [Header("Blend")]
    public float ambientStrength = 0.3f;
    public float blendMultiply = 1;
    public float blendOffset = 0.2f;
    public Color ambientAdjustmentColor;

    [Header("Shadow")]
    public float shadowDistance = 50;
    public float shadowFadeRange = 10;
    public float shadowBrightness = 0.2f;
    public Color shadowColor;

    [Header("Additional Light")]
    public float additionalLightIntensity = 1;
    public float additionalLightShadowStrength = 0.2f;
    public Color additionalLightShadowColor = Color.black;

    [Header("Specular")]
    public float glossiness = 4.5f;
    public float specularStrength = 1f;
    public float specularHeight = 0.5f;

    [Header("Season Settings")]
    public int maxZoneCount = 1;
    public List<SeasonSettings> seasonSettings;

#if UNITY_EDITOR
    [Header("LOD")]
    public bool drawBounds;
    public bool drawAllBounds;
    public DefaultAsset seasonSettingFolder;
#endif
    public int cullingTreeDepth = 1;

    [Header("LOD Quality Settings")]
    public float minFadeDistance = 40;
    public float maxFadeDistance = 125;

    [Header("Particles")]
    public GameObject cuttingParticles;

    [Header("Other")]
    public UnityEngine.Rendering.ShadowCastingMode castShadow;
}