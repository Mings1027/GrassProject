using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ChildObjectRenamerEditor : EditorWindow
{
    [SerializeField] private GameObject targetObject;
    [SerializeField] private string baseName = "Child";
    [SerializeField] private string separator = "_";
    [SerializeField] private int startNumber = 0;
    [SerializeField] private int numberPadding = 2;
    [SerializeField] private bool includeInactive = true;

    private SerializedObject serializedObject;
    private SerializedProperty targetObjectProperty;

    private Vector2 scrollPosition;
    private List<Transform> childrenList = new List<Transform>();
    private List<string> previewNames = new List<string>();

    [MenuItem("Tools/Child Object Renamer")]
    public static void ShowWindow()
    {
        ChildObjectRenamerEditor window = GetWindow<ChildObjectRenamerEditor>();
        window.titleContent = new GUIContent("Child Object Renamer");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        targetObjectProperty = serializedObject.FindProperty("targetObject");

        RefreshChildrenList();
    }

    private void OnGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Child Object Renamer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Target Object 선택
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(targetObjectProperty,
            new GUIContent("Target Object", "드래그하거나 선택해서 대상 오브젝트를 등록하세요"));
        if (EditorGUI.EndChangeCheck())
        {
            RefreshChildrenList();
        }

        if (targetObject == null)
        {
            EditorGUILayout.HelpBox("대상 오브젝트를 선택해주세요.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space();

        // 네이밍 설정
        EditorGUILayout.LabelField("Naming Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        var newBaseNameText = EditorGUILayout.TextField(new GUIContent("Base Name", "자식들의 기본 이름"), baseName);
        if (newBaseNameText != baseName)
        {
            baseName = newBaseNameText;
            GUI.changed = true;
        }
        
        var newSeparatorText = EditorGUILayout.TextField(new GUIContent("Separator", "구분자 (기본값: _)"), separator);
        if (newSeparatorText != separator)
        {
            separator = newSeparatorText;
            GUI.changed = true;
        }

        // Start Number를 더 안전하게 처리
        int newStartNumber = EditorGUILayout.IntField(new GUIContent("Start Number", "시작 번호"), startNumber);
        if (newStartNumber != startNumber)
        {
            startNumber = newStartNumber;
            GUI.changed = true;
        }

        // Number Padding을 더 안전하게 처리 (최소값 1)
        int newNumberPadding = EditorGUILayout.IntSlider(new GUIContent("Number Padding", "숫자 자릿수 (예: 2 → 01, 02)"),
            numberPadding, 1, 5);
        if (newNumberPadding != numberPadding)
        {
            numberPadding = newNumberPadding;
            GUI.changed = true;
        }

        var newIncludeInactive = EditorGUILayout.Toggle(new GUIContent("Include Inactive"), includeInactive);
        if (newIncludeInactive != includeInactive)
        {
            includeInactive = newIncludeInactive;
            GUI.changed = true;
        }

        if (EditorGUI.EndChangeCheck() || GUI.changed)
        {
            RefreshChildrenList();
            GUI.changed = false;
        }

        EditorGUILayout.Space();

        // 미리보기
        if (childrenList.Count > 0)
        {
            EditorGUILayout.LabelField($"Children Found: {childrenList.Count}", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            for (int i = 0; i < childrenList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // 현재 이름
                EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
                EditorGUILayout.LabelField(childrenList[i].name, GUILayout.Width(150));

                // 화살표
                EditorGUILayout.LabelField("→", GUILayout.Width(20));

                // 새 이름 (미리보기)
                EditorGUILayout.LabelField(previewNames[i], EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // 적용 버튼들
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Apply Rename", GUILayout.Height(30)))
            {
                ApplyRename();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Refresh Preview", GUILayout.Height(30)))
            {
                RefreshChildrenList();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("선택된 오브젝트에 자식이 없습니다.", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void RefreshChildrenList()
    {
        childrenList.Clear();
        previewNames.Clear();

        if (targetObject == null) return;

        // 자식들 수집
        for (int i = 0; i < targetObject.transform.childCount; i++)
        {
            Transform child = targetObject.transform.GetChild(i);

            if (!includeInactive && !child.gameObject.activeInHierarchy)
                continue;

            childrenList.Add(child);
        }

        // 미리보기 이름 생성
        for (int i = 0; i < childrenList.Count; i++)
        {
            string newName = GenerateName(i);
            previewNames.Add(newName);
        }
    }

    private string GenerateName(int index)
    {
        int number = startNumber + index;
        int safePadding = Mathf.Max(1, numberPadding); // 최소 1로 보장
        string numberStr = number.ToString().PadLeft(safePadding, '0');

        return $"{baseName}{separator}{numberStr}";
    }

    private void ApplyRename()
    {
        if (targetObject == null || childrenList.Count == 0) return;

        // Undo 등록
        Object[] gameObjects = childrenList.Select(t => t.gameObject).ToArray<Object>();
    
        Undo.RecordObjects(gameObjects, "Rename Children");

        for (int i = 0; i < childrenList.Count; i++)
        {
            childrenList[i].name = previewNames[i];
        }

        // 씬 마크 더티
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(childrenList[0].gameObject.scene);
        }

        Debug.Log($"Successfully renamed {childrenList.Count} children of '{targetObject.name}'");
    }

    // 선택된 오브젝트가 변경될 때 자동으로 타겟 설정
    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null && targetObject == null)
        {
            targetObject = Selection.activeGameObject;
            RefreshChildrenList();
            Repaint();
        }
    }
}