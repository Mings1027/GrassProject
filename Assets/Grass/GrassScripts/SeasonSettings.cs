using System;
using UnityEngine;

namespace Grass.GrassScripts
{
    [Serializable]
    public class SeasonSettings
    {
        public Color seasonColor;
        [Range(0f, 2f)] public float width = 1f;
        [Range(0f, 2f)] public float height = 1f;
    }
}