#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassInput.hlsl"
#include "Grass.hlsl"

half CalculateVerticalFade(half2 uv)
{
    half blendMul = uv.y * _BlendMult;
    half blendAdd = blendMul + _BlendOff;
    return saturate(blendAdd);
}

half4 CalculateZoneTint(half3 diffuseColor, half verticalFade, float3 worldPos)
{
    for (int i = _ZoneCount - 1; i >= 0; i--)
    {
        if (_ZonePositions[i].w < 0.5) continue;

        half3 delta = abs(worldPos - _ZonePositions[i].xyz);
        if (all(delta <= _ZoneScales[i].xyz * 0.5))
        {
            half4 baseTint = lerp(half4(0, 0, 0, 0), _ZoneColors[i], verticalFade);
            return baseTint * half4(1, 1, 1, 1);
        }
    }

    half4 baseTint = lerp(_BottomTint, _TopTint, verticalFade);
    return baseTint * half4(diffuseColor, 0);
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

half3 MediumAdditionalLight(float3 worldPos, float3 worldNormal)
{
    uint pixelLightCount = min(GetAdditionalLightsCount(), 2);
    half3 diffuseColor = 0;

    float4 positionsCS = TransformWorldToHClip(worldPos);
    float3 ndc = positionsCS.xyz / positionsCS.w;
    float2 screenUV = float2(ndc.x, ndc.y) * 0.5 + 0.5;

    #if UNITY_UV_STARTS_AT_TOP
    screenUV.y = 1.0 - screenUV.y;
    #endif

    InputData inputData;
    inputData.positionWS = worldPos;
    inputData.normalizedScreenSpaceUV = screenUV;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, worldPos);
        half NdotL = saturate(dot(worldNormal, light.direction));
        diffuseColor += light.color * light.distanceAttenuation * NdotL * _AdditionalLightIntensity;
    LIGHT_LOOP_END

    return diffuseColor;
}

half3 HighAdditionalLight(float3 worldPos, float3 worldNormal)
{
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;

    float4 positionsCS = TransformWorldToHClip(worldPos);
    float3 ndc = positionsCS.xyz / positionsCS.w;
    float2 screenUV = float2(ndc.x, ndc.y) * 0.5 + 0.5;

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
            half3 lightColor = light.color * light.distanceAttenuation * _AdditionalLightIntensity;
            half3 lighting = LightingLambert(lightColor, light.direction, worldNormal);

            half shadowAtten = light.shadowAttenuation;
            half calculatedShadowStrength = _AdditionalLightShadowStrength * (1 - shadowAtten);

            half3 shadowedColor = lerp(lighting, _AdditionalShadowColor.rgb * Luminance(lighting),
                                       calculatedShadowStrength);

            diffuseColor += shadowedColor;
        }
    LIGHT_LOOP_END

    return diffuseColor;
}

half4 LowQualityLighting(half3 diffuseColor, half3 normalWS, half3 worldPos)
{
    half3 mainLighting = CalculateMainLight(diffuseColor, normalWS, worldPos);
    return half4(mainLighting, 1);
}

half4 MediumQualityLighting(half2 uv, half3 diffuseColor, half3 normalWS, half3 worldPos)
{
    half verticalFade = CalculateVerticalFade(uv);
    half4 baseColor = CalculateZoneTint(diffuseColor, verticalFade, worldPos);
    half3 mainLighting = CalculateMainLight(baseColor.rgb, normalWS, worldPos);
    half3 additionalLight = MediumAdditionalLight(worldPos, normalWS);
    return half4(mainLighting + additionalLight, baseColor.a);
}

half4 HighQualityLighting(half2 uv, half3 diffuseColor, half3 normalWS, half3 worldPos)
{
    half verticalFade = CalculateVerticalFade(uv);
    half4 baseColor = CalculateZoneTint(diffuseColor, verticalFade, worldPos);
    half3 mainLighting = CalculateMainLight(baseColor.rgb, normalWS, worldPos);
    half3 additionalLight = HighAdditionalLight(worldPos, normalWS);
    return half4(mainLighting + additionalLight, baseColor.a);
}

FragmentData Vertex(VertexData input)
{
    FragmentData output;

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);
    output.positionCS = TransformObjectToHClip(output.worldPos);

    return output;
}

half4 Fragment(FragmentData input) : SV_Target
{
    // Early-Z 최적화를 위해 가장 먼저 clip 수행
    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    // distanceFade는 compute shader에서 전달받은 값 사용 (extraBuffer.y)
    half distanceFade = input.extraBuffer.y;

    // 멀리 있는 픽셀은 간단한 계산만 수행
    if (distanceFade < _LowQualityDistance)
    {
        return LowQualityLighting(input.diffuseColor, input.normalWS, input.worldPos);
    }

    // 중간 거리는 메인 라이트만 계산
    if (distanceFade < _MediumQualityDistance)
    {
        return MediumQualityLighting(input.uv, input.diffuseColor, input.normalWS, input.worldPos);
    }

    return HighQualityLighting(input.uv, input.diffuseColor, input.normalWS, input.worldPos);
}
