using UnityEngine;

public class ObstacleCheck : MonoBehaviour
{
    [SerializeField] private float capsuleHeight = 2.0f; // 캡슐의 높이
    [SerializeField] private float capsuleRadius = 0.5f; // 캡슐의 반지름
    [SerializeField] private float capsuleDistance = 1.0f; // 캡슐의 이동 거리
    [SerializeField] private LayerMask wallLayer; // 벽 레이어
    [SerializeField] private Vector3 inputDirection = Vector3.forward; // 사용자가 입력한 방향

    public bool CanMove(Vector3 direction)
    {
        inputDirection = direction;
        var offset = direction.normalized * capsuleDistance;
        var capsuleCenter = transform.position + offset;
        var capsuleBottom = capsuleCenter - Vector3.up * (capsuleHeight / 2 - capsuleRadius);
        var capsuleTop = capsuleCenter + Vector3.up * (capsuleHeight / 2 - capsuleRadius);

        return !Physics.CheckCapsule(capsuleBottom, capsuleTop, capsuleRadius, wallLayer);
    }

    private void OnDrawGizmos()
    {
        var offset = inputDirection.normalized * capsuleDistance;
        var capsuleCenter = transform.position + offset;
        var capsuleBottom = capsuleCenter - Vector3.up * (capsuleHeight / 2 - capsuleRadius);
        var capsuleTop = capsuleCenter + Vector3.up * (capsuleHeight / 2 - capsuleRadius);

        var hit = Physics.CheckCapsule(capsuleBottom, capsuleTop, capsuleRadius, wallLayer);

        // Draw the capsule as a gizmo
        Gizmos.color = hit ? Color.red : Color.green;
        Gizmos.DrawWireSphere(capsuleBottom, capsuleRadius);
        Gizmos.DrawWireSphere(capsuleTop, capsuleRadius);
        Gizmos.DrawLine(capsuleBottom + Vector3.right * capsuleRadius, capsuleTop + Vector3.right * capsuleRadius);
        Gizmos.DrawLine(capsuleBottom - Vector3.right * capsuleRadius, capsuleTop - Vector3.right * capsuleRadius);
        Gizmos.DrawLine(capsuleBottom + Vector3.forward * capsuleRadius, capsuleTop + Vector3.forward * capsuleRadius);
        Gizmos.DrawLine(capsuleBottom - Vector3.forward * capsuleRadius, capsuleTop - Vector3.forward * capsuleRadius);
    }
}