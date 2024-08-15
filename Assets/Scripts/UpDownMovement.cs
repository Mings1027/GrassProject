using UnityEngine;
using UnityEngine.InputSystem;

public class UpDownMovement : MonoBehaviour
{
    [SerializeField] private float liftSpeed = 2f;
    [SerializeField] private float maxHeight = 5f;

    private Rigidbody _rigid;
    private float _originalHeight;
    private bool _isLifting;

    private void Awake()
    {
        _rigid = GetComponent<Rigidbody>();
        _originalHeight = transform.position.y;
    }

    private void FixedUpdate()
    {
        HandleLift();
    }

    private void OnLift(InputValue value)
    {
        _isLifting = value.isPressed;
    }

    private void HandleLift()
    {
        if (_isLifting)
        {
            if (transform.position.y < _originalHeight + maxHeight)
            {
                _rigid.MovePosition(_rigid.position + Vector3.up * (liftSpeed * Time.fixedDeltaTime));
            }
        }
        else
        {
            if (transform.position.y > _originalHeight)
            {
                _rigid.MovePosition(_rigid.position - Vector3.up * (liftSpeed * Time.fixedDeltaTime));
            }
        }
    }
}