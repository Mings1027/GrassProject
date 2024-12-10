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
    float4 _TopTint;
    float4 _BottomTint;

    // Additional Light
    float _AdditionalLightIntensity;
    float _AdditionalLightShadowStrength;
    float4 _AdditionalShadowColor;

    // Blend
    float _BlendMult, _BlendOff;

    // Zone Settings
    float4 _ZonePositions[MAX_ZONES]; // w 컴포넌트는 활성상태 (1 = 활성, 0 = 비활성)
    float4 _ZoneScales[MAX_ZONES];
    float4 _ZoneColors[MAX_ZONES];
    int _ZoneCount;
CBUFFER_END
