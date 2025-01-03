using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class EventBusEditorHelper
    {
        private static Dictionary<string, bool> _foldoutStates = new();

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
    }
}