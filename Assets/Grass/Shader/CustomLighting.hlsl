#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float3 CalculateAdditionalLight(float3 worldPos, float3 worldNormal, float lightIntensity,
                                float shadowStrength, float3 shadowColor)
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
            /*===================================================================================================
            *
            *  이 아래 코드 잘되긴함 근데 왜 되는지 모르겠음 괘씸함
            *
             ===================================================================================================*/
            float3 lightColor = light.color * light.distanceAttenuation * lightIntensity;
            float3 lightContribution = LightingLambert(lightColor, light.direction, worldNormal);

            float shadowAttenuation = light.shadowAttenuation;
            float calculatedShadowStrength = shadowStrength * (1 - shadowAttenuation);

            float3 shadowedColor = lerp(lightContribution,
                                        shadowColor * dot(lightContribution, float3(0.299, 0.587, 0.114)),
                                        calculatedShadowStrength);

            diffuseColor += shadowedColor;

        
        }
    LIGHT_LOOP_END
    return diffuseColor;
}
