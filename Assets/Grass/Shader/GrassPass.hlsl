#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassInput.hlsl"
#include "Grass.hlsl"

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

    half3 lightDir = mainLight.direction;
    half3 lightColor = mainLight.color;
    half lightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

    half NdotL = saturate(dot(normalWS, lightDir));
    half3 radiance = lightColor * lightAttenuation * NdotL;

    half3 ambient = SampleSH(normalWS) * albedo;
    half3 diffuse = albedo * radiance;

    return ambient + diffuse;
}

half3 CalculateAdditionalLight(float3 worldPos, float3 worldNormal)
{
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;

    // shadowmask를 위해 아래 과정이 필요
    float4 positionsCS = TransformWorldToHClip(worldPos); // 월드 좌표 -> 클립 공간 변환
    float3 ndc = positionsCS.xyz / positionsCS.w; // 클립 공간에서 NDC로 변환
    float2 screenUV = float2(ndc.x, ndc.y) * 0.5 + 0.5; // NDC를 [0,1] 범위의 스크린 UV로 변환

    // DirectX/OpenGL 차이 보정
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
            // 기본 라이팅 계산
            half3 lightColor = light.color * light.distanceAttenuation * _AdditionalLightIntensity;
            half3 lighting = LightingLambert(lightColor, light.direction, worldNormal);

            // 그림자 계산
            half shadowAtten = light.shadowAttenuation;
            half calculatedShadowStrength = _AdditionalLightShadowStrength * (1 - shadowAtten);

            half3 shadowedColor = lerp(lighting, _AdditionalShadowColor.rgb * Luminance(lighting),
                                       calculatedShadowStrength);

            diffuseColor += shadowedColor;
        }
    LIGHT_LOOP_END

    return diffuseColor;
}

half3 AdjustSaturation(half3 color, half saturation)
{
    half grey = dot(color, half3(0.2126, 0.7152, 0.0722));
    return lerp(grey.xxx, color, saturation);
}

half3 CustomNeutralToneMapping(half3 color, half exposure)
{
    color *= exposure;
    return NeutralTonemap(color);
}

half4 CalculateZoneTint(half3 diffuseColor, half verticalFade, float3 worldPos)
{
    half3 zoneMin = _ZonePosData - _ZoneScaleData * 0.5;
    half3 zoneMax = _ZonePosData + _ZoneScaleData * 0.5f;

    half inZone = worldPos.x > zoneMin.x && worldPos.x < zoneMax.x &&
                  worldPos.y > zoneMin.y && worldPos.y < zoneMax.y &&
                  worldPos.z > zoneMin.z && worldPos.z < zoneMax.z
                      ? 1
                      : 0;

    half4 normalTint = lerp(_BottomTint, _TopTint, verticalFade);
    half4 zoneTint = lerp(_BottomTint, _SeasonTint, verticalFade);
    return half4(diffuseColor, 0) * lerp(normalTint, zoneTint, inZone);
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
    half verticalFade = CalculateVerticalFade(input.uv);

    half4 baseColor = CalculateZoneTint(input.diffuseColor, verticalFade, input.worldPos);

    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half3 mainLighting = CalculateMainLight(baseColor.rgb, input.normalWS, input.worldPos);
    half3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS);

    half3 finalColor = mainLighting + additionalLight;

    finalColor *= _OverallIntensity;
    finalColor = CustomNeutralToneMapping(finalColor, _Exposure);
    finalColor = AdjustSaturation(finalColor, _Saturation);

    return half4(finalColor, baseColor.a);
}
