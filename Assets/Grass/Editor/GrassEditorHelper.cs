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

        public static void ShowAngle(float value)
        {
            EditorGUILayout.LabelField($"{value * 90f:F1}°", GUILayout.Width(50));
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
    }
}