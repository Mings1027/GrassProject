// This describes a vertex on the generated mesh
struct DrawVertex
{
    float3 positionWS; // The position in world space
    float2 uv;
};

// A triangle on the generated mesh
struct DrawTriangle
{
    float3 normalOS;
    float3 diffuseColor;
    float4 extraBuffer;
    DrawVertex vertices[3]; // The three points on the triangle
};

// A buffer containing the generated mesh
StructuredBuffer<DrawTriangle> _DrawTriangles; // 읽는 역할

half CalculateVerticalFade(half2 uv)
{
    half blendMul = uv.y * _BlendMultiply;
    half blendAdd = blendMul + _BlendOffset;
    return saturate(blendAdd);
}

void CalculateCutOff(half extraBufferX, half worldPosY)
{
    half cutOffTop = extraBufferX >= worldPosY ? 1 : 0;
    clip(cutOffTop - 0.01);
}

half3 CalculateMainLight(half3 albedo, FragmentData input)
{
    half4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
    Light mainLight = GetMainLight(shadowCoord);

    float distanceFromCamera = length(_WorldSpaceCameraPos - input.worldPos);
    float shadowFade = saturate((distanceFromCamera - _ShadowDistance) / _ShadowFadeRange);
    float rawShadowAtten = lerp(mainLight.shadowAttenuation, 1, shadowFade);

    // 그림자 영역의 최소 밝기 보장
    float shadowAtten = lerp(_MinShadowBrightness, 1, rawShadowAtten);

    // Diffuse with shadow color
    half NdotL = saturate(dot(input.normalWS, mainLight.direction));
    half3 shadowedAlbedo = lerp(albedo * _ShadowColor.rgb, albedo, shadowAtten);
    half3 diffuse = shadowedAlbedo * (mainLight.color * NdotL);

    // Specular
    half3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
    half3 halfVector = normalize(mainLight.direction + viewDir);
    half NdotH = saturate(dot(input.normalWS, halfVector));
    half specularPower = exp2(_Glossiness);
    half heightFactor = saturate((input.uv.y - _SpecularHeight) / (1 - _SpecularHeight));
    half3 specular = mainLight.color * _SpecularStrength * pow(NdotH, specularPower) * shadowAtten * heightFactor;

    // Ambient
    half3 ambient = albedo * _AmbientStrength;

    return diffuse + specular + ambient;
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

FragmentData Vertex(VertexData input)
{
    FragmentData output;

    DrawTriangle tri = _DrawTriangles[input.vertexID / 3];
    DrawVertex vert = tri.vertices[input.vertexID % 3];
    output.worldPos = vert.positionWS;
    output.normalWS = tri.normalOS;
    output.uv = vert.uv;
    output.diffuseColor = tri.diffuseColor;

    if (tri.extraBuffer.x == -1)
    {
        output.extraBuffer = float4(99999, tri.extraBuffer.y, tri.extraBuffer.z, tri.extraBuffer.w);
    }
    else
    {
        output.extraBuffer = tri.extraBuffer;
    }
    output.positionCS = TransformObjectToHClip(output.worldPos);

    return output;
}

half4 Fragment(FragmentData input) : SV_Target
{
    CalculateCutOff(input.extraBuffer.x, input.worldPos.y);

    half verticalFade = CalculateVerticalFade(input.uv);
    half4 baseColor = lerp(_BottomTint, _TopTint, verticalFade) * half4(input.diffuseColor, 0);

    half3 mainLighting = CalculateMainLight(baseColor.rgb, input);
    half3 additionalLight = CalculateAdditionalLight(input.worldPos, input.normalWS);
    return half4(mainLighting + additionalLight, 1);
}
