Shader "Custom/Phong"
{
	Properties
	{
		[MainTexture] _BaseMap("Base Map", 2D) = "white" {}
		[HDR]_SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
		_SpecPower("Specular Power", Float) = 10
	}
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
		}
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float3 lightDir : TEXCOORD2;
				float3 viewDir : TEXCOORD3;
			};

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				half4 _SpecColor;
				half _SpecPower;
			CBUFFER_END

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				output.normal = TransformObjectToWorldNormal(input.normalOS);
				output.lightDir = normalize(_MainLightPosition.xyz);
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.viewDir = normalize(_WorldSpaceCameraPos.xyz - positionWS);
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				// 이걸 안 하면 버텍스 사이 픽셀 노멀의 길이가 1이 아닌 것들이 발생함.
				input.normal = normalize(input.normal);

				half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
				float NdotL = saturate(dot(input.normal, input.lightDir));

				// Phong Specular
				float3 reflectDir = reflect(-input.lightDir, input.normal);
				half spec = saturate(dot(reflectDir, input.viewDir));
				spec = pow(spec, _SpecPower);
				half3 specColor = spec * _SpecColor.rgb;

				half3 ambient = SampleSH(input.normal);
				half3 lighting = NdotL * _MainLightColor.rgb + ambient;
				color.rgb *= lighting;
				color.rgb += specColor;

				return color;
			}
			ENDHLSL
		}
		UsePass "Universal Render Pipeline/Lit/ShadowCaster"
	}
}