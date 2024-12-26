Shader "Custom/TextureWithShadow"
{
	Properties
	{
		[MainTexture] _BaseMap("Texture", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

		[Header(Additional Light)]
		_AdditionalLightIntensity("Light Intensity", Range(0, 1)) = 0.5
		_AdditionalLightShadowStrength("Shadow Strength", Range(0, 1)) = 0.8
		_AdditionalShadowColor("Shadow Color", Color) = (0, 0, 0, 1)
		_AmbientStrength("Ambient Strength", Range(0, 1)) = 0.3

		[Header(Shadow Settings)]
		_ShadowDistance("Shadow Distance", Range(0, 100)) = 50
		_ShadowFadeRange("Shadow Fade Range", Range(0.1, 30)) = 10
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Name "ForwardLit"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			// #pragma multi_compile_fragment _ _SHADOWS_SOFT

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float2 uv : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				half4 _BaseColor;
				half _AdditionalLightIntensity;
				half _AdditionalLightShadowStrength;
				half4 _AdditionalShadowColor;
				half _AmbientStrength;
				float _ShadowDistance;
				float _ShadowFadeRange;
			CBUFFER_END

			half3 CalculateMainLight(half3 albedo, half3 normalWS, half3 worldPos)
			{
				half4 shadowCoord = TransformWorldToShadowCoord(worldPos);
				Light mainLight = GetMainLight(shadowCoord);

				float distanceFromCamera = length(_WorldSpaceCameraPos - worldPos);

				float shadowFade = saturate((distanceFromCamera - _ShadowDistance) / _ShadowFadeRange);
				float shadowAtten = lerp(mainLight.shadowAttenuation, 1, shadowFade);

				half NdotL = saturate(dot(normalWS, mainLight.direction));
				return albedo * mainLight.color * (shadowAtten * NdotL + _AmbientStrength);
			}

			half3 CalculateAdditionalLight(half3 worldPos, half3 worldNormal)
			{
				uint pixelLightCount = GetAdditionalLightsCount();
				InputData inputData;

				float4 positionsCS = TransformWorldToHClip(worldPos);
				float3 ndc = positionsCS.xyz / positionsCS.w;
				float2 screenUV = half2(ndc.x, ndc.y) * 0.5 + 0.5;

				#if UNITY_UV_STARTS_AT_TOP
				screenUV.y = 1.0 - screenUV.y;
				#endif

				inputData.normalizedScreenSpaceUV = screenUV;
				inputData.positionWS = worldPos;

				half4 shadowMask = CalculateShadowMask(inputData);
				half3 diffuseColor = 0;

				LIGHT_LOOP_BEGIN(pixelLightCount)
					Light light = GetAdditionalLight(lightIndex, worldPos, shadowMask);

					#ifdef _LIGHT_LAYERS
				    uint meshRenderingLayers = GetMeshRenderingLayer();
					if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
					#endif
					{
						half NdotL = saturate(dot(worldNormal, light.direction));
						half3 lightColor = light.color * light.distanceAttenuation * NdotL * _AdditionalLightIntensity;
						half shadowAttenuation = lerp(1, light.shadowAttenuation, _AdditionalLightShadowStrength);
						diffuseColor += lerp(lightColor * _AdditionalShadowColor.rgb, lightColor, shadowAttenuation);
					}
				LIGHT_LOOP_END

				return diffuseColor;
			}

			Varyings vert(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input); // 추가
				UNITY_TRANSFER_INSTANCE_ID(input, output); // 추가

				output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(output.positionWS);
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input); // 추가

				half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

				half3 mainLighting = CalculateMainLight(baseColor.rgb, input.normalWS, input.positionWS);
				half3 additionalLighting = CalculateAdditionalLight(input.positionWS, input.normalWS);

				return half4(mainLighting + additionalLighting, baseColor.a);
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull Back

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			float3 _LightDirection;

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			float4 GetShadowPositionHClip(Attributes input)
			{
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

				float3 lightDirectionWS = _LightDirection;
				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				#if UNITY_REVERSED_Z
				positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				return positionCS;
			}

			Varyings ShadowPassVertex(Attributes input)
			{
				Varyings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);

				output.positionCS = GetShadowPositionHClip(input);
				return output;
			}

			half4 ShadowPassFragment(Varyings input) : SV_TARGET
			{
				return 0;
			}
			ENDHLSL
		}
	}
}