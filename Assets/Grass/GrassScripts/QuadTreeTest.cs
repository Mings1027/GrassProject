using Grass.GrassScripts;
using UnityEngine;

public class QuadTreeTest : MonoBehaviour
{
    private QuadTreeNode _quadTreeNode;

    [SerializeField] private Vector3 size;

    [ContextMenu("Create Quad Tree")]
    private void CreateQuadTree()
    {
        var bounds = new Bounds(transform.position, size);
        _quadTreeNode = new QuadTreeNode(bounds);
        _quadTreeNode.CreateTree(bounds);
    }

    private void OnDrawGizmos()
    {
        DrawQuadTreeGizmos(_quadTreeNode);
    }

    private void DrawQuadTreeGizmos(QuadTreeNode node)
    {
        if (node == null) return;

        Gizmos.DrawWireCube(node.Bounds.center, node.Bounds.size);

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child != null && child.Children != null)
                {
                    DrawQuadTreeGizmos(child);
                }
            }
        }
    }
}