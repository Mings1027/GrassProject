using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Grass.Editor
{
    public enum GrassEditorTab
    {
        PaintEdit,
        Modify,
        Generate,
        GeneralSettings,
    }

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
        Color,
        WidthHeight,
        Both
    }

    public enum GenerateTab
    {
        Basic,
        TerrainLayers,
    }

    public static class GrassPainterHelper
    {
        public static Bounds? GetObjectBounds(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                var meshBounds = meshFilter.sharedMesh.bounds;
                return new Bounds(
                    obj.transform.TransformPoint(meshBounds.center),
                    Vector3.Scale(meshBounds.size, obj.transform.localScale)
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

        public static Vector3 GetRandomBarycentricCoordWithSeed(System.Random random)
        {
            var r1 = Mathf.Sqrt((float)random.NextDouble());
            var r2 = (float)random.NextDouble();
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

        public static void DrawCellWireframe(Vector3 center, float size)
        {
            var halfSize = size * 0.5f;
            var points = new[]
            {
                center + new Vector3(-halfSize, -halfSize, -halfSize), // 0 bottom
                center + new Vector3(halfSize, -halfSize, -halfSize), // 1
                center + new Vector3(halfSize, -halfSize, halfSize), // 2
                center + new Vector3(-halfSize, -halfSize, halfSize), // 3
                center + new Vector3(-halfSize, halfSize, -halfSize), // 4 top
                center + new Vector3(halfSize, halfSize, -halfSize), // 5
                center + new Vector3(halfSize, halfSize, halfSize), // 6
                center + new Vector3(-halfSize, halfSize, halfSize) // 7
            };

            // Draw bottom square
            Handles.DrawLine(points[0], points[1]);
            Handles.DrawLine(points[1], points[2]);
            Handles.DrawLine(points[2], points[3]);
            Handles.DrawLine(points[3], points[0]);

            // Draw top square
            Handles.DrawLine(points[4], points[5]);
            Handles.DrawLine(points[5], points[6]);
            Handles.DrawLine(points[6], points[7]);
            Handles.DrawLine(points[7], points[4]);

            // Draw vertical lines
            Handles.DrawLine(points[0], points[4]);
            Handles.DrawLine(points[1], points[5]);
            Handles.DrawLine(points[2], points[6]);
            Handles.DrawLine(points[3], points[7]);
        }

        public static bool ToggleButton(string label, bool value, float width = 100f)
        {
            // 버튼의 기본 색상 설정
            var enabledColor = new Color(0.3f, 0.8f, 0.3f, 1f); // 초록빛 색상
            var disabledColor = new Color(0.8f, 0.3f, 0.3f, 1f); // 빨간빛 색상

            // 현재 상태에 따른 배경 텍스처 생성
            var bgColor = value ? enabledColor : disabledColor;
            var bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, bgColor);
            bgTexture.Apply();

            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 25,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    background = bgTexture,
                    textColor = Color.white // 텍스트는 항상 흰색으로
                },
                hover = new GUIStyleState
                {
                    background = bgTexture,
                    textColor = Color.white
                }
            };

            var buttonContent = new GUIContent(label);

            bool result = value;
            if (GUILayout.Button(buttonContent, style, GUILayout.Width(width)))
            {
                result = !value;
                GUI.changed = true;
            }

            // 텍스처 정리
            if (Event.current.type == EventType.Repaint)
            {
                Object.DestroyImmediate(bgTexture);
            }

            return result;
        }

        // 툴팁이 있는 버전
        public static bool ToggleButton(string label, string tooltip, bool value, float width = 100f)
        {
            // 버튼의 기본 색상 설정
            var enabledColor = new Color(0.3f, 0.8f, 0.3f, 1f); // 초록빛 색상
            var disabledColor = new Color(0.8f, 0.3f, 0.3f, 1f); // 빨간빛 색상

            // 현재 상태에 따른 배경 텍스처 생성
            var bgColor = value ? enabledColor : disabledColor;
            var bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, bgColor);
            bgTexture.Apply();

            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 25,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    background = bgTexture,
                    textColor = Color.white // 텍스트는 항상 흰색으로
                },
                hover = new GUIStyleState
                {
                    background = bgTexture,
                    textColor = Color.white
                }
            };

            var buttonContent = new GUIContent(label, tooltip);

            bool result = value;
            if (GUILayout.Button(buttonContent, style, GUILayout.Width(width)))
            {
                result = !value;
                GUI.changed = true;
            }

            // 텍스처 정리
            if (Event.current.type == EventType.Repaint)
            {
                Object.DestroyImmediate(bgTexture);
            }

            return result;
        }

        public static void DrawGridHandles(SpatialGrid spatialGrid, Ray mousePointRay, GrassToolSettingSo toolSettings)
        {
            if (spatialGrid == null) return;
            var cellSize = spatialGrid.CellSize;

            if (!Physics.Raycast(mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            var hitCell = spatialGrid.WorldToCell(hit.point);
            var cellRadius = Mathf.CeilToInt(toolSettings.BrushSize / cellSize / 2);

            var notActiveCellColor = new Color(1f, 0f, 0f, 0.3f);
            var activeCellColor = new Color(0f, 1f, 0f, 1f);

            // 브러시 범위 내 모든 셀 순회 (Y축은 hit point의 높이로 고정)
            for (var x = -cellRadius; x <= cellRadius; x++)
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                // 원형 브러시 범위 체크 (Y축 제외하고 X,Z 평면에서만 체크)
                if (x * x + z * z > cellRadius * cellRadius)
                    continue;

                var checkCell = new Vector3Int(hitCell.x + x, hitCell.y, hitCell.z + z);
                var cellWorldPos = spatialGrid.CellToWorld(checkCell);
                var cellCenter = cellWorldPos + Vector3.one * (cellSize * 0.5f);

                // XZ 평면에서의 거리만 체크
                var horizontalDistance = new Vector2(cellCenter.x - hit.point.x, cellCenter.z - hit.point.z).magnitude;
                if (horizontalDistance <= toolSettings.BrushSize)
                {
                    var key = SpatialGrid.GetKey(checkCell.x, checkCell.y, checkCell.z);
                    var hasGrass = spatialGrid.HasAnyObject(key);

                    Handles.color = hasGrass ? activeCellColor : notActiveCellColor;
                    GrassPainterHelper.DrawCellWireframe(cellCenter, cellSize);
                }
            }
        }
    }
}