using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Grass.Editor
{
    [System.Serializable]
    public struct ColliderData
    {
        public float3 Center;
        public float3 Size;
        public quaternion Rotation;
        public int Type; // 0: Box, 1: Sphere, 2: Capsule

        public static ColliderData FromCollider(Collider collider)
        {
            ColliderData data = new ColliderData();
            data.Center = collider.bounds.center;
            data.Size = collider.bounds.size;
            data.Rotation = collider.transform.rotation;

            if (collider is BoxCollider) data.Type = 0;
            else if (collider is SphereCollider) data.Type = 1;
            else if (collider is CapsuleCollider) data.Type = 2;
            else data.Type = 0; // 기본값으로 Box 사용

            return data;
        }
    }

    [BurstCompile]
    public struct PhysicsOverlapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Points;
        [ReadOnly] public float3 HalfExtents;
        [ReadOnly] public NativeArray<ColliderData> Colliders;
        [WriteOnly] public NativeArray<bool> Results;

        public void Execute(int index)
        {
            float3 point = Points[index];
            bool overlapped = false;

            for (int i = 0; i < Colliders.Length; i++)
            {
                if (OverlapCollider(point, HalfExtents, Colliders[i]))
                {
                    overlapped = true;
                    break;
                }
            }

            Results[index] = overlapped;
        }

        private bool OverlapCollider(float3 center, float3 halfExtents, ColliderData collider)
        {
            switch (collider.Type)
            {
                case 0: // Box
                    return OverlapBox(center, halfExtents, collider.Center, collider.Size / 2, collider.Rotation);
                case 1: // Sphere
                    return OverlapSphere(center, halfExtents, collider.Center, math.cmin(collider.Size) / 2);
                case 2: // Capsule
                    return OverlapCapsule(center, halfExtents, collider.Center, collider.Size, collider.Rotation);
                default:
                    return false;
            }
        }

        private bool OverlapBox(float3 centerA, float3 halfExtentsA, float3 centerB, float3 halfExtentsB,
                                quaternion rotationB)
        {
            // 간단한 AABB 검사 (회전 무시)
            float3 minA = centerA - halfExtentsA;
            float3 maxA = centerA + halfExtentsA;
            float3 minB = centerB - halfExtentsB;
            float3 maxB = centerB + halfExtentsB;

            return (minA.x <= maxB.x && maxA.x >= minB.x) &&
                   (minA.y <= maxB.y && maxA.y >= minB.y) &&
                   (minA.z <= maxB.z && maxA.z >= minB.z);
        }

        private bool OverlapSphere(float3 center, float3 halfExtents, float3 sphereCenter, float sphereRadius)
        {
            float3 closestPoint = math.clamp(sphereCenter, center - halfExtents, center + halfExtents);
            return math.distancesq(closestPoint, sphereCenter) <= sphereRadius * sphereRadius;
        }

        private bool OverlapCapsule(float3 center, float3 halfExtents, float3 capsuleCenter, float3 capsuleSize,
                                    quaternion capsuleRotation)
        {
            // 간단한 구현을 위해 캡슐을 구로 근사
            float capsuleRadius = math.max(capsuleSize.x, capsuleSize.z) / 2;
            return OverlapSphere(center, halfExtents, capsuleCenter, capsuleRadius);
        }
    }
}