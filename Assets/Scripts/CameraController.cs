using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;

    [SerializeField] private float speed;

    [SerializeField] private float smoothSpeed;
    [SerializeField, Range(1, 10)] private float offset;

    [SerializeField, Range(5f, 20f)] private float camDistance = 10f;

    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Transform cam;

    private void Start()
    {
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 5f;
            distanceSlider.maxValue = 20f;
            distanceSlider.value = camDistance;

            distanceSlider.onValueChanged.AddListener(SetCameraDistance);
        }
    }

    private void FixedUpdate()
    {
        transform.position = followTarget.position;
        var desiredPos = followTarget.position + cam.forward * -camDistance + new Vector3(0, offset, 0);
        var smoothedPos = Vector3.Lerp(cam.position, desiredPos, Time.deltaTime * smoothSpeed);
        cam.position = smoothedPos;
    }

    public void SetCameraDistance(float distance)
    {
        camDistance = Mathf.Clamp(distance, 5f, 20f);
    }
}