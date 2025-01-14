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
StructuredBuffer<DrawTriangle> _DrawTriangles;  // 읽는 역할

//get the data from the compute shader
void GetComputeData_float(uint vertexID, out float3 worldPos, out float3 normal, out float2 uv, out float3 col,
                          out float4 extraBuffer)
{
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex vert = tri.vertices[vertexID % 3];
    worldPos = vert.positionWS;
    normal = tri.normalOS;
    uv = vert.uv;
    col = tri.diffuseColor;

    // for some reason doing this with a comparison node results in a glitchy alpha, so we're doing it here, if your grass is at a point higher than 99999 Y position then you should make this even higher or find a different solution
    if (tri.extraBuffer.x == -1)
    {
        extraBuffer = float4(99999, tri.extraBuffer.y, tri.extraBuffer.z, tri.extraBuffer.w);
    }
    else
    {
        extraBuffer = tri.extraBuffer;
    }
}
