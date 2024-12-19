using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class TerrainGrassGenerationOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;
        private readonly SpatialGrid _spatialGrid;

        private struct TerrainGrassPlacementData
        {
            public Vector3 position;
            public Vector3 normal;
            public float heightScale;
        }

        private class TerrainGenerationData
        {
            public float[,,] splatmapData;
            public TerrainData terrainData;
            public int alphamapWidth;
            public int alphamapHeight;
            public Vector3 terrainPosition;
            public Vector3 terrainSize;
            public float totalArea;
            public int targetGrassCount;
            public List<TerrainGrassPlacementData> validPositions;
        }

        public TerrainGrassGenerationOperation(GrassPainterTool tool, GrassComputeScript grassCompute,
                                               GrassToolSettingSo toolSettings, SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
            _spatialGrid = spatialGrid;
        }

        public async UniTask GenerateGrass(List<Terrain> terrains)
        {
            var spacing = _toolSettings.GrassSpacing;
            var existingGrassCount = _grassCompute.GrassDataList.Count;
            var terrainDataList = new List<TerrainGenerationData>();
            var totalArea = 0f;

            // 1단계: 지형 데이터 수집 및 초기 설정
            await _tool.UpdateProgress(0, 100, "Analyzing terrain...");

            foreach (var terrain in terrains)
            {
                var terrainData = CollectTerrainData(terrain);
                if (terrainData != null)
                {
                    terrainDataList.Add(terrainData);
                    totalArea += terrainData.totalArea;
                }
            }

            if (terrainDataList.Count == 0)
            {
                Debug.LogWarning("No valid terrain data found!");
                return;
            }

            // 전체 목표 잔디 수 계산
            var maxGrassForArea = Mathf.FloorToInt(totalArea / (spacing * spacing));
            var totalTargetGrassCount = Mathf.Min(_toolSettings.GenerateGrassCount, maxGrassForArea);

            // 각 지형별 목표 잔디 수 할당
            foreach (var data in terrainDataList)
            {
                data.targetGrassCount = Mathf.FloorToInt((data.totalArea / totalArea) * totalTargetGrassCount);
            }

            // 2단계: 병렬로 각 지형의 잔디 위치 계산
            await _tool.UpdateProgress(20, 100, "Calculating positions...");

            var generationTasks = new List<UniTask>();
            foreach (var terrainData in terrainDataList)
            {
                generationTasks.Add(GenerateGrassPositions(terrainData));
            }

            await UniTask.WhenAll(generationTasks);

            // 3단계: 결과 수집 및 최종 데이터 생성
            await _tool.UpdateProgress(80, 100, "Finalizing grass generation...");

            var currentIndex = existingGrassCount;
            var allGrassData = new List<GrassData>();
            foreach (var terrainData in terrainDataList)
            {
                foreach (var placement in terrainData.validPositions)
                {
                    var grassData = new GrassData
                    {
                        position = placement.position,
                        normal = placement.normal,
                        color = GrassEditorHelper.GetRandomColor(_toolSettings),
                        widthHeight = new Vector2(
                            _toolSettings.GrassWidth,
                            _toolSettings.GrassHeight * placement.heightScale
                        )
                    };

                    allGrassData.Add(grassData);
                    _spatialGrid.AddObject(placement.position, currentIndex++);
                }
            }

            _grassCompute.GrassDataList.AddRange(allGrassData);
            await _tool.UpdateProgress(100, 100, $"Complete! Generated {allGrassData.Count} grass instances");
        }

        private TerrainGenerationData CollectTerrainData(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return null;

            var terrainData = terrain.terrainData;
            var alphamapWidth = terrainData.alphamapWidth;
            var alphamapHeight = terrainData.alphamapHeight;

            return new TerrainGenerationData
            {
                terrainData = terrainData,
                splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight),
                alphamapWidth = alphamapWidth,
                alphamapHeight = alphamapHeight,
                terrainPosition = terrain.transform.position,
                terrainSize = terrainData.size,
                totalArea = terrainData.size.x * terrainData.size.z,
                validPositions = new List<TerrainGrassPlacementData>()
            };
        }

        private async UniTask GenerateGrassPositions(TerrainGenerationData terrainData)
        {
            var spacing = _toolSettings.GrassSpacing;
            var attempts = 0;
            var maxAttempts = terrainData.targetGrassCount * 2;

            var bounds = new Bounds(terrainData.terrainPosition, terrainData.terrainSize);
            var objectGrid = new SpatialGrid(bounds, spacing);

            while (terrainData.validPositions.Count < terrainData.targetGrassCount && attempts < maxAttempts)
            {
                attempts++;

                if (attempts % Mathf.Max(1, maxAttempts / 100) == 0)
                {
                    var progress = 20 + Mathf.RoundToInt((float)attempts / maxAttempts * 60);
                    progress = Mathf.Min(progress, 80);
                    await _tool.UpdateProgress(progress, 100, "Calculating positions...");
                }

                var randomX = Random.value * terrainData.terrainSize.x;
                var randomZ = Random.value * terrainData.terrainSize.z;

                var normX = randomX / terrainData.terrainSize.x;
                var normZ = randomZ / terrainData.terrainSize.z;

                var mapX = Mathf.FloorToInt(normX * (terrainData.alphamapWidth - 1));
                var mapZ = Mathf.FloorToInt(normZ * (terrainData.alphamapHeight - 1));

                var finalHeightScale = 1f;
                var isLayerEnabled = true;

                // 레이어가 있는 경우 dominant 레이어의 설정을 사용
                if (terrainData.splatmapData != null && terrainData.splatmapData.GetLength(2) > 0)
                {
                    var layerCount = terrainData.splatmapData.GetLength(2);
                    var dominantLayer = -1;
                    var maxWeight = 0f;

                    // 가장 큰 weight를 가진 레이어 찾기
                    for (var layer = 0; layer < layerCount; layer++)
                    {
                        var weight = terrainData.splatmapData[mapZ, mapX, layer];
                        if (weight > maxWeight)
                        {
                            maxWeight = weight;
                            dominantLayer = layer;
                        }
                    }

                    // dominant 레이어의 설정 사용
                    if (dominantLayer >= 0)
                    {
                        isLayerEnabled = _toolSettings.LayerEnabled[dominantLayer];
                        finalHeightScale = _toolSettings.HeightFading[dominantLayer];
                    }
                }

                // 레이어가 비활성화되어 있으면 즉시 다음 위치로
                if (!isLayerEnabled) continue;

                // 높이 스케일이 0이면 즉시 다음 위치로
                if (finalHeightScale <= 0) continue;

                var worldPos = terrainData.terrainPosition + new Vector3(randomX, 0, randomZ);
                worldPos.y = terrainData.terrainData.GetHeight(
                    Mathf.RoundToInt(normX * terrainData.terrainData.heightmapResolution),
                    Mathf.RoundToInt(normZ * terrainData.terrainData.heightmapResolution)
                );

                var tempLocalPositionIds = new List<int>();
                objectGrid.GetObjectsInRadius(worldPos, spacing, tempLocalPositionIds);

                if (tempLocalPositionIds.Count > 0) continue;
                
                // Calculate normal and check slope
                var normal = terrainData.terrainData.GetInterpolatedNormal(normX, normZ);
                var surfaceAngle = Mathf.Acos(normal.y) * Mathf.Rad2Deg;
                if (surfaceAngle > _toolSettings.NormalLimit * 90.01f) continue;

                // Check obstacles
                if (Physics.CheckSphere(worldPos, 0.01f, _toolSettings.PaintBlockMask.value))
                    continue;

                var placementData = new TerrainGrassPlacementData
                {
                    position = worldPos,
                    normal = normal,
                    heightScale = finalHeightScale
                };

                terrainData.validPositions.Add(placementData);
                objectGrid.AddObject(worldPos, terrainData.validPositions.Count - 1);
            }
        }
    }
}