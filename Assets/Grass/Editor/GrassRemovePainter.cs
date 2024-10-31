using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GrassRemovePainter
{
    private const int CHUNK_SIZE = 100000; // 각 리스트당 최대 잔디 수
    private readonly List<GrassData> _grassList;
    private readonly GrassComputeScript _grassCompute;

    public GrassRemovePainter(GrassComputeScript grassCompute)
    {
        _grassCompute = grassCompute;
        _grassList = _grassCompute.GrassDataList;
    }

    public async UniTask RemoveGrass(Vector3 hitPoint, float radius)
    {
        float radiusSqr = radius * radius;
        var grassListCount = _grassList.Count;
        var totalChunks = (grassListCount + CHUNK_SIZE - 1) / CHUNK_SIZE;
        var tasks = new UniTask[totalChunks];

        // 각 청크별로 병렬 처리
        for (int i = 0; i < totalChunks; i++)
        {
            int startIdx = i * CHUNK_SIZE;
            int endIdx = Mathf.Min(startIdx + CHUNK_SIZE, grassListCount);

            tasks[i] = UniTask.RunOnThreadPool(() =>
            {
                for (int j = endIdx - 1; j >= startIdx && j < grassListCount; j--)
                {
                    var grassData = _grassList[j];
                    var distanceSqr = Vector3.SqrMagnitude(grassData.position - hitPoint);
                    if (distanceSqr <= radiusSqr)
                    {
                        _grassList.RemoveAt(j);
                    }
                }
            });
        }

        await UniTask.WhenAll(tasks);

        // 변경사항 적용
        _grassCompute.GrassDataList = _grassList;
        _grassCompute.ResetFaster();
    }

    public void Clear()
    {
        // 필요한 경우 여기에 정리 로직 추가
        // 여기서 Reset호출하면 로딩이 김 수정해야함
        _grassCompute.Reset();
    }
}