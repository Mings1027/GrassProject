Shader "Custom/TestGrass"
{
	Properties
	{
		[Toggle(BLEND)] _BlendFloor("Blend with floor", Float) = 0
		_BlendMult("Blend Multiply", Range(0, 5)) = 1
		_BlendOff("Blend Offset", Range(0, 1)) = 1
		[HDR] _AmbientAdjustmentColor("Ambient Adjustment Color", Color) = (0.5, 0.5, 0.5, 1)
		[Header(Main Light)]
		_ShadowStrength("Shadow Strength", Range(0, 1)) = 0.5
		[HDR] _ShadowColor("Shadow Color", Color) = (0.5, 0.5, 0.5, 1)
		[HideInInspector] _BaseMap("Base Color", 2D) = "white" {}
		[Header(Additional Light)]
		_AdditionalLightIntensity("Additional Light Intensity", Range(0, 1)) = 0.3
		_AdditionalLightShadowStrength("Additional Light Shadow Strength", Range(0, 1)) = 0.5
		[HDR] _AdditionalLightShadowColor("Additional Light Shadow Color", Color) = (0.5, 0.5, 0.5, 1)

	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			//            "RenderType" = "Opaque"
			//            "UniversalMaterialType" = "Lit"
			//            "Queue" = "AlphaTest"
		}

		Pass
		{
			Name "Universal Forward"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			Cull Off
			//            Blend One Zero
			//            ZTest LEqual
			//            ZWrite On
			//            AlphaToMask On

			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature BLEND

			// #pragma multi_compile_instancing

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			// #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			// #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Grass.hlsl"
			#include "CustomLighting.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _TopTint;
				float4 _BottomTint;
				float _BlendMult, _BlendOff;
				uniform TEXTURE2D(_TerrainDiffuse);
				uniform SAMPLER(sampler_TerrainDiffuse);
				float4 _AmbientAdjustmentColor;
				float _AdditionalLightIntensity;
				float _AdditionalLightShadowStrength;
				float4 _AdditionalLightShadowColor;
				float _ShadowStrength;
				float4 _ShadowColor;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
				uint vertexID : SV_VertexID;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float2 uv : TEXCOORD2;
				float3 diffuseColor : TEXCOORD3;
				float4 extraBuffer : TEXCOORD4;
			};

			float CalculateVerticalFade(Varyings input)
			{
				float blendMul = input.uv.y * _BlendMult;
				float blendAdd = blendMul + _BlendOff;
				return saturate(blendAdd);
			}

			float4 CalculateBaseColor(Varyings input, float verticalFade)
			{
				return lerp(_BottomTint, _TopTint * _AmbientAdjustmentColor, verticalFade) * float4(
					input.diffuseColor, 1);
			}

			float4 BlendWithTerrain(Varyings input, float verticalFade)
			{
				float2 uv = input.worldPos.xz - _OrthographicCamPosTerrain.xz;
				uv /= _OrthographicCamSizeTerrain * 2;
				uv += 0.5;

				float4 terrainForBlending = SAMPLE_TEXTURE2D(_TerrainDiffuse, sampler_TerrainDiffuse, uv);
				return lerp(terrainForBlending,
				            terrainForBlending + _TopTint * float4(input.diffuseColor, 1) *
				            _AmbientAdjustmentColor, verticalFade);
			}

			void CalculateCutOff(float extraBufferX, float worldPosY)
			{
				float cutOffTop = extraBufferX >= worldPosY ? 1 : 0;
				clip(cutOffTop - 0.01);
				// if (cutOffTop == 0)
				// {
				//     clip(-1);
				// }
			}

			float4 ApplyMainLightShadow(Varyings input, float4 finalColor)
			{
				float shadow = 0;
				#if _MAIN_LIGHT_SHADOWS_CASCADE || _MAIN_LIGHT_SHADOWS
                    half4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                    Light mainLight = GetMainLight(shadowCoord);
				#else
				Light mainLight = GetMainLight();
				#endif
				shadow = mainLight.shadowAttenuation;

				if (shadow <= 0)
				{
					finalColor.rgb *= lerp(float3(1, 1, 1), _ShadowColor.rgb, _ShadowStrength);
				}
				return finalColor;
			}

			Varyings vert(Attributes input)
			{
				Varyings output;

				GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
				                     output.extraBuffer);
				output.positionHCS = TransformObjectToHClip(output.worldPos);

				return output;
			}

			float4 frag(Varyings input) : SV_Target
			{
				float verticalFade = CalculateVerticalFade(input);

				float4 finalColor = CalculateBaseColor(input, verticalFade);

				#if defined(BLEND)
				finalColor = BlendWithTerrain(input, verticalFade);
				#endif

				CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

				finalColor = ApplyMainLightShadow(input, finalColor);
				finalColor = ApplyAdditionalLight(input.worldPos, input.normalWS, finalColor,
				                                  _AdditionalLightIntensity, _AdditionalLightShadowStrength,
				                                  _AdditionalLightShadowColor);

				return finalColor;
			}
			ENDHLSL
		}
	}
	//    Fallback "Universal Render Pipeline/Lit"
}