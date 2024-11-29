using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Vector3 _moveDir;
    private ObstacleCheck _obstacleDetector;
    private Rigidbody _rigid;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 700f;

    [SerializeField] private InputActionReference moveAction;

    private void Awake()
    {
        _obstacleDetector = GetComponent<ObstacleCheck>();
        _rigid = GetComponent<Rigidbody>();
        _rigid.freezeRotation = true;
    }

    private void Update()
    {
        var input = moveAction.action.ReadValue<Vector2>();
        _moveDir = new Vector3(input.x, 0, input.y).normalized;
    }

    private void FixedUpdate()
    {
        Movement();
    }

    private void Movement()
    {
        if (_moveDir != Vector3.zero)
        {
            var targetRotation = Quaternion.LookRotation(_moveDir);
            _rigid.MoveRotation(Quaternion.RotateTowards(_rigid.rotation, targetRotation,
                rotationSpeed * Time.fixedDeltaTime));

            if (_obstacleDetector.CanMove(_moveDir))
            {
                var movement = moveSpeed * Time.fixedDeltaTime * _moveDir;
                _rigid.MovePosition(_rigid.position + movement);
            }
        }
    }
}