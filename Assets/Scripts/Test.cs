using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

public class Test : MonoBehaviour
{
    [SerializeField] private PolygonCollider2D polygonCollider2D;

    private void Start()
    {
        polygonCollider2D.pathCount = 1;
        var paths = new Vector2[5];
        paths[0] = new Vector2(0, 0);
        paths[1] = new Vector2(1, 1);
        paths[2] = new Vector2(0, 1);
        paths[3] = new Vector2(1, 1);
        paths[4] = new Vector2(1, 2);
        polygonCollider2D.SetPath(0,paths);
    }
}