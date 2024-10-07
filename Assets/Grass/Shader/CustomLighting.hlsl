//
float4 AdditionalLights(float3 worldPos, float3 worldNormal, float4 finalColor, float4 shadowColor,
                            float additionalLightIntensity, float mainLightShadow)
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
            else if (mainLightShadow > 0)
            {
                finalColor.rgb *= shadowColor.rgb;
            }
        }
    LIGHT_LOOP_END

    finalColor.rgb += diffuseColor;

    return finalColor;
}
