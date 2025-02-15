using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EventBusSystem.Editor
{
    public static class EditorHelper
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
    }
}