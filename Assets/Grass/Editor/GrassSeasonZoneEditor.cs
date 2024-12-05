using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonZone)), CanEditMultipleObjects]
    public class GrassSeasonZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _showGizmos;
        private SerializedProperty _overrideGlobalSettings;
        private SerializedProperty _winterSettings;
        private SerializedProperty _springSettings;
        private SerializedProperty _summerSettings;
        private SerializedProperty _autumnSettings;

        private SerializedProperty _seasonValue;
        private SerializedProperty _seasonRange;

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
            _winterSettings = serializedObject.FindProperty("winterSettings");
            _springSettings = serializedObject.FindProperty("springSettings");
            _summerSettings = serializedObject.FindProperty("summerSettings");
            _autumnSettings = serializedObject.FindProperty("autumnSettings");

            _seasonValue = serializedObject.FindProperty("seasonValue");
            _seasonRange = serializedObject.FindProperty("seasonRange");

            _grassCompute = FindAnyObjectByType<GrassComputeScript>();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGizmosControl();
            EditorGUILayout.Space(5);
            DrawOverrideSettingsSection();

            if (_overrideGlobalSettings.boolValue)
            {
                EditorGUILayout.Space(10);
                DrawSeasonSettingsSection();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGizmosControl()
        {
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Show Gizmos" };
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showGizmos.boolValue, out var showGizmosState))
            {
                _showGizmos.boolValue = showGizmosState;
                serializedObject.ApplyModifiedProperties();
                UpdateController();
            }
        }

        private void DrawOverrideSettingsSection()
        {
            var overrideContent = new GUIContent(EditorIcons.Settings)
            {
                text = "Override Global Season Settings",
                tooltip =
                    "When enabled, this zone will use its own season settings instead of the global settings\n\n" +
                    "• From/To Seasons: Define custom season transitions\n" +
                    "• Season Settings: Set unique colors, width, and height\n" +
                    "• Independent Control: Changes only affect this specific zone"
            };

            if (_overrideGlobalSettings.boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    DrawOverrideToggle(overrideContent);
                    EditorGUILayout.Space(5);
                    DrawSeasonValueControl();
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                DrawOverrideToggle(overrideContent);
                DrawSeasonValueControl();
                EditorGUILayout.Space(15);

                EditorGUILayout.BeginHorizontal();
                var iconContent = new GUIContent(EditorIcons.Info.image);
                EditorGUILayout.LabelField(iconContent, GUILayout.Width(20));
                EditorGUILayout.LabelField("This zone follows Global Season Settings");
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button(new GUIContent("Open Global Season Settings"), GUILayout.Height(25)))
                {
                    var window = EditorWindow.GetWindow<GrassPainterWindow>();
                    window.GrassEditorTab = GrassEditorTab.GeneralSettings;
                    GrassPainterWindow.Open();
                    GrassEditorHelper.FoldoutStates["Global Season Settings"] = true;
                    EditorApplication.delayCall += () =>
                    {
                        var scrollPos = window.ScrollPos;
                        scrollPos.y = 0;
                        window.ScrollPos = scrollPos;
                    };
                }
            }
        }

        private void DrawOverrideToggle(GUIContent content)
        {
            if (GrassEditorHelper.DrawToggleButton(content, _overrideGlobalSettings.boolValue, out var overrideState))
            {
                _overrideGlobalSettings.boolValue = overrideState;
                serializedObject.ApplyModifiedProperties();
                UpdateController();
            }
        }

        private void DrawSeasonSettingsSection()
        {
            const string tooltip = "Customize how grass looks in each season\n\n" +
                                   "• Colors: Set unique grass colors for each season\n" +
                                   "• Width Scale: Set grass blade width for each season\n" +
                                   "• Height Scale: Set grass blade height for each season\n\n" +
                                   "Changes here only apply when 'Override Global Settings' is enabled";

            if (_seasonSettingsExpanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    DrawSeasonSettingsHeader(tooltip);
                    EditorGUILayout.Space(5);
                    DrawAllSeasonSettings();
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                DrawSeasonSettingsHeader(tooltip);
            }
        }

        private void DrawSeasonSettingsHeader(string tooltip)
        {
            if (GrassEditorHelper.DrawToggleButton("Season Settings", tooltip, _seasonSettingsExpanded,
                    out var expanded))
            {
                _seasonSettingsExpanded = expanded;
            }
        }

        private void DrawAllSeasonSettings()
        {
            DrawSeasonSetting(_winterSettings, 0);
            DrawSeasonSetting(_springSettings, 1);
            DrawSeasonSetting(_summerSettings, 2);
            DrawSeasonSetting(_autumnSettings, 3);
        }

        private void DrawSeasonValueControl()
        {
            if (_overrideGlobalSettings.boolValue)
            {
                EditorGUILayout.PropertyField(_seasonRange);
                EditorGUILayout.Space(5);

                var seasonZone = (GrassSeasonZone)target;
                DrawSeasonSlider(seasonZone.MinRange, seasonZone.MaxRange);
            }
            else
            {
                var grassSettings = _grassCompute?.GrassSetting;
                if (!grassSettings) return;

                var (minRange, maxRange) = GetGlobalSeasonRange(grassSettings);
                DrawSeasonSlider(minRange, maxRange);
            }
        }

        private (float min, float max) GetGlobalSeasonRange(GrassSettingSO settings)
        {
            return settings.seasonRange.GetRange();
        }

        private void DrawSeasonSlider(float minRange, float maxRange)
        {
            EditorGUI.BeginChangeCheck();
            var roundedValue = (float)System.Math.Round(_seasonValue.floatValue, 2);
            var newValue = EditorGUILayout.FloatField("Season Value", roundedValue);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonValue.floatValue = Mathf.Clamp(newValue, minRange, maxRange);
                UpdateController();
            }

            EditorGUI.BeginChangeCheck();
            newValue = GUILayout.HorizontalSlider(_seasonValue.floatValue, minRange, maxRange);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonValue.floatValue = newValue;
                UpdateController();
            }

            EditorGUILayout.Space(8);
            DrawSeasonProgressLabels();
            EditorGUILayout.Space(5);
        }

        private void DrawSeasonProgressLabels()
        {
            var zone = (GrassSeasonZone)target;
            var grassSettings = _grassCompute?.GrassSetting;

            if (!zone.OverrideGlobalSettings && grassSettings != null)
            {
                DrawGlobalSeasonLabels(grassSettings);
                return;
            }

            // Override된 경우 From -> To 진행 상황 표시
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            string currentSeason = GetSeasonName((_seasonValue.floatValue + 4f) % 4f);

            var labelRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.LabelField(labelRect, $"Current: {currentSeason}", style);
        }

        private void DrawGlobalSeasonLabels(GrassSettingSO settings)
        {
            // Get the season range from global settings
            var fromSeason = settings.seasonRange.From;
            var toSeason = settings.seasonRange.To;
            var isFullCycle = settings.seasonRange.IsFullCycle;

            // Draw labels
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            var currentSeason = GetSeasonName((_seasonValue.floatValue + 4f) % 4f);
            var sequence = SeasonRange.GetSeasonRangeInfo(fromSeason, toSeason, isFullCycle);
            EditorGUILayout.LabelField($"Current: {currentSeason}", style);
            EditorGUILayout.LabelField($"Sequence: {sequence}", style);
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

        private string GetSeasonName(float value)
        {
            int seasonIndex = Mathf.FloorToInt(value);
            return seasonIndex switch
            {
                0 => "Winter",
                1 => "Spring",
                2 => "Summer",
                3 => "Autumn",
                _ => "Winter"
            };
        }

        private void UpdateController()
        {
            var zone = (GrassSeasonZone)target;
            var grassSettings = _grassCompute?.GrassSetting;
            if (grassSettings != null)
            {
                var (min, max) = zone.OverrideGlobalSettings
                    ? (zone.MinRange, zone.MaxRange)
                    : grassSettings.seasonRange.GetRange();
                
                zone.UpdateSeasonValue(_seasonValue.floatValue, min, max);
            }
        }
    }
}