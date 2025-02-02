#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class EditorIcons
    {
        public static GUIContent Settings => IconContent("d_SettingsIcon");
        public static GUIContent Gizmos => IconContent("d_SceneViewVisibility");
        public static GUIContent Cube => IconContent("d_PreMatCube");
        public static GUIContent Info => IconContent("d_console.infoicon");
        public static GUIContent Warning => IconContent("d_console.warnicon");
        public static GUIContent Trash => IconContent("d_TreeEditor.Trash");
        public static GUIContent Minus => IconContent("d_Toolbar Minus");
        
        private static GUIContent IconContent(string iconName)
        {
            var icon = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(" ", icon?.image);
        }
    }
}
#endif