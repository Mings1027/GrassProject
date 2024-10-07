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

    [SerializeField] private int a;

    [ContextMenu("Print hash code")]
    private void PrintHashCode()
    {
        print(a.GetTypeCode());
        Heal(ref a);
    }

    private void Heal(ref int hp)
    {
        print(hp.GetTypeCode());
        hp += 10;
    }
}