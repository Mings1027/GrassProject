using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grass.Editor
{
    public class GrassModifyOperation
    {
        private readonly GrassPainterTool _painterTool;
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;

        public GrassModifyOperation(GrassPainterTool painterTool, GrassComputeScript grassCompute,
                                    GrassToolSettingSo toolSettings)
        {
            _painterTool = painterTool;
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
        }

        public async UniTask ModifyColor()
        {
            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.color = GrassEditorHelper.GetRandomColor(_toolSettings);
                grassData[i] = newData;

                await _painterTool.UpdateProgress(i + 1, totalCount, "Modifying colors");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
        }

        public async UniTask ModifySize()
        {
            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight);
                grassData[i] = newData;

                await _painterTool.UpdateProgress(i + 1, totalCount, "Modifying size");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
        }

        public async UniTask ModifySizeAndColor()
        {
            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < totalCount; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight);
                newData.color = GrassEditorHelper.GetRandomColor(_toolSettings);
                grassData[i] = newData;

                await _painterTool.UpdateProgress(i + 1, totalCount, "Modifying size and color");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
        }

        private void RebuildMesh()
        {
            _grassCompute.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}