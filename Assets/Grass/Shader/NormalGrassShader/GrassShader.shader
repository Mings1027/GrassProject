Shader "Custom/GrassShader"
{
    Properties
    {
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.3

        [Header(Additional Light)]
        _AdditionalLightIntensity("Light Intensity", Range(0, 1)) = 0.5
        _AdditionalLightShadowStrength("Shadow Strength", Range(0, 1)) = 0.8
        _AdditionalShadowColor("Shadow Color", Color) = (0, 0, 0, 1)

        [Header(Blend)]
        _BlendMultiply("Blend Multiply", Range(0, 5)) = 1
        _BlendOffset("Blend Offset", Range(0, 1)) = 0.2

        [Header(Tint)]
        _TopTint("Top Tint", Color) = (1,1,1,1)
        _BottomTint("Bottom Tint", Color) = (1,1,1,1)

        [Header(Shadow Settings)]
        _ShadowDistance("Shadow Distance", Range(0, 300)) = 50
        _ShadowFadeRange("Shadow Fade Range", Range(0.1, 30)) = 10
        _MinShadowBrightness ("Min Shadow Brightness", Range(0, 1)) = 0.3
        _ShadowColor ("Shadow Color", Color) = (0.5, 0.5, 0.5, 1)

        [Header(Specular)]
        _SpecularFalloff("Specular Falloff", Range(0, 10)) = 0.5
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.5
        _SpecularHeight("Specular Height", Range(0, 1)) = 0.7
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
            ZClip False

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassInput.hlsl"
            #include "GrassPass.hlsl"
            ENDHLSL
        }
        // Shadow Caster Pass
        //		Pass
        //		{
        //			Name "ShadowCaster"
        //			Tags
        //			{
        //				"LightMode" = "ShadowCaster"
        //			}
        //
        //			HLSLPROGRAM
        //			#pragma vertex ShadowPassVertex
        //			#pragma fragment ShadowPassFragment
        //
        //			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        //			#include "GrassInput.hlsl"
        //			#include "Grass.hlsl"
        //			#include "GrassShadowPass.hlsl"
        //			ENDHLSL
        //		}
    }
}