using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Ref - https://github.com/needle-tools/shader-variant-explorer

public class ShaderVariantChecker : EditorWindow
{
    private static MethodInfo _getVariantCount, _getShaderGlobalKeywords, _getShaderLocalKeywords;

    private Shader _shader;

    private ulong _variantCount;
    private ulong _usedVariantCount;
    private int _keywordCount;
    private int _materialCount;
    private readonly Dictionary<string, int> _materialKeywords = new();
    private bool _isChecked;

    private Vector2 _scrollPosition;

    [MenuItem("CustomTool/ShaderVariantChecker")]
    private static void ShowWindow()
    {
        GetWindow<ShaderVariantChecker>().Show();
    }


    public void OnGUI()
    {
        _shader = EditorGUILayout.ObjectField("Shader", _shader, typeof(Shader), true) as Shader;
        if (GUILayout.Button("Check"))
        {
            if (!_shader) return;
            Process(_shader);
        }

        if (!_isChecked) return;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Variant Count : " + _variantCount);
        EditorGUILayout.LabelField("Used Variant Count : " + _usedVariantCount);
        EditorGUILayout.LabelField("Keyword Count : " + _keywordCount);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Used Material Count : " + _materialCount);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        Color defaultColor = GUI.color;
        foreach (KeyValuePair<string, int> keyword in _materialKeywords)
        {
            EditorGUILayout.BeginHorizontal();
            bool isValid = keyword.Value > 0;
            if (isValid) GUI.color = Color.green;
            EditorGUILayout.TextField(keyword.Key);
            EditorGUILayout.TextField(keyword.Value.ToString(), GUILayout.Width(30));

            GUI.color = defaultColor;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void Process(Shader shader)
    {
        if (!shader) return;


        GetShaderDetails(shader, out var variantCount, out var usedVariantCount, out string[] localKeywords,
            out string[] globalKeywords);

        _materialKeywords.Clear();
        for (int i = 0; i < localKeywords.Length; i++)
        {
            _materialKeywords.Add(localKeywords[i], 0);
        }

        for (int i = 0; i < globalKeywords.Length; i++)
        {
            if (_materialKeywords.ContainsKey(globalKeywords[i])) continue;
            _materialKeywords.Add(globalKeywords[i], 0);
        }

        _variantCount = variantCount;
        _usedVariantCount = usedVariantCount;
        _keywordCount = _materialKeywords.Count;

        // Debug.Log("variantCount : " + variantCount + ", usedVariantCount : " + usedVariantCount + ", keywordCount : " + keywordTotalCount);

        var materials = FindMaterialsUsingShader(shader);
        _materialCount = materials.Count;
        for (int i = 0; i < materials.Count; i++)
        {
            var mat = materials[i];
            if (!mat) continue;
            var keywords = mat.shaderKeywords;
            if (keywords == null || keywords.Length == 0) continue;

            for (int j = 0; j < keywords.Length; j++)
            {
                if (!_materialKeywords.ContainsKey(keywords[j])) continue;
                _materialKeywords[keywords[j]]++;
            }
        }

        _isChecked = true;
    }

    void GetShaderDetails(Shader requestedShader, out ulong shaderVariantCount, out ulong usedShaderVariantCount,
                          out string[] localKeywords, out string[] globalKeywords)
    {
        if (_getVariantCount == null)
            _getVariantCount = typeof(ShaderUtil).GetMethod("GetVariantCount", (BindingFlags)(-1));
        if (_getShaderGlobalKeywords == null)
            _getShaderGlobalKeywords = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", (BindingFlags)(-1));
        if (_getShaderLocalKeywords == null)
            _getShaderLocalKeywords = typeof(ShaderUtil).GetMethod("GetShaderLocalKeywords", (BindingFlags)(-1));

        if (_getVariantCount == null || _getShaderGlobalKeywords == null || _getShaderLocalKeywords == null)
        {
            shaderVariantCount = 0;
            usedShaderVariantCount = 0;
            localKeywords = null;
            globalKeywords = null;
            return;
        }

        shaderVariantCount = (ulong)_getVariantCount.Invoke(null, new object[] { requestedShader, false });
        usedShaderVariantCount = (ulong)_getVariantCount.Invoke(null, new object[] { requestedShader, true });
        localKeywords = (string[])_getShaderLocalKeywords.Invoke(null, new object[] { requestedShader });
        globalKeywords = (string[])_getShaderGlobalKeywords.Invoke(null, new object[] { requestedShader });

        // var name = $"{requestedShader.name}: ({shaderVariantCount} variants, {localKeywords.Length} local, {globalKeywords.Length} global)";
    }

    private static List<Material> FindMaterialsUsingShader(Shader shader)
    {
        var materialsUsingShader = new List<Material>();
        var materialAssetGUIDs = AssetDatabase.FindAssets("t:Material");
        for (int i = 0; i < materialAssetGUIDs.Length; i++)
        {
            string guid = materialAssetGUIDs[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material != null && material.shader != null)
            {
                if (material.shader.name == shader.name)
                {
                    materialsUsingShader.Add(material);
                }
            }
        }

        return materialsUsingShader;
    }
}