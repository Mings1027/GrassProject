using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(SeasonEffectVolume))]
    public class SeasonEffectVolumeEditor : UnityEditor.Editor
    {
        private SerializedProperty _showGizmos;
        private SerializedProperty _overrideGlobalSettings;
        private SerializedProperty _seasonValue;
        private SerializedProperty _winterSettings;
        private SerializedProperty _springSettings;
        private SerializedProperty _summerSettings;
        private SerializedProperty _autumnSettings;
        private SerializedProperty _seasonRange; // 추가: 계절 범위 프로퍼티

        private bool _seasonSettingsExpanded;

        private GrassComputeScript _grassCompute;

        private readonly GUIContent[] _seasonLabels =
        {
            new("Winter Settings"),
            new("Spring Settings"),
            new("Summer Settings"),
            new("Autumn Settings")
        };

        private void OnEnable()
        {
            _showGizmos = serializedObject.FindProperty("showGizmos");
            _overrideGlobalSettings = serializedObject.FindProperty("overrideGlobalSettings");
            _seasonValue = serializedObject.FindProperty("seasonValue");
            _winterSettings = serializedObject.FindProperty("winterSettings");
            _springSettings = serializedObject.FindProperty("springSettings");
            _summerSettings = serializedObject.FindProperty("summerSettings");
            _autumnSettings = serializedObject.FindProperty("autumnSettings");
            _seasonRange = serializedObject.FindProperty("seasonRange"); // 추가: 계절 범위 초기화

            _grassCompute = FindAnyObjectByType<GrassComputeScript>();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Gizmos 토글 버튼
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Show Gizmos" };
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showGizmos.boolValue, out var showGizmosState))
            {
                _showGizmos.boolValue = showGizmosState;
                serializedObject.ApplyModifiedProperties();
                UpdateController();
            }

            EditorGUILayout.Space(5);

            // Override Settings 토글 버튼
            var settingsContent = new GUIContent(EditorIcons.Settings) { text = "Override Global Settings" };
            if (GrassEditorHelper.DrawToggleButton(settingsContent, _overrideGlobalSettings.boolValue,
                    out var overrideState))
            {
                _overrideGlobalSettings.boolValue = overrideState;
                serializedObject.ApplyModifiedProperties();
                UpdateController();
            }

            if (_overrideGlobalSettings.boolValue)
            {
                EditorGUILayout.Space(5);
                DrawSeasonValueControl();
                EditorGUILayout.Space(5);
                DrawSeasonSettings();
            }
            else
            {
                DrawSeasonValueControl();
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Using global settings from GrassSettingSO", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSeasonValueControl()
        {
            if (!_overrideGlobalSettings.boolValue)
            {
                var grassSettings = _grassCompute?.GrassSetting;
                if (!grassSettings) return;

                DrawSeasonSlider(grassSettings.seasonRangeMin, grassSettings.seasonRangeMax);
            }
            else
            {
                // Season Range
                var rangeMin = _seasonRange.vector2Value.x;
                var rangeMax = _seasonRange.vector2Value.y;

                EditorGUI.BeginChangeCheck();
                GrassEditorHelper.DrawMinMaxSection("Season Range", ref rangeMin, ref rangeMax, 0f, 4f);
                if (EditorGUI.EndChangeCheck())
                {
                    _seasonRange.vector2Value = new Vector2(rangeMin, rangeMax);
                    UpdateController();
                }

                EditorGUILayout.Space(5);

                // Season Value Slider
                DrawSeasonSlider(rangeMin, rangeMax);
            }
        }

        private void DrawSeasonSlider(float minRange, float maxRange)
        {
            EditorGUI.BeginChangeCheck();
            var roundedValue = (float)System.Math.Round(_seasonValue.floatValue, 2); // 소수점 둘째자리까지 반올림
            var newValue = EditorGUILayout.FloatField("Season Value", roundedValue);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonValue.floatValue = Mathf.Clamp(newValue, minRange, maxRange);
                UpdateController();
            }

            // Slider
            EditorGUI.BeginChangeCheck();
            newValue = GUILayout.HorizontalSlider(_seasonValue.floatValue, minRange, maxRange);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonValue.floatValue = newValue;
                UpdateController();
            }

            // Add more space before season labels
            EditorGUILayout.Space(8);

            // Season Labels
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
            DrawSeasonLabels(rect, minRange, maxRange);

            // Add space after labels
            EditorGUILayout.Space(5);
        }

        private static void DrawSeasonLabels(Rect rect, float minRange, float maxRange)
        {
            var visibleSeasons = new List<(string label, float position)>();

            // 표시할 계절 결정
            if (minRange <= 1f && maxRange >= 0f) visibleSeasons.Add(("Winter", 0f));
            if (minRange <= 2f && maxRange >= 1f) visibleSeasons.Add(("Spring", 1f));
            if (minRange <= 3f && maxRange >= 2f) visibleSeasons.Add(("Summer", 2f));
            if (minRange <= 4f && maxRange >= 3f) visibleSeasons.Add(("Autumn", 3f));
            if (maxRange >= 4f && minRange <= 4f) visibleSeasons.Add(("Winter", 4f));

            if (visibleSeasons.Count == 0) return;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            // 계절 수에 따른 위치 조정
            for (var i = 0; i < visibleSeasons.Count; i++)
            {
                float xPosition;
                if (visibleSeasons.Count == 1)
                {
                    xPosition = rect.center.x - 20; // 중앙
                }
                else
                {
                    var ratio = (float)i / (visibleSeasons.Count - 1);
                    xPosition = Mathf.Lerp(rect.x, rect.x + rect.width - 40, ratio);
                }

                var labelRect = new Rect(xPosition, rect.y, 40, rect.height);
                EditorGUI.LabelField(labelRect, visibleSeasons[i].label, style);
            }
        }

        private void DrawSeasonSettings()
        {
            EditorGUILayout.Space(10);
   
            if (GrassEditorHelper.DrawToggleButton(
                    "Season Settings", 
                    "Configure grass behavior and appearance for each season",
                    _seasonSettingsExpanded,
                    out var expanded))
            {
                _seasonSettingsExpanded = expanded;
            }

            if (_seasonSettingsExpanded)
            {
                // EditorGUI.indentLevel++;

                DrawSeasonSetting(_winterSettings, 0);
                DrawSeasonSetting(_springSettings, 1);
                DrawSeasonSetting(_summerSettings, 2);
                DrawSeasonSetting(_autumnSettings, 3);

                // EditorGUI.indentLevel--;
            }
        }

        private void DrawSeasonSetting(SerializedProperty seasonSetting, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(_seasonLabels[index], EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(seasonSetting.FindPropertyRelative("seasonColor"),
                new GUIContent("Color"));

            EditorGUILayout.PropertyField(seasonSetting.FindPropertyRelative("width"),
                new GUIContent("Width Scale"));

            EditorGUILayout.PropertyField(seasonSetting.FindPropertyRelative("height"),
                new GUIContent("Height Scale"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void UpdateController()
        {
            var controller = FindAnyObjectByType<GrassSeasonController>();
            controller.UpdateSeasonVolumes();
        }
    }
}