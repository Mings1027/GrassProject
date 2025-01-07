using UnityEngine;

namespace Grass.GrassScripts
{
    public interface IGrassColorProvider
    {
        Color GetGrassColor(Vector3 position, Color defaultColor);
    }
}