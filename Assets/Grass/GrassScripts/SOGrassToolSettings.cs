using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Grass Tool Settings", menuName = "Utility/GrassToolSettings")]
public class SoGrassToolSettings : ScriptableObject
{
    public enum VertexColorSetting
    {
        None,
        Red,
        Blue,
        Green
    }

    [Header("Terrain Layer Settings")] public float[] layerBlocking;

    public bool[] layerFading;

    [Header("Vertex Color Settings")] public VertexColorSetting vertexColorSettings;

    public VertexColorSetting vertexFade;

    // length/width
    public float sizeWidth = 1f;
    public float sizeLength = 1f;

    // length/width adjustments
    public float adjustWidth;
    public float adjustLength;
    public float adjustWidthMax = 2f;
    public float adjustHeightMax = 2f;

    // reproject settings
    public float reprojectOffset = 1f;

    // color settings
    public float rangeR, rangeG, rangeB;
    public Color adjustedColor = Color.white;

    // brush settings
    public LayerMask paintBlockMask = 0; // Generate나 Add 할 때 미리 거를 레이어를 설정해놓을 수 있음
    public LayerMask hitMask = 1; // 기본적으로 설정할 때 paintMask 값을 포함하는 값으로 설정해야함 paintMask가 2라면 hitMask는 1,2,3 이런식 2를 포함하는 값
    public LayerMask paintMask = 1; // 값의 크기가 int로 바꿨을때 hitMask >= paintMask
    // 기본적으로 땅에 풀을 그린다고 할 때 땅보다 높은 턱이있는 지형에 풀을 그릴땐 paintMask에서 땅을 제외해놓고 그리는게 나음 그렇지 않으면 턱이 있는 곳 위와 아래에 중복으로 풀이 그려짐

    public float brushSize = 4f;
    public float brushFalloffSize = 0.8f;
    public float flow;
    public float density = 1f;
    public float normalLimit = 1;
    public int grassAmountToGenerate = 100000;
    public float generationDensity = 1;

    public void CreateNewLayers()
    {
        Debug.Log("Setting up initial tool settings");
        layerBlocking = new float[8];
        for (int i = 0; i < layerBlocking.Length; i++)
        {
            layerBlocking[i] = 1;
        }

        layerFading = new bool[8];
        layerFading[0] = true;
    }
}