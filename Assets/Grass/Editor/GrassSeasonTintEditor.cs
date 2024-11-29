using System;
using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonController))]
    public class GrassSeasonTintEditor : UnityEditor.Editor
    {
        private const float LABEL_WIDTH = 40f;
        private const float LABEL_FONT_SIZE = 10f;

        private SerializedProperty _currentSeasonValue;

        private float _lastSliderValue;

        private void OnEnable()
        {
            _currentSeasonValue = serializedObject.FindProperty("currentSeasonValue");
            _lastSliderValue = _currentSeasonValue.floatValue;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawSeasonControl();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSeasonControl()
        {
            EditorGUILayout.FloatField("Season Value", (float)Math.Round(_currentSeasonValue.floatValue, 2));

            var sliderRect =
                GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
            var labelRect =
                GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);

            var (minRange, maxRange) = GetSeasonRange();
            DrawSeasonSlider(sliderRect, minRange, maxRange);
            DrawSeasonLabels(labelRect, minRange, maxRange);
        }

        private (float min, float max) GetSeasonRange()
        {
            var grassCompute = ((GrassSeasonController)target).GrassCompute;
            var grassSettings = grassCompute ? grassCompute.GrassSetting : null;
            return (
                grassSettings?.seasonRangeMin ?? 0f,
                grassSettings?.seasonRangeMax ?? 4f
            );
        }

        private void DrawSeasonSlider(Rect sliderRect, float minRange, float maxRange)
        {
            EditorGUI.BeginChangeCheck();
            var newValue = GUI.HorizontalSlider(sliderRect, _currentSeasonValue.floatValue, minRange, maxRange);

            if (EditorGUI.EndChangeCheck() && !Mathf.Approximately(_lastSliderValue, newValue))
            {
                _currentSeasonValue.floatValue = newValue;
                var script = (GrassSeasonController)target;
                script.UpdateSeasonEffects(_currentSeasonValue.floatValue);
                _lastSliderValue = _currentSeasonValue.floatValue;
            }
        }

        private void DrawSeasonLabels(Rect rect, float minRange, float maxRange)
        {
            var labels = GetSeasonLabels(minRange, maxRange);
            if (labels.Count == 0) return;

            for (int i = 0; i < labels.Count; i++)
            {
                var labelPosition = GetLabelPosition(rect, i, labels.Count);
                var style = CreateLabelStyle(i, labels.Count);
                EditorGUI.LabelField(labelPosition, labels[i], style);
            }
        }

        private List<string> GetSeasonLabels(float minRange, float maxRange)
        {
            var labels = new List<string>();
            if (minRange <= 1f) labels.Add("Winter");
            if (maxRange >= 1f && minRange <= 2f) labels.Add("Spring");
            if (maxRange >= 2f && minRange <= 3f) labels.Add("Summer");
            if (maxRange >= 3f && minRange <= 4f) labels.Add("Autumn");
            if (maxRange >= 4f) labels.Add("Winter");
            return labels;
        }

        private Rect GetLabelPosition(Rect rect, int index, int totalCount)
        {
            float xPosition;
            if (index == 0)
                xPosition = rect.x;
            else if (index == totalCount - 1)
                xPosition = rect.x + rect.width - LABEL_WIDTH;
            else
                xPosition = rect.x + (rect.width * index / (totalCount - 1)) - (LABEL_WIDTH / 2);

            return new Rect(xPosition, rect.y, LABEL_WIDTH, rect.height);
        }

        private GUIStyle CreateLabelStyle(int index, int totalCount)
        {
            return new GUIStyle(EditorStyles.label)
            {
                alignment = index == 0 ? TextAnchor.MiddleLeft :
                    index == totalCount - 1 ? TextAnchor.MiddleRight :
                    TextAnchor.MiddleCenter,
                fontSize = (int)LABEL_FONT_SIZE
            };
        }
    }
}