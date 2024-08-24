Shader "Custom/GrassInstancing"
{
    Properties
    {
        _Color("Color", Color) = (0.3, 0.8, 0.3, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma instancing_options assumeuniformscaling

            #include "UnityCG.cginc"

            struct appdata
            {
                float2 uv : TEXCOORD0;
                float3 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
            };

            // 인스턴스 데이터 버퍼
            StructuredBuffer<float4x4> _InstanceBuffer;

            float4 _Color;

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float4x4 modelMatrix = _InstanceBuffer[instanceID]; // 인스턴스 행렬 읽기
                float4 worldPos = mul(modelMatrix, float4(v.vertex, 1.0));
                o.uv = v.uv;
                o.vertex = UnityObjectToClipPos(worldPos);
                o.worldNormal = mul((float3x3)modelMatrix, v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, 1.0);
            }
            ENDCG
        }
    }
}