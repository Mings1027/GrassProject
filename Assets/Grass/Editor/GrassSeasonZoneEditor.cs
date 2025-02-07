using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonZone)), CanEditMultipleObjects]
    public class GrassSeasonZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _seasonValue;
        private SerializedProperty _showGizmos;
        private SerializedProperty _useLocalSeasonSettings;
        private SerializedProperty _seasonSettingList;
        private SerializedProperty _grassSetting;

        private void OnEnable()
        {
            _seasonValue = serializedObject.FindProperty("seasonValue");
            _showGizmos = serializedObject.FindProperty("showGizmos");
            _useLocalSeasonSettings = serializedObject.FindProperty("useLocalSeasonSettings");
            _seasonSettingList = serializedObject.FindProperty("seasonSettings");
            _grassSetting = serializedObject.FindProperty("grassSetting");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGizmosToggle();

            if (CustomEditorHelper.DrawToggleButton("Use Local Season Setting", _useLocalSeasonSettings.boolValue,
                    out var newState))
            {
                _useLocalSeasonSettings.boolValue = newState;
                UpdateGrass();
            }

            if (_useLocalSeasonSettings.boolValue)
            {
                DrawOverrideSettings();
            }
            else
            {
                DrawGlobalSettings();
            }

            EditorGUI.BeginChangeCheck();
            DrawSeasonValueSlider();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateGrass();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGizmosToggle()
        {
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Show Gizmos" };
            EditorGUI.BeginChangeCheck();
            if (CustomEditorHelper.DrawToggleButton(gizmosContent, _showGizmos.boolValue, out var newState))
            {
                _showGizmos.boolValue = newState;
                SceneView.RepaintAll();
            }
        }

        private void UpdateGrass()
        {
            var seasonZone = (GrassSeasonZone)target;
            if (seasonZone != null)
            {
                // SeasonValue가 변경되면 업데이트
                var min = seasonZone.MinRange;
                var max = seasonZone.MaxRange;
                if (_grassSetting != null)
                {
                    seasonZone.UpdateSeasonValue(_seasonValue.floatValue, min, max);
                }
            }
        }

        private void DrawSeasonValueSlider()
        {
            var seasonZone = (GrassSeasonZone)target;
            var grassSettingObj = _grassSetting.objectReferenceValue as GrassSettingSO;

            if (grassSettingObj == null) return;

            var settings = _useLocalSeasonSettings.boolValue
                ? GetSettingsFromProperty(_seasonSettingList)
                : grassSettingObj.seasonSettings;

            // 설정이 없거나 비어있는 경우 체크
            if (settings == null || settings.Count == 0)
            {
                EditorGUILayout.HelpBox("No valid season settings available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Season Value", EditorStyles.boldLabel);

            var minRange = seasonZone.MinRange;
            var maxRange = seasonZone.MaxRange;

            if (maxRange > minRange)
            {
                EditorGUI.BeginChangeCheck();

                var newValue = EditorGUILayout.Slider(_seasonValue.floatValue, minRange, maxRange);
                if (EditorGUI.EndChangeCheck())
                {
                    _seasonValue.floatValue = newValue;
                    seasonZone.SetSeasonValue(newValue);
                }

                var (currentSeason, transition) = seasonZone.GetCurrentSeasonTransition();
                DrawSeasonTransitionInfo(settings, currentSeason, transition);
            }
            else
            {
                EditorGUILayout.HelpBox("No season settings available.", MessageType.Warning);
            }
        }

        private List<SeasonSettings> GetSettingsFromProperty(SerializedProperty listProperty)
        {
            var result = new List<SeasonSettings>();
            for (int i = 0; i < listProperty.arraySize; i++)
            {
                var element = listProperty.GetArrayElementAtIndex(i).objectReferenceValue as SeasonSettings;
                if (element != null)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private void DrawSeasonTransitionInfo(List<SeasonSettings> settings,
                                              SeasonType currentSeason, float transition)
        {
            var sequence = new System.Text.StringBuilder();
            for (int i = 0; i < settings.Count; i++)
            {
                if (settings[i] != null)
                {
                    if (sequence.Length > 0)
                    {
                        sequence.Append(" → ");
                    }

                    var season = settings[i].seasonType.ToString();
                    sequence.Append(season == currentSeason.ToString() ? $"<b>{season}</b>" : season);
                }
            }

            var style = new GUIStyle(EditorStyles.label) { richText = true };
            EditorGUILayout.LabelField(sequence.ToString(), style);
            EditorGUILayout.LabelField($"Transition: {transition:P0}");
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Global Season Settings (Read Only)", EditorStyles.boldLabel);

            var grassSettingObj = _grassSetting.objectReferenceValue as GrassSettingSO;
            if (grassSettingObj == null)
            {
                EditorGUILayout.HelpBox("No GrassSettingSO found in scene.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            GrassEditorHelper.DrawSeasonSettings(grassSettingObj.seasonSettings, grassSettingObj);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawOverrideSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Local Season Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var grassSettingObj = _grassSetting.objectReferenceValue as GrassSettingSO;
            if (grassSettingObj != null)
            {
                GrassEditorHelper.DrawSeasonSettings(GetSettingsFromProperty(_seasonSettingList), grassSettingObj);

                if (EditorGUI.EndChangeCheck())
                {
                    var seasonZone = (GrassSeasonZone)target;
                    seasonZone.UpdateZoneImmediate();
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
}