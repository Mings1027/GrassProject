using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using EventBusSystem.Scripts;
using Grass.Editor;

#if UNITY_EDITOR
public class EventBusDebugWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private List<Type> _cachedEventTypes;

    [MenuItem("Tools/Event Bus Debug")]
    public static void ShowWindow()
    {
        var window = GetWindow<EventBusDebugWindow>("Event Bus Debug");
        window.Show();
    }

    private void OnEnable()
    {
        UpdateEventTypesCache();
        
    }

    private void UpdateEventTypesCache()
    {
        _cachedEventTypes = PredefinedAssemblyUtil.GetTypes(typeof(IEvent));
    }

    private void OnGUI()
    {
        // Global debug toggle
        if (EventBusEditorHelper.DrawToggleButton("Enable All Event Bus Log", EventBusDebug.EnableLog,
                out var enableLog))
        {
            EventBusDebug.SetLogEnabled(enableLog);
            SetAllEventBusLogs(enableLog);
            EditorPrefs.SetBool(EventBusDebug.EventBusDebugEnableLog, enableLog);
        }

        EditorGUILayout.Space(10);

        // Button to refresh event types
        if (GUILayout.Button("Refresh Event Types"))
        {
            UpdateEventTypesCache();
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_cachedEventTypes == null || _cachedEventTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("No event types found. Try refreshing event types.", MessageType.Info);
        }
        else
        {
            DisplayEventTypes();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // Clear all buses button
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

        if (GUILayout.Button("Clear ALL Event Buses", GUILayout.Height(30)))
        {
            string message = Application.isPlaying 
                ? "Are you sure you want to clear ALL event buses?\nThis will remove all registered listeners from every event bus."
                : "Are you sure you want to clear ALL event buses?\nThis will remove all registered listeners including those registered in Edit Mode.";
        
            if (EditorUtility.DisplayDialog("Clear All Event Buses",
                    message,
                    "Yes, Clear All", "Cancel"))
            {
                EventBusUtil.ClearAllBuses();
        
                // Force repaint all inspector windows to reflect the changes
                foreach (var editor in ActiveEditorTracker.sharedTracker.activeEditors)
                {
                    editor.Repaint();
                }
        
                Debug.Log($"Cleared all event buses in {(Application.isPlaying ? "Play" : "Edit")} mode");
            }
        }

        GUI.backgroundColor = prevColor;
    }

    private void SetAllEventBusLogs(bool enabled)
    {
        if (_cachedEventTypes == null) return;
        foreach (var eventType in _cachedEventTypes)
        {
            var busType = typeof(EventBus<>).MakeGenericType(eventType);
            var setLogEnabled = busType.GetMethod("SetLogEnabled");
            if (setLogEnabled != null)
            {
                setLogEnabled.Invoke(null, new object[] { enabled });
            }
        }
    }

    private void DisplayEventTypes()
    {
        foreach (var eventType in _cachedEventTypes)
        {
            EventBusEditorHelper.DrawFoldoutSection($"Event: {eventType.Name}", () =>
            {
                var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Properties:", EditorStyles.boldLabel);
                    foreach (var prop in properties)
                    {
                        EditorGUILayout.LabelField($"• {prop.PropertyType.Name} {prop.Name}");
                    }

                    EditorGUI.indentLevel--;
                }

                if (Application.isPlaying)
                {
                    DisplayRuntimeStats(eventType);
                }
                else
                {
                    DisplayEditorTimeStats(eventType);
                }
            });
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enable 'Show Editor-time Bindings' to see event bindings registered during edit mode, or enter play mode to see runtime statistics.",
                MessageType.Info
            );
        }
    }

    private void DisplayEditorTimeStats(Type eventType)
    {
        var busType = typeof(EventBus<>).MakeGenericType(eventType);
        var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);

        if (activeBindings > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Editor-time Active Bindings: {activeBindings}", EditorStyles.boldLabel);

            var getRegisteredList = busType.GetMethod("GetRegisteredList");
            if (getRegisteredList != null)
            {
                var registeredMethods = (List<string>)getRegisteredList.Invoke(null, null);
                if (registeredMethods != null && registeredMethods.Count > 0)
                {
                    EditorGUI.indentLevel++;

                    var linkStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(0.3f, 0.7f, 1f) },
                        hover = { textColor = new Color(1f, 1f, 1f) }
                    };
                    linkStyle.padding.left = 15; // 들여쓰기를 위한 패딩

                    foreach (var method in registeredMethods)
                    {
                        var rect = EditorGUILayout.GetControlRect();
                        var bulletRect = new Rect(rect.x, rect.y, 15, rect.height);
                        var linkRect = new Rect(rect.x + 15, rect.y, rect.width - 15, rect.height);

                        // 불릿 포인트 그리기
                        EditorGUI.LabelField(bulletRect, "•");

                        // 링크처럼 보이는 메서드 이름
                        if (GUI.Button(linkRect, method, linkStyle))
                        {
                            OpenScriptWithMethod(method);
                        }

                        // 마우스가 링크 위에 있을 때 커서 변경
                        GUIUtility.GetControlID(FocusType.Passive);
                        if (linkRect.Contains(Event.current.mousePosition))
                        {
                            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("No editor-time bindings registered");
        }
    }

    private void DisplayRuntimeStats(Type eventType)
    {
        var busType = typeof(EventBus<>).MakeGenericType(eventType);

        var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);
        var totalRaised = (int)busType.GetMethod("GetTotalEventsRaised")?.Invoke(null, null);

        EditorGUILayout.Space(5);

        var setLogEnabled = busType.GetMethod("SetLogEnabled");
        if (setLogEnabled != null)
        {
            var getLogEnabled = busType.GetField("localLogEnabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (getLogEnabled != null)
            {
                var currentLogEnabled = (bool)getLogEnabled.GetValue(null);
                var newLogEnabled = EditorGUILayout.Toggle("Enable Log", currentLogEnabled);
                if (newLogEnabled != currentLogEnabled)
                {
                    setLogEnabled.Invoke(null, new object[] { newLogEnabled });
                }
            }
        }

        EditorGUILayout.LabelField($"Active Bindings: {activeBindings}");
        EditorGUILayout.LabelField($"Total Events Raised: {totalRaised}");

        var getRegisteredList = busType.GetMethod("GetRegisteredList");
        if (getRegisteredList != null)
        {
            var registeredMethods = (List<string>)getRegisteredList.Invoke(null, null);
            if (registeredMethods != null && registeredMethods.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Registered Methods:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                var linkStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.3f, 0.7f, 1f) },
                    hover = { textColor = new Color(1f, 1f, 1f) }
                };
                linkStyle.padding.left = 15; // 들여쓰기를 위한 패딩

                foreach (var method in registeredMethods)
                {
                    var rect = EditorGUILayout.GetControlRect();
                    var bulletRect = new Rect(rect.x, rect.y, 15, rect.height);
                    var linkRect = new Rect(rect.x + 15, rect.y, rect.width - 15, rect.height);

                    // 불릿 포인트 그리기
                    EditorGUI.LabelField(bulletRect, "•");

                    // 링크처럼 보이는 메서드 이름
                    if (GUI.Button(linkRect, method, linkStyle))
                    {
                        OpenScriptWithMethod(method);
                    }

                    // 마우스가 링크 위에 있을 때 커서 변경
                    GUIUtility.GetControlID(FocusType.Passive);
                    if (linkRect.Contains(Event.current.mousePosition))
                    {
                        EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        if (GUILayout.Button($"Clear {eventType.Name} Event Bus"))
        {
            if (EditorUtility.DisplayDialog($"Clear {eventType.Name} Event Bus",
                    $"Are you sure you want to clear {eventType.Name} event bus? This will remove all registered listeners.",
                    "Yes", "No"))
            {
                var clearMethod = busType.GetMethod("Clear",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (clearMethod != null)
                {
                    clearMethod.Invoke(null, null);
                    Debug.Log($"Cleared {eventType.Name} Event Bus");
                }
                else
                {
                    Debug.LogError($"Could not find Clear method for {eventType.Name} Event Bus");
                }
            }
        }
    }

    private void OpenScriptWithMethod(string methodInfo)
    {
        var parts = methodInfo.Split('.');
        if (parts.Length != 2) return;

        string className = parts[0];
        string methodName = parts[1];

        var guids = AssetDatabase.FindAssets("t:Script");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

            if (script != null)
            {
                var scriptClass = script.GetClass();
                if (scriptClass != null && scriptClass.Name == className)
                {
                    var method = scriptClass.GetMethod(methodName,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.Instance);

                    if (method != null)
                    {
                        int lineNumber = GetMethodLineNumber(script.text, method.Name);
                        if (lineNumber > 0)
                        {
                            AssetDatabase.OpenAsset(script, lineNumber);
                            return;
                        }
                    }

                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }
        }
    }

    private int GetMethodLineNumber(string scriptText, string methodName)
    {
        string[] lines = scriptText.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(methodName) &&
                lines[i].Contains("(") &&
                !lines[i].Contains(";")) // 메서드 호출이 아닌 선언을 찾기 위함
            {
                return i + 1;
            }
        }

        return -1;
    }
}
#endif