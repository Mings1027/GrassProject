#include "Grass.hlsl"
#include "GrassInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float3 _LightDirection;

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;

    output.terrainBlendingColor = float4(0, 0, 0, 0);

    GetComputeData_float(input.vertexID, output.worldPos, output.normalWS, output.uv, output.diffuseColor,
                         output.extraBuffer);

    float3 positionWS = TransformObjectToWorld(output.worldPos);
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, _LightDirection));

    #if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    return 0;
}
