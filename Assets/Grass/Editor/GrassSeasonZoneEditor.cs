using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonZone)), CanEditMultipleObjects]
    public class GrassSeasonZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _seasonValue;
        private SerializedProperty _showGizmos;
        private GrassSeasonZone _seasonZone;
        private List<SeasonSettings> _seasonSettingList;
        private GrassSettingSO _grassSetting;
        private bool _isSeasonValueSliderDragging;

        private void OnEnable()
        {
            _seasonValue = serializedObject.FindProperty("seasonValue");
            _showGizmos = serializedObject.FindProperty("showGizmos");
            _seasonZone = (GrassSeasonZone)target;
            _seasonSettingList = _seasonZone.SeasonSettings;
            _grassSetting = _seasonZone.GrassSetting;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGizmosToggle();

            if (GrassEditorHelper.DrawToggleButton("Override Global Season Setting", _seasonZone.OverrideGlobalSettings,
                    out var newState))
            {
                _seasonZone.OverrideGlobalSettings = newState;
                UpdateGrass();
            }

            if (_seasonZone.OverrideGlobalSettings)
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
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showGizmos.boolValue, out var newState))
            {
                _showGizmos.boolValue = newState;
                SceneView.RepaintAll();
            }
        }

        private void UpdateGrass()
        {
            if (_seasonZone != null)
            {
                // SeasonValue가 변경되면 업데이트
                var min = _seasonZone.MinRange;
                var max = _seasonZone.MaxRange;
                if (_grassSetting != null)
                {
                    _seasonZone.UpdateSeasonValue(_seasonValue.floatValue, min, max);
                }
            }
        }

        private void DrawSeasonValueSlider()
        {
            var settings = _seasonZone.OverrideGlobalSettings ? _seasonSettingList : _grassSetting?.seasonSettings;

            // 설정이 없거나 비어있는 경우 체크
            if (settings == null || settings.Count == 0 || settings.All(s => s == null))
            {
                EditorGUILayout.HelpBox("No valid season settings available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Season Value", EditorStyles.boldLabel);

            var minRange = _seasonZone.MinRange;
            var maxRange = _seasonZone.MaxRange;

            if (maxRange > minRange)
            {
                EditorGUI.BeginChangeCheck();

                var newValue = EditorGUILayout.Slider(_seasonValue.floatValue, minRange, maxRange);
                if (EditorGUI.EndChangeCheck())
                {
                    _seasonValue.floatValue = newValue;
                    _seasonZone.SetSeasonValue(newValue);
                }

                // 현재 시즌 전환 상태 가져오기
                var (currentSeason, transition) = _seasonZone.GetCurrentSeasonTransition();
                var seasonSettingList = _seasonZone.OverrideGlobalSettings
                    ? _seasonSettingList
                    : _grassSetting?.seasonSettings;

                if (seasonSettingList != null && seasonSettingList.Count > 0)
                {
                    var sequence = new System.Text.StringBuilder();
                    for (int i = 0; i < seasonSettingList.Count; i++)
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
            }
            else
            {
                EditorGUILayout.HelpBox("No season settings available.", MessageType.Warning);
            }
        }

        private void DrawGlobalSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Global Season Settings (Read Only)", EditorStyles.boldLabel);

            if (_grassSetting == null)
            {
                EditorGUILayout.HelpBox("No GrassSettingSO found in scene.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            GrassEditorHelper.DrawSeasonSettings(_grassSetting.seasonSettings, _grassSetting);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawOverrideSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            GrassEditorHelper.DrawSeasonSettings(_seasonSettingList, _grassSetting);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonZone.UpdateZoneImmediate();
            }
        }
    }
}