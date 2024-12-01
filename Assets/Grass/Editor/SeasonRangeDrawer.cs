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

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var from = (SeasonType)property.FindPropertyRelative("from").enumValueIndex;
            var to = (SeasonType)property.FindPropertyRelative("to").enumValueIndex;
            var isFullCycleProp = property.FindPropertyRelative("isFullCycle");

            float totalHeight = (from == to) ? lineHeight * 3 + spacing * 2 : lineHeight * 2 + spacing;

            // From/To 라인 (첫 번째 줄)
            var seasonRect = position;
            seasonRect.height = lineHeight;

            // Layout 계산
            float totalWidth = seasonRect.width;
            float enumWidth = (totalWidth - 90) / 2;
            float labelWidth = 40f;
            float padding = 10f;

            // From 레이블
            var fromLabelRect = seasonRect;
            fromLabelRect.width = labelWidth;
            EditorGUI.LabelField(fromLabelRect, "From");

            // From enum
            var fromEnumRect = fromLabelRect;
            fromEnumRect.x += labelWidth;
            fromEnumRect.width = enumWidth;
            EditorGUI.PropertyField(fromEnumRect, property.FindPropertyRelative("from"), GUIContent.none);

            // To 레이블
            var toLabelRect = fromEnumRect;
            toLabelRect.x += enumWidth + padding;
            toLabelRect.width = labelWidth;
            EditorGUI.LabelField(toLabelRect, "To");

            // To enum
            var toEnumRect = toLabelRect;
            toEnumRect.x += labelWidth;
            toEnumRect.width = enumWidth;
            EditorGUI.PropertyField(toEnumRect, property.FindPropertyRelative("to"), GUIContent.none);

            // Full Cycle 토글 (From과 To가 같을 때만)
            if (from == to)
            {
                var cycleRect = position;
                cycleRect.y += lineHeight + spacing;
                cycleRect.height = lineHeight;
                EditorGUI.PropertyField(cycleRect, isFullCycleProp, FullCycleContent);
            }

            // Sequence 정보 (마지막 줄)
            var infoRect = position;
            infoRect.y += totalHeight - lineHeight;
            infoRect.height = lineHeight;
            string rangeInfo = GetSeasonRangeInfo(from, to, isFullCycleProp.boolValue);
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

        private string GetSeasonRangeInfo(SeasonType from, SeasonType to, bool isFullCycle)
        {
            if (from == to && isFullCycle)
            {
                // Full Cycle: 한 바퀴 도는 경우
                string seasons = "";
                SeasonType current = from;
                do
                {
                    seasons += current.ToString() + " → ";
                    current = (SeasonType)(((int)current + 1) % 4);
                } while (current != from);

                seasons += from.ToString();
                return seasons;
            }
            else if (from == to)
            {
                // 같은 계절: 고정
                return from.ToString();
            }
            else if (from < to)
            {
                // 정방향 순서
                string seasons = "";
                for (SeasonType season = from; season <= to; season++)
                {
                    seasons += season.ToString() + " → ";
                }

                return seasons.TrimEnd('→', ' ');
            }
            else
            {
                // 역방향 순서 (한 사이클)
                string seasons = "";
                for (SeasonType season = from; season <= SeasonType.Autumn; season++)
                {
                    seasons += season.ToString() + " → ";
                }

                for (SeasonType season = SeasonType.Winter; season <= to; season++)
                {
                    seasons += season.ToString() + " → ";
                }

                return seasons.TrimEnd('→', ' ');
            }
        }
    }
}