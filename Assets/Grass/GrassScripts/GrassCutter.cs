using System;
using Grass.GrassScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrassCutter : MonoBehaviour
{
    private GrassComputeScript _grassCompute;
    [SerializeField] private InputActionReference cutAction;
    [SerializeField] private float radius = 1f;
    [SerializeField, Range(0, 1)]
    private float minMoveDistance = 0.5f;

    public bool updateCuts;

    private Vector3 _cachedPos;

    private void Awake()
    {
        _grassCompute = FindAnyObjectByType<GrassComputeScript>();
    }

    private void OnEnable()
    {
        cutAction.action.performed += OnCut;
    }

    private void OnDisable()
    {
        cutAction.action.performed -= OnCut;
    }

    private void Start()
    {
        _cachedPos = transform.position;
    }

    private void OnCut(InputAction.CallbackContext context) => updateCuts = !updateCuts;

    private void Update()
    {
        if (updateCuts && _grassCompute.isActiveAndEnabled)
        {
            var distance = Vector3.Distance(transform.position, _cachedPos);

            if (distance >= minMoveDistance)
            {
                _grassCompute.CutGrass(transform.position, radius);
                _cachedPos = transform.position;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!updateCuts) return;
        Gizmos.color = new Color(1, 0, 0);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}