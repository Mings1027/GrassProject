using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class EditorIcons
    {
        public static GUIContent Settings => IconContent("d_SettingsIcon");
        public static GUIContent Gizmos => IconContent("d_SceneViewVisibility");

        private static GUIContent IconContent(string iconName)
        {
            var icon = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(" ", icon?.image);
        }
    }
}