using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(SeasonTest))]
    public class SeasonTestEditor : UnityEditor.Editor
    {
        private SerializedProperty _seasonManagerProp;
        private SerializedProperty _transitionDurationProp;

        private void OnEnable()
        {
            _seasonManagerProp = serializedObject.FindProperty("_seasonManager");
            _transitionDurationProp = serializedObject.FindProperty("transitionDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_seasonManagerProp);
            EditorGUILayout.PropertyField(_transitionDurationProp);

            var seasonTest = (SeasonTest)target;
            var manager = seasonTest.SeasonManager;

            if (GUILayout.Button("Play Full Cycle"))
            {
                manager.PlayCycles(seasonTest.TransitionDuration);
            }

            if (GUILayout.Button("Play Next Season"))
            {
                manager.PlayNextSeasons(seasonTest.TransitionDuration);
            }

            if (GUILayout.Button("Pause Transition"))
            {
                manager.PauseTransitions();
            }

            if (GUILayout.Button("Resume Transition"))
            {
                manager.ResumeTransitions();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}