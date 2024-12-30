using UnityEditor;
using UnityEngine;
using Grass.GrassScripts;

namespace Grass.Editor
{
    [CustomEditor(typeof(GrassSeasonManager))]
    public class GrassSeasonManagerEditor : UnityEditor.Editor
    {
        private bool _showAllGizmos;
        private GrassSeasonManager _manager;
        private SerializedProperty _seasonValue;
        private GrassSettingSO _grassSetting;

        private void OnEnable()
        {
            _seasonValue = serializedObject.FindProperty("globalSeasonValue");
            _manager = (GrassSeasonManager)target;
            var volumes = _manager.GetComponentsInChildren<GrassSeasonZone>();
            _showAllGizmos = AreAllGizmosEnabled(volumes);
            
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

            DrawManualUpdateButton();
            DrawGizmosToggle();
            DrawGlobalSeasonValueSlider();
            EditorGUILayout.Space(10);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawManualUpdateButton()
        {
            if(GUILayout.Button("Manual Update", GUILayout.Height(25)))
            {
                _manager.Init();
            }
        }

        private void DrawGizmosToggle()
        {
            var gizmosContent = new GUIContent(EditorIcons.Gizmos) { text = "Toggle All Gizmos" };
            EditorGUI.BeginChangeCheck();
            if (GrassEditorHelper.DrawToggleButton(gizmosContent, _showAllGizmos, out var newState))
            {
                _showAllGizmos = newState;
                SetAllGizmos(_showAllGizmos);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
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
            var newValue = EditorGUILayout.Slider(_seasonValue.floatValue, 0, _grassSetting.seasonSettings.Count);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonValue.floatValue = newValue;
                _manager.UpdateAllSeasonZones();
            }
        }
    }
}