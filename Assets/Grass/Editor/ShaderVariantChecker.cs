using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Ref - https://github.com/needle-tools/shader-variant-explorer

public class ShaderVariantChecker : EditorWindow
{
    private static MethodInfo GetVariantCount, GetShaderGlobalKeywords, GetShaderLocalKeywords;

    private Shader _shader;

    private ulong _variantCount = 0;
    private ulong _usedVariantCount = 0;
    private int _keywordCount = 0;
    private int _materialCount = 0;
    private Dictionary<string, int> _materialKeywords = new();
    private bool _isChecked = false;

    private Vector2 scrollPosition;

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
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

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
        if (GetVariantCount == null)
            GetVariantCount = typeof(ShaderUtil).GetMethod("GetVariantCount", (BindingFlags)(-1));
        if (GetShaderGlobalKeywords == null)
            GetShaderGlobalKeywords = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords", (BindingFlags)(-1));
        if (GetShaderLocalKeywords == null)
            GetShaderLocalKeywords = typeof(ShaderUtil).GetMethod("GetShaderLocalKeywords", (BindingFlags)(-1));

        if (GetVariantCount == null || GetShaderGlobalKeywords == null || GetShaderLocalKeywords == null)
        {
            shaderVariantCount = 0;
            usedShaderVariantCount = 0;
            localKeywords = null;
            globalKeywords = null;
            return;
        }

        shaderVariantCount = (ulong)GetVariantCount.Invoke(null, new object[] { requestedShader, false });
        usedShaderVariantCount = (ulong)GetVariantCount.Invoke(null, new object[] { requestedShader, true });
        localKeywords = (string[])GetShaderLocalKeywords.Invoke(null, new object[] { requestedShader });
        globalKeywords = (string[])GetShaderGlobalKeywords.Invoke(null, new object[] { requestedShader });

        // var name = $"{requestedShader.name}: ({shaderVariantCount} variants, {localKeywords.Length} local, {globalKeywords.Length} global)";
    }

    public static List<Material> FindMaterialsUsingShader(Shader shader)
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