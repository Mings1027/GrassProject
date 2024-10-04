
void AdditionalLights(float3 WorldPos, float3 WorldNormal, out float3 Diffuse)
{
    float3 diffuseColor = 0;

    uint pixelLightCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, WorldPos);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            // Blinn-Phong
            float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
            diffuseColor += LightingLambert(attenuatedLightColor, light.direction, WorldNormal);
        }
    }
    #endif

    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPos;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPos);
        #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            // Blinn-Phong
            float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
            diffuseColor += LightingLambert(attenuatedLightColor, light.direction, WorldNormal);
        }
    LIGHT_LOOP_END

    Diffuse = diffuseColor;
}

float GetAdditionalLightShadow(float3 worldPos)
{
    float shadow = 1.0;
    uint pixelLightCount = GetAdditionalLightsCount();

    InputData inputData;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(worldPos));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = worldPos;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, worldPos);
    shadow += light.shadowAttenuation;
    LIGHT_LOOP_END

    return shadow;
}