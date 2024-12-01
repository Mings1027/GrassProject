using UnityEditor;
using UnityEngine;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonManager))]
    public class GrassSeasonManagerEditor : UnityEditor.Editor
    {
        private bool _showAllGizmos;
        private float _sliderValue;
        private bool _initialized;

        private void OnEnable()
        {
            var controller = (GrassSeasonManager)target;
            var volumes = controller.GetComponentsInChildren<GrassSeasonZone>();
            _showAllGizmos = AreAllGizmosEnabled(volumes);

            if (!_initialized)
            {
                _sliderValue = controller.GlobalSeasonValue;
                _initialized = true;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var controller = (GrassSeasonManager)target;
            DrawGizmosToggle(controller);
            EditorGUILayout.Space(10);
            DrawSeasonControl(controller);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGizmosToggle(GrassSeasonManager manager)
        {
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Toggle All Gizmos" };
            EditorGUI.BeginChangeCheck();
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showAllGizmos, out var newState))
            {
                _showAllGizmos = newState;
                SetAllGizmos(manager, _showAllGizmos);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawSeasonControl(GrassSeasonManager manager)
        {
            EditorGUILayout.LabelField("Global Season Control", EditorStyles.boldLabel);

            var min = manager.GlobalMinRange;
            var max = manager.GlobalMaxRange;

            EditorGUI.BeginChangeCheck();

            var roundedValue = (float)System.Math.Round(_sliderValue, 2);
            _sliderValue = EditorGUILayout.FloatField("Season Value", roundedValue);

            EditorGUILayout.BeginVertical(GUILayout.Height(20));
            _sliderValue = GUILayout.HorizontalSlider(_sliderValue, min, max);
            EditorGUILayout.EndVertical();

            // 현재 계절 정보 표시
            // string currentSeason = GetSeasonName((_sliderValue + 4f) % 4f);
            // EditorGUILayout.LabelField($"Current: {currentSeason} ({_sliderValue:F2})", EditorStyles.miniLabel);

            if (EditorGUI.EndChangeCheck())
            {
                _sliderValue = Mathf.Clamp(_sliderValue, min, max);
                manager.SetGlobalSeasonValue(_sliderValue);
                SceneView.RepaintAll();
            }
        }

        // private string GetSeasonName(float value)
        // {
        //     int seasonIndex = Mathf.FloorToInt(value);
        //     return seasonIndex switch
        //     {
        //         0 => "Winter",
        //         1 => "Spring",
        //         2 => "Summer",
        //         3 => "Autumn",
        //         _ => "Winter"
        //     };
        // }

        private bool AreAllGizmosEnabled(GrassSeasonZone[] volumes)
        {
            if (volumes.Length == 0) return false;

            foreach (var volume in volumes)
            {
                if (!volume) continue;

                SerializedObject serializedVolume = new SerializedObject(volume);
                var showGizmos = serializedVolume.FindProperty("showGizmos");
                if (!showGizmos.boolValue) return false;
            }

            return true;
        }

        private void SetAllGizmos(GrassSeasonManager manager, bool state)
        {
            var volumes = manager.GetComponentsInChildren<GrassSeasonZone>();
            foreach (var volume in volumes)
            {
                if (!volume) continue;

                SerializedObject serializedVolume = new SerializedObject(volume);
                var showGizmos = serializedVolume.FindProperty("showGizmos");
                showGizmos.boolValue = state;
                serializedVolume.ApplyModifiedProperties();
            }

            SceneView.RepaintAll();
        }
    }
}