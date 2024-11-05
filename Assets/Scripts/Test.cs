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
    private void Start()
    {
#if UNITY_IOS
        Debug.Log("iOS");
#endif

#if !UNITY_EDITOR
        Debug.Log("Editor");
#endif
    }
}