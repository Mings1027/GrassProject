using System;
using UnityEngine;

namespace Grass.GrassScripts
{
    public class SeasonTest : MonoBehaviour
    {
        [SerializeField] private GrassSeasonManager _seasonManager;
        [SerializeField] private float transitionDuration = 1f;
        
        public GrassSeasonManager SeasonManager => _seasonManager;
        public float TransitionDuration => transitionDuration;
    }
}