//
float4 ApplyAdditionalLight(float3 worldPos, float3 worldNormal, float4 finalColor, float additionalLightIntensity,
                            float additionalLightShadowStrength, float4 additionalLightShadowColor)
{
    float3 diffuseColor = 0;
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = worldPos;

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
                if (light.shadowAttenuation == 0)
                {
                    lightColor *= additionalLightShadowStrength * additionalLightShadowColor;
                }
                diffuseColor += LightingLambert(lightColor, light.direction, worldNormal);
            }
        }
    LIGHT_LOOP_END

    finalColor.rgb += diffuseColor;

    return finalColor;
}

float4 AdditionalLightTest(float3 worldPos, float3 worldNormal, float4 finalColor, float additionalLightIntensity)
{
    float3 diffuseColor = 0;
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = worldPos;

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
        if (light.shadowAttenuation > 0)
        {
            float3 attenuatedLightColor = light.color * light.distanceAttenuation * light.shadowAttenuation *
                additionalLightIntensity;
            diffuseColor += LightingLambert(attenuatedLightColor, light.direction, worldNormal);
        }
    }
    LIGHT_LOOP_END

    finalColor.rgb += diffuseColor;

    return finalColor;
}
