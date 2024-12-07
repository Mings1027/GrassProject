using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class GrassLODSettingEditor
    {
        private static readonly Color LowQualityColor = new(0.3f, 0.7f, 0.3f);
        private static readonly Color MediumQualityColor = new(0.7f, 0.7f, 0.3f);
        private static readonly Color HighQualityColor = new(0.7f, 0.3f, 0.3f);

        private struct QualityBarData
        {
            private float NormalizedMinFade;
            private float RemainingPercent;
            public float LowWidth;
            public float MedWidth;
            public float HighWidth;

            public static QualityBarData Calculate(GrassSettingSO settings, float totalWidth)
            {
                var data = new QualityBarData
                {
                    NormalizedMinFade = settings.minFadeDistance / settings.maxFadeDistance
                };

                data.RemainingPercent = 1f - data.NormalizedMinFade;

                // shader와 동일한 계산식 사용
                data.LowWidth = totalWidth * (settings.lowQualityDistance * data.RemainingPercent);
                data.MedWidth = totalWidth * ((settings.mediumQualityDistance - settings.lowQualityDistance) *
                                              data.RemainingPercent);
                data.HighWidth = totalWidth * data.NormalizedMinFade +
                                 totalWidth * ((1f - settings.mediumQualityDistance) * data.RemainingPercent);

                return data;
            }
        }

        public static void DrawLODSettingsPanel(GrassSettingSO settings)
        {
            var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            rect.y += 5;

            var barData = QualityBarData.Calculate(settings, rect.width);
            DrawQualityBar(rect, barData);
            DrawDistanceLabels(rect, barData);

            EditorGUILayout.Space(5);

            DrawDistanceSliders(settings);
        }

        private static void DrawQualityBar(Rect position, QualityBarData barData)
        {
            var barRect = new Rect(position.x, position.y, position.width, 30);
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

            var lowRect = new Rect(barRect.x, barRect.y, barData.LowWidth, barRect.height);
            EditorGUI.DrawRect(lowRect, LowQualityColor);

            var medRect = new Rect(barRect.x + barData.LowWidth, barRect.y, barData.MedWidth, barRect.height);
            EditorGUI.DrawRect(medRect, MediumQualityColor);

            var highRect = new Rect(barRect.x + barData.LowWidth + barData.MedWidth, barRect.y, barData.HighWidth,
                barRect.height);
            EditorGUI.DrawRect(highRect, HighQualityColor);

            DrawQualityHandles(barRect, barData.LowWidth, barData.MedWidth);
        }

        private static void DrawQualityHandles(Rect barRect, float lowWidth, float medWidth)
        {
            const float handleWidth = 2f;

            if (lowWidth > 0)
            {
                var lowHandle = new Rect(barRect.x + lowWidth - handleWidth / 2,
                    barRect.y, handleWidth, barRect.height);
                EditorGUI.DrawRect(lowHandle, Color.black);
            }

            if (medWidth > 0)
            {
                var medHandle = new Rect(barRect.x + lowWidth + medWidth - handleWidth / 2,
                    barRect.y, handleWidth, barRect.height);
                EditorGUI.DrawRect(medHandle, Color.black);
            }
        }

        private static void DrawDistanceLabels(Rect position, QualityBarData barData)
        {
            var labelRect = new Rect(position.x, position.y + 32, position.width, 20);
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };

            if (barData.LowWidth > 0)
            {
                EditorGUI.LabelField(
                    new Rect(labelRect.x, labelRect.y, barData.LowWidth, labelRect.height),
                    "Low", style);
            }

            if (barData.MedWidth > 0)
            {
                EditorGUI.LabelField(
                    new Rect(labelRect.x + barData.LowWidth, labelRect.y, barData.MedWidth, labelRect.height),
                    "Medium", style);
            }

            EditorGUI.LabelField(
                new Rect(labelRect.x + barData.LowWidth + barData.MedWidth, labelRect.y, barData.HighWidth,
                    labelRect.height),
                "High", style);
        }

        private static void DrawDistanceSliders(GrassSettingSO settings)
        {
            settings.minFadeDistance = EditorGUILayout.Slider("Min Fade Distance", settings.minFadeDistance,
                0f, settings.maxFadeDistance - 1);
            settings.maxFadeDistance = EditorGUILayout.Slider("Max Fade Distance", settings.maxFadeDistance,
                settings.minFadeDistance + 1, 300f);

            settings.lowQualityDistance = EditorGUILayout.Slider("Low Quality Distance", settings.lowQualityDistance,
                0.01f, settings.mediumQualityDistance - 0.01f);
            settings.mediumQualityDistance = EditorGUILayout.Slider("Medium Quality Distance",
                settings.mediumQualityDistance,
                settings.lowQualityDistance + 0.01f, 1f);
        }
    }
}