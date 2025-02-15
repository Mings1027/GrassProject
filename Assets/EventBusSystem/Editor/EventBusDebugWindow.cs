using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventBusSystem.Scripts;
using UnityEditor;
using UnityEngine;

namespace EventBusSystem.Editor
{
    public class EventBusDebugWindow : EditorWindow
    {
        private const string ShowAllEventBusKey = "EventBusDebug_ShowAllEventBus";
        private bool _showAllEventBus;

        private void OnEnable()
        {
            _showAllEventBus = EditorPrefs.GetBool(ShowAllEventBusKey, false);
        }

        private void OnGUI()
        {
            DrawButtons();
            DrawEventBusBindings();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // Show All Event Bus toggle button
            if (EditorHelper.DrawToggleButton(
                    new GUIContent("Show All Event Bus", "Show all event buses including those with no bindings"),
                    _showAllEventBus,
                    out bool newShowAllState))
            {
                _showAllEventBus = newShowAllState;
                EditorPrefs.SetBool(ShowAllEventBusKey, newShowAllState);
            }

            // Enable All Log toggle button
            if (EditorHelper.DrawToggleButton(
                    new GUIContent("Enable All Log", "Enable logging for all event buses"),
                    EventBusDebug.EnableLog,
                    out bool newEnableLogState))
            {
                EventBusDebug.SetLogEnabled(newEnableLogState);

                // Also update all individual event bus log states
                if (EventBusUtil.EventTypes != null)
                {
                    foreach (var eventType in EventBusUtil.EventTypes)
                    {
                        var busType = typeof(EventBus<>).MakeGenericType(eventType);
                        var setLogEnabled = busType.GetMethod("SetLogEnabled");
                        setLogEnabled?.Invoke(null, new object[] { newEnableLogState });
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawEventBusBindings()
        {
            if (EventBusUtil.EventTypes == null) return;

            foreach (var eventType in EventBusUtil.EventTypes)
            {
                var bindingCount = GetBindingCount(eventType);
                if (bindingCount == 0 && !_showAllEventBus) continue;

                EditorHelper.DrawFoldoutSection(new GUIContent($"{eventType.Name}    Registered: {bindingCount}"),
                    () => DrawEventBindings(eventType)
                );
            }
        }

        private void DrawEventBindings(Type eventType)
        {
            var bindings = GetBindings(eventType);
            if (bindings == null) return;

            foreach (var binding in bindings)
            {
                if (binding == null) continue;


                // OnEvent 델리게이트
                var onEventDelegate = GetFieldValue<Delegate>(binding, "_onEvent");
                if (onEventDelegate != null)
                {
                    foreach (var handler in onEventDelegate.GetInvocationList())
                    {
                        if (IsValidMethod(handler.Method))
                            DrawMethodInfo(handler.Method, handler.Target);
                    }
                }

                // OnEventNoArgs 델리게이트
                var onEventNoArgsDelegate = GetFieldValue<Delegate>(binding, "_onEventNoArgs");
                if (onEventNoArgsDelegate != null)
                {
                    foreach (var handler in onEventNoArgsDelegate.GetInvocationList())
                    {
                        if (IsValidMethod(handler.Method))
                            DrawMethodInfo(handler.Method, handler.Target);
                    }
                }
            }
        }

        private bool IsValidMethod(MethodInfo method)
        {
            if (method == null) return false;

            // 컴파일러 생성 메서드 필터링
            if (method.Name.Contains("<") || method.Name.Contains(">")) return false;
            if (method.Name.Contains(".ctor")) return false;
            if (method.DeclaringType?.Name.Contains("<") ?? false) return false;

            return true;
        }

        private void DrawMethodInfo(MethodInfo method, object target)
        {
            EditorGUILayout.BeginHorizontal();

            // 스크립트 아이콘
            var scriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image;
            GUILayout.Label(new GUIContent(scriptIcon), GUILayout.Width(20), GUILayout.Height(20));

            // 타겟 객체 정보와 메서드 이름
            var targetType = target?.GetType();
            string targetInfo = targetType != null ? $"{targetType.Name}" : "Static";

            var buttonStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.4f, 0.6f, 1.0f) }, // 링크처럼 보이게 파란색으로
                hover = { textColor = new Color(0.6f, 0.8f, 1.0f) }
            };

            // 메서드 이름을 버튼으로 표시
            if (GUILayout.Button($"{targetInfo}.{method.Name}", buttonStyle))
            {
                OpenScriptAtMethod(method);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OpenScriptAtMethod(MethodInfo method)
        {
            if (method == null) return;

            var scripts = AssetDatabase.FindAssets($"t:Script {method.DeclaringType.Name}");
            foreach (var guid in scripts)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script != null && script.GetClass() == method.DeclaringType)
                {
                    // 메소드 이름으로 해당 라인을 찾습니다
                    var lines = script.text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains($" {method.Name}(")) // 메소드 선언 패턴 매칭
                        {
                            // 해당 라인 번호로 파일을 엽니다
                            AssetDatabase.OpenAsset(script, i + 1);
                            return;
                        }
                    }

                    // 메소드를 찾지 못했다면 그냥 파일만 엽니다
                    AssetDatabase.OpenAsset(script);
                    break;
                }
            }
        }

        private IEnumerable<object> GetBindings(Type eventType)
        {
            try
            {
                var busType = typeof(EventBus<>).MakeGenericType(eventType);
                var bindingsField = busType.GetField("Bindings",
                    BindingFlags.NonPublic | BindingFlags.Static);

                return bindingsField?.GetValue(null) as IEnumerable<object>;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting bindings for {eventType.Name}: {e.Message}");
                return null;
            }
        }

        private int GetBindingCount(Type eventType)
        {
            var bindings = GetBindings(eventType);
            return bindings?.Count() ?? 0;
        }

        private T GetFieldValue<T>(object obj, string fieldName)
        {
            try
            {
                var field = obj.GetType().GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return (T)field?.GetValue(obj);
            }
            catch
            {
                return default;
            }
        }
    }
}