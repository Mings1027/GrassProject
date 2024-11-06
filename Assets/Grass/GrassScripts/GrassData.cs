using System;
using UnityEngine;

[Serializable]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct GrassData
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 widthHeight;
    public Vector3 color;
}