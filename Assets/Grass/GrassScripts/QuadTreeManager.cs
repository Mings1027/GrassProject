using System;
using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEngine;
using Random = UnityEngine.Random;

public class QuadTreeManager : MonoBehaviour
{
    private QuadTree _quadTree;

    [SerializeField] private Vector3 size;
    [SerializeField] private int depth;
    [SerializeField] private List<Transform> objects;

    public List<Transform> AllObjects => objects;

    private void Awake()
    {
        InitializeCollections();
        CreateQuadTree();
    }

    private void InitializeCollections()
    {
        var allObjects = FindObjectsByType<SphereCollider>(FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            objects.Add(allObjects[i].transform);
        }
    }

    [ContextMenu("Create Quad Tree")]
    private void CreateQuadTree()
    {
        var bounds = CalculateBounds();
        _quadTree = new QuadTree(bounds, depth);
        foreach (var obj in objects)
        {
            _quadTree.InsertObject(obj.position, obj);
        }

        _quadTree.ClearEmpty();
    }

    private Bounds CalculateBounds()
    {
        var bounds = new Bounds(transform.position, size);
        foreach (var obj in objects)
        {
            bounds.Encapsulate(obj.position);
        }

        bounds.extents *= 1.1f;
        return bounds;
    }

    public void GetNearbyObjects(Vector3 position, List<Transform> objectList, float radius)
    {
        _quadTree.GetNearbyObjects(position, objectList, radius);
    }

    public Vector3 GetRandomPosition()
    {
        var bounds = _quadTree.Bounds;
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z));
    }

    private void OnDrawGizmos()
    {
        if (_quadTree == null) return;
    
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        DrawNonEmptyNodes(_quadTree);
    }

    private void DrawNonEmptyNodes(QuadTree node)
    {
        if (node == null) return;

        // 리프 노드이고 객체가 있는 경우
        if (node.Children.Length == 0 && node.HasObjects)
        {
            Gizmos.DrawWireCube(node.Bounds.center, node.Bounds.size);
            return;
        }

        // 내부 노드의 경우 자식들 검사
        foreach (var child in node.Children)
        {
            if (child != null)
            {
                DrawNonEmptyNodes(child);
            }
        }
    }
}