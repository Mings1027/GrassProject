Shader "Custom/TestGrass"
{
	Properties
	{
		[Header(Additional Light)]
		_AdditionalLightIntensity("Light Intensity", Range(0, 1)) = 0.5
		_AdditionalLightShadowStrength("Shadow Strength", Range(0, 1)) = 0.8
		_AdditionalLightShadowColor("Shadow Color", Color) = (0, 0, 0, 1)

		[Header(Tone Mapping)]
		_OverallIntensity("Overall Intensity", Range(0, 1)) = 1
		_Exposure("Exposure", Range(0.1, 2)) = 0.85
		_Saturation("Saturation", Range(0, 2)) = 0.95

		[Header(Blend)]
		[Toggle(BLEND)] _BlendFloor("Blend with floor", Float) = 0
		_BlendMult("Blend Multiply", Range(0, 5)) = 1
		_BlendOff("Blend Offset", Range(0, 1)) = 0.2
		_AmbientAdjustmentColor("Ambient Adjustment Color", Color) = (0.5, 0.5, 0.5, 1)
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
			Name "Universal Forward"
			Tags
			{
				"LightMode" = "UniversalForward"
			}

			Cull Off

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex vert
			#pragma fragment frag

			#pragma shader_feature BLEND

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _FORWARD_PLUS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

			#include "GrassPass.hlsl"
			ENDHLSL
		}
		// Shadow Caster Pass
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
			Cull Off

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#include "GrassShadowCasterPass.hlsl"
			ENDHLSL
		}
	}
	Fallback "Hidden/Universal Render Pipeline/FallbackError"
}