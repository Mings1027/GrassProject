using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.ProjectWindowCallback;

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

    private static void CreateShader(string shaderType)
    {
        var templatePath = Path.Combine(TEMPLATE_PATH, $"{shaderType}.shader.txt");

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

        // 새 셰이더 생성을 위한 EndNameEditAction 정의
        var endAction = ScriptableObject.CreateInstance<CustomShaderNameProcessor>();
        endAction.templateContent = templateContent;

        // ProjectWindowUtil을 사용하여 새 에셋 생성 (이름 편집 가능)
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            endAction,
            $"New{shaderType}.shader",
            EditorGUIUtility.FindTexture("Shader Icon"),
            null
        );
    }
}

public class CustomShaderNameProcessor : EndNameEditAction
{
    public string templateContent;

    public override void Action(int instanceId, string pathName, string resourceFile)
    {
        // 파일 이름에서 확장자를 제외한 이름 추출
        string fileName = Path.GetFileNameWithoutExtension(pathName);

        // 템플릿의 셰이더 이름 교체
        string finalContent = templateContent.Replace("Custom/#NAME#", $"Custom/{fileName}");

        // 파일 생성
        File.WriteAllText(pathName, finalContent);

        // 에셋 데이터베이스 리프레시
        AssetDatabase.ImportAsset(pathName);
    }
}