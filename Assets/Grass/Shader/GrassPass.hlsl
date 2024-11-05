#include "GrassInput.hlsl"
#include "Grass.hlsl"
#include "CustomLighting.hlsl"

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

    half3 mainLight = CalculateMainLight(baseColor.rgb, input.normalWS, input.worldPos);

    half3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS, _AdditionalLightIntensity,
                                                           _AdditionalLightShadowStrength,
                                                           _AdditionalLightShadowColor.rgb);

    half3 finalColor = mainLight + additionalLight;


    finalColor *= _OverallIntensity;
    finalColor = CustomNeutralToneMapping(finalColor, _Exposure);
    finalColor = AdjustSaturation(finalColor, _Saturation);

    return half4(finalColor, baseColor.a);
}
