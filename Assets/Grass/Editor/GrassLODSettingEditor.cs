using UnityEditor;
using UnityEngine;

public static class GrassLODSettingEditor
{
    private static readonly Color LowQualityColor = new Color(0.3f, 0.7f, 0.3f); // 가벼운 작업 = 초록색
    private static readonly Color MediumQualityColor = new Color(0.7f, 0.7f, 0.3f); // 중간 작업 = 노란색 
    private static readonly Color HighQualityColor = new Color(0.7f, 0.3f, 0.3f); // 무거운 작업 = 빨간색

    public static bool DrawLODSettingsPanel(GrassSettingSO settings)
    {
        var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        rect.y += 5;

        DrawQualityBar(rect, settings);
        DrawDistanceLabels(rect, settings);

        EditorGUILayout.Space(10);

        return DrawDistanceSliders(settings);
    }

    private static void DrawQualityBar(Rect position, GrassSettingSO settings)
    {
        var barRect = new Rect(position.x, position.y, position.width, 30);
        EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

        var normalizedMinFade = Mathf.Clamp01(settings.minFadeDistance / settings.maxFadeDistance);

        var highPercent = normalizedMinFade;
        var remainingPercent = 1f - highPercent;

        var lowWidth = barRect.width * (settings.lowQualityDistance * remainingPercent);
        var medWidth = barRect.width *
                       ((settings.mediumQualityDistance - settings.lowQualityDistance) * remainingPercent);
        var highWidth = barRect.width * highPercent +
                        barRect.width * ((1 - settings.mediumQualityDistance) * remainingPercent);

        var lowRect = new Rect(barRect.x, barRect.y, lowWidth, barRect.height);
        EditorGUI.DrawRect(lowRect, LowQualityColor);

        var medRect = new Rect(barRect.x + lowWidth, barRect.y, medWidth, barRect.height);
        EditorGUI.DrawRect(medRect, MediumQualityColor);

        var highRect = new Rect(barRect.x + lowWidth + medWidth, barRect.y, highWidth, barRect.height);
        EditorGUI.DrawRect(highRect, HighQualityColor);

        var handleWidth = 2f;

        if (lowWidth > 0)
        {
            var lowHandle = new Rect(barRect.x + lowWidth - handleWidth / 2,
                barRect.y, handleWidth, barRect.height);
            EditorGUI.DrawRect(lowHandle, new Color(0, 0, 0));
        }

        if (medWidth > 0)
        {
            var medHandle = new Rect(barRect.x + lowWidth + medWidth - handleWidth / 2,
                barRect.y, handleWidth, barRect.height);
            EditorGUI.DrawRect(medHandle, new Color(0, 0, 0));
        }
    }

    private static void DrawDistanceLabels(Rect position, GrassSettingSO settings)
    {
        var labelRect = new Rect(position.x, position.y + 32, position.width, 20);
        var style = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white },
            clipping = TextClipping.Clip
        };

        var normalizedMinFade = Mathf.Clamp01(settings.minFadeDistance / settings.maxFadeDistance);
        var remainingPercent = 1f - normalizedMinFade;

        var lowWidth = labelRect.width * (settings.lowQualityDistance * remainingPercent);
        var medWidth = labelRect.width *
                       ((settings.mediumQualityDistance - settings.lowQualityDistance) * remainingPercent);
        var highWidth = labelRect.width * normalizedMinFade +
                        labelRect.width * ((1 - settings.mediumQualityDistance) * remainingPercent);

        if (lowWidth > 0)
        {
            EditorGUI.LabelField(
                new Rect(labelRect.x, labelRect.y, lowWidth, labelRect.height),
                "Low", style);
        }

        if (medWidth > 0)
        {
            EditorGUI.LabelField(
                new Rect(labelRect.x + lowWidth, labelRect.y, medWidth, labelRect.height),
                "Medium", style);
        }

        EditorGUI.LabelField(
            new Rect(labelRect.x + lowWidth + medWidth, labelRect.y, highWidth, labelRect.height),
            "High", style);
    }

    private static bool DrawDistanceSliders(GrassSettingSO settings)
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Min Fade Distance
        var minFade = EditorGUILayout.Slider(new GUIContent("Min Fade Distance",
                "Distance at which grass begins to fade in"),
            settings.minFadeDistance, 0, settings.maxFadeDistance);

        // Max Fade Distance
        var maxFade = EditorGUILayout.Slider(new GUIContent("Max Fade Distance",
                "Maximum distance at which grass is rendered"),
            settings.maxFadeDistance, minFade, 300);

        EditorGUILayout.Space(5);

        // Quality Transition Distances
        var lowQuality = EditorGUILayout.Slider(new GUIContent("Low Quality Distance",
                "Distance at which grass transitions to low quality"),
            settings.lowQualityDistance, 0, settings.mediumQualityDistance);

        var mediumQuality = EditorGUILayout.Slider(new GUIContent("Medium Quality Distance",
                "Distance at which grass transitions to medium quality"),
            settings.mediumQualityDistance, lowQuality, 1);

        EditorGUILayout.EndVertical();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Modified LOD Settings");

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