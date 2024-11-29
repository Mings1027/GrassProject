using UnityEngine;

namespace Grass.GrassScripts
{
    public class SeasonEffectVolume : MonoBehaviour
    {
        [SerializeField] private bool showGizmos;

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}