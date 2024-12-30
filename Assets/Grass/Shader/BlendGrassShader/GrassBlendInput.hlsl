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
    float4 terrainBlendingColor : TEXCOORD5;
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
    float _BlendMultiply, _BlendOffset;
    float4 _AmbientAdjustmentColor;
    uniform TEXTURE2D(_TerrainDiffuse);
    uniform SAMPLER(sampler_TerrainDiffuse);

    // Shadow
    float _ShadowDistance;
    float _ShadowFadeRange;
    half _MinShadowBrightness;
    half4 _ShadowColor;

    //Specular
    float _Glossiness; // 반사광의 선명도 조절
    float _SpecularStrength; // 반사광의 강도
    float _SpecularHeight;
CBUFFER_END
