#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassInput.hlsl"
#include "Grass.hlsl"

half4 SampleTerrainTexture(half3 worldPos)
{
    half2 terrainUV = worldPos.xz - _OrthographicCamPosTerrain.xz;
    terrainUV /= _OrthographicCamSizeTerrain * 2;
    terrainUV += 0.5;

    return SAMPLE_TEXTURE2D_LOD(_TerrainDiffuse, sampler_TerrainDiffuse, terrainUV, 0);
}

half CalculateVerticalFade(half2 uv)
{
    half blendMul = uv.y * _BlendMult;
    half blendAdd = blendMul + _BlendOff;
    return saturate(blendAdd);
}

half4 CalculateBaseColor(half3 diffuseColor, half verticalFade)
{
    return half4(diffuseColor, 0) * lerp(_BottomTint, _TopTint, verticalFade);
}

half4 BlendWithTerrain(FragmentData input, half verticalFade)
{
    return lerp(input.terrainBlendingColor,
                input.terrainBlendingColor + _TopTint * half4(input.diffuseColor, 1) *
                _AmbientAdjustmentColor, verticalFade);
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

half3 CalculateRimLight(half3 normalWS, half3 viewDirWS)
{
    half3 lightDir = GetMainLight().direction;
    half t = smoothstep(0, 0.1, lightDir.y);
    half rimDot = 1 - dot(normalWS, viewDirWS);
    half rimPower = pow(rimDot, _RimPower);
    half rimIntensity = rimPower * _RimIntensity;
    half3 rimColor = rimIntensity * _RimColor * t;
    return rimColor;
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

FragmentData Vertex(VertexData input)
{
    FragmentData output;

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);
    output.positionCS = TransformObjectToHClip(output.worldPos);

    #if defined(BLEND)
    output.terrainBlendingColor = SampleTerrainTexture(output.worldPos);
    #endif
    return output;
}

half4 Fragment(FragmentData input) : SV_Target
{
    half verticalFade = CalculateVerticalFade(input.uv);

    half4 baseColor = CalculateBaseColor(input.diffuseColor, verticalFade);

    #if defined(BLEND)
    baseColor = BlendWithTerrain(input, verticalFade);
    #endif

    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half3 mainLighting = CalculateMainLight(baseColor.rgb, input.normalWS, input.worldPos);
    half3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS);

    half3 viewDirWS = normalize(_WorldSpaceCameraPos - input.worldPos);

    half3 rimLight = CalculateRimLight(input.normalWS, viewDirWS);

    half3 finalColor = mainLighting + additionalLight + rimLight;

    finalColor *= _OverallIntensity;
    finalColor = CustomNeutralToneMapping(finalColor, _Exposure);
    finalColor = AdjustSaturation(finalColor, _Saturation);

    return half4(finalColor, baseColor.a);
}
