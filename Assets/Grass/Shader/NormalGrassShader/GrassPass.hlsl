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

half3 CalculateMainLight(half3 albedo, FragmentData input)
{
    half4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
    Light mainLight = GetMainLight(shadowCoord);

    float distanceFromCamera = length(_WorldSpaceCameraPos - input.worldPos);
    float shadowFade = saturate((distanceFromCamera - _ShadowDistance) / _ShadowFadeRange);
    float shadowAtten = lerp(mainLight.shadowAttenuation, 1, shadowFade);

    // Diffuse
    half NdotL = saturate(dot(input.normalWS, mainLight.direction));
    half3 diffuse = albedo * (mainLight.color * shadowAtten * NdotL);

    // Specular
    half3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
    half3 halfVector = normalize(mainLight.direction + viewDir);
    half NdotH = saturate(dot(input.normalWS, halfVector));
    half specularPower = exp2(_Glossiness);
    half heightFactor = saturate((input.uv.y - _SpecularHeight) / (1 - _SpecularHeight));
    half3 specular = mainLight.color * _SpecularStrength * pow(NdotH, specularPower) * shadowAtten * heightFactor;

    return diffuse + specular + albedo * _AmbientStrength;
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

    half3 mainLighting = CalculateMainLight(baseColor.rgb, input);
    half3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS);
    return half4(mainLighting + additionalLight, 1);
}
