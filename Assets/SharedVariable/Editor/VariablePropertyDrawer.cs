using SharedVariable.VariableScripts;
using UnityEditor;
using UnityEngine;

namespace SharedVariable.Editor
{
    [CustomPropertyDrawer(typeof(Variable<>), true)]
    public class VariablePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = property.objectReferenceValue as BaseVariable;
            if (target == null) return;

            var serializedObject = new SerializedObject(target);
            var initialValueProp = serializedObject.FindProperty("initialValue");
            var saveRuntimeChangesProp = serializedObject.FindProperty("saveRuntimeChanges");
            var runtimeValueProp = serializedObject.FindProperty("runtimeValue");

            serializedObject.Update();

            var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(position.x, labelRect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, 
                                   position.width, EditorGUIUtility.singleLineHeight);
            var saveChangesRect = new Rect(position.x, valueRect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, 
                                         position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(labelRect, property, label);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                if (Application.isPlaying)
                    EditorGUI.PropertyField(valueRect, runtimeValueProp);
                else
                    EditorGUI.PropertyField(valueRect, initialValueProp);
                EditorGUI.PropertyField(saveChangesRect, saveRuntimeChangesProp);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) 
                return EditorGUIUtility.singleLineHeight;
            
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
        }
    }
}