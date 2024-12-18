using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class GrassGenerationOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;
        private readonly SpatialGrid _spatialGrid;

        private struct GrassPlacementData
        {
            public Vector3 position;
            public Vector3 normal;
        }

        private class ObjectGenerationData
        {
            public List<(Vector3[] vertices, Vector3 normal, float area)> triangles;
            public float totalArea;
            public int targetGrassCount;
            public List<GrassPlacementData> validPositions;
        }

        public GrassGenerationOperation(GrassPainterTool tool, GrassComputeScript grassCompute,
                                        GrassToolSettingSo toolSettings, SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
            _spatialGrid = spatialGrid;
        }

        public async UniTask GenerateGrass(List<MeshFilter> meshFilters)
        {
            var spacing = _toolSettings.GrassSpacing;
            var existingGrassCount = _grassCompute.GrassDataList.Count;
            var totalArea = 0f;

            // 1단계: 메시 데이터 수집 및 초기 설정
            await _tool.UpdateProgress(0, 100, "Analyzing meshes...");

            var objectDataList = new List<ObjectGenerationData>();

            // 각 오브젝트의 메시 데이터 수집
            foreach (var obj in meshFilters)
            {
                var objectData = CollectMeshData(obj);
                if (objectData != null)
                {
                    objectDataList.Add(objectData);
                    totalArea += objectData.totalArea;
                }
            }

            if (objectDataList.Count == 0)
            {
                Debug.LogWarning("No valid triangles found for grass generation!");
                return;
            }

            // 전체 목표 잔디 수 계산
            var maxGrassForArea = Mathf.FloorToInt(totalArea / (spacing * spacing));
            var totalTargetGrassCount = Mathf.Min(_toolSettings.GenerateGrassCount, maxGrassForArea);

            // 각 오브젝트별 목표 잔디 수 할당
            foreach (var objData in objectDataList)
            {
                objData.targetGrassCount = Mathf.FloorToInt((objData.totalArea / totalArea) * totalTargetGrassCount);
            }

            // 2단계: 병렬로 각 오브젝트의 잔디 위치 계산
            await _tool.UpdateProgress(0, 100, "Calculating positions...");

            var generationTasks = new UniTask[objectDataList.Count];
            for (int i = 0; i < objectDataList.Count; i++)
            {
                generationTasks[i] = GenerateGrassForObject(objectDataList[i]);
            }

            // 모든 작업 병렬 실행
            await UniTask.WhenAll(generationTasks);

            // 3단계: 결과 수집 및 최종 데이터 생성
            await _tool.UpdateProgress(0, 100, "Finalizing grass generation...");

            var allGrassData = new List<GrassData>();
            var currentIndex = existingGrassCount;

            foreach (var objData in objectDataList)
            {
                foreach (var placement in objData.validPositions)
                {
                    var grassData = new GrassData
                    {
                        position = placement.position,
                        normal = placement.normal,
                        color = GrassEditorHelper.GetRandomColor(_toolSettings),
                        widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight)
                    };

                    allGrassData.Add(grassData);
                    _spatialGrid.AddObject(placement.position, currentIndex++);
                }
            }

            _grassCompute.GrassDataList.AddRange(allGrassData);
            await _tool.UpdateProgress(100, 100, $"Complete! Generated {allGrassData.Count} grass instances");
        }

        private ObjectGenerationData CollectMeshData(MeshFilter meshFilter)
        {
            var objectData = new ObjectGenerationData
            {
                triangles = new List<(Vector3[] vertices, Vector3 normal, float area)>(),
                validPositions = new List<GrassPlacementData>()
            };

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var normals = mesh.normals;
            var transform = meshFilter.transform;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v1 = transform.TransformPoint(vertices[triangles[i]]);
                var v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                var v3 = transform.TransformPoint(vertices[triangles[i + 2]]);

                var normal = transform.TransformDirection(
                    (normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f
                ).normalized;

                var area = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;
                    
                var surfaceAngle = _toolSettings.allowUndersideGrass 
                    ? Mathf.Acos(Mathf.Abs(normal.y)) * Mathf.Rad2Deg  // 위아래 모두 허용
                    : Mathf.Acos(normal.y) * Mathf.Rad2Deg;           // 위쪽만 허용
                
                if (surfaceAngle <= _toolSettings.NormalLimit * 90.01f)
                {
                    objectData.triangles.Add((new[] { v1, v2, v3 }, normal, area));
                    objectData.totalArea += area;
                }
            }

            return objectData;
        }

        private async UniTask GenerateGrassForObject(ObjectGenerationData objData)
        {
            var spacing = _toolSettings.GrassSpacing;
            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            var objectGrid = new SpatialGrid(bounds, spacing);
            var attempts = 0;
            var maxAttempts = objData.targetGrassCount * 2;

            while (objData.validPositions.Count < objData.targetGrassCount && attempts < maxAttempts)
            {
                attempts++;

                if (attempts % Mathf.Max(1, maxAttempts / 100) == 0)
                {
                    var progress = Mathf.RoundToInt((float)attempts / maxAttempts * 100);
                    progress = Mathf.Min(progress, 100);
                    await _tool.UpdateProgress(progress, 100, "Calculating positions... ");
                }

                // 면적 기반으로 삼각형 선택
                var randomValue = Random.value * objData.totalArea;
                var currentSum = 0f;
                int selectedIndex = 0;

                for (int i = 0; i < objData.triangles.Count; i++)
                {
                    currentSum += objData.triangles[i].area;
                    if (currentSum >= randomValue)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                var (vertices, normal, _) = objData.triangles[selectedIndex];
                var randomPoint = GetRandomPointInTriangle(vertices[0], vertices[1], vertices[2]);

                // 현재 오브젝트의 그리드에서만 간격 체크
                var tempPositionIds = new List<int>();
                objectGrid.GetObjectsInRadius(randomPoint, spacing, tempPositionIds);

                if (tempPositionIds.Count > 0) continue;

                // 장애물 체크
                if (Physics.CheckSphere(randomPoint, 0.01f, _toolSettings.PaintBlockMask))
                    continue;

                var placementData = new GrassPlacementData
                {
                    position = randomPoint,
                    normal = normal
                };

                objData.validPositions.Add(placementData);
                objectGrid.AddObject(randomPoint, objData.validPositions.Count - 1);
            }
        }

        private Vector3 GetRandomPointInTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;

            float m1 = 1 - r1;
            float m2 = r1 * (1 - r2);
            float m3 = r1 * r2;

            return v1 * m1 + v2 * m2 + v3 * m3;
        }
    }
}