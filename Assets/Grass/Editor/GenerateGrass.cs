using Unity.Collections;
using UnityEngine;

namespace Grass.Editor
{
    public enum GenerateType
    {
        Mesh,
        Terrain
    }

    public abstract class GenerateGrass
    {
        public GenerateType GenerateType { get; protected set; }
        public string ObjName { get; protected set; }
        public int Layer { get; protected set; }
        public Matrix4x4 LocalToWorldMatrix { get; protected set; }
        public Bounds Bounds { get; protected set; }

    }

    public class GenerateMeshFilter : GenerateGrass
    {
        public Vector3 Direction { get; protected set; }
        public Vector3 LossyScale { get; protected set; }
       
        public NativeArray<float> Sizes { get; protected set; }
        public NativeArray<float> CumulativeSizes { get; protected set; }
        public NativeArray<float> Total { get; protected set; }

        public int[] OTriangles { get; protected set; }
        public Vector3[] OVertices { get; protected set; }
        public Color[] OColors { get; protected set; }
        public Vector3[] ONormals { get; protected set; }
        
    
        public NativeArray<int> Triangles { get; protected set; }
        public NativeArray<Vector4> Vertices { get; protected set; }
        public NativeArray<Color> Colors { get; protected set; }
        public NativeArray<Vector3> Normals { get; protected set; }

        public GenerateMeshFilter(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                Bounds = meshFilter.sharedMesh.bounds;
                LossyScale = meshFilter.transform.localScale;
                GenerateType = GenerateType.Mesh;
                ObjName = obj.name;
                Layer = obj.layer;

                Sizes = GetTriSizes(meshFilter.sharedMesh.triangles, meshFilter.sharedMesh.vertices);
                CumulativeSizes = new NativeArray<float>(Sizes.Length, Allocator.Temp);
                Total = new NativeArray<float>(1, Allocator.Temp);

                OTriangles = meshFilter.sharedMesh.triangles;
                OVertices = meshFilter.sharedMesh.vertices;
                OColors = meshFilter.sharedMesh.colors;
                ONormals = meshFilter.sharedMesh.normals;
                
                LocalToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                Triangles = new NativeArray<int>(OTriangles, Allocator.Temp);
                Vertices = new NativeArray<Vector4>(OVertices.Length, Allocator.Temp);
                Colors = new NativeArray<Color>(OVertices.Length, Allocator.Temp);
                Normals = new NativeArray<Vector3>(ONormals, Allocator.Temp);

                Direction = meshFilter.transform.TransformDirection(Normals[0]);

                for (var i = 0; i < Sizes.Length; i++)
                {
                    var nativeArray = Total;
                    nativeArray[0] += Sizes[i];
                    var cumulativeSizes = CumulativeSizes;
                    cumulativeSizes[i] = nativeArray[0];
                }
            }
        }


        private NativeArray<float> GetTriSizes(int[] tris, Vector3[] verts)
        {
            var triCount = tris.Length / 3;
            var sizes = new NativeArray<float>(triCount, Allocator.Temp);
            for (var i = 0; i < triCount; i++)
            {
                sizes[i] = .5f * Vector3.Cross(
                    verts[tris[i * 3 + 1]] - verts[tris[i * 3]],
                    verts[tris[i * 3 + 2]] - verts[tris[i * 3]]).magnitude;
            }

            return sizes;
        }
    }

    public class GenerateTerrain : GenerateGrass
    {
        public Vector3 TerrainDataSize { get; protected set; }
        
        public GenerateTerrain(GameObject obj)
        {
            if (obj.TryGetComponent(out Terrain terrain))
            {
                GenerateType = GenerateType.Terrain;
                ObjName = obj.name;
                Layer = obj.layer;
                
                TerrainDataSize = terrain.terrainData.size;
                LocalToWorldMatrix = terrain.transform.localToWorldMatrix;
                Bounds = terrain.terrainData.bounds;
            }
        }
    }
}