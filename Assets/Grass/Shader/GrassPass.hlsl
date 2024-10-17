#include "GrassInput.hlsl"
#include "Grass.hlsl"
#include "CustomLighting.hlsl"

float4 SampleTerrainTexture(float3 worldPos)
{
    float2 terrainUV = worldPos.xz - _OrthographicCamPosTerrain.xz;
    terrainUV /= _OrthographicCamSizeTerrain * 2;
    terrainUV += 0.5;

    return SAMPLE_TEXTURE2D_LOD(_TerrainDiffuse, sampler_TerrainDiffuse, terrainUV, 0);
}

float CalculateVerticalFade(float2 uv)
{
    float blendMul = uv.y * _BlendMult;
    float blendAdd = blendMul + _BlendOff;
    return saturate(blendAdd);
}

float4 CalculateBaseColor(float3 diffuseColor, float verticalFade)
{
    return float4(diffuseColor, 0) * lerp(_BottomTint, _TopTint, verticalFade);
}

float4 BlendWithTerrain(Varyings input, float verticalFade)
{
    return lerp(input.terrainBlendingColor,
                input.terrainBlendingColor + _TopTint * float4(input.diffuseColor, 1) *
                _AmbientAdjustmentColor, verticalFade);
}

void CalculateCutOff(float extraBufferX, float worldPosY)
{
    float cutOffTop = extraBufferX >= worldPosY ? 1 : 0;
    clip(cutOffTop - 0.01);
}

float3 CalculateMainLight(float3 albedo, float3 normalWS, float3 worldPos)
{
    float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
    Light mainLight = GetMainLight(shadowCoord);

    float3 lightDir = mainLight.direction;
    float3 lightColor = mainLight.color;
    float lightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

    float NdotL = saturate(dot(normalWS, lightDir));
    float3 radiance = lightColor * lightAttenuation * NdotL;

    float3 ambient = SampleSH(normalWS) * albedo;
    float3 diffuse = albedo * radiance;

    return ambient + diffuse;
}

float3 AdjustSaturation(float3 color, float saturation)
{
    float grey = dot(color, float3(0.2126, 0.7152, 0.0722));
    return lerp(grey.xxx, color, saturation);
}

float3 CustomNeutralToneMapping(float3 color, float exposure)
{
    color *= exposure;
    return NeutralTonemap(color);
}

Varyings vert(Attributes input)
{
    Varyings output;

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);
    output.positionCS = TransformObjectToHClip(output.worldPos);

    #if defined(BLEND)
    output.terrainBlendingColor = SampleTerrainTexture(output.worldPos);
    #endif
    return output;
}

float4 frag(Varyings input) : SV_Target
{
    float verticalFade = CalculateVerticalFade(input.uv);

    float4 baseColor = CalculateBaseColor(input.diffuseColor, verticalFade);

    #if defined(BLEND)
    baseColor = BlendWithTerrain(input, verticalFade);
    #endif

    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    float3 mainLight = CalculateMainLight(baseColor.rgb, input.normalWS, input.worldPos);

    float3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS, _AdditionalLightIntensity,
                                                           _AdditionalLightShadowStrength,
                                                           _AdditionalLightShadowColor.rgb);

    float3 finalColor = mainLight + additionalLight;


    finalColor *= _OverallIntensity;
    finalColor = CustomNeutralToneMapping(finalColor, _Exposure);
    finalColor = AdjustSaturation(finalColor, _Saturation);

    return float4(finalColor, baseColor.a);
}
