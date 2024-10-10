#include "GrassInput.hlsl"
#include "Grass.hlsl"
#include "CustomLighting.hlsl"

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
    float2 uv = input.worldPos.xz - _OrthographicCamPosTerrain.xz;
    uv /= _OrthographicCamSizeTerrain * 2;
    uv += 0.5;

    float4 terrainForBlending = SAMPLE_TEXTURE2D(_TerrainDiffuse, sampler_TerrainDiffuse, uv);
    return lerp(terrainForBlending,
                terrainForBlending + _TopTint * float4(input.diffuseColor, 1) *
                _AmbientAdjustmentColor, verticalFade);
}

void CalculateCutOff(float extraBufferX, float worldPosY)
{
    float cutOffTop = extraBufferX >= worldPosY ? 1 : 0;
    clip(cutOffTop - 0.01);
}

float4 ApplyMainLightShadow(Varyings input, float4 finalColor)
{
    float shadow = 0;
    #if _MAIN_LIGHT_SHADOWS_CASCADE || _MAIN_LIGHT_SHADOWS
                    half4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                    Light mainLight = GetMainLight(shadowCoord);
    #else
    Light mainLight = GetMainLight();
    #endif
    shadow = mainLight.shadowAttenuation;

    if (shadow <= 0)
    {
        finalColor.rgb *= lerp(finalColor.rgb, _ShadowColor.rgb, _ShadowStrength);
    }
    return finalColor;
}

Varyings vert(Attributes input)
{
    Varyings output;
    output.worldPos = float3(0, 0, 0);
    output.normalWS = float3(0, 0, 0);
    output.uv = input.uv;
    output.diffuseColor = float3(1, 1, 1);
    output.extraBuffer = float4(0, 0, 0, 0);

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);
    output.positionCS = TransformObjectToHClip(output.worldPos);

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

    float4 mainShadowColor = ApplyMainLightShadow(input, baseColor);
    float3 additionalLightColor = ApplyAdditionalLight(input.worldPos, input.normalWS, _AdditionalLightIntensity,
                                                           _AdditionalLightShadowStrength, _AdditionalLightColor.rgb);

    float3 finalColor = mainShadowColor.rgb + additionalLightColor;

    return float4(finalColor, baseColor.a);
}
