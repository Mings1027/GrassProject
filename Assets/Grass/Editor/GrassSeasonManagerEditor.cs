using UnityEditor;
using UnityEngine;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonManager))]
    public class GrassSeasonManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _globalSeasonValueProp;
        private bool _showAllGizmos;

        private void OnEnable()
        {
            var controller = (GrassSeasonManager)target;
            var volumes = controller.GetComponentsInChildren<GrassSeasonZone>();
            _showAllGizmos = AreAllGizmosEnabled(volumes);

            _globalSeasonValueProp = serializedObject.FindProperty("globalSeasonValue");
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

            var min = manager.GlobalMinRange();
            var max = manager.GlobalMaxRange();

            EditorGUI.BeginChangeCheck();

            var roundedValue = (float)System.Math.Round(_globalSeasonValueProp.floatValue, 2);
            float newValue = EditorGUILayout.FloatField("Season Value", roundedValue);

            EditorGUILayout.BeginVertical(GUILayout.Height(20));
            newValue = GUILayout.HorizontalSlider(newValue, min, max);
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                _globalSeasonValueProp.floatValue = Mathf.Clamp(newValue, min, max);
                manager.SetGlobalSeasonValue(newValue);
                SceneView.RepaintAll();
            }
        }

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