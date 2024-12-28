using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateCustomShaderMenu
{
    private const string TEMPLATE_PATH = "Assets/CustomShaderTemplate/ShaderTemplates/";
    
    [MenuItem("Assets/Create/Shader/Forward Shader", false, 85)]
    private static void CreateForwardShader()
    {
        CreateShader("Forward");
    }

    [MenuItem("Assets/Create/Shader/Forward+ Shader", false, 85)]
    private static void CreateForwardPlusShader()
    {
        CreateShader("ForwardPlus");
    }

    [MenuItem("Assets/Create/Shader/Blinn-Phong Shader", false, 85)]
    private static void CreateBlinnPhongShader()
    {
        CreateShader("BlinnPhong");
    }

    private static void CreateShader(string shaderName)
    {
        var templatePath = Path.Combine(TEMPLATE_PATH, $"{shaderName}.shader.txt");
        
        // 템플릿 파일 읽기
        string templateContent;
        if (File.Exists(templatePath))
        {
            templateContent = File.ReadAllText(templatePath);
        }
        else
        {
            Debug.LogError("Template file not found at: " + templatePath);
            return;
        }

        // ProjectWindowUtil을 사용하여 새 에셋 생성
        ProjectWindowUtil.CreateAssetWithContent(
            $"New{shaderName}.shader",
            templateContent,
            EditorGUIUtility.FindTexture("Shader Icon")
        );
    }
}