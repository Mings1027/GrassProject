Shader "URP/Unlit/#NAME#"
{
    Properties
    { 
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name  "URPUnlit"
            Tags {"LightMode" = "SRPDefaultUnlit"}

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles d3d9
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            }; 
            

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float4 positionOS = input.positionOS;
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                // float3 positionVS = TransformWorldToView(positionWS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                output.positionCS = positionCS;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 baseMapUV = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);
                
                float4 finalColor = texColor * _BaseColor;
                return finalColor;
            }
            
            ENDHLSL
        }
    }
}