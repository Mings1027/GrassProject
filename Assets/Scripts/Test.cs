using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private int value;

    private void Update()
    {
        Graphics.DrawProceduralNow(MeshTopology.Triangles, value);
    }
}