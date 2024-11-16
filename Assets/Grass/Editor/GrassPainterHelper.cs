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

    public enum GenerateTab
    {
        Basic,
        TerrainLayers,
        Advanced
    }

    public abstract class GrassPainterHelper
    {
        public static Bounds? GetObjectBounds(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                var meshBounds = meshFilter.sharedMesh.bounds;
                return new Bounds(
                    obj.transform.TransformPoint(meshBounds.center),
                    Vector3.Scale(meshBounds.size, obj.transform.lossyScale)
                );
            }

            if (obj.TryGetComponent(out Terrain terrain))
            {
                var size = terrain.terrainData.size;
                return new Bounds(
                    terrain.transform.position + size * 0.5f,
                    size
                );
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
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            rect.height = thickness;
            rect.y += (float)padding / 2;
            rect.x -= 2;
            rect.width += 6;
            EditorGUI.DrawRect(rect, color);
        }

        public static bool ToggleWithLabel(string label, string tooltip, bool value, float toggleWidth = 20f)
        {
            return EditorGUILayout.Toggle(new GUIContent(label, tooltip), value, GUILayout.Width(toggleWidth));
        }

        /// <summary>
        /// Creates an integer slider with tooltip
        /// </summary>
        /// <param name="label">Slider label text</param>
        /// <param name="tooltip">Tooltip text shown on hover</param>
        /// <param name="value">Current value</param>
        /// <param name="minValue">Minimum allowed value</param>
        /// <param name="maxValue">Maximum allowed value</param>
        /// <returns>The modified integer value</returns>
        public static int IntSlider(string label, string tooltip, int value, int minValue, int maxValue)
        {
            return EditorGUILayout.IntSlider(new GUIContent(label, tooltip), value, minValue, maxValue);
        }

        /// <summary>
        /// Creates a float slider with tooltip
        /// </summary>
        /// <param name="label">Slider label text</param>
        /// <param name="tooltip">Tooltip text shown on hover</param>
        /// <param name="value">Current value</param>
        /// <param name="minValue">Minimum allowed value</param>
        /// <param name="maxValue">Maximum allowed value</param>
        /// <returns>The modified float value</returns>
        public static float FloatSlider(string label, string tooltip, float value, float minValue, float maxValue)
        {
            return EditorGUILayout.Slider(new GUIContent(label, tooltip), value, minValue, maxValue);
        }

        /// <summary>
        /// Creates a float slider with tooltip and an additional header label
        /// </summary>
        /// <param name="headerLabel">Header label text</param>
        /// <param name="label">Slider label text</param>
        /// <param name="tooltip">Tooltip text shown on hover</param>
        /// <param name="value">Current value</param>
        /// <param name="minValue">Minimum allowed value</param>
        /// <param name="maxValue">Maximum allowed value</param>
        /// <returns>The modified float value</returns>
        public static float FloatSliderWithHeader(string headerLabel, string label, string tooltip, float value,
                                                  float minValue, float maxValue)
        {
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);
            return EditorGUILayout.Slider(new GUIContent(label, tooltip), value, minValue, maxValue);
        }

        /// <summary>
        /// Creates an integer slider with tooltip and an additional header label
        /// </summary>
        /// <param name="headerLabel">Header label text</param>
        /// <param name="label">Slider label text</param>
        /// <param name="tooltip">Tooltip text shown on hover</param>
        /// <param name="value">Current value</param>
        /// <param name="minValue">Minimum allowed value</param>
        /// <param name="maxValue">Maximum allowed value</param>
        /// <returns>The modified integer value</returns>
        public static int IntSliderWithHeader(string headerLabel, string label, string tooltip, int value, int minValue,
                                              int maxValue)
        {
            EditorGUILayout.LabelField(headerLabel, EditorStyles.boldLabel);
            return EditorGUILayout.IntSlider(new GUIContent(label, tooltip), value, minValue, maxValue);
        }

        public static void ShowAngle(float value)
        {
            EditorGUILayout.LabelField($"{value * 90f:F1}°", GUILayout.Width(50));
        }
    }
}