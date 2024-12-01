using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonZone))]
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
                EditorGUILayout.HelpBox("Using global settings from GrassSettingSO", MessageType.Info);
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
            if (!_overrideGlobalSettings.boolValue)
            {
                var grassSettings = _grassCompute?.GrassSetting;
                if (!grassSettings) return;

                var (minRange, maxRange) = GetGlobalSeasonRange(grassSettings);
                DrawSeasonSlider(minRange, maxRange);
            }
            else
            {
                EditorGUILayout.PropertyField(_seasonRange);
                EditorGUILayout.Space(5);

                var seasonZone = (GrassSeasonZone)target;
                DrawSeasonSlider(seasonZone.MinRange, seasonZone.MaxRange);
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
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            // float progress = Mathf.InverseLerp(minRange, maxRange, _seasonValue.floatValue);
            string currentSeason = GetSeasonName((_seasonValue.floatValue + 4f) % 4f);

            var labelRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.LabelField(labelRect, $"Current: {currentSeason}", style);
        }

        private void DrawGlobalSeasonLabels(GrassSettingSO settings)
        {
            float verticalOffset = 10f; // 위치 조정을 위한 오프셋
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            // Get the season range from global settings
            var (minRange, maxRange) = settings.seasonRange.GetRange();
            var fromSeason = settings.seasonRange.From;
            var toSeason = settings.seasonRange.To;
            var isFullCycle = settings.seasonRange.IsFullCycle;

            // Calculate current season progress
            // float normalizedValue = (_seasonValue.floatValue - minRange) / (maxRange - minRange);
            float currentValue = (_seasonValue.floatValue + 4f) % 4f;
            string currentSeason = GetSeasonName(currentValue);

            // Draw season transition label
            string transitionLabel;
            if (isFullCycle && fromSeason == toSeason)
            {
                transitionLabel = $"{fromSeason} → {fromSeason} (Full Cycle)";
            }
            else if (fromSeason == toSeason)
            {
                transitionLabel = fromSeason.ToString();
            }
            else
            {
                transitionLabel = $"{fromSeason} → {toSeason}";
            }

            // Draw labels
            var valueRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            EditorGUI.LabelField(valueRect,
                $"Current: {currentSeason} ({_seasonValue.floatValue:F2}) | Range: {transitionLabel}",
                style);

            // Draw season markers
            DrawSeasonMarkers(rect, minRange, maxRange, fromSeason, toSeason, isFullCycle);
        }

        private void DrawSeasonMarkers(Rect rect, float minRange, float maxRange,
                                       SeasonType fromSeason, SeasonType toSeason, bool isFullCycle)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            float totalRange = maxRange - minRange;
            if (totalRange <= 0) return;

            // Calculate visible seasons based on the range
            var visibleSeasons = new List<(string name, float position)>();

            if (isFullCycle && fromSeason == toSeason)
            {
                // Show all seasons for full cycle
                for (int i = 0; i <= 4; i++)
                {
                    if (i >= minRange && i <= maxRange)
                    {
                        visibleSeasons.Add((GetSeasonName(i), i));
                    }
                }
            }
            else
            {
                // Show only seasons in the specified range
                float current = minRange;
                while (current <= maxRange)
                {
                    visibleSeasons.Add((GetSeasonName(current), current));
                    current += 1f;
                }
            }

            // Draw season markers
            foreach (var season in visibleSeasons)
            {
                float normalizedPos = (season.position - minRange) / totalRange;
                float xPos = Mathf.Lerp(rect.x + 20, rect.x + rect.width - 60, normalizedPos);
                var markerRect = new Rect(xPos, rect.y + rect.height + 2, 40, rect.height);
                EditorGUI.LabelField(markerRect, season.name, style);
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
            var controller = FindAnyObjectByType<GrassSeasonManager>();
            if (controller != null)
            {
                controller.UpdateSeasonZones();
            }
        }
    }
}