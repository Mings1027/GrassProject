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

    float distanceFromCamera = length(_WorldSpaceCameraPos - worldPos);

    float shadowFade = saturate((distanceFromCamera - _ShadowDistance) / _ShadowFadeRange);
    float shadowAtten = lerp(mainLight.shadowAttenuation, 1, shadowFade);

    half NdotL = saturate(dot(normalWS, mainLight.direction));

    // ambient 상수화 0.4 값을 줄이면 그림자가 어두워짐
    return albedo * mainLight.color * (shadowAtten * NdotL + 0.3);
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
            half shadowAttenuation = lerp(1, light.shadowAttenuation, _AdditionalLightShadowStrength);
            diffuseColor += lerp(lightColor * _AdditionalShadowColor.rgb, lightColor, shadowAttenuation);
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

    return output;
}

half4 Fragment(FragmentData input) : SV_Target
{
    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half verticalFade = CalculateVerticalFade(input.uv);
    half4 baseColor = lerp(_BottomTint, _TopTint, verticalFade) * half4(input.diffuseColor, 0);

    return HighQualityLighting(baseColor.rgb, input.normalWS, input.worldPos);
}
