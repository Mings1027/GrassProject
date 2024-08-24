using UnityEngine;

public class GrassRenderer : MonoBehaviour
{
    public Mesh grassMesh;
    public Material grassMaterial;
    private GraphicsBuffer argsBuffer;
    private GraphicsBuffer instanceBuffer;
    private RenderParams renderParams;

    void Start()
    {
        // 인스턴스 데이터 초기화 (예: 위치, 변환 행렬 등)
        Matrix4x4[] instanceData = new Matrix4x4[1000]; // 예시: 1000개의 풀 인스턴스
        for (int i = 0; i < instanceData.Length; i++)
        {
            instanceData[i] = Matrix4x4.TRS(
                new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)),
                Quaternion.identity,
                Vector3.one
            );
        }

        // 인스턴스 데이터 버퍼 생성
        instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instanceData.Length, sizeof(float) * 16);
        instanceBuffer.SetData(instanceData);

        // RenderParams 설정
        renderParams = new RenderParams(grassMaterial)
        {
            matProps = new MaterialPropertyBlock(),
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true
        };

        // 셰이더에 인스턴스 버퍼 연결
        renderParams.matProps.SetBuffer("_InstanceBuffer", instanceBuffer);

        // 간접 드로우 인수 설정
        uint[] args = new uint[5] { grassMesh.GetIndexCount(0), (uint)instanceData.Length, 0, 0, 0 };
        argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
        argsBuffer.SetData(args);

        Debug.Log($"Instance count: {instanceData.Length}");
        Debug.Log($"Mesh index count: {grassMesh.GetIndexCount(0)}");
    }

    void Update()
    {
        // 간접 드로우 호출
        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, argsBuffer);
    }

    void OnDestroy()
    {
        if (argsBuffer != null)
            argsBuffer.Release();
        if (instanceBuffer != null)
            instanceBuffer.Release();
    }
}