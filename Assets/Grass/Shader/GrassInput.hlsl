#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#ifndef UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED
#define UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED

struct VertexData
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FragmentData
{
    float4 positionCS : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float3 diffuseColor : TEXCOORD3;
    float4 extraBuffer : TEXCOORD4;
    float4 terrainBlendingColor : TEXCOORD5;
};

CBUFFER_START (UnityPerMaterial)
float4 _TopTint;
float4 _BottomTint;

// Additional Light
float _AdditionalLightIntensity;
float _AdditionalLightShadowStrength;
float4 _AdditionalShadowColor;

// Tone Mapping
float _Saturation;
float _OverallIntensity;
float _Exposure;

half4 _RimColor;
half _RimPower;
half _RimIntensity;
half _RimGradientStart;
half _RimGradientEnd;

// Blend
float _BlendMult, _BlendOff;
float4 _AmbientAdjustmentColor;
uniform TEXTURE2D (_TerrainDiffuse);
uniform SAMPLER (sampler_TerrainDiffuse);
CBUFFER_END

#endif
