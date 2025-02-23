using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grass.Editor
{
    public readonly struct ModifyOperation
    {
        public readonly string title;
        public readonly string message;
        public readonly string undoMessage;
        public readonly Func<UniTask> action;

        public ModifyOperation(string title, string message, string undoMessage, Func<UniTask> action)
        {
            this.title = title;
            this.message = message;
            this.undoMessage = undoMessage;
            this.action = action;
        }
    }

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

        private async UniTask ModifyColor()
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

        private async UniTask ModifySize()
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

        private async UniTask ModifySizeAndColor()
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

        public ModifyOperation GetModifyOperation(ModifyOption modifyOption)
        {
            return modifyOption switch
            {
                ModifyOption.Color => new ModifyOperation(
                    "Modify Color",
                    "Modify the color of all grass elements?",
                    "Modified Color",
                    ModifyColor),
                ModifyOption.WidthHeight => new ModifyOperation(
                    "Modify Width and Height",
                    "Modify the width and height of all grass elements?",
                    "Modified Size",
                    ModifySize),
                ModifyOption.Both => new ModifyOperation(
                    "Modify Width Height and Color",
                    "Modify the width, height and color of all grass elements?",
                    "Modified Size and Color",
                    ModifySizeAndColor),
                _ => throw new ArgumentOutOfRangeException(nameof(modifyOption), modifyOption, null)
            };
        }

        public async UniTask ExecuteOperation(Func<UniTask> action)
        {
            await action();
        }

        private void RebuildMesh()
        {
            _grassCompute.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}

namespace Grass.Editor
{
    public readonly struct SyncModifyOperation
    {
        public readonly string title;
        public readonly string message;
        public readonly string undoMessage;
        public readonly Action action;

        public SyncModifyOperation(string title, string message, string undoMessage, Action action)
        {
            this.title = title;
            this.message = message;
            this.undoMessage = undoMessage;
            this.action = action;
        }
    }

    public class SyncGrassModifyOperation
    {
        private readonly GrassComputeScript _grassCompute;
        private readonly GrassToolSettingSo _toolSettings;

        public SyncGrassModifyOperation(GrassComputeScript grassCompute, GrassToolSettingSo toolSettings)
        {
            _grassCompute = grassCompute;
            _toolSettings = toolSettings;
        }

        private void ModifyColor()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Modifying Grass", "Preparing to modify colors...", 0f);

                var grassData = new List<GrassData>(_grassCompute.GrassDataList);
                var totalCount = grassData.Count;

                for (var i = 0; i < grassData.Count; i++)
                {
                    var newData = grassData[i];
                    newData.color = GrassEditorHelper.GetRandomColor(_toolSettings);
                    grassData[i] = newData;

                    if (i % 1000 == 0 || i == totalCount - 1) // Update progress every 1000 items
                    {
                        var progress = (float)(i + 1) / totalCount;
                        EditorUtility.DisplayProgressBar("Modifying Grass",
                            $"Modifying colors ({i + 1}/{totalCount})...",
                            progress);
                    }
                }

                EditorUtility.DisplayProgressBar("Modifying Grass", "Finalizing changes...", 0.95f);
                _grassCompute.GrassDataList = grassData;
                RebuildMesh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ModifySize()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Modifying Grass", "Preparing to modify size...", 0f);

                var grassData = new List<GrassData>(_grassCompute.GrassDataList);
                var totalCount = grassData.Count;

                for (var i = 0; i < grassData.Count; i++)
                {
                    var newData = grassData[i];
                    newData.widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight);
                    grassData[i] = newData;

                    if (i % 1000 == 0 || i == totalCount - 1) // Update progress every 1000 items
                    {
                        var progress = (float)(i + 1) / totalCount;
                        EditorUtility.DisplayProgressBar("Modifying Grass",
                            $"Modifying size ({i + 1}/{totalCount})...",
                            progress);
                    }
                }

                EditorUtility.DisplayProgressBar("Modifying Grass", "Finalizing changes...", 0.95f);
                _grassCompute.GrassDataList = grassData;
                RebuildMesh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ModifySizeAndColor()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Modifying Grass", "Preparing to modify size and color...", 0f);

                var grassData = new List<GrassData>(_grassCompute.GrassDataList);
                var totalCount = grassData.Count;

                for (var i = 0; i < totalCount; i++)
                {
                    var newData = grassData[i];
                    newData.widthHeight = new Vector2(_toolSettings.GrassWidth, _toolSettings.GrassHeight);
                    newData.color = GrassEditorHelper.GetRandomColor(_toolSettings);
                    grassData[i] = newData;

                    if (i % 1000 == 0 || i == totalCount - 1) // Update progress every 1000 items
                    {
                        var progress = (float)(i + 1) / totalCount;
                        EditorUtility.DisplayProgressBar("Modifying Grass",
                            $"Modifying size and color ({i + 1}/{totalCount})...",
                            progress);
                    }
                }

                EditorUtility.DisplayProgressBar("Modifying Grass", "Finalizing changes...", 0.95f);
                _grassCompute.GrassDataList = grassData;
                RebuildMesh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public SyncModifyOperation GetSyncModifyOperation(ModifyOption modifyOption)
        {
            return modifyOption switch
            {
                ModifyOption.Color => new SyncModifyOperation(
                    "Modify Color",
                    "Modify the color of all grass elements?",
                    "Modified Color",
                    ModifyColor),
                ModifyOption.WidthHeight => new SyncModifyOperation(
                    "Modify Width and Height",
                    "Modify the width and height of all grass elements?",
                    "Modified Size",
                    ModifySize),
                ModifyOption.Both => new SyncModifyOperation(
                    "Modify Width Height and Color",
                    "Modify the width, height and color of all grass elements?",
                    "Modified Size and Color",
                    ModifySizeAndColor),
                _ => throw new ArgumentOutOfRangeException(nameof(modifyOption), modifyOption, null)
            };
        }

        public void ExecuteOperation(Action action)
        {
            action();
        }

        private void RebuildMesh()
        {
            _grassCompute.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}