using System;
using System.Collections.Generic;
using System.IO;
using Grass.GrassScripts;
using UnityEditor;
using UnityEditorInternal;
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

        public static void FloatSlider(ref float value, string label, string tooltip, float minValue, float maxValue)
        {
            var roundedValue = (float)Math.Round(value, 2);
            value = EditorGUILayout.Slider(new GUIContent(label, tooltip), roundedValue, minValue, maxValue);
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

        // GrassEditorHelper.cs
        public static bool DrawFoldoutSection(string title, Action drawContent)
        {
            return DrawFoldoutSection(new GUIContent(title), drawContent);
        }

        public static bool DrawFoldoutSection(GUIContent titleContent, Action drawContent)
        {
            var title = titleContent.text;
            var prefKey = $"GrassTool_Foldout_{title}";
            _foldoutStates.TryAdd(title, EditorPrefs.GetBool(prefKey, true));

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

            var rect = GUILayoutUtility.GetRect(titleContent, headerStyle, GUILayout.Height(25));
            var arrowRect = new Rect(rect.x + 10, rect.y + 5, 20, 20);

            if (GUI.Button(rect, titleContent, headerStyle))
            {
                _foldoutStates[title] = !_foldoutStates[title];
                EditorPrefs.SetBool(prefKey, _foldoutStates[title]);
                GUI.changed = true;
            }

            var arrowContent = EditorGUIUtility.IconContent(_foldoutStates[title] ? "d_dropdown" : "d_forward");
            GUI.Label(arrowRect, arrowContent);

            if (_foldoutStates[title])
            {
                EditorGUILayout.BeginVertical(contentStyle);
                drawContent?.Invoke();
                EditorGUILayout.EndVertical();
            }

            return _foldoutStates[title];
        }

        private static ReorderableList _seasonList;

        private static void CreateNewSeasonAsset(DefaultAsset targetFolder)
        {
            if (targetFolder == null)
            {
                EditorUtility.DisplayDialog("Invalid Path", "Please select a folder first.", "OK");
                return;
            }

            // 새로운 SeasonSettings 에셋 생성
            var asset = ScriptableObject.CreateInstance<SeasonSettings>();
            asset.seasonType = (SeasonType)Random.Range(0, System.Enum.GetValues(typeof(SeasonType)).Length);
            asset.seasonColor = Color.white;
            asset.width = asset.height = 1f;

            // 선택된 폴더 경로 가져오기
            var folderPath = AssetDatabase.GetAssetPath(targetFolder);

            // 기본 파일명 설정
            var defaultName = $"New Season Settings";

            // 에셋 생성 다이얼로그 표시
            ProjectWindowUtil.CreateAsset(asset, $"{folderPath}/{defaultName}.asset");
        }

        public static void DrawSeasonSettings(List<SeasonSettings> seasonSettings, GrassSettingSO grassSetting)
        {
            // 폴더 선택 필드
            grassSetting.seasonSettingFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("Season Settings Path",
                    "Select a folder to store season assets.\n" +
                    "New season settings will be created here when using 'Create New Season Asset'. "),
                grassSetting.seasonSettingFolder,
                typeof(DefaultAsset),
                false);

            EditorGUILayout.Space(5);

            if (_seasonList == null || _seasonList.list != seasonSettings)
            {
                _seasonList = new ReorderableList(seasonSettings, typeof(SeasonSettings), true, true, true, true);

                // 헤더 그리기
                _seasonList.drawHeaderCallback = rect =>
                {
                    if (seasonSettings.Count == 0)
                    {
                        EditorGUI.LabelField(rect, "Season Settings (No Seasons)");
                        return;
                    }

                    var sequence = new System.Text.StringBuilder();
                    for (int i = 0; i < seasonSettings.Count; i++)
                    {
                        var season = seasonSettings[i];
                        if (season != null)
                        {
                            if (i > 0) sequence.Append(" → ");
                            sequence.Append(season.seasonType.ToString());
                        }
                    }

                    if (seasonSettings[0] != null)
                    {
                        sequence.Append(" → ");
                        sequence.Append(seasonSettings[0].seasonType.ToString());
                    }

                    EditorGUI.LabelField(rect, sequence.ToString());
                };

                // 각 요소 그리기
                _seasonList.drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var settings = seasonSettings[index];
                    float padding = 2f;
                    rect.y += padding;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // 첫 줄: SeasonType 레이블 + Object Field
                    var labelWidth = 70f;
                    var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                    EditorGUI.LabelField(labelRect, settings != null ? settings.seasonType.ToString() : "Empty");

                    var objectRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
                    settings = (SeasonSettings)EditorGUI.ObjectField(objectRect, settings, typeof(SeasonSettings),
                        false);

                    if (settings != null)
                    {
                        // 두 번째 줄: Type + Color
                        rect.y += EditorGUIUtility.singleLineHeight + padding;
                        float labelWidth2 = 40f;
                        var halfWidth = (rect.width - padding) / 2;

                        // Type
                        var typeRect = new Rect(rect.x, rect.y, halfWidth, rect.height);
                        EditorGUI.LabelField(new Rect(typeRect.x, typeRect.y, labelWidth2, typeRect.height), "Type");
                        typeRect.x += labelWidth2;
                        typeRect.width -= labelWidth2;
                        settings.seasonType = (SeasonType)EditorGUI.EnumPopup(typeRect, settings.seasonType);

                        // Color
                        var colorRect = new Rect(rect.x + halfWidth + padding, rect.y, halfWidth, rect.height);
                        EditorGUI.LabelField(new Rect(colorRect.x, colorRect.y, labelWidth2, colorRect.height),
                            "Color");
                        colorRect.x += labelWidth2;
                        colorRect.width -= labelWidth2;
                        settings.seasonColor = EditorGUI.ColorField(colorRect, settings.seasonColor);

                        // 세 번째 줄: Width + Height 슬라이더
                        rect.y += EditorGUIUtility.singleLineHeight + padding;

                        // Width
                        var widthRect = new Rect(rect.x, rect.y, halfWidth, rect.height);
                        EditorGUI.LabelField(new Rect(widthRect.x, widthRect.y, labelWidth2, widthRect.height),
                            "Width");
                        widthRect.x += labelWidth2;
                        widthRect.width -= labelWidth2;
                        settings.width = EditorGUI.Slider(widthRect, settings.width, 0, 2);

                        // Height
                        var heightRect = new Rect(rect.x + halfWidth + padding, rect.y, halfWidth, rect.height);
                        EditorGUI.LabelField(new Rect(heightRect.x, heightRect.y, labelWidth2, heightRect.height),
                            "Height");
                        heightRect.x += labelWidth2;
                        heightRect.width -= labelWidth2;
                        settings.height = EditorGUI.Slider(heightRect, settings.height, 0, 2);
                    }

                    seasonSettings[index] = settings;
                };

                // 요소 높이 설정
                _seasonList.elementHeightCallback = index =>
                {
                    float baseHeight = EditorGUIUtility.singleLineHeight;
                    float padding = 2f;
                    float totalHeight = baseHeight + padding * 2; // 기본 한 줄 + 패딩

                    if (seasonSettings[index] != null)
                    {
                        totalHeight += (baseHeight + padding) * 2; // 추가 두 줄 + 패딩
                    }

                    return totalHeight;
                };

                // 새 요소 추가
                _seasonList.onAddCallback = list => { seasonSettings.Add(null); };

                // 요소 제거
                _seasonList.onRemoveCallback = list => { seasonSettings.RemoveAt(list.index); };
            }

            _seasonList.DoLayoutList();

            EditorGUILayout.Space(5);

            // Create Asset 버튼
            if (GUILayout.Button("Create New Season Asset", GUILayout.Height(25)))
            {
                CreateNewSeasonAsset(grassSetting.seasonSettingFolder);
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

        public static bool IsLayerInMask(int layer, LayerMask layerMask) => ((1 << layer) & layerMask.value) != 0;

        public static bool IsLayerInMask(int layer, int layerMask) => ((1 << layer) & layerMask) != 0;

        public static bool IsNotLayerInMask(int layer, LayerMask layerMask) => ((1 << layer) & layerMask.value) == 0;

        public static bool IsNotLayerInMask(int layer, int layerMask) => ((1 << layer) & layerMask) == 0;
    }
}