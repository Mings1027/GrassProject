using System;
using UnityEngine;

namespace Grass.GrassScripts
{
    public enum SeasonType
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3
    }

    [CreateAssetMenu(fileName = "Season Settings", menuName = "Grass/SeasonSettings")]
    public class SeasonSettings : ScriptableObject
    {
        public SeasonType seasonType;
        public Color seasonColor;
        [Range(0f, 2f)] public float width = 1f;
        [Range(0f, 2f)] public float height = 1f;
    }
}