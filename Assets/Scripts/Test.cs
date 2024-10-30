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
    [SerializeField] private int count;
    private List<int> intList1;
    private List<int> intList2;

    private void Awake()
    {
        intList1 = new List<int>();
        intList2 = new List<int>();
    }

    private void Start()
    {
        
    }
}