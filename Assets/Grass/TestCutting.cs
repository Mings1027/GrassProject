using UnityEngine;

public class TestCutting : MonoBehaviour
{
    [SerializeField] GrassComputeScript grassComputeScript;

    [SerializeField] private float radius = 1f;
    [SerializeField] private float minMoveDistance = 0.5f;

    public bool updateCuts;

    private Vector3 _cachedPos;

    private void Start()
    {
        _cachedPos = transform.position;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) updateCuts = !updateCuts;

        if (updateCuts)
        {
            var distance = Vector3.Distance(transform.position, _cachedPos);

            if (distance >= minMoveDistance)
            {
                grassComputeScript.UpdateCutBuffer(transform.position, radius);
                _cachedPos = transform.position;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}