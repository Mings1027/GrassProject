#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using Editor;
using EventBusSystem.Scripts;

public class EventBusDebugWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private List<Type> _cachedEventTypes;

    [MenuItem("Tools/Event Bus/Event Bus Debug")]
    public static void ShowWindow()
    {
        var window = GetWindow<EventBusDebugWindow>("Event Bus Debug");
        window.Show();
    }

    private void OnEnable() => UpdateEventTypesCache();

    private void UpdateEventTypesCache()
    {
        _cachedEventTypes = PredefinedAssemblyUtil.GetTypes(typeof(IEvent));
    }

    private void OnGUI()
    {
        DrawGlobalControls();
        DrawEventTypeList();
        DrawClearAllButton();
    }

    private void DrawGlobalControls()
    {
        if (CustomEditorHelper.DrawToggleButton("Enable All Event Bus Log", EventBusDebug.EnableLog,
                out var enableLog))
        {
            EventBusDebug.SetLogEnabled(enableLog);
            SetAllEventBusLogs(enableLog);
            EditorPrefs.SetBool(EventBusDebug.EventBusDebugEnableLog, enableLog);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Refresh Event Types"))
        {
            UpdateEventTypesCache();
        }
    }

    private void DrawEventTypeList()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_cachedEventTypes?.Count > 0)
        {
            if (Application.isPlaying)
            {
                DisplayEventList(DisplayRuntimeEventInfo);
                Repaint();
            }
            else
            {
                DisplayEventList(DisplayEditorTimeEventInfo);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No event types found. Try refreshing event types.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10);
    }

    private void DisplayEventList(Action<Type> displayFunc)
    {
        foreach (var eventType in _cachedEventTypes)
        {
            CustomEditorHelper.DrawFoldoutSection($"Event: {eventType.Name}", () =>
            {
                DisplayEventProperties(eventType);
                EditorGUILayout.Space(5);
                displayFunc(eventType);
            });
        }
    }

    private void DisplayEditorTimeEventInfo(Type eventType)
    {
        var busType = typeof(EventBus<>).MakeGenericType(eventType);
        var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);

        DisplayLogToggle(eventType.Name, busType);
        if (activeBindings > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Editor-time Active Bindings: {activeBindings}", EditorStyles.boldLabel);
            DisplayRegisteredMethodsList(busType);
        }
        else
        {
            EditorGUILayout.LabelField("No editor-time bindings registered");
        }
    }

    private void DisplayRuntimeEventInfo(Type eventType)
    {
        var busType = typeof(EventBus<>).MakeGenericType(eventType);

        DisplayLogToggle(eventType.Name, busType);
        DisplayEventStats(busType);
        DisplayRegisteredMethodsList(busType);
        DisplayClearButton(eventType, busType);
    }

    private void DisplayEventStats(Type busType)
    {
        var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);
        var totalRaised = (int)busType.GetMethod("GetTotalEventsRaised")?.Invoke(null, null);

        EditorGUILayout.LabelField($"Active Bindings: {activeBindings}");
        EditorGUILayout.LabelField($"Total Events Raised: {totalRaised}");
    }

    private void DisplayRegisteredMethodsList(Type busType)
    {
        var getRegisteredList = busType.GetMethod("GetRegisteredList");
        if (getRegisteredList != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Registered Methods:", EditorStyles.boldLabel);
            var registeredMethods = (List<string>)getRegisteredList.Invoke(null, null);
            DisplayRegisteredMethods(registeredMethods);
        }
    }

    private void DisplayRegisteredMethods(List<string> registeredMethods)
    {
        if (registeredMethods?.Count > 0)
        {
            EditorGUI.indentLevel++;
            var linkStyle = CreateLinkStyle();

            foreach (var method in registeredMethods)
            {
                DrawMethodLink(method, linkStyle);
            }

            EditorGUI.indentLevel--;
        }
    }

    private GUIStyle CreateLinkStyle()
    {
        return new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.3f, 0.7f, 1f) },
            hover = { textColor = new Color(1f, 1f, 1f) },
            padding = new RectOffset(15, 0, 0, 0)
        };
    }

    private void DrawMethodLink(string method, GUIStyle linkStyle)
    {
        var rect = EditorGUILayout.GetControlRect();
        var bulletRect = new Rect(rect.x, rect.y, 15, rect.height);
        var linkRect = new Rect(rect.x + 15, rect.y, rect.width - 15, rect.height);

        EditorGUI.LabelField(bulletRect, "•");

        if (GUI.Button(linkRect, method, linkStyle))
        {
            OpenScriptWithMethod(method);
        }

        if (linkRect.Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
        }
    }

    private void DrawClearAllButton()
    {
        bool hasAnyBindings = false;
        foreach (var eventType in _cachedEventTypes)
        {
            var busType = typeof(EventBus<>).MakeGenericType(eventType);
            var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);
            if (activeBindings > 0)
            {
                hasAnyBindings = true;
                break;
            }
        }

        if (hasAnyBindings)
        {
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            var buttonText = Application.isPlaying
                ? "Clear ALL Runtime Event Buses"
                : "Clear ALL Editor Event Buses";

            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                string message = Application.isPlaying
                    ? "Are you sure you want to clear ALL event buses?\nThis will remove all registered listeners from every event bus."
                    : "Are you sure you want to clear ALL event buses?\nThis will remove all registered listeners including those registered in Edit Mode.";

                if (EditorUtility.DisplayDialog("Clear All Event Buses", message, "Yes, Clear All", "Cancel"))
                {
                    EventBusUtil.ClearAllBuses();
                    Debug.Log($"Cleared all event buses in {(Application.isPlaying ? "Play" : "Edit")} mode");
                }
            }

            GUI.backgroundColor = prevColor;
        }
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

    private void DisplayLogToggle(string eventTypeName, Type busType)
    {
        bool currentState = EventBusDebug.GetEventSpecificLogEnabled(eventTypeName);
        if (CustomEditorHelper.DrawToggleButton("Enable Log", currentState, out var newState))
        {
            EventBusDebug.SetEventSpecificLogEnabled(eventTypeName, newState);
            var setLogEnabled = busType.GetMethod("SetLogEnabled");
            if (setLogEnabled != null)
            {
                setLogEnabled.Invoke(null, new object[] { newState });
            }
        }
    }

    private void DisplayClearButton(Type eventType, Type busType)
    {
        var activeBindings = (int)busType.GetMethod("GetActiveBindingsCount")?.Invoke(null, null);

        if (activeBindings > 0 && GUILayout.Button($"Clear {eventType.Name} Event Bus"))
        {
            if (EditorUtility.DisplayDialog($"Clear {eventType.Name} Event Bus",
                    $"Are you sure you want to clear {eventType.Name} event bus? This will remove all registered listeners.",
                    "Yes", "No"))
            {
                var clearMethod = busType.GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Static);
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

    private static void DisplayEventProperties(Type eventType)
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
    }
}
#endif