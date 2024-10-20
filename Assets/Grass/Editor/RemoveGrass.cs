using UnityEngine;

namespace Grass.Editor
{
    public abstract class RemoveGrass
    {
        public abstract Bounds GetBounds();
    }

    public class RemoveMeshFilter : RemoveGrass
    {
        private readonly Vector3 _center;
        private readonly Vector3 _size;

        public RemoveMeshFilter(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                var meshBounds = meshFilter.sharedMesh.bounds;
                _center = obj.transform.TransformPoint(meshBounds.center);
                _size = Vector3.Scale(meshBounds.size, obj.transform.lossyScale);
            }
            else
            {
                Debug.LogWarning($"MeshFilter not found on {obj.name}");
            }
        }

        public override Bounds GetBounds()
        {
            return new Bounds(_center, _size);
        }
    }

    public class RemoveTerrain : RemoveGrass
    {
        private readonly Vector3 _position;
        private readonly Vector3 _size;

        public RemoveTerrain(GameObject obj)
        {
            if (obj.TryGetComponent(out Terrain terrain))
            {
                _position = terrain.transform.position;
                _size = terrain.terrainData.size;
            }
            else
            {
                Debug.LogWarning($"Terrain not found on {obj.name}");
            }
        }

        public override Bounds GetBounds()
        {
            return new Bounds(
                _position + _size * 0.5f,
                _size
            );
        }
    }
}