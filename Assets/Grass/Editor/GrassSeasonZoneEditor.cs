using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Grass.GrassScripts;
using NUnit.Framework;
using UnityEditorInternal;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonZone)), CanEditMultipleObjects]
    public class GrassSeasonZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty _seasonValue;
        private GrassSeasonZone _seasonZone;
        private List<SeasonSettings> _seasonSettingList;
        private GrassSettingSO _grassSetting;
        private bool _isSeasonValueSliderDragging;

        private void OnEnable()
        {
            _seasonValue = serializedObject.FindProperty("seasonValue");
            _seasonZone = (GrassSeasonZone)target;
            _seasonSettingList = _seasonZone.SeasonSettings;

            // GrassSettingSO 찾기
            var grassCompute = FindAnyObjectByType<GrassComputeScript>();
            if (grassCompute != null)
            {
                _grassSetting = grassCompute.GrassSetting;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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
                var (currentSeason, nextSeason, transition) = _seasonZone.GetCurrentSeasonTransition();

                EditorGUILayout.LabelField($"{currentSeason} → {nextSeason} ({transition:P0})");
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
            GrassEditorHelper.DrawSeasonSettings(_grassSetting.seasonSettings, "Zone");
            EditorGUI.EndDisabledGroup();
        }

        private void DrawOverrideSettings()
        {
            EditorGUILayout.Space(10);
            GrassEditorHelper.DrawSeasonSettings(_seasonSettingList, "Zone");
        }
    }
}