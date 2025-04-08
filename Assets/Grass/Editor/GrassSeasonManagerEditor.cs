using Editor;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonManager))]
    public class GrassSeasonManagerEditor : UnityEditor.Editor
    {
        private bool _showAllGizmos;
        private GrassSeasonManager _manager;
        private SerializedProperty _globalSeasonValue;
        private SerializedProperty _transitionDuration;

        private void OnEnable()
        {
            _globalSeasonValue = serializedObject.FindProperty("globalSeasonValue");
            _transitionDuration = serializedObject.FindProperty("transitionDuration");
            _manager = (GrassSeasonManager)target;
            var volumes = _manager.GetComponentsInChildren<GrassSeasonZone>();
            _showAllGizmos = AreAllGizmosEnabled(volumes);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGizmosToggle();
            DrawCreateSeasonZoneButton();
            DrawGlobalSeasonValueSlider();
            EditorGUILayout.Space(5);
            DrawSeasonControlButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGizmosToggle()
        {
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Toggle All Gizmos" };
            EditorGUI.BeginChangeCheck();
            if (CustomEditorHelper.DrawToggleButton(gizmosContent, _showAllGizmos, out var newState))
            {
                _showAllGizmos = newState;
                SetAllGizmos(_showAllGizmos);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawCreateSeasonZoneButton()
        {
            if (GUILayout.Button("Create Season Zone", GUILayout.Height(25)))
            {
                _manager.CreateSeasonZone();
            }
        }

        private bool AreAllGizmosEnabled(GrassSeasonZone[] volumes)
        {
            if (volumes.Length == 0) return false;

            foreach (var volume in volumes)
            {
                if (!volume) continue;

                var serializedVolume = new SerializedObject(volume);
                var showGizmos = serializedVolume.FindProperty("showGizmos");
                if (!showGizmos.boolValue) return false;
            }

            return true;
        }

        private void SetAllGizmos(bool state)
        {
            var volumes = _manager.GetComponentsInChildren<GrassSeasonZone>();
            foreach (var volume in volumes)
            {
                if (!volume) continue;

                var serializedVolume = new SerializedObject(volume);
                var showGizmos = serializedVolume.FindProperty("showGizmos");
                showGizmos.boolValue = state;
                serializedVolume.ApplyModifiedProperties();
            }

            SceneView.RepaintAll();
        }

        private void DrawGlobalSeasonValueSlider()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Global Season Value", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var grassCompute = FindAnyObjectByType<GrassComputeScript>();

            var newValue = EditorGUILayout.Slider(_globalSeasonValue.floatValue, 0,
                grassCompute.GrassSetting.seasonSettings.Count);
            if (EditorGUI.EndChangeCheck())
            {
                _globalSeasonValue.floatValue = newValue;
                _manager.UpdateAllSeasonZones();
            }
        }

        private void DrawSeasonControlButtons()
        {
            _transitionDuration.floatValue =
                EditorGUILayout.FloatField("Transition Duration", _transitionDuration.floatValue);
            if (GUILayout.Button("Play Full Cycle"))
            {
                _manager.PlayCycles(_transitionDuration.floatValue);
            }
            
            if (GUILayout.Button("Play Next Season"))
            {
                _manager.PlayNextSeasons(_transitionDuration.floatValue);
            }

            if (GUILayout.Button("Pause Transition"))
            {
                _manager.PauseTransitions();
            }
            
            if (GUILayout.Button("Resume Transition"))
            {
                _manager.ResumeTransitions();
            }
        }
    }
}