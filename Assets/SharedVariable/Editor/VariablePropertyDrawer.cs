using SharedVariable.VariableScripts;
using UnityEngine;
using UnityEditor;

namespace SharedVariable.Editor
{
    [CustomPropertyDrawer(typeof(BaseVariable), true)]
    public class VariablePropertyDrawer : PropertyDrawer
    {
        private const float FOLD_BUTTON_WIDTH = 15f;
        private bool isExpanded = false;
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!isExpanded || property.objectReferenceValue == null)
                return EditorGUIUtility.singleLineHeight;
                
            float totalHeight = EditorGUIUtility.singleLineHeight;

            if (Application.isPlaying)
            {
                totalHeight += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight * 2;
            }
            else 
            {
                totalHeight += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            }

            totalHeight += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            
            return totalHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldRect = new Rect(position.x, position.y, FOLD_BUTTON_WIDTH, EditorGUIUtility.singleLineHeight);
            var fieldRect = new Rect(position.x + FOLD_BUTTON_WIDTH, position.y, 
                position.width - FOLD_BUTTON_WIDTH, EditorGUIUtility.singleLineHeight);

            // Variable이 할당되어 있을 때만 폴드아웃 표시
            if (property.objectReferenceValue != null)
            {
                isExpanded = EditorGUI.Foldout(foldRect, isExpanded, GUIContent.none);
            }
            else
            {
                EditorGUI.LabelField(foldRect, GUIContent.none);
            }
            
            EditorGUI.PropertyField(fieldRect, property, label, true);

            if (isExpanded && property.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                
                var targetObject = property.objectReferenceValue as BaseVariable;
                var serializedObject = new SerializedObject(targetObject);
                var initialValueProp = serializedObject.FindProperty("initialValue");
                var saveRuntimeChangesProp = serializedObject.FindProperty("saveRuntimeChanges");

                serializedObject.Update();

                float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                
                if (Application.isPlaying)
                {
                    // Initial Value
                    var initialValueRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(initialValueRect, initialValueProp, new GUIContent("Value"));

                    // Runtime Value                    
                    yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    var runtimeValueRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);

                    if (targetObject != null)
                    {
                        var runtimeValueField = targetObject.GetType().GetField("runtimeValue", 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance);
                        
                        if (runtimeValueField != null)
                        {
                            var value = runtimeValueField.GetValue(targetObject);
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                var labelRect = EditorGUI.PrefixLabel(runtimeValueRect, new GUIContent("Runtime Value"));
                                object newValue = value;

                                // 값 타입에 따라 다른 필드 표시 및 값 수정
                                if (value is float floatValue)
                                    newValue = EditorGUI.FloatField(labelRect, floatValue);
                                else if (value is int intValue)
                                    newValue = EditorGUI.IntField(labelRect, intValue);
                                else if (value is bool boolValue)
                                    newValue = EditorGUI.Toggle(labelRect, boolValue);
                                else if (value is string stringValue)
                                    newValue = EditorGUI.TextField(labelRect, stringValue);
                                else if (value is Vector2 vector2Value)
                                    newValue = EditorGUI.Vector2Field(labelRect, "", vector2Value);
                                else if (value is Vector3 vector3Value)
                                    newValue = EditorGUI.Vector3Field(labelRect, "", vector3Value);
                                else if (value is Color colorValue)
                                    newValue = EditorGUI.ColorField(labelRect, colorValue);
                                else if (value is UnityEngine.Object objectValue)
                                    newValue = EditorGUI.ObjectField(labelRect, objectValue, value.GetType(), true);
                                else
                                    EditorGUI.LabelField(labelRect, value?.ToString() ?? "null");

                                // 값이 변경되었으면 runtimeValue에 반영
                                if (check.changed)
                                {
                                    Undo.RecordObject(targetObject, "Change Runtime Value");
                                    runtimeValueField.SetValue(targetObject, newValue);
                                    EditorUtility.SetDirty(targetObject);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var valueRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(valueRect, initialValueProp, new GUIContent("Value"));
                }

                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                var saveRuntimeRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(saveRuntimeRect, saveRuntimeChangesProp);

                serializedObject.ApplyModifiedProperties();

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}