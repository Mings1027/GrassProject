using UnityEngine;

public class ObstacleCheck : MonoBehaviour
{
    private Vector3 _inputDirection;
    private bool _isHit;
    private RaycastHit _hitInfo;

    [SerializeField] private float sphereRadius = 1.0f;
    [SerializeField] private float sphereDistance = 1.0f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private int rayCount = 8;
    [SerializeField] private float wallSlideSpeedMultiplier = 0.7f; // 벽을 타고 이동할 때의 속도 계수
#if UNITY_EDITOR
    [SerializeField] private bool drawGizmos;
#endif

    public (Vector3 direction, float speedMultiplier) GetMoveDirection(Vector3 direction)
    {
        _inputDirection = direction.normalized;
        var offset = _inputDirection * sphereDistance;
        var sphereCenter = transform.position + offset;

        // 메인 SphereCast 체크
        _isHit = Physics.SphereCast(transform.position, sphereRadius, _inputDirection,
            out _hitInfo, sphereDistance, wallLayer);

        if (_isHit)
        {
            Vector3 normal = _hitInfo.normal;
            normal.y = 0;
            normal.Normalize();

            // 슬라이딩 벡터 계산
            Vector3 slideDirection = _inputDirection - Vector3.Dot(_inputDirection, normal) * normal;
            slideDirection.Normalize();

            // 충돌 각도에 따른 속도 계수 계산
            float angle = Vector3.Angle(_inputDirection, slideDirection);
            float speedMultiplier = Mathf.Lerp(wallSlideSpeedMultiplier, 1f,
                Mathf.Clamp01(1f - angle / 90f));

            // 전방 충돌 체크
            if (!Physics.CheckSphere(transform.position + slideDirection * (sphereDistance * 0.5f),
                    sphereRadius * 0.8f, wallLayer))
            {
                return (slideDirection, speedMultiplier);
            }
        }

        // 이동 방향에 장애물이 없는 경우
        if (!Physics.CheckSphere(sphereCenter, sphereRadius * 0.8f, wallLayer))
        {
            return (_inputDirection, 1f);
        }

        return (Vector3.zero, 0f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        var offset = _inputDirection * sphereDistance;
        var sphereCenter = transform.position + offset;

        // 충돌 체크 시각화
        Gizmos.color = _isHit ? new Color(1, 0, 0, 0.5f) : new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, sphereRadius);
        Gizmos.DrawWireSphere(sphereCenter, sphereRadius);
        Gizmos.DrawLine(transform.position, sphereCenter);

        if (_isHit)
        {
            // 충돌 정보 표시
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_hitInfo.point, _hitInfo.normal);
            
            Vector3 slideDirection = _inputDirection - 
                                     Vector3.Dot(_inputDirection, _hitInfo.normal) * _hitInfo.normal;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_hitInfo.point, slideDirection);
        }
    }
#endif
}