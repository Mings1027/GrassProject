using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

public abstract class Test : MonoBehaviour
{
    public virtual IEnumerator TestCo()
    {
        Debug.Log("Parent");
        yield return null;
    }

    public virtual async UniTask TestAsync()
    {
        Debug.Log("Parent");
        await UniTask.Yield();
    }
}

public class TestChildren : Test
{
    public override IEnumerator TestCo()
    {
        Debug.Log("Children");
        return null;
    }

    public override async UniTask TestAsync()
    {
        Debug.Log("Children");
        await UniTask.Yield();
    }
}