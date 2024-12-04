using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class EditorIcons
    {
        public static GUIContent Settings => IconContent("d_SettingsIcon");
        public static GUIContent Gizmos => IconContent("d_SceneViewVisibility");
        public static GUIContent Cube => IconContent("d_PreMatCube");
        
        private static GUIContent IconContent(string iconName)
        {
            var icon = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(" ", icon?.image);
        }
    }
}