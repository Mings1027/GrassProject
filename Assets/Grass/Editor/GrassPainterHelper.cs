using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public enum BrushOption
    {
        Add,
        Remove,
        Edit,
        Reposition
    }

    public enum EditOption
    {
        EditColors,
        EditWidthHeight,
        Both
    }

    public enum ModifyOption
    {
        WidthHeight,
        Color,
        Both
    }

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
           
        // 최적화된 거리 계산 (제곱근 계산 제거)
        public static float SqrDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }
        
        public static void DrawHorizontalLine(Color color, int thickness = 1, int padding = 10)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            rect.height = thickness;
            rect.y += padding/2;
            rect.x -= 2;
            rect.width += 6;
            EditorGUI.DrawRect(rect, color);
        }
    }
}