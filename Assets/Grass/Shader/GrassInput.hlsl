#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#ifndef UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED
#define UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float3 diffuseColor : TEXCOORD3;
    float4 extraBuffer : TEXCOORD4;
};

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    float4 _TopTint;
    float4 _BottomTint;
    float _BlendMult, _BlendOff;
    uniform TEXTURE2D(_TerrainDiffuse);
    uniform SAMPLER(sampler_TerrainDiffuse);
    float4 _AmbientAdjustmentColor;
    float _AdditionalLightIntensity;
    float _AdditionalLightShadowStrength;
    float4 _AdditionalLightColor;
    float _ShadowStrength;
    float4 _ShadowColor;
CBUFFER_END

#endif
