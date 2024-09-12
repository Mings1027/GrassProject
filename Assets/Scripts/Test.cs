using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

public class Test : MonoBehaviour
{
    [SerializeField] private LayerMask testLayer;

    [ContextMenu("Test")]
    private void CompareLayer()
    {
        if (((1 << gameObject.layer) & testLayer) != 0)
        {
            Debug.Log("Test");
        }
    }
}