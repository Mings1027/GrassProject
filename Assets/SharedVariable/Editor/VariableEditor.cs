using SharedVariable.VariableScripts;
using UnityEditor;
using UnityEngine;

namespace SharedVariable.Editor
{
    [CustomEditor(typeof(BaseVariable), true)]
    public class VariableEditor : UnityEditor.Editor
    {
        private SerializedProperty initialValueProp;
        private SerializedProperty saveRuntimeChangesProp;
        
#if UNITY_EDITOR
        private SerializedProperty variableNameProp;
#endif

        private void OnEnable()
        {
            initialValueProp = serializedObject.FindProperty("initialValue");
            saveRuntimeChangesProp = serializedObject.FindProperty("saveRuntimeChanges");
#if UNITY_EDITOR
            variableNameProp = serializedObject.FindProperty("variableName");
#endif
        }

        public override void OnInspectorGUI()
        {
#if UNITY_EDITOR
            serializedObject.Update();
            EditorGUILayout.PropertyField(variableNameProp);
#endif
            
            if (Application.isPlaying)
            {
                GUI.enabled = false;
                EditorGUILayout.PropertyField(initialValueProp, new GUIContent("Value"));
                GUI.enabled = true;
                
                // runtimeValue 표시
                var variable = target as BaseVariable;
                if (variable != null)
                {
                    var runtimeValueField = variable.GetType().GetField("runtimeValue", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                
                    if (runtimeValueField != null)
                    {
                        var value = runtimeValueField.GetValue(variable);
                        EditorGUILayout.LabelField("Runtime Value", value.ToString());
                    }
                }
            }
            else
            {
                EditorGUILayout.PropertyField(initialValueProp, new GUIContent("Value"));
            }

            EditorGUILayout.PropertyField(saveRuntimeChangesProp);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}