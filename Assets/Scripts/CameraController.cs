using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [SerializeField] private float rotationSpeed = 0.5f; // 회전 속도
    [SerializeField] private bool inverseRotation; // 회전 방향 제어를 위한 변수

    private float _currentRotationY;
    private Vector2 _lastTouchPosition;
    private bool _isDragging;

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

    private void Update()
    {
        CameraRotation();
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

    private void CameraRotation()
    {
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (IsPointerOverUI(touch.position))
                    {
                        _isDragging = false;
                        return;
                    }

                    _isDragging = true;
                    _lastTouchPosition = touch.position;
                    break;

                case TouchPhase.Moved:
                    if (_isDragging)
                    {
                        // 회전 방향을 inverseRotation 값에 따라 결정
                        var direction = inverseRotation ? -1f : 1f;
                        var deltaX = direction * (touch.position.x - _lastTouchPosition.x) * rotationSpeed;

                        _currentRotationY += deltaX;
                        transform.rotation = Quaternion.Euler(0, _currentRotationY, 0);

                        _lastTouchPosition = touch.position;
                    }

                    break;

                case TouchPhase.Ended:
                    _isDragging = false;
                    break;
            }
        }
    }

    private bool IsPointerOverUI(Vector2 position)
    {
        // UI 레이캐스트 결과를 저장할 리스트
        var eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = position;
        var results = new System.Collections.Generic.List<RaycastResult>();

        // 현재 위치에서 UI 레이캐스트 수행
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        // 하나라도 결과가 있다면 UI 위에 있는 것
        return results.Count > 0;
    }
}