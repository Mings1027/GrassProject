using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Grass.Editor
{
    // Using BurstCompile to compile a Job with burst
    // Set CompileSynchronously to true to make sure that the method will not be compiled asynchronously
    // but on the first schedule

    [BurstCompile(CompileSynchronously = true)]
    public struct GrassJob : IJob
    {
        [ReadOnly] public NativeArray<float> sizes;
        [ReadOnly] public NativeArray<float> total;
        [ReadOnly] public NativeArray<float> cumulativeSizes;
        [ReadOnly] public NativeArray<Color> meshColors;
        [ReadOnly] public NativeArray<Vector4> meshVertices;
        [ReadOnly] public NativeArray<Vector3> meshNormals;
        [ReadOnly] public NativeArray<int> meshTriangles;
        [WriteOnly] public NativeArray<Vector3> point;
        [WriteOnly] public NativeArray<float> widthHeight;
        [WriteOnly] public NativeArray<Vector3> normals;

        public GrassToolSettingSo.VertexColorSetting vertexColorSettings;
        public GrassToolSettingSo.VertexColorSetting vertexFade;

        public void Execute()
        {
            var randomSample = Random.value * total[0];
            var triIndex = -1;

            for (var i = 0; i < sizes.Length; i++)
            {
                if (randomSample <= cumulativeSizes[i])
                {
                    triIndex = i;
                    break;
                }
            }

            if (triIndex == -1) Debug.LogError("triIndex should never be -1");

            switch (vertexColorSettings)
            {
                case GrassToolSettingSo.VertexColorSetting.Red:
                    if (meshColors[meshTriangles[triIndex * 3]].r > 0.5f)
                    {
                        point[0] = Vector3.zero;
                        return;
                    }

                    break;
                case GrassToolSettingSo.VertexColorSetting.Green:
                    if (meshColors[meshTriangles[triIndex * 3]].g > 0.5f)
                    {
                        point[0] = Vector3.zero;
                        return;
                    }

                    break;
                case GrassToolSettingSo.VertexColorSetting.Blue:
                    if (meshColors[meshTriangles[triIndex * 3]].b > 0.5f)
                    {
                        point[0] = Vector3.zero;
                        return;
                    }

                    break;
            }

            switch (vertexFade)
            {
                case GrassToolSettingSo.VertexColorSetting.Red:
                    var red = meshColors[meshTriangles[triIndex * 3]].r;
                    var red2 = meshColors[meshTriangles[triIndex * 3 + 1]].r;
                    var red3 = meshColors[meshTriangles[triIndex * 3 + 2]].r;

                    widthHeight[0] = 1.0f - (red + red2 + red3) * 0.3f;
                    break;
                case GrassToolSettingSo.VertexColorSetting.Green:
                    var green = meshColors[meshTriangles[triIndex * 3]].g;
                    var green2 = meshColors[meshTriangles[triIndex * 3 + 1]].g;
                    var green3 = meshColors[meshTriangles[triIndex * 3 + 2]].g;

                    widthHeight[0] = 1.0f - (green + green2 + green3) * 0.3f;
                    break;
                case GrassToolSettingSo.VertexColorSetting.Blue:
                    var blue = meshColors[meshTriangles[triIndex * 3]].b;
                    var blue2 = meshColors[meshTriangles[triIndex * 3 + 1]].b;
                    var blue3 = meshColors[meshTriangles[triIndex * 3 + 2]].b;

                    widthHeight[0] = 1.0f - (blue + blue2 + blue3) * 0.3f;
                    break;
                case GrassToolSettingSo.VertexColorSetting.None:
                    widthHeight[0] = 1.0f;
                    break;
            }

            Vector3 a = meshVertices[meshTriangles[triIndex * 3]];
            Vector3 b = meshVertices[meshTriangles[triIndex * 3 + 1]];
            Vector3 c = meshVertices[meshTriangles[triIndex * 3 + 2]];

            // Generate random barycentric coordinates
            var r = Random.value;
            var s = Random.value;

            if (r + s >= 1)
            {
                r = 1 - r;
                s = 1 - s;
            }

            normals[0] = meshNormals[meshTriangles[triIndex * 3 + 1]];

            // Turn point back to a Vector3
            var pointOnMesh = a + r * (b - a) + s * (c - a);

            point[0] = pointOnMesh;
        }
    }
}