using UnityEngine;

public class ObstacleCheck : MonoBehaviour
{
    private Vector3 _inputDirection; // 사용자가 입력한 방향
    private bool _isHit; // Will store if there was a hit

    [SerializeField] private float sphereRadius = 1.0f; // 구의 반지름
    [SerializeField] private float sphereDistance = 1.0f; // 구의 이동 거리
    [SerializeField] private LayerMask wallLayer; // 벽 레이어
#if UNITY_EDITOR
    [SerializeField] private bool drawGizmos;
#endif

    public bool CanMove(Vector3 direction)
    {
        _inputDirection = direction;
        var offset = direction.normalized * sphereDistance;
        var sphereCenter = transform.position + offset;

        _isHit = Physics.CheckSphere(sphereCenter, sphereRadius, wallLayer);
        return !_isHit;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Calculate the sphere parameters
        var offset = _inputDirection.normalized * sphereDistance;
        var sphereCenter = transform.position + offset;

        // Set the color and transparency based on hit status
        Gizmos.color = _isHit ? new Color(1, 0, 0, 0.5f) : new Color(0, 1, 0, 0.5f);
        Gizmos.DrawSphere(sphereCenter, sphereRadius);
    }
#endif
}