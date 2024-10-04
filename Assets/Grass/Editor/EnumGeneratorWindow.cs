using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnumGeneratorWindow : EditorWindow
{
    private string _enumName = "New Enum";
    private string _savePath;
    private string _enumValuesInput = "";

    private readonly List<string[]> _folderLevels = new(); // 각 폴더 깊이를 저장
    private readonly List<int> _selectedIndices = new(); // 각 깊이의 선택된 인덱스 저장
    private Texture2D _folderIcon;

    private string _errorMessage = "";

    [MenuItem("Tools/Enum Generator")]
    public static void ShowWindow()
    {
        GetWindow<EnumGeneratorWindow>("Enum Generator");
    }

    private void OnEnable()
    {
        LoadFoldersAtLevel(Application.dataPath, 0); // 최상위 폴더를 먼저 로드
        _folderIcon = EditorGUIUtility.FindTexture("Folder");
    }

    private void LoadFoldersAtLevel(string basePath, int level)
    {
        while (true)
        {
            var allFolders = Directory.GetDirectories(basePath);
            var folderNames = new string[allFolders.Length];

            for (var i = 0; i < allFolders.Length; i++)
            {
                folderNames[i] = Path.GetFileName(allFolders[i]);
            }

            // 해당 레벨에 폴더가 없으면 리스트에 추가
            if (_folderLevels.Count <= level)
            {
                _folderLevels.Add(folderNames);
                _selectedIndices.Add(0); // 해당 레벨의 선택된 인덱스를 0으로 초기화
            }
            else
            {
                // 이미 폴더가 로드된 레벨이면 업데이트
                _folderLevels[level] = folderNames;
                _selectedIndices[level] = 0; // 해당 레벨 선택 초기화
            }

            // 하위 레벨을 리셋
            for (var i = _folderLevels.Count - 1; i > level; i--)
            {
                _folderLevels.RemoveAt(i);
                _selectedIndices.RemoveAt(i);
            }

            // 첫 번째 폴더가 존재하면 자동으로 하위 폴더 로드
            if (folderNames.Length > 0)
            {
                var selectedPath = GetSelectedPathUpToLevel(level);
                basePath = selectedPath;
                level += 1;
                continue;
            }

            break;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Enum Generator", EditorStyles.boldLabel);

        // Enum 이름 입력 필드
        _enumName = EditorGUILayout.TextField("Enum Name", _enumName);

        // 동적 경로 선택을 위한 Popup들을 생성
        for (var i = 0; i < _folderLevels.Count; i++)
        {
            if (_folderLevels[i].Length > 0)
            {
                var newIndex = EditorGUILayout.Popup($"Select Folder {i + 1}", _selectedIndices[i],
                    _folderLevels[i]);

                // 선택이 변경되었을 경우, 하위 폴더 로드
                if (newIndex != _selectedIndices[i])
                {
                    _selectedIndices[i] = newIndex;
                    var selectedPath = GetSelectedPathUpToLevel(i);
                    LoadFoldersAtLevel(selectedPath, i + 1); // 하위 폴더 로드
                }
            }
        }

        // 최종 선택된 경로 계산
        _savePath = GetSelectedPathUpToLevel(_folderLevels.Count - 1);

        GUILayout.Space(10);
        GUILayout.Label("Enum Values (Each line will be a new value)");

        // 여러 줄 입력 필드
        _enumValuesInput = EditorGUILayout.TextArea(_enumValuesInput, GUILayout.Height(100));

        GUILayout.Space(10);

        // Generate 버튼
        if (GUILayout.Button("Generate Enum"))
        {
            GenerateEnum();
        }

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            GUILayout.Space(10);

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                normal =
                {
                    textColor = new Color(1f, .3f, 0f, 1f),
                }
            };
            GUILayout.Label("Error",style);
            GUILayout.Label(_errorMessage, style);
        }
    }

    private string GetSelectedPathUpToLevel(int level)
    {
        // 선택한 경로를 계산
        var path = Application.dataPath;

        for (var i = 0; i <= level; i++)
        {
            if (_folderLevels[i].Length > 0)
            {
                path = Path.Combine(path, _folderLevels[i][_selectedIndices[i]]);
            }
        }

        return path;
    }

    private void GenerateEnum()
    {
        // 입력된 텍스트를 줄 단위로 나눠서 배열로 변환
        var enumValues = _enumValuesInput.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);

        var duplicates = FindDuplicates(enumValues);
        if (duplicates.Count > 0)
        {
            _errorMessage = "Duplicate values found: " + string.Join(", ", duplicates);
            return;
        }

        var path = Path.Combine(Application.dataPath, _savePath, _enumName + ".cs");

        // 랜덤 값의 범위 설정
        const int minValue = 0;
        const int maxValue = 1000; // 랜덤 값의 최대 범위, 필요에 따라 조정 가능

        // 랜덤 값이 중복되지 않도록 하기 위한 HashSet
        var usedValues = new HashSet<int>();

        using (var writer = new StreamWriter(path, false))
        {
            writer.WriteLine("public enum " + _enumName);
            writer.WriteLine("{");

            for (var i = 0; i < enumValues.Length; i++)
            {
                int randomValue;

                // 랜덤 값이 중복되지 않도록 새로운 값을 계속해서 생성
                do
                {
                    randomValue = Random.Range(minValue, maxValue);
                } while (usedValues.Contains(randomValue));

                // 중복되지 않은 값을 HashSet에 추가
                usedValues.Add(randomValue);

                writer.WriteLine($"\t{enumValues[i].Trim()} = {randomValue},");
            }

            writer.WriteLine("}");
        }

        AssetDatabase.Refresh();
        Debug.Log(_enumName + " enum has been generated at " + path);
        _enumName = "";
        _enumValuesInput = "";
        _errorMessage = "";
    }

    private List<string> FindDuplicates(string[] enumValues)
    {
        var duplicates = new List<string>();
        var seenValues = new HashSet<string>();

        for (int i = 0; i < enumValues.Length; i++)
        {
            var trimmedValue = enumValues[i].Trim();
            if (seenValues.Contains(trimmedValue))
            {
                duplicates.Add(trimmedValue);
            }
            else
            {
                seenValues.Add(trimmedValue);
            }
        }

        return duplicates;
    }
}