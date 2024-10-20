using Grass.GrassScripts;
using UnityEngine;

namespace Grass.Editor
{
    public abstract class GrassPainterHelper
    {
        public static RemoveGrass CreateRemoveObject(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter _))
            {
                return new RemoveMeshFilter(obj);
            }

            if (obj.TryGetComponent(out Terrain _))
            {
                return new RemoveTerrain(obj);
            }

            Debug.LogWarning($"Unsupported object type for {obj.name}");
            return null;
        }

        public static Vector3 GetRandomBarycentricCoord()
        {
            var r1 = Mathf.Sqrt(Random.value);
            var r2 = Random.value;
            return new Vector3(1 - r1, r1 * (1 - r2), r2 * r1);
        }

        public static Color GetVertexColor(Color[] colors, int index1, int index2, int index3, Vector3 barycentricCoord)
        {
            if (colors == null || colors.Length == 0) return Color.white;
            return colors[index1] * barycentricCoord.x +
                   colors[index2] * barycentricCoord.y +
                   colors[index3] * barycentricCoord.z;
        }

        public static bool AreModifierKeysPressed(KeyBinding keyBindings)
        {
            if (keyBindings == KeyBinding.None) return true;

            bool isPressed = true;

            if (keyBindings.HasFlag(KeyBinding.LeftControl) ||
                keyBindings.HasFlag(KeyBinding.RightControl))
                isPressed &= Event.current.control;
            if (keyBindings.HasFlag(KeyBinding.LeftAlt) ||
                keyBindings.HasFlag(KeyBinding.RightAlt))
                isPressed &= Event.current.alt;
            if (keyBindings.HasFlag(KeyBinding.LeftShift) ||
                keyBindings.HasFlag(KeyBinding.RightShift))
                isPressed &= Event.current.shift;
            if (keyBindings.HasFlag(KeyBinding.LeftCommand) ||
                keyBindings.HasFlag(KeyBinding.RightCommand))
                isPressed &= Event.current.command;

            return isPressed;
        }

        public static bool IsMouseButtonPressed(MouseButton button)
        {
            return Event.current.button == (int)button;
        }
    }
}