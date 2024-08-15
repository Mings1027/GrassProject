using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;

    [SerializeField] private float speed;

    [SerializeField] private float smoothSpeed;
    [SerializeField, Range(1, 10)] private float offset;

    private void FixedUpdate()
    {
        var desiredPos = followTarget.position + new Vector3(0, offset, -offset);
        var smoothedPos = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);
        transform.position = smoothedPos;
    }
}