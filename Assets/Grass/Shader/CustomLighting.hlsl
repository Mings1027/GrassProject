#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float3 ApplyAdditionalLight(float3 worldPos, float3 worldNormal, float additionalLightIntensity,
                            float additionalLightShadowStrength, float3 shadowColor)
{
    float3 additionalLightColor = 0;
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = worldPos;

    float invertedShadowStrength = 1 - additionalLightShadowStrength;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light;
        #if _MAIN_LIGHT_SHADOWS_CASCADE || _MAIN_LIGHT_SHADOWS
    half4 shadowMask = CalculateShadowMask(inputData);
    light = GetAdditionalLight(lightIndex, worldPos, shadowMask);
        #else
        light = GetAdditionalLight(lightIndex, worldPos);
        #endif
        #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            float3 lightColor = light.color * light.distanceAttenuation * additionalLightIntensity;

            if (light.shadowAttenuation >= 0)
            {
                if (light.shadowAttenuation == 0)
                {
                    float3 shadowedColor = lerp(shadowColor, lightColor, light.shadowAttenuation);
                    lightColor = lerp(shadowedColor, lightColor, invertedShadowStrength);
                }
                additionalLightColor += LightingLambert(lightColor, light.direction, worldNormal);
            }
        }
    LIGHT_LOOP_END

    return additionalLightColor;
}

//  위 함수사용하면 additional light 경계부분에 계단현상 생기는것을 확인함
//  현재 아래함수 사용중인데 additional light 그림자 영역의 색이 정확하게 shadowColor만 맺히는게 아닌듯한
//  grass색이 초록계열, additional light컬러가 빨강, shadowColor가 파랑이면 그림자영역색이 초록으로 보임 
float3 ApplyAdditionalLightTest(float3 worldPos, float3 worldNormal, float additionalLightIntensity,
                                float additionalLightShadowStrength, float3 shadowColor)
{
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = worldPos;

    float3 diffuseColor = 0;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light;
        #if _MAIN_LIGHT_SHADOWS_CASCADE || _MAIN_LIGHT_SHADOWS
    half4 shadowMask = CalculateShadowMask(inputData);
    light = GetAdditionalLight(lightIndex, worldPos, shadowMask);
        #else
        light = GetAdditionalLight(lightIndex, worldPos);
        #endif
        #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            if (light.shadowAttenuation >= 0)
            {
                float3 lightColor = light.color * light.distanceAttenuation * additionalLightIntensity;
                if (light.shadowAttenuation < 1) // If there is shadow
                {
                    float shadowStrength = lerp(0, 1 - light.shadowAttenuation, additionalLightShadowStrength);
                    
                    lightColor = lerp(lightColor, lightColor * shadowColor, shadowStrength);
                }
                diffuseColor += LightingLambert(lightColor, light.direction, worldNormal);
            }
        }
    LIGHT_LOOP_END
    return diffuseColor;
}
