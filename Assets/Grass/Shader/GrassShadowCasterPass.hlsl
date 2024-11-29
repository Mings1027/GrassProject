#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassInput.hlsl"
#include "Grass.hlsl"

float3 _LightDirection;

FragmentData ShadowPassVertex(VertexData input)
{
    FragmentData output;

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

half4 ShadowPassFragment() : SV_TARGET
{
    return 0;
}
