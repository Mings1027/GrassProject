using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Grass.GrassScripts
{
    public class Scanner : MonoBehaviour
    {
        [SerializeField] private float radius;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float minDistanceToTarget = 0.1f;
        [SerializeField] private List<Transform> nearbyObjects;
        [SerializeField] private Color highLightColor;
        [SerializeField] private bool useQuadTree;

        private readonly Dictionary<Transform, Color> _originalColors = new();
        private Vector3 _lastPosition;
        private QuadTreeManager _quadTreeManager;
        private Vector3 _targetPosition;
        private MaterialPropertyBlock _propertyBlock;
        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _quadTreeManager = FindAnyObjectByType<QuadTreeManager>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (Vector3.Distance(transform.position, _targetPosition) < minDistanceToTarget)
            {
                SetNewTarget();
            }

            var directionToTarget = (_targetPosition - transform.position).normalized;
            var targetRotation = Quaternion.LookRotation(directionToTarget);
            var newPosition = Vector3.MoveTowards(transform.position, _targetPosition, moveSpeed * Time.deltaTime);
            var newRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(newPosition, newRotation);

            if (transform.position == _lastPosition) return;

            ResetColors();

            nearbyObjects.Clear();
            if (useQuadTree)
            {
                Profiler.BeginSample("Quad Tree");
                _quadTreeManager.GetNearbyObjects(transform.position, nearbyObjects, radius);
            }

            else
            {
                Profiler.BeginSample("No Quad Tree");
                GetNearbyObjects();
            }

            Profiler.EndSample();

            SetColors();

            _lastPosition = transform.position;
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        private void GetNearbyObjects()
        {
            var radiusSqr = radius * radius;
            foreach (var obj in _quadTreeManager.AllObjects)
            {
                var sqrDist = (obj.position - transform.position).sqrMagnitude;
                if (sqrDist <= radiusSqr)
                {
                    nearbyObjects.Add(obj);
                }
            }
        }

        private void ResetColors()
        {
            foreach (var obj in nearbyObjects)
            {
                if (_originalColors.TryGetValue(obj, out var color))
                {
                    if (obj.TryGetComponent(out Renderer objRenderer))
                    {
                        _propertyBlock.SetColor(ColorProperty, color);
                        objRenderer.SetPropertyBlock(_propertyBlock);
                    }
                }
            }
        }

        private void SetColors()
        {
            foreach (var obj in nearbyObjects)
            {
                if (obj.TryGetComponent(out Renderer objRenderer))
                {
                    // 원래 색상 저장 (처음 발견했을 때만)
                    if (!_originalColors.ContainsKey(obj))
                    {
                        objRenderer.GetPropertyBlock(_propertyBlock);
                        _originalColors[obj] = _propertyBlock.GetColor(ColorProperty);
                    }

                    _propertyBlock.SetColor(ColorProperty, highLightColor);
                    objRenderer.SetPropertyBlock(_propertyBlock);
                }
            }
        }

        private void SetNewTarget()
        {
            _targetPosition = _quadTreeManager.GetRandomPosition();
        }
    }
}