using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using Grass.Editor;

#if UNITY_EDITOR
public class EventBusDebugWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private bool _showEditorBindings;
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
        if (EventBusEditorHelper.DrawToggleButton("Debug Mode", EventBusDebug.IsGlobalDebugEnabled,
                out var enableDebugMode))
        {
            EventBusDebug.EnableGlobalDebugMode(enableDebugMode);
        }

        if (!Application.isPlaying)
        {
            // Show editor bindings toggle only in edit mode
            if (EventBusEditorHelper.DrawToggleButton(
                    "Show Editor-time Bindings",
                    "Display event bindings registered during edit mode (e.g. from [ExecuteInEditMode] scripts)",
                    _showEditorBindings,
                    out var showEditorBindings))
            {
                _showEditorBindings = showEditorBindings;
            }
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

        // Clear all buses button (only in play mode)
        if (Application.isPlaying)
        {
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Button("Clear ALL Event Buses", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All Event Buses",
                        "Are you sure you want to clear ALL event buses?\nThis will remove all registered listeners from every event bus.",
                        "Yes, Clear All", "Cancel"))
                {
                    EventBusUtil.ClearAllBuses();
                    Debug.Log("Cleared all event buses");
                }
            }

            GUI.backgroundColor = prevColor;
        }

        // Auto-repaint in play mode to update statistics
        if (Application.isPlaying)
        {
            Repaint();
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
                else if (_showEditorBindings)
                {
                    DisplayEditorTimeStats(eventType);
                }
            });
        }

        if (!Application.isPlaying && !_showEditorBindings)
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
                    foreach (var method in registeredMethods)
                    {
                        EditorGUILayout.LabelField($"• {method}");
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
                foreach (var method in registeredMethods)
                {
                    EditorGUILayout.LabelField($"• {method}");
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
}
#endif