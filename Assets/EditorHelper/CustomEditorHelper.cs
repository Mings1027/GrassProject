using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorHelper
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

        public static bool DrawFoldoutSection(string title, Action drawContent)
        {
            return DrawFoldoutSection(title, null, drawContent);
        }

        public static bool DrawFoldoutSection(GUIContent titleContent, Action drawContent)
        {
            return DrawFoldoutSection(titleContent.text, null, drawContent);
        }

        public static bool DrawFoldoutSection(string title, string subtitle, Action drawContent)
        {
            var prefKey = $"Foldout_{title}";
            FoldoutStates.TryAdd(title, EditorPrefs.GetBool(prefKey, true));

            var headerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(35, 10, 5, 5),
                margin = new RectOffset(0, 0, 5, 0)
            };

            var fullTitle = new GUIContent($"{title}{(string.IsNullOrEmpty(subtitle) ? "" : " " + subtitle)}");
            var rect = GUILayoutUtility.GetRect(fullTitle, headerStyle, GUILayout.Height(25));
            var arrowRect = new Rect(rect.x + 10, rect.y + 5, 20, 20);

            if (GUI.Button(rect, title, headerStyle))
            {
                FoldoutStates[title] = !FoldoutStates[title];
                EditorPrefs.SetBool(prefKey, FoldoutStates[title]);
                GUI.changed = true;
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleStyle = new GUIStyle(headerStyle)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };

                var subtitleRect = new Rect(
                    rect.x + headerStyle.CalcSize(new GUIContent(title)).x + 5,
                    rect.y,
                    rect.width - headerStyle.CalcSize(new GUIContent(title)).x - 5,
                    rect.height
                );
                GUI.Label(subtitleRect, subtitle, subtitleStyle);
            }

            var arrowContent = EditorGUIUtility.IconContent(FoldoutStates[title] ? "d_dropdown" : "d_forward");
            GUI.Label(arrowRect, arrowContent);

            if (FoldoutStates[title])
            {
                var contentStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(15, 15, 10, 10),
                    margin = new RectOffset(0, 0, 0, 0)
                };

                EditorGUILayout.BeginVertical(contentStyle);
                drawContent?.Invoke();
                EditorGUILayout.EndVertical();
            }

            return FoldoutStates[title];
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