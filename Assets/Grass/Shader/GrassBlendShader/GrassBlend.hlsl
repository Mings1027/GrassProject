#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct VertexData
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    uint vertexID : SV_VertexID;
};

struct FragmentData
{
    float4 positionCS : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float3 diffuseColor : TEXCOORD3;
    float4 extraBuffer : TEXCOORD4;
};

#define MAX_ZONES 9

CBUFFER_START(UnityPerMaterial)
    // Additional Light
    float _AdditionalLightIntensity;
    float _AdditionalLightShadowStrength;
    float4 _AdditionalShadowColor;

    // Blend
    float _BlendMult, _BlendOff;

    float4 _TopTint;
    float4 _BottomTint;

    // Zone Settings
    float4 _ZonePositions[MAX_ZONES]; // w 컴포넌트는 활성상태 (1 = 활성, 0 = 비활성)
    float4 _ZoneScales[MAX_ZONES];
    float4 _ZoneColors[MAX_ZONES];
    int _ZoneCount;

    // LOD
    float _LowQualityDistance;
    float _MediumQualityDistance;
CBUFFER_END

// This describes a vertex on the generated mesh
struct DrawVertex
{
    float3 positionWS; // The position in world space
    float2 uv;
};

// A triangle on the generated mesh
struct DrawTriangle
{
    float3 normalOS;
    float3 diffuseColor;
    float4 extraBuffer;
    DrawVertex vertices[3]; // The three points on the triangle
};

// A buffer containing the generated mesh
StructuredBuffer<DrawTriangle> _DrawTriangles; // 읽는 역할


//get the data from the compute shader
void GetComputeData_float(uint vertexID, out float3 worldPos, out float3 normal, out float2 uv, out float3 col,
                          out float4 extraBuffer)
{
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex input = tri.vertices[vertexID % 3];
    worldPos = input.positionWS;
    normal = tri.normalOS;
    uv = input.uv;
    col = tri.diffuseColor;

    // for some reason doing this with a comparison node results in a glitchy alpha, so we're doing it here, if your grass is at a point higher than 99999 Y position then you should make this even higher or find a different solution
    if (tri.extraBuffer.x == -1)
    {
        extraBuffer = float4(99999, tri.extraBuffer.y, tri.extraBuffer.z, tri.extraBuffer.w);
    }
    else
    {
        extraBuffer = tri.extraBuffer;
    }
}

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

    // 노멀 계산 최적화 (half dot product)
    half NdotL = saturate(dot(normalWS, mainLight.direction));

    // ambient 상수화 0.4 값을 줄이면 그림자가 어두워짐
    return albedo * mainLight.color * (mainLight.shadowAttenuation * NdotL + 0.3);
}

half3 MediumAdditionalLight(float3 worldPos, float3 worldNormal)
{
    uint pixelLightCount = min(GetAdditionalLightsCount(), 2);
    half3 diffuseColor = 0;

    float4 positionsCS = TransformWorldToHClip(worldPos);
    float3 ndc = positionsCS.xyz / positionsCS.w;
    float2 screenUV = float2(ndc.x, ndc.y) * 0.5 + 0.5;

    #if UNITY_UV_STARTS_AT_TOP
    screenUV.y = 1.0 - screenUV.y;
    #endif

    InputData inputData;
    inputData.positionWS = worldPos;
    inputData.normalizedScreenSpaceUV = screenUV;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, worldPos);
        half NdotL = saturate(dot(worldNormal, light.direction));
        diffuseColor += light.color * light.distanceAttenuation * NdotL * _AdditionalLightIntensity;
    LIGHT_LOOP_END

    return diffuseColor;
}

half3 HighAdditionalLight(float3 worldPos, float3 worldNormal)
{
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData;

    float4 positionsCS = TransformWorldToHClip(worldPos);
    float3 ndc = positionsCS.xyz / positionsCS.w;
    float2 screenUV = float2(ndc.x, ndc.y) * 0.5 + 0.5;

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
            half3 lightColor = light.color * light.distanceAttenuation * _AdditionalLightIntensity;
            half3 lighting = LightingLambert(lightColor, light.direction, worldNormal);

            half shadowAtten = light.shadowAttenuation;
            half calculatedShadowStrength = _AdditionalLightShadowStrength * (1 - shadowAtten);

            half3 shadowedColor = lerp(lighting, _AdditionalShadowColor.rgb * Luminance(lighting),
                                       calculatedShadowStrength);

            diffuseColor += shadowedColor;
        }
    LIGHT_LOOP_END

    return diffuseColor;
}

half4 LowQualityLighting(half3 baseColor, half3 normalWS, half3 worldPos)
{
    half3 mainLighting = CalculateMainLight(baseColor, normalWS, worldPos);
    return half4(mainLighting, 1);
}

half4 MediumQualityLighting(half3 baseColor, half3 normalWS, half3 worldPos)
{
    half3 mainLighting = CalculateMainLight(baseColor, normalWS, worldPos);
    half3 additionalLight = MediumAdditionalLight(worldPos, normalWS);
    return half4(mainLighting + additionalLight, 1);
}

half4 HighQualityLighting(half3 baseColor, half3 normalWS, half3 worldPos)
{
    half3 mainLighting = CalculateMainLight(baseColor, normalWS, worldPos);
    half3 additionalLight = HighAdditionalLight(worldPos, normalWS);
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
    // Early-Z 최적화를 위해 가장 먼저 clip 수행
    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half distanceFade = input.extraBuffer.y;

    half verticalFade = CalculateVerticalFade(input.uv);
    half4 baseColor = lerp(_BottomTint, _TopTint, verticalFade) * half4(input.diffuseColor, 0);

    if (distanceFade < _LowQualityDistance)
    {
        return LowQualityLighting(baseColor.rgb, input.normalWS, input.worldPos);
    }

    if (distanceFade < _MediumQualityDistance)
    {
        return MediumQualityLighting(baseColor.rgb, input.normalWS, input.worldPos);
    }

    return HighQualityLighting(baseColor.rgb, input.normalWS, input.worldPos);
}
