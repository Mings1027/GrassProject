using Grass.GrassScripts;
using UnityEngine;
using UnityEditor;

namespace Grass.Editor
{
    [CustomPropertyDrawer(typeof(SeasonRange))]
    public class SeasonRangeDrawer : PropertyDrawer
    {
        private static readonly GUIContent FullCycleContent = new GUIContent(
            "Full Cycle",
            "When enabled, the season will complete a full cycle from the selected season back to itself.\n\n" +
            "For example:\n" +
            "- Winter to Winter: Winter → Spring → Summer → Autumn → Winter\n" +
            "- Summer to Summer: Summer → Autumn → Winter → Spring → Summer"
        );

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var from = (SeasonType)property.FindPropertyRelative("from").enumValueIndex;
            var to = (SeasonType)property.FindPropertyRelative("to").enumValueIndex;
            var isFullCycleProp = property.FindPropertyRelative("isFullCycle");

            // From/To line
            var seasonRect = position;
            seasonRect.height = lineHeight;

            // Dynamic width calculation
            var availableWidth = seasonRect.width;
            var labelWidth = EditorGUIUtility.labelWidth * 0.5f;
            var enumWidth = (availableWidth - (labelWidth * 2) - 20) * 0.5f;

            // From section
            var fromLabelRect = seasonRect;
            fromLabelRect.width = labelWidth;
            EditorGUI.LabelField(fromLabelRect, "From");

            var fromEnumRect = fromLabelRect;
            fromEnumRect.x += labelWidth;
            fromEnumRect.width = enumWidth;
            EditorGUI.PropertyField(fromEnumRect, property.FindPropertyRelative("from"), GUIContent.none);

            // To section
            var toLabelRect = fromEnumRect;
            toLabelRect.x += enumWidth + 10;
            toLabelRect.width = labelWidth;
            EditorGUI.LabelField(toLabelRect, "To");

            var toEnumRect = toLabelRect;
            toEnumRect.x += labelWidth;
            toEnumRect.width = enumWidth;
            EditorGUI.PropertyField(toEnumRect, property.FindPropertyRelative("to"), GUIContent.none);

            // Full Cycle toggle
            if (from == to)
            {
                var cycleRect = position;
                cycleRect.y += lineHeight + spacing;
                cycleRect.height = lineHeight;
                EditorGUI.PropertyField(cycleRect, isFullCycleProp, FullCycleContent);
            }

            // Sequence info
            var totalHeight = (from == to) ? lineHeight * 3 + spacing * 2 : lineHeight * 2 + spacing;
            var infoRect = position;
            infoRect.y += totalHeight - lineHeight;
            infoRect.height = lineHeight;
            var rangeInfo = SeasonRange.GetSeasonRangeInfo(from, to, isFullCycleProp.boolValue);
            EditorGUI.LabelField(infoRect, "Sequence: " + rangeInfo, EditorStyles.miniLabel);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var from = (SeasonType)property.FindPropertyRelative("from").enumValueIndex;
            var to = (SeasonType)property.FindPropertyRelative("to").enumValueIndex;

            // From과 To가 같을 때만 FullCycle 줄 추가
            return (from == to)
                ? (EditorGUIUtility.singleLineHeight * 3) + (EditorGUIUtility.standardVerticalSpacing * 2)
                : (EditorGUIUtility.singleLineHeight * 2) + EditorGUIUtility.standardVerticalSpacing;
        }
        
    }
}