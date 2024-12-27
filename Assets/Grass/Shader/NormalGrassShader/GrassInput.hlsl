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

CBUFFER_START(UnityPerMaterial)
    float4 _TopTint;
    float4 _BottomTint;

    half _AmbientStrength;

// Additional Light
    float _AdditionalLightIntensity;
    float _AdditionalLightShadowStrength;
    float4 _AdditionalShadowColor;

    // Blend
    float _BlendMult, _BlendOff;

    // Shadow
    float _ShadowDistance;
    float _ShadowFadeRange;

CBUFFER_END
