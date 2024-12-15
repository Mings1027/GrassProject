using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Grass.Editor
{
    public class GrassGenerationOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;
        private readonly SpatialGrid _spatialGrid;
        private readonly List<int> _nearbyPoints = new();

        private struct GrassPlacementData
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        public GrassGenerationOperation(
            GrassPainterTool tool,
            GrassComputeScript grassCompute,
            GrassToolSettingSo toolSettings,
            SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
            _spatialGrid = spatialGrid;
        }

        public async UniTask GenerateGrass(GameObject[] selectedObjects)
        {
            var spacing = _toolSettings.GrassSpacing;
            var existingGrassCount = _grassCompute.GrassDataList.Count;
            var totalArea = 0f;
            var triangleData = new List<(Vector3[] vertices, Vector3 normal, float area)>();

            // 1단계: 메시 데이터 수집
            await _tool.UpdateProgress(0, 100, "Analyzing meshes...");
            
            foreach (var obj in selectedObjects)
            {
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter == null) continue;

                var mesh = meshFilter.sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var normals = mesh.normals;
                var transform = obj.transform;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var v1 = transform.TransformPoint(vertices[triangles[i]]);
                    var v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                    var v3 = transform.TransformPoint(vertices[triangles[i + 2]]);
                    
                    var normal = transform.TransformDirection(
                        (normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]]) / 3f
                    ).normalized;

                    var area = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;
                    
                    var objectUp = transform.up;
                    var normalDot = Mathf.Abs(Vector3.Dot(normal, objectUp));
        
                    if (normalDot >= 1 - _toolSettings.NormalLimit)
                    {
                        triangleData.Add((new[] { v1, v2, v3 }, normal, area));
                        totalArea += area;
                    }
                }
            }

            if (triangleData.Count == 0)
            {
                Debug.LogWarning("No valid triangles found for grass generation!");
                return;
            }

            // 2단계: 유효한 잔디 위치 계산           
            await _tool.UpdateProgress(0, 100, "Starting position calculation...");
            
            var validPositions = new List<GrassPlacementData>();
            var maxGrassForArea = Mathf.FloorToInt(totalArea / (spacing * spacing));
            var targetGrassCount = Mathf.Min(_toolSettings.GenerateGrassCount, maxGrassForArea);
            var attempts = 0;
            var maxAttempts = targetGrassCount * 2;

            // 위치 계산용 임시 SpatialGrid 생성
            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            var tempGrid = new SpatialGrid(bounds, spacing);
            var tempPositionIds = new List<int>();
            
            Debug.Log($"Starting position calculation. Target: {targetGrassCount}, Max attempts: {maxAttempts}");

            while (validPositions.Count < targetGrassCount && attempts < maxAttempts)
            {
                attempts++;
                
                // maxAttempts의 1%마다 업데이트
                int updateInterval = maxAttempts / 100;
                // 최소한 1의 간격은 보장
                updateInterval = Mathf.Max(1, updateInterval);
    
                if (attempts % updateInterval == 0)
                {
                    var progress = Mathf.RoundToInt((float)attempts / maxAttempts * 100);
                    progress = Mathf.Min(progress, 100);

                    await _tool.UpdateProgress(progress, 100, 
                        $"Calculating positions... {validPositions.Count}/{targetGrassCount}");
                }

                // 면적 기반으로 삼각형 선택
                var randomValue = Random.value * totalArea;
                var currentSum = 0f;
                int selectedIndex = 0;
                
                for (int i = 0; i < triangleData.Count; i++)
                {
                    currentSum += triangleData[i].area;
                    if (currentSum >= randomValue)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                var (vertices, normal, _) = triangleData[selectedIndex];
                var randomPoint = GetRandomPointInTriangle(vertices[0], vertices[1], vertices[2]);
                
                // 간격 체크 - 기존 잔디
                _nearbyPoints.Clear();
                _spatialGrid.GetObjectsInRadius(randomPoint, spacing, _nearbyPoints);
                
                if (_nearbyPoints.Count > 0) continue;

                // 간격 체크 - 새로운 위치
                tempPositionIds.Clear();
                tempGrid.GetObjectsInRadius(randomPoint, spacing, tempPositionIds);
                
                if (tempPositionIds.Count > 0) continue;

                // 장애물 체크
                if (Physics.Raycast(randomPoint + normal * 0.5f, -normal, out _, 1f, _toolSettings.PaintBlockMask))
                    continue;

                validPositions.Add(new GrassPlacementData 
                { 
                    Position = randomPoint, 
                    Normal = normal 
                });
                
                // 임시 그리드에 위치 추가
                tempGrid.AddObject(randomPoint, validPositions.Count - 1);
            }

            // 최종 위치 계산 진행률 표시
            await _tool.UpdateProgress(100, 100, 
                $"Position calculation complete. Found {validPositions.Count} valid positions");

            Debug.Log($"Found {validPositions.Count} valid positions after {attempts} attempts");

            // 3단계: 실제 잔디 생성 시작
            await _tool.UpdateProgress(0, 100, "Starting grass generation...");
            
            var generatedCount = 0;
            var grassDataList = new List<GrassData>();

            foreach (var placementData in validPositions)
            {
                var grassData = new GrassData
                {
                    position = placementData.Position,
                    normal = placementData.Normal,
                    color = GetRandomColor(),
                    widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight)
                };

                grassDataList.Add(grassData);
                _spatialGrid.AddObject(placementData.Position, existingGrassCount + generatedCount);
                generatedCount++;

                // 현재 진행률 계산 (0-100%)
                if (generatedCount % 1000 == 0 || generatedCount == validPositions.Count)
                {
                    var progress = Mathf.RoundToInt((float)generatedCount / validPositions.Count * 100);
                    await _tool.UpdateProgress(
                        progress,
                        100,
                        $"Generating grass... {generatedCount}/{validPositions.Count}"
                    );
                }
            }

            _grassCompute.GrassDataList.AddRange(grassDataList);
            await _tool.UpdateProgress(100, 100,
                $"Complete! Generated {grassDataList.Count} grass instances");
        }

        private Vector3 GetRandomColor()
        {
            var baseColor = _toolSettings.BrushColor;
            var newRandomCol = new Color(
                baseColor.r + Random.Range(0, _toolSettings.RangeR),
                baseColor.g + Random.Range(0, _toolSettings.RangeG),
                baseColor.b + Random.Range(0, _toolSettings.RangeB),
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
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