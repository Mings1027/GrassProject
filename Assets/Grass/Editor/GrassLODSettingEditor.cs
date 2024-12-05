using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class GrassLODSettingEditor
    {
        private static readonly Color LowQualityColor = new Color(0.3f, 0.7f, 0.3f);
        private static readonly Color MediumQualityColor = new Color(0.7f, 0.7f, 0.3f);
        private static readonly Color HighQualityColor = new Color(0.7f, 0.3f, 0.3f);

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

        public static bool DrawLODSettingsPanel(GrassSettingSO settings)
        {
            var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            rect.y += 5;

            var barData = QualityBarData.Calculate(settings, rect.width);
            DrawQualityBar(rect, barData);
            DrawDistanceLabels(rect, barData);

            EditorGUILayout.Space(5);

            return DrawDistanceSliders(settings);
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

        private static bool DrawDistanceSliders(GrassSettingSO settings)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Fade Distance", GUILayout.Width(150));
            var minFade = GUILayout.HorizontalSlider(settings.minFadeDistance, 0f, settings.maxFadeDistance);
            var minFadeFormatted = Mathf.Round(minFade * 10) / 10f;
            minFade = EditorGUILayout.FloatField(minFadeFormatted, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Fade Distance", GUILayout.Width(150));
            var maxFade = GUILayout.HorizontalSlider(settings.maxFadeDistance, minFade, 300f);
            var maxFadeFormatted = Mathf.Round(maxFade * 10) / 10f;
            maxFade = EditorGUILayout.FloatField(maxFadeFormatted, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Low Quality Distance", GUILayout.Width(150));
            var lowQuality =
                GUILayout.HorizontalSlider(settings.lowQualityDistance, 0f, settings.mediumQualityDistance);
            var lowQualityFormatted = Mathf.Round(lowQuality * 100) / 100f;
            lowQuality = EditorGUILayout.FloatField(lowQualityFormatted, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Medium Quality Distance", GUILayout.Width(150));
            var mediumQuality = GUILayout.HorizontalSlider(settings.mediumQualityDistance, lowQuality, 1f);
            var mediumQualityFormatted = Mathf.Round(mediumQuality * 100) / 100f;
            mediumQuality = EditorGUILayout.FloatField(mediumQualityFormatted, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                settings.minFadeDistance = minFade;
                settings.maxFadeDistance = maxFade;
                settings.lowQualityDistance = lowQuality;
                settings.mediumQualityDistance = mediumQuality;

                EditorUtility.SetDirty(settings);
                return true;
            }

            return false;
        }
    }
}