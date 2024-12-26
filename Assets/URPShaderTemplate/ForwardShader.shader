Shader "URP/Unlit/ForwardShader"
{
	Properties
	{
		[MainTexture] _BaseMap("Texture", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
		_ShadowStrength("Shadow Strength", Range(0, 1)) = 0.5
		_AmbientStrength("Ambient Strength", Range(0, 1)) = 0.2
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
		}
		LOD 100

		Pass
		{
			Name "URPUnlit"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT

			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				float4 _BaseColor;
				float _ShadowStrength;
				float _AmbientStrength;
			CBUFFER_END

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;

				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

				output.positionCS = TransformWorldToHClip(positionWS);
				output.positionWS = positionWS;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.uv = input.uv;

				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				half2 baseMapUV = input.uv.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;
				half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseMapUV);

				half4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				Light mainLight = GetMainLight(shadowCoord);

				half NdotL = saturate(dot(input.normalWS, mainLight.direction));
				half shadow = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

				half3 lighting = NdotL * shadow * mainLight.color;
				
				#ifdef _ADDITIONAL_LIGHTS
				uint additionalLightsCount = GetAdditionalLightsCount();
				for (uint lightIndex = 0; lightIndex < additionalLightsCount; ++lightIndex)
				{
					Light light = GetAdditionalLight(lightIndex, input.positionWS);
					half NdotL = saturate(dot(input.normalWS, light.direction));
					half shadowAttenuation = lerp(1.0, light.shadowAttenuation, _ShadowStrength);
					lighting += NdotL * shadowAttenuation * light.color * light.distanceAttenuation;
				}
				#endif

				half3 ambient = half3(1, 1, 1) * _AmbientStrength;
				lighting += ambient;

				half4 finalColor = texColor * _BaseColor;
				finalColor.rgb *= lighting;

				return finalColor;
			}
			ENDHLSL
		}

		Pass
		{
			Name "Shadow"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			ZWrite On
			ZTest LEqual
			ColorMask 0

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
			ENDHLSL
		}
	}
}