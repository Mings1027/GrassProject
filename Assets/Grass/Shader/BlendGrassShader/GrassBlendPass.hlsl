#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassBlendData.hlsl"
#include "GrassTerrainBlend.hlsl"

half4 SampleTerrainTexture(half3 worldPos)
{
    half2 terrainUV = worldPos.xz - _OrthographicCamPosTerrain.xz;
    terrainUV /= _OrthographicCamSizeTerrain * 2;
    terrainUV += 0.5;

    return SAMPLE_TEXTURE2D_LOD(_TerrainDiffuse, sampler_TerrainDiffuse, terrainUV, 0);
}

half4 BlendWithTerrain(FragmentData input, half verticalFade)
{
    return lerp(input.terrainBlendingColor,
                input.terrainBlendingColor + _TopTint * half4(input.diffuseColor, 1) *
                _AmbientAdjustmentColor, verticalFade);
}

half CalculateVerticalFade(half2 uv)
{
    half blendMul = uv.y * _BlendMult;
    half blendAdd = blendMul + _BlendOff;
    return saturate(blendAdd);
}

void CalculateCutOff(half extraBufferX, half worldPosY)
{
    half cutOffTop = extraBufferX >= worldPosY ? 1 : 0;
    clip(cutOffTop - 0.01);
}

half3 CalculateMainLight(half3 albedo, half3 normalWS, half3 worldPos)
{
    half4 shadowCoord = TransformWorldToShadowCoord(worldPos);
    Light mainLight = GetMainLight(shadowCoord);

    // 노멀 계산 최적화 (half dot product)
    half NdotL = saturate(dot(normalWS, mainLight.direction));

    // ambient 상수화 0.4 값을 줄이면 그림자가 어두워짐
    return albedo * mainLight.color * (mainLight.shadowAttenuation * NdotL + 0.3);
}

half3 CalculateAdditionalLight(half3 worldPos, half3 worldNormal)
{
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;

    float4 positionsCS = TransformWorldToHClip(worldPos);
    float3 ndc = positionsCS.xyz / positionsCS.w;
    float2 screenUV = half2(ndc.x, ndc.y) * 0.5 + 0.5;

    #if UNITY_UV_STARTS_AT_TOP
    screenUV.y = 1.0 - screenUV.y;
    #endif

    inputData.normalizedScreenSpaceUV = screenUV;
    inputData.positionWS = worldPos;

    half4 shadowMask = CalculateShadowMask(inputData);
    half3 diffuseColor = 0;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, worldPos, shadowMask);

        #ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            half NdotL = saturate(dot(worldNormal, light.direction));
            half3 lightColor = light.color * light.distanceAttenuation * NdotL * _AdditionalLightIntensity;
            diffuseColor += lerp(lightColor * _AdditionalShadowColor.rgb, lightColor, light.shadowAttenuation);
        }
    LIGHT_LOOP_END

    return diffuseColor;
}

half4 HighQualityLighting(half3 baseColor, half3 normalWS, half3 worldPos)
{
    half3 mainLighting = CalculateMainLight(baseColor, normalWS, worldPos);
    half3 additionalLight = CalculateAdditionalLight(worldPos, normalWS);
    return half4(mainLighting + additionalLight, 1);
}

FragmentData Vertex(VertexData input)
{
    FragmentData output;

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);
    output.positionCS = TransformObjectToHClip(output.worldPos);
    output.terrainBlendingColor = SampleTerrainTexture(output.worldPos);
    return output;
}

half4 Fragment(FragmentData input) : SV_Target
{
    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half verticalFade = CalculateVerticalFade(input.uv);
    half4 baseColor = lerp(_BottomTint, _TopTint, verticalFade) * half4(input.diffuseColor, 0);
    baseColor += BlendWithTerrain(input, verticalFade);
    return HighQualityLighting(baseColor.rgb, input.normalWS, input.worldPos);
}
