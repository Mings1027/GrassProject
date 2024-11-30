using UnityEditor;
using UnityEngine;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonController))]
    public class GrassSeasonControllerEditor : UnityEditor.Editor
    {
        private bool _showAllGizmos;
        private float _globalSeasonValue;
        private bool _isInitialized;

        private void OnEnable()
        {
            var controller = (GrassSeasonController)target;
            var volumes = controller.GetComponentsInChildren<SeasonEffectVolume>();
            _showAllGizmos = AreAllGizmosEnabled(volumes);

            if (!_isInitialized && volumes.Length > 0)
            {
                foreach (var volume in volumes)
                {
                    if (volume != null)
                    {
                        _globalSeasonValue = volume.SeasonValue;
                        _isInitialized = true;
                        break;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();
            EditorGUILayout.Space(10);

            var controller = (GrassSeasonController)target;
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Toggle All Gizmos" };

            EditorGUI.BeginChangeCheck();
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showAllGizmos, out var newState))
            {
                _showAllGizmos = newState;
                SetAllGizmos(controller, _showAllGizmos);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(10);

            // Global Season Control
            EditorGUILayout.LabelField("Global Season Control", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            // Season Value Float Field
            var roundedValue = (float)System.Math.Round(_globalSeasonValue, 2);
            _globalSeasonValue = EditorGUILayout.FloatField("Season Value", roundedValue);

            // Season Value Slider (without label)
            _globalSeasonValue = GUILayout.HorizontalSlider(_globalSeasonValue, 0f, 4f);

            if (EditorGUI.EndChangeCheck())
            {
                _globalSeasonValue = Mathf.Clamp(_globalSeasonValue, 0f, 4f);
                UpdateAllSeasonValues(controller, _globalSeasonValue);
            }

            EditorGUILayout.Space(10);
        }

        private bool AreAllGizmosEnabled(SeasonEffectVolume[] volumes)
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

        private void SetAllGizmos(GrassSeasonController controller, bool state)
        {
            var volumes = controller.GetComponentsInChildren<SeasonEffectVolume>();
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

        private void UpdateAllSeasonValues(GrassSeasonController controller, float globalValue)
        {
            controller.SetGlobalSeasonValue(globalValue);
            SceneView.RepaintAll();
        }
    }
}