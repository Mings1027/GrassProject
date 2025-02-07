#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class CustomEditorHelper
    {
        private static readonly Dictionary<string, bool> FoldoutStates = new();

        public static bool DrawToggleButton(string text, bool currentState, out bool newState)
        {
            return DrawToggleButton(text, "", currentState, out newState);
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

        public static bool DrawFoldoutSection(string title, string subtitle, Action drawContent)
        {
            return DrawFoldoutSection(new GUIContent(title), subtitle, drawContent);
        }

        public static bool DrawFoldoutSection(string title, Action drawContent)
        {
            return DrawFoldoutSection(new GUIContent(title), null, drawContent);
        }

        public static bool DrawFoldoutSection(GUIContent content, Action drawContent)
        {
            return DrawFoldoutSection(content, null, drawContent);
        }

        public static bool DrawFoldoutSection(GUIContent titleContent, string subtitle, Action drawContent)
        {
            var prefKey = $"Foldout_{titleContent.text}";
            FoldoutStates.TryAdd(titleContent.text, EditorPrefs.GetBool(prefKey, true));

            // Header style setup
            var headerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(35, 10, 5, 5),
                fixedHeight = 25
            };

            EditorGUILayout.BeginHorizontal();
            {
                // Get arrow content
                var arrowContent =
                    EditorGUIUtility.IconContent(FoldoutStates[titleContent.text] ? "d_dropdown" : "d_forward");

                // Combine: arrow + text + icon
                var combinedContent = new GUIContent
                {
                    text = titleContent.text,
                    image = titleContent.image
                };

                if (GUILayout.Button(new GUIContent($"    {combinedContent.text}", combinedContent.image), headerStyle))
                {
                    FoldoutStates[titleContent.text] = !FoldoutStates[titleContent.text];
                    EditorPrefs.SetBool(prefKey, FoldoutStates[titleContent.text]);
                }

                // Draw arrow
                var buttonRect = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(buttonRect.x + 10, buttonRect.y + 4, 20, 20), arrowContent);

                // Subtitle if exists
                if (!string.IsNullOrEmpty(subtitle))
                {
                    var subtitleStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Normal,
                        normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                    };
                    EditorGUILayout.LabelField(subtitle, subtitleStyle);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Content section
            if (FoldoutStates[titleContent.text])
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Space(5);
                drawContent?.Invoke();
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
            }

            return FoldoutStates[titleContent.text];
        }

        public static int IntSlider(string label, string tooltip, int value, int minValue, int maxValue)
        {
            return EditorGUILayout.IntSlider(new GUIContent(label, tooltip), value, minValue, maxValue);
        }

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

        public static (float min, float max) DrawMinMaxSection(string label, float currentMin, float currentMax,
                                                               float minLimit, float maxLimit)
        {
            float tempMin = currentMin;
            float tempMax = currentMax;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider(new GUIContent(label), ref tempMin, ref tempMax, minLimit, maxLimit);

            var roundedMin = (float)Math.Round(tempMin, 2);
            var roundedMax = (float)Math.Round(tempMax, 2);

            EditorGUI.BeginChangeCheck();
            var newMin = EditorGUILayout.FloatField(
                new GUIContent("", $"Min value for {label}"),
                roundedMin,
                GUILayout.Width(50)
            );
            if (EditorGUI.EndChangeCheck())
            {
                tempMin = Mathf.Clamp(newMin, minLimit, tempMax);
            }

            EditorGUI.BeginChangeCheck();
            var newMax = EditorGUILayout.FloatField(
                new GUIContent("", $"Max value for {label}"),
                roundedMax,
                GUILayout.Width(50)
            );
            if (EditorGUI.EndChangeCheck())
            {
                tempMax = Mathf.Clamp(newMax, tempMin, maxLimit);
            }

            EditorGUILayout.EndHorizontal();
            return (tempMin, tempMax);
        }

        // 최적화된 거리 계산 (제곱근 계산 제거)
        public static float SqrDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
#endif