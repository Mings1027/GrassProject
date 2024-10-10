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
            }

            additionalLightColor += LightingLambert(lightColor, light.direction, worldNormal);
        }
    LIGHT_LOOP_END

    return additionalLightColor;
}
