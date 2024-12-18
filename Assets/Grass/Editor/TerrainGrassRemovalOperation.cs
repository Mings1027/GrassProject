using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.Editor
{
    public class TerrainGrassRemovalOperation
    {
        private readonly GrassPainterTool _tool;
        private readonly GrassComputeScript _grassCompute;
        private readonly SpatialGrid _spatialGrid;
        private readonly List<int> _tempList = new();

        public TerrainGrassRemovalOperation(GrassPainterTool tool, GrassComputeScript grassCompute,
                                            SpatialGrid spatialGrid)
        {
            _tool = tool;
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
        }

        public async UniTask RemoveGrass(List<Terrain> terrains)
        {
            var grassToRemove = new HashSet<int>();
            var totalGrassToCheck = 0;

            // 첫 번째 패스 - 터레인 바운드 내의 모든 잔디 수집
            foreach (var terrain in terrains)
            {
                var bounds = GetTerrainBounds(terrain);
                _tempList.Clear();
                _spatialGrid.GetObjectsInBounds(bounds, _tempList);
                totalGrassToCheck += _tempList.Count;
            }

            // 두 번째 패스 - 터레인의 표면에 있는 잔디 제거
            var currentProgress = 0;
            foreach (var terrain in terrains)
            {
                var terrainData = terrain.terrainData;
                var terrainPos = terrain.transform.position;
                var size = terrainData.size;

                _tempList.Clear();
                _spatialGrid.GetObjectsInBounds(GetTerrainBounds(terrain), _tempList);

                foreach (var grassIndex in _tempList)
                {
                    if (grassToRemove.Contains(grassIndex))
                    {
                        currentProgress++;
                        continue;
                    }

                    var grassPosition = _grassCompute.GrassDataList[grassIndex].position;

                    // 정규화된 좌표 계산
                    float normX = (grassPosition.x - terrainPos.x) / size.x;
                    float normZ = (grassPosition.z - terrainPos.z) / size.z;

                    // XZ 범위 체크
                    if (normX < 0 || normX > 1 || normZ < 0 || normZ > 1)
                    {
                        currentProgress++;
                        continue;
                    }

                    // 해당 위치의 터레인 높이 가져오기
                    float terrainHeight = terrainData.GetHeight(
                        Mathf.RoundToInt(normX * terrainData.heightmapResolution),
                        Mathf.RoundToInt(normZ * terrainData.heightmapResolution)
                    );

                    Vector3 surfacePoint = new Vector3(grassPosition.x, terrainPos.y + terrainHeight, grassPosition.z);

                    // 잔디가 터레인 표면 근처에 있는지 확인
                    const float tolerance = 0.2f;
                    if (Vector3.Distance(grassPosition, surfacePoint) <= tolerance)
                    {
                        grassToRemove.Add(grassIndex);
                    }

                    currentProgress++;
                    await _tool.UpdateProgress(currentProgress, totalGrassToCheck, "Removing grass from terrains");
                }
            }

            // 선택된 잔디 제거
            if (grassToRemove.Count > 0)
            {
                var updatedList = new List<GrassData>();
                for (var index = 0; index < _grassCompute.GrassDataList.Count; index++)
                {
                    if (!grassToRemove.Contains(index))
                    {
                        updatedList.Add(_grassCompute.GrassDataList[index]);
                    }
                }

                _grassCompute.GrassDataList = updatedList;
            }
        }

        private Bounds GetTerrainBounds(Terrain terrain)
        {
            var size = terrain.terrainData.size;
            return new Bounds(
                terrain.transform.position + size / 2f,
                size
            );
        }
    }
}