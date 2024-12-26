using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateURPShaderMenu
{
    [MenuItem("Assets/Create/Shader/Forward Shader", false, 85)]
    private static void CreateForwardShader()
    {
        // 템플릿 파일 경로 지정
        string templatePath = "Assets/URPShaderTemplate/ShaderTemplates/ForwardShader.shader.txt"; // 본인의 템플릿 경로로 수정하세요

        // 템플릿 파일 읽기
        string templateContent = "";
        if (File.Exists(templatePath))
        {
            templateContent = File.ReadAllText(templatePath);
        }
        else
        {
            Debug.LogError("Template file not found at: " + templatePath);
            return;
        }

        // 새 파일 저장 경로 가져오기
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (path == "")
        {
            path = "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            path = path.Replace(Path.GetFileName(path), "");
        }

        // 새 셰이더 파일 생성
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/NewURPShader.shader");
        File.WriteAllText(assetPath, templateContent);
        AssetDatabase.Refresh();

        // 새로 만든 셰이더 선택
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
        Selection.activeObject = shader;
    }

    [MenuItem("Assets/Create/Shader/Forward+ Shader", false, 85)]
    private static void CreateForwardPlusShader()
    {
        var templatePath = "Assets/URPShaderTemplate/ShaderTemplates/ForwardPlusShader.shader.txt";
        // 템플릿 파일 읽기
        string templateContent = "";
        if (File.Exists(templatePath))
        {
            templateContent = File.ReadAllText(templatePath);
        }
        else
        {
            Debug.LogError("Template file not found at: " + templatePath);
            return;
        }

        // 새 파일 저장 경로 가져오기
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (path == "")
        {
            path = "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            path = path.Replace(Path.GetFileName(path), "");
        }

        // 새 셰이더 파일 생성
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/NewURPShader.shader");
        File.WriteAllText(assetPath, templateContent);
        AssetDatabase.Refresh();

        // 새로 만든 셰이더 선택
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
        Selection.activeObject = shader;
    }
}