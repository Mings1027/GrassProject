Shader "Custom/LightingShader"
{
	Properties
	{
		_Tint ("Tint", Color) = (1,1,1,1)
		_Alpha("Alpha", Range(0, 1)) = 1
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Pass
		{
			HLSLPROGRAM
			// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			#pragma vertex Vertex
			#pragma fragment Fragment

			float4 _Tint;
			float _Alpha;
			Texture2D _MainTex;
			SamplerState sampler_MainTex;

			struct VertexData
			{
				float3 position : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct FragmentData
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			FragmentData Vertex(VertexData input)
			{
				FragmentData output;
				output.position = TransformObjectToHClip(input.position);
				output.worldPos = TransformObjectToWorld(input.position);
				output.normal = TransformObjectToWorldNormal(input.normal);
				output.uv = input.uv;
				return output;
			}

			half4 Fragment(FragmentData input) : SV_Target
			{
				half4 albedo = _MainTex.Sample(sampler_MainTex, input.uv) * _Tint;

				Light light = GetMainLight();
				half3 lightDir = light.direction;
				half3 lightColor = light.color;

				float intensity = saturate(dot(input.normal, lightDir));
				float3 lambert = intensity * lightColor;

				half3 finalColor = albedo.rgb * lambert;
				return half4(finalColor, _Alpha);
			}
			ENDHLSL
		}
	}
}