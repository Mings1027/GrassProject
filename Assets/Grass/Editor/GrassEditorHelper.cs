using System;
using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
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

    public static class GrassEditorHelper
    {
        // Add to existing class
        private static Dictionary<string, bool> _foldoutStates = new();
        public static Dictionary<string, bool> FoldoutStates => _foldoutStates;

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

        public static bool IsShortcutPressed(KeyType keyType)
        {
            switch (keyType)
            {
                case KeyType.Control:
                    return Event.current.control;
                case KeyType.Alt:
                    return Event.current.alt;
                case KeyType.Shift:
                    return Event.current.shift;
                case KeyType.Command:
                    return Event.current.command;
            }

            return false;
        }

        public static string GetMouseButtonName(MouseButton button)
        {
            return button switch
            {
                MouseButton.LeftMouse => "LeftMouse",
                MouseButton.RightMouse => "RightMouse",
                MouseButton.MiddleMouse => "MiddleMouse",
                _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
            };
        }

        public static string GetShortcutName(KeyType keyType)
        {
            return keyType switch
            {
                KeyType.Control => "Control",
                KeyType.Alt => "Alt",
                KeyType.Shift => "Shift",
                KeyType.Command => "Command",
                _ => throw new ArgumentOutOfRangeException(nameof(keyType), keyType, null)
            };
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

        public static void DrawVerticalLine(Color color, int thickness = 1, int padding = 0)
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(thickness + padding * 2));
            rect.width = thickness;
            rect.x += padding;
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
            var roundedValue = (float)Math.Round(value, 2);
            return EditorGUILayout.Slider(new GUIContent(label, tooltip), roundedValue, minValue, maxValue);
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
            return FloatSlider(label, tooltip, value, minValue, maxValue);
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

        public static void DrawMinMaxSection(string label, ref float min, ref float max, float minLimit, float maxLimit)
        {
            DrawMinMaxSection(label, "", ref min, ref max, minLimit, maxLimit);
        }

        public static void DrawMinMaxSection(string label, string tooltip, ref float min, ref float max, float minLimit,
                                             float maxLimit)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider(new GUIContent(label, tooltip), ref min, ref max, minLimit, maxLimit);

            var roundedMin = (float)Math.Round(min, 2);
            var roundedMax = (float)Math.Round(max, 2);

            EditorGUI.BeginChangeCheck();
            var newMin = EditorGUILayout.FloatField(
                new GUIContent("", $"Min value for {label}"),
                roundedMin,
                GUILayout.Width(50)
            );
            if (EditorGUI.EndChangeCheck())
            {
                min = Mathf.Clamp(newMin, minLimit, max);
            }

            EditorGUI.BeginChangeCheck();
            var newMax = EditorGUILayout.FloatField(
                new GUIContent("", $"Max value for {label}"),
                roundedMax,
                GUILayout.Width(50)
            );
            if (EditorGUI.EndChangeCheck())
            {
                max = Mathf.Clamp(newMax, min, maxLimit);
            }

            EditorGUILayout.EndHorizontal();
        }

        public static bool DrawToggleButton(GUIContent content, bool currentState, out bool newState)
        {
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(0, 0, 5, 5)
            };

            EditorGUILayout.BeginHorizontal();

            // 버튼 클릭 상태를 저장
            var clicked = GUILayout.Button(content, buttonStyle, GUILayout.Height(30));

            // 토글 위치 계산
            var toggleRect = GUILayoutUtility.GetLastRect();
            var checkRect = new Rect(
                toggleRect.x + toggleRect.width - 30,
                toggleRect.y + 7,
                16,
                16
            );

            // 토글 상태 변경 확인
            EditorGUI.BeginChangeCheck();
            newState = EditorGUI.Toggle(checkRect, currentState);
            var toggleChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.EndHorizontal();

            // 버튼이나 토글 중 하나라도 변경되었다면 true 반환
            if (clicked)
            {
                newState = !currentState; // 버튼 클릭 시 토글 상태 반전
                return true;
            }

            return toggleChanged;
        }

        public static bool DrawToggleButton(string text, bool currentState, out bool newState)
        {
            return DrawToggleButton(text, "", currentState, out newState);
        }

        public static bool DrawToggleButton(string text, string tooltip, bool currentState, out bool newState)
        {
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 30, 5, 5),
                margin = new RectOffset(0, 0, 5, 5)
            };

            EditorGUILayout.BeginHorizontal();

            // 버튼에만 툴팁 적용
            var content = new GUIContent(text, tooltip);
            var clicked = GUILayout.Button(content, buttonStyle, GUILayout.Height(30));

            var toggleRect = GUILayoutUtility.GetLastRect();
            var checkRect = new Rect(
                toggleRect.x + toggleRect.width - 30,
                toggleRect.y + 7,
                16,
                16
            );

            EditorGUI.BeginChangeCheck();
            newState = EditorGUI.Toggle(checkRect, currentState);
            var toggleChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.EndHorizontal();

            if (clicked)
            {
                newState = !currentState;
                return true;
            }

            return toggleChanged;
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
                    DrawCellWireframe(cellCenter, cellSize);
                }
            }
        }

        private static void DrawCellWireframe(Vector3 center, float size)
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

        public static void DrawLODPreview(float lowQualityDistance, float mediumQualityDistance, out float newLow,
                                          out float newMedium)
        {
            newLow = lowQualityDistance;
            newMedium = mediumQualityDistance;

            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(40));
            var padding = 20f;
            rect.x += padding;
            rect.width -= padding * 2;

            // 배경
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // LOD 영역
            var width = rect.width;
            var lowWidth = width * lowQualityDistance;
            var mediumWidth = width * (mediumQualityDistance - lowQualityDistance);
            var highWidth = width * (1 - mediumQualityDistance);

            var lowRect = new Rect(rect.x, rect.y, lowWidth, rect.height);
            var mediumRect = new Rect(rect.x + lowWidth, rect.y, mediumWidth, rect.height);
            var highRect = new Rect(rect.x + lowWidth + mediumWidth, rect.y, highWidth, rect.height);

            EditorGUI.DrawRect(lowRect, new Color(0.8f, 0.3f, 0.3f, 0.8f));
            EditorGUI.DrawRect(mediumRect, new Color(0.3f, 0.8f, 0.3f, 0.8f));
            EditorGUI.DrawRect(highRect, new Color(0.3f, 0.3f, 0.8f, 0.8f));

            // 드래그 핸들
            var handleWidth = 10f;
            var handleHeight = rect.height;

            var lowHandle = new Rect(rect.x + lowWidth - handleWidth / 2, rect.y, handleWidth, handleHeight);
            var mediumHandle = new Rect(rect.x + lowWidth + mediumWidth - handleWidth / 2, rect.y, handleWidth,
                handleHeight);

            EditorGUI.DrawRect(lowHandle, new Color(1f, 1f, 1f, 0.5f));
            EditorGUI.DrawRect(mediumHandle, new Color(1f, 1f, 1f, 0.5f));

            // 드래그 처리
            var controlID = GUIUtility.GetControlID(FocusType.Passive);
            var evt = Event.current;

            switch (evt.GetTypeForControl(controlID))
            {
                case EventType.MouseDown when evt.button == 0:
                    if (lowHandle.Contains(evt.mousePosition) || mediumHandle.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        evt.Use();
                    }

                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlID:
                    var normalizedX = (evt.mousePosition.x - rect.x) / rect.width;
                    normalizedX = Mathf.Round(normalizedX * 10f) / 10f;

                    if (Vector2.Distance(evt.mousePosition, lowHandle.center) <
                        Vector2.Distance(evt.mousePosition, mediumHandle.center))
                    {
                        newLow = Mathf.Clamp(normalizedX, 0, newMedium);
                    }
                    else
                    {
                        newMedium = Mathf.Clamp(normalizedX, newLow, 1);
                    }

                    GUI.changed = true;
                    evt.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }

                    break;
            }

            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            EditorGUI.LabelField(lowRect, "Low", style);
            EditorGUI.LabelField(mediumRect, "Medium", style);
            EditorGUI.LabelField(highRect, "High", style);

            var percentStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = Color.white }
            };

            EditorGUI.LabelField(lowRect, $"{lowQualityDistance:P0}", percentStyle);
            EditorGUI.LabelField(mediumRect, $"{mediumQualityDistance:P0}", percentStyle);
        }

        public static bool DrawFoldoutSection(string title, Action drawContent)
        {
            _foldoutStates.TryAdd(title, true);

            var headerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(35, 10, 5, 5),
                margin = new RectOffset(0, 0, 5, 0)
            };

            var contentStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(15, 15, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };


            // Draw header with arrow icon
            var rect = GUILayoutUtility.GetRect(new GUIContent(title), headerStyle, GUILayout.Height(25));
            var arrowRect = new Rect(rect.x + 10, rect.y + 5, 20, 20);

            if (GUI.Button(rect, title, headerStyle))
            {
                _foldoutStates[title] = !_foldoutStates[title];
                GUI.changed = true;
            }

            // Draw arrow
            var arrowContent = EditorGUIUtility.IconContent(_foldoutStates[title] ? "d_dropdown" : "d_forward");
            GUI.Label(arrowRect, arrowContent);

            // Draw content if expanded
            if (_foldoutStates[title])
            {
                EditorGUILayout.BeginVertical(contentStyle);
                drawContent?.Invoke();
                EditorGUILayout.EndVertical();
            }

            return _foldoutStates[title];
        }

        public static void DrawSeasonSettingsTable(Rect position, GrassSettingSO grassSetting,
                                                   GrassSeasonManager grassSeasonManager)
        {
            var labelWidth = 40f;
            var cellWidth = (position.width - labelWidth) / 4;
            var rowHeight = EditorGUIUtility.singleLineHeight;
            var padding = 5f;
            var fieldWidth = cellWidth - padding;
            var valueFieldWidth = 35f; // 값 필드의 너비
            var sliderWidth = fieldWidth - valueFieldWidth - 2f; // 슬라이더 너비

            // Headers (기존 코드와 동일)
            var seasonLabels = new[] { "Winter", "Spring", "Summer", "Autumn" };
            for (int i = 0; i < seasonLabels.Length; i++)
            {
                var rect = new Rect(position.x + labelWidth + (cellWidth * i), position.y, fieldWidth, rowHeight);
                EditorGUI.LabelField(rect, seasonLabels[i], EditorStyles.boldLabel);
            }

            var y = position.y + rowHeight + padding;
            var settings = new[]
            {
                grassSetting.winterSettings, grassSetting.springSettings,
                grassSetting.summerSettings, grassSetting.autumnSettings
            };

            EditorGUI.BeginChangeCheck();
            EditorGUI.LabelField(new Rect(position.x, y, labelWidth, rowHeight), "Color", EditorStyles.boldLabel);
            for (int i = 0; i < settings.Length; i++)
            {
                var rect = new Rect(position.x + labelWidth + (cellWidth * i) + padding, y, fieldWidth, rowHeight);
                settings[i].seasonColor = EditorGUI.ColorField(rect, settings[i].seasonColor);
            }

            if (EditorGUI.EndChangeCheck())
            {
                grassSeasonManager.UpdateShaderData();
            }

            y += rowHeight + padding;

            EditorGUI.BeginChangeCheck();
            EditorGUI.LabelField(new Rect(position.x, y, labelWidth, rowHeight), "Width", EditorStyles.boldLabel);
            for (int i = 0; i < settings.Length; i++)
            {
                var startX = position.x + labelWidth + (cellWidth * i) + padding;
                var sliderRect = new Rect(startX, y, sliderWidth, rowHeight);
                var fieldRect = new Rect(startX + sliderWidth + 2f, y, valueFieldWidth, rowHeight);

                settings[i].width = GUI.HorizontalSlider(sliderRect, settings[i].width, 0.1f, 2f);
                settings[i].width = EditorGUI.FloatField(fieldRect, settings[i].width);
                settings[i].width = Mathf.Clamp(settings[i].width, 0.1f, 2f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                grassSeasonManager.UpdateShaderData();
            }

            y += rowHeight + padding;

            EditorGUI.BeginChangeCheck();
            EditorGUI.LabelField(new Rect(position.x, y, labelWidth, rowHeight), "Height", EditorStyles.boldLabel);
            for (int i = 0; i < settings.Length; i++)
            {
                var startX = position.x + labelWidth + (cellWidth * i) + padding;
                var sliderRect = new Rect(startX, y, sliderWidth, rowHeight);
                var fieldRect = new Rect(startX + sliderWidth + 2f, y, valueFieldWidth, rowHeight);

                settings[i].height = GUI.HorizontalSlider(sliderRect, settings[i].height, 0.1f, 2f);
                settings[i].height = EditorGUI.FloatField(fieldRect, settings[i].height);
                settings[i].height = Mathf.Clamp(settings[i].height, 0.1f, 2f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                grassSeasonManager.UpdateShaderData();
            }
        }

        public static Vector3 GetRandomColor(GrassToolSettingSo toolSettings)
        {
            var baseColor = toolSettings.BrushColor;
            var newRandomCol = new Color(
                baseColor.r + Random.Range(0, toolSettings.RangeR),
                baseColor.g + Random.Range(0, toolSettings.RangeG),
                baseColor.b + Random.Range(0, toolSettings.RangeB),
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
        }

        public static bool IsLayerInMask(int layer, LayerMask layerMask)
        {
            return ((1 << layer) & layerMask.value) != 0;
        }

        public static bool IsLayerInMask(int layer, int layerMask)
        {
            return ((1 << layer) & layerMask) != 0;
        }

        public static bool IsNotLayerInMask(int layer, LayerMask layerMask)
        {
            return ((1 << layer) & layerMask.value) == 0;
        }

        public static bool IsNotLayerInMask(int layer, int layerMask)
        {
            return ((1 << layer) & layerMask) == 0;
        }
    }
}