using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Vector3 _moveDir;
    private ObstacleCheck _obstacleDetector;
    private Rigidbody _rigid;
    private Transform _cameraTransform;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 700f;

    [SerializeField] private InputActionReference moveAction;

    private void Awake()
    {
        _obstacleDetector = GetComponent<ObstacleCheck>();
        _rigid = GetComponent<Rigidbody>();
        _rigid.freezeRotation = true;
        _cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        PlayerInput();
    }

    private void FixedUpdate()
    {
        Movement();
    }

    private void PlayerInput()
    {
        var input = moveAction.action.ReadValue<Vector2>();

        var cameraForward = _cameraTransform.forward;
        var cameraRight = _cameraTransform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        _moveDir = (cameraForward * input.y + cameraRight * input.x).normalized;
    }

    private void Movement()
    {
        if (_moveDir != Vector3.zero)
        {
            var (actualMoveDir, speedMultiplier) = _obstacleDetector.GetMoveDirection(_moveDir);

            if (actualMoveDir != Vector3.zero)
            {
                var targetRotation = Quaternion.LookRotation(_moveDir);
                _rigid.MoveRotation(Quaternion.RotateTowards(_rigid.rotation, targetRotation,
                    rotationSpeed * Time.fixedDeltaTime));
                
                var movement = moveSpeed * speedMultiplier * Time.fixedDeltaTime * actualMoveDir;
                _rigid.MovePosition(_rigid.position + movement);
            }
        }
    }
}