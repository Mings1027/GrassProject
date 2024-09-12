using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grass.Editor
{
    public class GrassPainterWindow : EditorWindow
    {
        // main tabs
        private readonly string[] _mainTabBarStrings = { "Paint/Edit", "Modify", "Generate", "General Settings" };

        private int _mainTabCurrent;
        private Vector2 _scrollPos;

        private bool _paintModeActive;

        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reproject" };

        private readonly string[] _toolbarStringsEdit = { "Edit Colors", "Edit Length/Width", "Both" };

        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private SoGrassToolSettings toolSettings;

        // options
        [HideInInspector] public int toolbarInt;

        [HideInInspector] public int toolbarIntEdit;

        public List<GrassData> grassData = new();

        private int _grassAmount;

        private Ray _ray;

        private Vector3 _mousePos;

        private RaycastHit[] _terrainHit;
        private int _flowTimer;
        private Vector3 _lastPosition = Vector3.zero;

        [SerializeField] private GameObject grassObject;

        private GrassComputeScript _grassComputeScript;

        private NativeArray<float> _sizes;
        private NativeArray<float> _cumulativeSizes;
        private NativeArray<float> _total;

        private Vector3 _cachedPos;

        private bool _showLayers;

        [MenuItem("Tools/Grass Tool")]
        private static void Init()
        {
            Debug.Log("init");
            // Get existing open window or if none, make a new one:
            var window =
                (GrassPainterWindow)GetWindow(typeof(GrassPainterWindow), false, "Grass Tool", true);
            var icon = EditorGUIUtility.FindTexture("tree_icon");
            var mToolSettings =
                (SoGrassToolSettings)AssetDatabase.LoadAssetAtPath("Assets/Grass/Settings/Grass Tool Settings.asset",
                    typeof(SoGrassToolSettings));
            if (mToolSettings == null)
            {
                Debug.Log("creating new one");
                mToolSettings = CreateInstance<SoGrassToolSettings>();

                AssetDatabase.CreateAsset(mToolSettings, "Assets/Grass/Settings/Grass Tool Settings.asset");
                mToolSettings.CreateNewLayers();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            window.titleContent = new GUIContent("Grass Tool", icon);
            window.toolSettings = mToolSettings;
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            if (GUILayout.Button("Manual Update", GUILayout.Height(50)))
            {
                _grassComputeScript.Reset();
            }

            grassObject =
                (GameObject)EditorGUILayout.ObjectField("Grass Compute Object", grassObject, typeof(GameObject), true);

            if (grassObject == null)
            {
                grassObject = FindFirstObjectByType<GrassComputeScript>()?.gameObject;
            }

            if (grassObject != null)
            {
                _grassComputeScript = grassObject.GetComponent<GrassComputeScript>();
                _grassComputeScript.currentPresets = (SoGrassSettings)EditorGUILayout.ObjectField(
                    "Grass Settings Object",
                    _grassComputeScript.currentPresets, typeof(SoGrassSettings), false);

                if (_grassComputeScript.SetGrassPaintedDataList.Count > 0)
                {
                    grassData = _grassComputeScript.SetGrassPaintedDataList;
                    _grassAmount = grassData.Count;
                }
                else
                {
                    grassData.Clear();
                }

                if (_grassComputeScript.currentPresets == null)
                {
                    EditorGUILayout.LabelField(
                        "No Grass Settings Set, create or assign one first. \n Create > Utility> Grass Settings",
                        GUILayout.Height(150));
                    EditorGUILayout.EndScrollView();
                    return;
                }
            }
            else
            {
                if (GUILayout.Button("Create Grass Object"))
                {
                    if (EditorUtility.DisplayDialog("Create a new Grass Object?",
                            "No Grass Object Found, create a new Object?", "Yes", "No"))
                    {
                        CreateNewGrassObject();
                    }
                }

                EditorGUILayout.LabelField("No Grass System Holder found, create a new one", EditorStyles.label);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            _grassComputeScript.currentPresets.materialToUse = (Material)EditorGUILayout.ObjectField("Grass Material",
                _grassComputeScript.currentPresets.materialToUse, typeof(Material), false);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Total Grass Amount: " + _grassAmount.ToString(), EditorStyles.label);
            EditorGUILayout.BeginHorizontal();
            _mainTabCurrent = GUILayout.Toolbar(_mainTabCurrent, _mainTabBarStrings, GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel);
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            switch (_mainTabCurrent)
            {
                case 0: //paint
                    ShowPaintPanel();
                    break;
                case 1: // Modify
                    ShowModifyPanel();
                    break;
                case 2: // generate
                    ShowGeneratePanel();
                    break;
                case 3: //settings
                    ShowMainSettingsPanel();
                    break;
            }

            if (GUILayout.Button("Clear Grass"))
            {
                if (EditorUtility.DisplayDialog("Clear All Grass?",
                        "Are you sure you want to clear the grass?", "Clear", "Don't Clear"))
                {
                    ClearMesh();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(toolSettings);
            EditorUtility.SetDirty(_grassComputeScript.currentPresets);
        }

        private void ShowModifyPanel()
        {
            EditorGUILayout.LabelField("Modify Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Modify Length/Width"))
            {
                if (EditorUtility.DisplayDialog("Confirm Modification",
                        "Are you sure you want to modify the length and width of all grass elements?",
                        "Yes", "No"))
                {
                    ModifyLengthAndWidth();
                }
            }

            if (GUILayout.Button("Modify Colors"))
            {
                if (EditorUtility.DisplayDialog("Confirm Modification",
                        "Are you sure you want to modify the color of all grass elements?",
                        "Yes", "No"))
                {
                    ModifyColor();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth, 0.01f, 2f);
            toolSettings.sizeHeight = EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight, 0.01f, 2f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.adjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.adjustedColor);
            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            toolSettings.rangeR = EditorGUILayout.Slider("Red", toolSettings.rangeR, 0f, 1f);
            toolSettings.rangeG = EditorGUILayout.Slider("Green", toolSettings.rangeG, 0f, 1f);
            toolSettings.rangeB = EditorGUILayout.Slider("Blue", toolSettings.rangeB, 0f, 1f);
        }

        private void ShowGeneratePanel()
        {
            toolSettings.grassAmountToGenerate =
                EditorGUILayout.IntField("Grass Place Max Amount", toolSettings.grassAmountToGenerate);
            toolSettings.generationDensity =
                EditorGUILayout.Slider("Grass Place Density", toolSettings.generationDensity, 0.01f, 1f);

            EditorGUILayout.Separator();
            LayerMask paintingMask = EditorGUILayout.MaskField("Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintMask),
                InternalEditorUtility.layers);
            toolSettings.paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintingMask);

            LayerMask blockingMask = EditorGUILayout.MaskField("Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintBlockMask),
                InternalEditorUtility.layers);
            toolSettings.paintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(blockingMask);

            toolSettings.vertexColorSettings =
                (SoGrassToolSettings.VertexColorSetting)EditorGUILayout.EnumPopup("Block On vertex Colors",
                    toolSettings.vertexColorSettings);
            toolSettings.vertexFade =
                (SoGrassToolSettings.VertexColorSetting)EditorGUILayout.EnumPopup("Fade on Vertex Colors",
                    toolSettings.vertexFade);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth, 0.01f, 2f);
            toolSettings.sizeHeight = EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight, 0.01f, 2f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.adjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.adjustedColor);
            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            toolSettings.rangeR = EditorGUILayout.Slider("Red", toolSettings.rangeR, 0f, 1f);
            toolSettings.rangeG = EditorGUILayout.Slider("Green", toolSettings.rangeG, 0f, 1f);
            toolSettings.rangeB = EditorGUILayout.Slider("Blue", toolSettings.rangeB, 0f, 1f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Normal Limit", EditorStyles.boldLabel);
            toolSettings.normalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.normalLimit, 0f, 1f);

            EditorGUILayout.Separator();
            _showLayers = EditorGUILayout.Foldout(_showLayers, "Layer Settings(Cutoff Value, Fade Height Toggle", true);

            if (_showLayers)
            {
                for (var i = 0; i < toolSettings.layerBlocking.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    toolSettings.layerBlocking[i] =
                        EditorGUILayout.Slider(i.ToString(), toolSettings.layerBlocking[i], 0f, 1f);
                    toolSettings.layerFading[i] = EditorGUILayout.Toggle(toolSettings.layerFading[i]);
                    EditorGUILayout.EndHorizontal();
                }
            }

            var selection = Selection.gameObjects;

            if (GUILayout.Button("Add Positions From Mesh"))
            {
                if (selection == null)
                {
                    // no objects selected
                }
                else
                {
                    Undo.RegisterCompleteObjectUndo(this, "Add new Positions from Mesh(es)");
                    for (var i = 0; i < selection.Length; i++)
                    {
                        GeneratePositions(selection[i]);
                    }
                }
            }

            if (GUILayout.Button("Regenerate on current Mesh (Clears Current)"))
            {
                if (selection == null)
                {
                    // no object selected
                    return;
                }

                // ClearMesh();
                for (int i = 0; i < selection.Length; i++)
                {
                    RemoveGrassOnSelectObject(selection[i]);
                }

                Undo.RegisterCompleteObjectUndo(this, "Regenerated Positions on Mesh(es)");
                for (var i = 0; i < selection.Length; i++)
                {
                    GeneratePositions(selection[i]);
                }
            }

            EditorGUILayout.Separator();
        }

        private void ShowPaintPanel()
        {
            EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
            LayerMask tempMask2 = EditorGUILayout.MaskField("Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintMask),
                InternalEditorUtility.layers);
            toolSettings.paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask2);
            LayerMask tempMask0 = EditorGUILayout.MaskField("Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintBlockMask),
                InternalEditorUtility.layers);
            toolSettings.paintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask0);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Paint Status (Control + Alt + Left Mouse to paint)", EditorStyles.boldLabel);
            toolbarInt = GUILayout.Toolbar(toolbarInt, _toolbarStrings, GUILayout.Height(25));

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            toolSettings.brushSize = EditorGUILayout.Slider("Brush Size", toolSettings.brushSize, 0.1f, 50f);

            if (toolbarInt == 0)
            {
                toolSettings.normalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.normalLimit, 0f, 1f);
                toolSettings.density = EditorGUILayout.IntSlider("Density", toolSettings.density, 0, 100);
            }

            if (toolbarInt == 2)
            {
                toolbarIntEdit = GUILayout.Toolbar(toolbarIntEdit, _toolbarStringsEdit);
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Soft Falloff Settings", EditorStyles.boldLabel);
                toolSettings.brushFalloffSize =
                    EditorGUILayout.Slider("Brush Falloff Size", toolSettings.brushFalloffSize, 0.01f, 1f);
                toolSettings.flow = EditorGUILayout.Slider("Brush Flow", toolSettings.flow, 0.1f, 10f);
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);
                toolSettings.adjustWidth =
                    EditorGUILayout.Slider("Grass Width Adjustment", toolSettings.adjustWidth, -1f, 1f);
                toolSettings.adjustHeight =
                    EditorGUILayout.Slider("Grass Length Adjustment", toolSettings.adjustHeight, -1f, 1f);

                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true // 자동 줄 바꿈
                };

                EditorGUILayout.LabelField("Grass Width Adjustment Max Clamp", labelStyle);
                toolSettings.adjustWidthMax = EditorGUILayout.Slider(toolSettings.adjustWidthMax, 0.01f, 3f);

                EditorGUILayout.LabelField("Grass Length Adjustment Max Clamp", labelStyle);
                toolSettings.adjustHeightMax = EditorGUILayout.Slider(toolSettings.adjustHeightMax, 0.01f, 3f);

                EditorGUILayout.Separator();
            }

            if (toolbarInt is 0 or 2)
            {
                EditorGUILayout.Separator();

                if (toolbarInt == 0)
                {
                    EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
                    toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth, 0.01f, 2f);
                    toolSettings.sizeHeight =
                        EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight, 0.01f, 2f);
                    if (toolSettings.sizeHeight > _grassComputeScript.currentPresets.maxHeight)
                    {
                        EditorGUILayout.HelpBox(
                            "Grass Height must be less than Blade Height Max in the General Settings",
                            MessageType.Warning, true);
                    }

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Random Height Min/Max");
                    EditorGUILayout.MinMaxSlider(ref _grassComputeScript.currentPresets.grassRandomHeightMin,
                        ref _grassComputeScript.currentPresets.grassRandomHeightMax, 0f, 5f);
                    _grassComputeScript.currentPresets.grassRandomHeightMin =
                        EditorGUILayout.FloatField(_grassComputeScript.currentPresets.grassRandomHeightMin);
                    _grassComputeScript.currentPresets.grassRandomHeightMax =
                        EditorGUILayout.FloatField(_grassComputeScript.currentPresets.grassRandomHeightMax);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                toolSettings.adjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.adjustedColor);
                EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                toolSettings.rangeR = EditorGUILayout.Slider("Red", toolSettings.rangeR, 0f, 1f);
                toolSettings.rangeG = EditorGUILayout.Slider("Green", toolSettings.rangeG, 0f, 1f);
                toolSettings.rangeB = EditorGUILayout.Slider("Blue", toolSettings.rangeB, 0f, 1f);
            }

            if (toolbarInt == 3)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Reprojection Y Offset", EditorStyles.boldLabel);

                toolSettings.reprojectOffset = EditorGUILayout.FloatField(toolSettings.reprojectOffset);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
        }

        private void ShowMainSettingsPanel()
        {
            EditorGUILayout.LabelField("Blade Min/Max Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider("Blade Width Min/Max", ref _grassComputeScript.currentPresets.minWidth,
                ref _grassComputeScript.currentPresets.maxWidth, 0.01f, 1f);
            _grassComputeScript.currentPresets.minWidth =
                EditorGUILayout.FloatField(_grassComputeScript.currentPresets.minWidth);
            _grassComputeScript.currentPresets.maxWidth =
                EditorGUILayout.FloatField(_grassComputeScript.currentPresets.maxWidth);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider("Blade Height Min/Max", ref _grassComputeScript.currentPresets.minHeight,
                ref _grassComputeScript.currentPresets.maxHeight, 0.01f, 3f);
            _grassComputeScript.currentPresets.minHeight =
                EditorGUILayout.FloatField(_grassComputeScript.currentPresets.minHeight);
            _grassComputeScript.currentPresets.maxHeight =
                EditorGUILayout.FloatField(_grassComputeScript.currentPresets.maxHeight);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            // EditorGUILayout.LabelField("Random Height", EditorStyles.boldLabel);
            // _grassComputeScript.currentPresets.grassRandomHeightMin = EditorGUILayout.FloatField("Min Random:",
            //     _grassComputeScript.currentPresets.grassRandomHeightMin);
            // _grassComputeScript.currentPresets.grassRandomHeightMax = EditorGUILayout.FloatField("Max Random:",
            //     _grassComputeScript.currentPresets.grassRandomHeightMax);
            //
            // EditorGUILayout.MinMaxSlider("Random Grass Height",
            //     ref _grassComputeScript.currentPresets.grassRandomHeightMin,
            //     ref _grassComputeScript.currentPresets.grassRandomHeightMax, -5f, 5f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Blade Shape Settings", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.bladeRadius = EditorGUILayout.Slider("Blade Radius",
                _grassComputeScript.currentPresets.bladeRadius, 0f, 2f);
            _grassComputeScript.currentPresets.bladeForwardAmount = EditorGUILayout.Slider("Blade Forward",
                _grassComputeScript.currentPresets.bladeForwardAmount, 0f, 2f);
            _grassComputeScript.currentPresets.bladeCurveAmount = EditorGUILayout.Slider("Blade Curve",
                _grassComputeScript.currentPresets.bladeCurveAmount, 0f, 2f);
            _grassComputeScript.currentPresets.bottomWidth = EditorGUILayout.Slider("Bottom Width",
                _grassComputeScript.currentPresets.bottomWidth, 0f, 2f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Blade Amount Settings", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.allowedBladesPerVertex = EditorGUILayout.IntSlider(
                "Allowed Blades Per Vertex", _grassComputeScript.currentPresets.allowedBladesPerVertex, 1, 10);
            _grassComputeScript.currentPresets.allowedSegmentsPerBlade = EditorGUILayout.IntSlider(
                "Allowed Segments Per Blade", _grassComputeScript.currentPresets.allowedSegmentsPerBlade, 1, 4);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Wind Settings", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.windSpeed =
                EditorGUILayout.Slider("Wind Speed", _grassComputeScript.currentPresets.windSpeed, -2f, 2f);
            _grassComputeScript.currentPresets.windStrength = EditorGUILayout.Slider("Wind Strength",
                _grassComputeScript.currentPresets.windStrength, 0, 2f);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Tinting Settings", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.topTint =
                EditorGUILayout.ColorField("Top Tint", _grassComputeScript.currentPresets.topTint);
            _grassComputeScript.currentPresets.bottomTint =
                EditorGUILayout.ColorField("Bottom Tint", _grassComputeScript.currentPresets.bottomTint);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("LOD/Culling Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Culling Bounds:", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.drawBounds =
                EditorGUILayout.Toggle(_grassComputeScript.currentPresets.drawBounds);
            EditorGUILayout.EndHorizontal();
            _grassComputeScript.currentPresets.minFadeDistance = EditorGUILayout.FloatField("Min Fade Distance",
                _grassComputeScript.currentPresets.minFadeDistance);
            _grassComputeScript.currentPresets.maxDrawDistance = EditorGUILayout.FloatField("Max Draw Distance",
                _grassComputeScript.currentPresets.maxDrawDistance);
            _grassComputeScript.currentPresets.cullingTreeDepth = EditorGUILayout.IntField("Culling Tree Depth",
                _grassComputeScript.currentPresets.cullingTreeDepth);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Other Settings", EditorStyles.boldLabel);
            _grassComputeScript.currentPresets.affectStrength = EditorGUILayout.FloatField("Interactor Strength",
                _grassComputeScript.currentPresets.affectStrength);
            _grassComputeScript.currentPresets.castShadow =
                (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup("Shadow Settings",
                    _grassComputeScript.currentPresets.castShadow);
        }

        private void CreateNewGrassObject()
        {
            grassObject = new GameObject
            {
                name = "Grass System - Holder"
            };
            _grassComputeScript = grassObject.AddComponent<GrassComputeScript>();

            // setup object
            grassData = new List<GrassData>();
            _grassComputeScript.SetGrassPaintedDataList = grassData;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hasFocus && _paintModeActive)
            {
                DrawHandles();
            }
        }

        private readonly RaycastHit[] _results = new RaycastHit[1];

        // draw the painter handles
        private void DrawHandles()
        {
            //  Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            var hits = Physics.RaycastNonAlloc(_ray, _results, 200f, toolSettings.paintMask.value);
            for (var i = 0; i < hits; i++)
            {
                _hitPos = _results[i].point;
                _hitNormal = _results[i].normal;
            }

            //base
            var discColor = Color.green;
            Color discColor2 = new(0, 0.5f, 0, 0.4f);
            switch (toolbarInt)
            {
                case 1:
                    discColor = Color.red;
                    discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                    break;
                case 2:
                    discColor = Color.yellow;
                    discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);

                    Handles.color = discColor;
                    Handles.DrawWireDisc(_hitPos, _hitNormal, toolSettings.brushFalloffSize * toolSettings.brushSize);
                    Handles.color = discColor2;
                    Handles.DrawSolidDisc(_hitPos, _hitNormal,
                        toolSettings.brushFalloffSize * toolSettings.brushSize);

                    break;
                case 3:
                    discColor = Color.cyan;
                    discColor2 = new Color(0, 0.5f, 0.5f, 0.4f);
                    break;
            }

            Handles.color = discColor;
            Handles.DrawWireDisc(_hitPos, _hitNormal, toolSettings.brushSize);
            Handles.color = discColor2;
            Handles.DrawSolidDisc(_hitPos, _hitNormal, toolSettings.brushSize);

            if (_hitPos != _cachedPos)
            {
                SceneView.RepaintAll();
                _cachedPos = _hitPos;
            }
        }

#if UNITY_EDITOR
        private void HandleUndo()
        {
            if (_grassComputeScript != null)
            {
                SceneView.RepaintAll();
                _grassComputeScript.Reset();
            }
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;
            _terrainHit = new RaycastHit[10];
        }

        private void RemoveDelegates()
        {
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui -= OnScene;
            Undo.undoRedoPerformed -= HandleUndo;
        }

        private void OnDisable()
        {
            RemoveDelegates();
        }

        private void OnDestroy()
        {
            RemoveDelegates();
        }

        private void ClearMesh()
        {
            Undo.RegisterCompleteObjectUndo(this, "Cleared Grass");
            _grassAmount = 0;
            grassData.Clear();
            _grassComputeScript.SetGrassPaintedDataList = grassData;
            _grassComputeScript.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private readonly Collider[] _generateColliders = new Collider[1];

        private void GeneratePositions(GameObject selection)
        {
            var selectionLayer = selection.layer;
            var paintMask = toolSettings.paintMask.value;
            var paintBlockMask = toolSettings.paintBlockMask.value;
            // 현재 선택한 오브젝트의 레이어가 paintMask가 아니라면 return
            if (((1 << selectionLayer) & paintMask) == 0) return;
            // mesh
            if (selection.TryGetComponent(out MeshFilter sourceMesh))
            {
                CalcAreas(sourceMesh.sharedMesh);
                var localToWorld = sourceMesh.transform.localToWorldMatrix;

                var oTriangles = sourceMesh.sharedMesh.triangles;
                var oVertices = sourceMesh.sharedMesh.vertices;
                var oColors = sourceMesh.sharedMesh.colors;
                var oNormals = sourceMesh.sharedMesh.normals;

                var meshTriangles = new NativeArray<int>(oTriangles.Length, Allocator.Temp);
                var meshVertices = new NativeArray<Vector4>(oVertices.Length, Allocator.Temp);
                var meshColors = new NativeArray<Color>(oVertices.Length, Allocator.Temp);
                var meshNormals = new NativeArray<Vector3>(oNormals.Length, Allocator.Temp);
                for (var i = 0; i < meshTriangles.Length; i++)
                {
                    meshTriangles[i] = oTriangles[i];
                }

                for (var i = 0; i < meshVertices.Length; i++)
                {
                    meshVertices[i] = oVertices[i];
                    meshNormals[i] = oNormals[i];
                    if (oColors.Length == 0)
                    {
                        meshColors[i] = Color.black;
                    }
                    else
                    {
                        meshColors[i] = oColors[i];
                    }
                }

                var point = new NativeArray<Vector3>(1, Allocator.Temp);
                var normals = new NativeArray<Vector3>(1, Allocator.Temp);
                var lengthWidth = new NativeArray<float>(1, Allocator.Temp);
                var job = new GrassJob
                {
                    cumulativeSizes = _cumulativeSizes,
                    meshColors = meshColors,
                    meshTriangles = meshTriangles,
                    meshVertices = meshVertices,
                    meshNormals = meshNormals,
                    total = _total,
                    sizes = _sizes,
                    point = point,
                    normals = normals,
                    vertexColorSettings = toolSettings.vertexColorSettings,
                    vertexFade = toolSettings.vertexFade,
                    lengthWidth = lengthWidth,
                };

                var bounds = sourceMesh.sharedMesh.bounds;

                var meshSize = new Vector3(
                    bounds.size.x * sourceMesh.transform.lossyScale.x,
                    bounds.size.y * sourceMesh.transform.lossyScale.y,
                    bounds.size.z * sourceMesh.transform.lossyScale.z
                );
                meshSize += Vector3.one;

                var meshVolume = meshSize.x * meshSize.y * meshSize.z;
                var floorToInt = Mathf.FloorToInt(meshVolume * toolSettings.generationDensity);
                var numPoints = Mathf.Min(floorToInt, toolSettings.grassAmountToGenerate);
                for (var j = 0; j < numPoints; j++)
                {
                    job.Execute();
                    GrassData newData = new();
                    var newPoint = point[0];
                    newData.position = localToWorld.MultiplyPoint3x4(newPoint);

                    var size = Physics.OverlapBoxNonAlloc(newData.position, Vector3.one * 0.2f, _generateColliders,
                        Quaternion.identity, paintBlockMask);
                    if (size > 0)
                    {
                        newPoint = Vector3.zero;
                    }
                    // check normal limit

                    var worldNormal = selection.transform.TransformDirection(normals[0]);

                    if (worldNormal.y <= 1 + toolSettings.normalLimit &&
                        worldNormal.y >= 1 - toolSettings.normalLimit)
                    {
                        if (newPoint != Vector3.zero)
                        {
                            newData.color = GetRandomColor();
                            newData.length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeHeight) *
                                             lengthWidth[0];
                            newData.normal = worldNormal;
                            grassData.Add(newData);
                        }
                    }
                }

                _sizes.Dispose();
                _cumulativeSizes.Dispose();
                _total.Dispose();
                meshColors.Dispose();
                meshTriangles.Dispose();
                meshVertices.Dispose();
                meshNormals.Dispose();
                point.Dispose();
                lengthWidth.Dispose();

                RebuildMesh();
            }

            else if (selection.TryGetComponent(out Terrain terrain))
            {
                // terrainmesh

                var meshVolume = terrain.terrainData.size.x * terrain.terrainData.size.y *
                                 terrain.terrainData.size.z;
                var numPoints = Mathf.Min(Mathf.FloorToInt(meshVolume * toolSettings.generationDensity),
                    toolSettings.grassAmountToGenerate);

                for (var j = 0; j < numPoints; j++)
                {
                    var localToWorld = terrain.transform.localToWorldMatrix;
                    GrassData newData = new();
                    var newPoint = Vector3.zero;
                    var newNormal = Vector3.zero;
                    var maps = new float[0, 0, 0];
                    GetRandomPointOnTerrain(localToWorld, ref maps, terrain, terrain.terrainData.size, ref newPoint,
                        ref newNormal);
                    newData.position = newPoint;

                    var size = Physics.OverlapBoxNonAlloc(newData.position, Vector3.one * 0.2f, _generateColliders,
                        Quaternion.identity, paintBlockMask);
                    if (size > 0)
                    {
                        newPoint = Vector3.zero;
                    }

                    float getFadeMap = 0;
                    // check map layers
                    for (var i = 0; i < maps.Length; i++)
                    {
                        getFadeMap += System.Convert.ToInt32(toolSettings.layerFading[i]) * maps[0, 0, i];
                        if (maps[0, 0, i] > toolSettings.layerBlocking[i])
                        {
                            newPoint = Vector3.zero;
                        }
                    }

                    if (newNormal.y <= 1 + toolSettings.normalLimit && newNormal.y >= 1 - toolSettings.normalLimit)
                    {
                        var fade = Mathf.Clamp(getFadeMap, 0, 1f);
                        newData.color = GetRandomColor();
                        newData.length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeHeight * fade);
                        newData.normal = newNormal;
                        if (newPoint != Vector3.zero)
                        {
                            grassData.Add(newData);
                        }
                    }
                }

                RebuildMesh();
            }
        }

        private void RemoveGrassOnSelectObject(GameObject selection)
        {
            if (selection.TryGetComponent(out MeshFilter sourceMesh))
            {
                var localToWorld = selection.transform.localToWorldMatrix;
                var bounds = sourceMesh.sharedMesh.bounds;
                var meshSize = new Vector3(
                    bounds.size.x * sourceMesh.transform.lossyScale.x,
                    bounds.size.y * sourceMesh.transform.lossyScale.y,
                    bounds.size.z * sourceMesh.transform.lossyScale.z);
                meshSize += Vector3.one;

                var worldBounds = new Bounds(
                    localToWorld.MultiplyPoint3x4(bounds.center),
                    Vector3.Scale(bounds.size, selection.transform.lossyScale));

                grassData.RemoveAll(g => worldBounds.Contains(g.position));
            }
        }

        private Vector3 GetRandomColor()
        {
            Color newRandomCol = new(toolSettings.adjustedColor.r + Random.Range(0, 1.0f) * toolSettings.rangeR,
                toolSettings.adjustedColor.g + Random.Range(0, 1.0f) * toolSettings.rangeG,
                toolSettings.adjustedColor.b + Random.Range(0, 1.0f) * toolSettings.rangeB, 1);
            Vector3 color = new(newRandomCol.r, newRandomCol.g, newRandomCol.b);
            return color;
        }

        private void GetRandomPointOnTerrain(Matrix4x4 localToWorld, ref float[,,] maps, Terrain terrain, Vector3 size,
                                             ref Vector3 point, ref Vector3 normal)
        {
            point = new Vector3(Random.Range(0, size.x), 0, Random.Range(0, size.z));
            // sample layers wip

            var pointSizeX = point.x / size.x;
            var pointSizeZ = point.z / size.z;

            Vector3 newScale2 = new(pointSizeX * terrain.terrainData.alphamapResolution, 0,
                pointSizeZ * terrain.terrainData.alphamapResolution);
            var terrainX = Mathf.RoundToInt(newScale2.x);
            var terrainZ = Mathf.RoundToInt(newScale2.z);

            maps = terrain.terrainData.GetAlphamaps(terrainX, terrainZ, 1, 1);
            normal = terrain.terrainData.GetInterpolatedNormal(pointSizeX, pointSizeZ);
            point = localToWorld.MultiplyPoint3x4(point);
            point.y = terrain.SampleHeight(point) + terrain.GetPosition().y;
        }

        private void CalcAreas(Mesh mesh)
        {
            _sizes = GetTriSizes(mesh.triangles, mesh.vertices);
            _cumulativeSizes = new NativeArray<float>(_sizes.Length, Allocator.Temp);
            _total = new NativeArray<float>(1, Allocator.Temp);

            for (var i = 0; i < _sizes.Length; i++)
            {
                _total[0] += _sizes[i];
                _cumulativeSizes[i] = _total[0];
            }
        }

        // Using BurstCompile to compile a Job with burst
        // Set CompileSynchronously to true to make sure that the method will not be compiled asynchronously
        // but on the first schedule
        [BurstCompile(CompileSynchronously = true)]
        private struct GrassJob : IJob
        {
            [ReadOnly] public NativeArray<float> sizes;
            [ReadOnly] public NativeArray<float> total;
            [ReadOnly] public NativeArray<float> cumulativeSizes;
            [ReadOnly] public NativeArray<Color> meshColors;
            [ReadOnly] public NativeArray<Vector4> meshVertices;
            [ReadOnly] public NativeArray<Vector3> meshNormals;
            [ReadOnly] public NativeArray<int> meshTriangles;
            [WriteOnly] public NativeArray<Vector3> point;
            [WriteOnly] public NativeArray<float> lengthWidth;
            [WriteOnly] public NativeArray<Vector3> normals;

            public SoGrassToolSettings.VertexColorSetting vertexColorSettings;
            public SoGrassToolSettings.VertexColorSetting vertexFade;

            public void Execute()
            {
                var randomSample = Random.value * total[0];
                var triIndex = -1;

                for (var i = 0; i < sizes.Length; i++)
                {
                    if (randomSample <= cumulativeSizes[i])
                    {
                        triIndex = i;
                        break;
                    }
                }

                if (triIndex == -1) Debug.LogError("triIndex should never be -1");

                switch (vertexColorSettings)
                {
                    case SoGrassToolSettings.VertexColorSetting.Red:
                        if (meshColors[meshTriangles[triIndex * 3]].r > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case SoGrassToolSettings.VertexColorSetting.Green:
                        if (meshColors[meshTriangles[triIndex * 3]].g > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case SoGrassToolSettings.VertexColorSetting.Blue:
                        if (meshColors[meshTriangles[triIndex * 3]].b > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                }

                switch (vertexFade)
                {
                    case SoGrassToolSettings.VertexColorSetting.Red:
                        var red = meshColors[meshTriangles[triIndex * 3]].r;
                        var red2 = meshColors[meshTriangles[triIndex * 3 + 1]].r;
                        var red3 = meshColors[meshTriangles[triIndex * 3 + 2]].r;

                        lengthWidth[0] = 1.0f - (red + red2 + red3) * 0.3f;
                        break;
                    case SoGrassToolSettings.VertexColorSetting.Green:
                        var green = meshColors[meshTriangles[triIndex * 3]].g;
                        var green2 = meshColors[meshTriangles[triIndex * 3 + 1]].g;
                        var green3 = meshColors[meshTriangles[triIndex * 3 + 2]].g;

                        lengthWidth[0] = 1.0f - (green + green2 + green3) * 0.3f;
                        break;
                    case SoGrassToolSettings.VertexColorSetting.Blue:
                        var blue = meshColors[meshTriangles[triIndex * 3]].b;
                        var blue2 = meshColors[meshTriangles[triIndex * 3 + 1]].b;
                        var blue3 = meshColors[meshTriangles[triIndex * 3 + 2]].b;

                        lengthWidth[0] = 1.0f - (blue + blue2 + blue3) * 0.3f;
                        break;
                    case SoGrassToolSettings.VertexColorSetting.None:
                        lengthWidth[0] = 1.0f;
                        break;
                }

                Vector3 a = meshVertices[meshTriangles[triIndex * 3]];
                Vector3 b = meshVertices[meshTriangles[triIndex * 3 + 1]];
                Vector3 c = meshVertices[meshTriangles[triIndex * 3 + 2]];

                // Generate random barycentric coordinates
                var r = Random.value;
                var s = Random.value;

                if (r + s >= 1)
                {
                    r = 1 - r;
                    s = 1 - s;
                }

                normals[0] = meshNormals[meshTriangles[triIndex * 3 + 1]];

                // Turn point back to a Vector3
                var pointOnMesh = a + r * (b - a) + s * (c - a);

                point[0] = pointOnMesh;
            }
        }

        private NativeArray<float> GetTriSizes(int[] tris, Vector3[] verts)
        {
            var triCount = tris.Length / 3;
            var sizes = new NativeArray<float>(triCount, Allocator.Temp);
            for (var i = 0; i < triCount; i++)
            {
                sizes[i] = .5f * Vector3.Cross(
                    verts[tris[i * 3 + 1]] - verts[tris[i * 3]],
                    verts[tris[i * 3 + 2]] - verts[tris[i * 3]]).magnitude;
            }

            return sizes;
        }

        private void ModifyColor()
        {
            Undo.RegisterCompleteObjectUndo(this, "Modified Color");
            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.color = GetRandomColor();
                grassData[i] = newData;
            }

            RebuildMesh();
        }

        private void ModifyLengthAndWidth()
        {
            Undo.RegisterCompleteObjectUndo(this, "Modified Length/Width");
            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeHeight);
                grassData[i] = newData;
            }

            RebuildMesh();
        }

        private Ray RandomRay(Vector3 position, Vector3 normal, float radius, float falloff)
        {
            var a = Vector3.zero;
            var rotation = Quaternion.LookRotation(normal, Vector3.up);

            var rad = Random.Range(0f, 2 * Mathf.PI);
            a.x = Mathf.Cos(rad);
            a.y = Mathf.Sin(rad);

            float r;

            //In the case the curve isn't valid, only sample within the falloff range
            r = Mathf.Sqrt(Random.Range(0f, falloff));

            a = position + rotation * (a.normalized * r * radius);
            return new Ray(a + normal, -normal);
        }

        private void OnScene(SceneView scene)
        {
            if (this != null && _paintModeActive)
            {
                var e = Event.current;
                _mousePos = e.mousePosition;
                var ppp = EditorGUIUtility.pixelsPerPoint;
                _mousePos.y = scene.camera.pixelHeight - _mousePos.y * ppp;
                _mousePos.x *= ppp;
                _mousePos.z = 0;

                // ray for gizmo(disc)
                _ray = scene.camera.ScreenPointToRay(_mousePos);
                // undo system
                if (e.type == EventType.MouseDown && e.button == 0 && e.control && e.alt)
                {
                    switch (toolbarInt)
                    {
                        case 0:
                            Undo.RegisterCompleteObjectUndo(this, "Added Grass");
                            break;
                        case 1:
                            Undo.RegisterCompleteObjectUndo(this, "Removed Grass");
                            break;
                        case 2:
                            Undo.RegisterCompleteObjectUndo(this, "Edited Grass");
                            break;
                        case 3:
                            Undo.RegisterCompleteObjectUndo(this, "Reprojected Grass");
                            break;
                    }
                }

                if (e.type == EventType.MouseDrag && e.button == 0 && e.control && e.alt)
                {
                    switch (toolbarInt)
                    {
                        case 0:
                            AddGrass(e);
                            break;
                        case 1:
                            RemoveAtPoint(_terrainHit, e);
                            break;
                        case 2:
                            EditGrassPainting(_terrainHit, e);
                            break;
                        case 3:
                            ReprojectGrassPainting(_terrainHit, e);
                            break;
                    }

                    RebuildMeshFast();
                }

                // on up
                if (e.type == EventType.MouseUp && e.button == 0 && e.control && e.alt)
                {
                    RebuildMesh();
                }
            }
        }


        private void RemoveAtPoint(RaycastHit[] terrainHit, Event e)
        {
            var hits = Physics.RaycastNonAlloc(_ray, terrainHit, 100f, toolSettings.paintMask.value);
            for (var i = 0; i < hits; i++)
            {
                _hitPos = terrainHit[i].point;
                _hitNormal = terrainHit[i].normal;
                RemovePositionsNearRayCastHit(_hitPos, toolSettings.brushSize);
            }

            e.Use();
        }

        private void RemovePositionsNearRayCastHit(Vector3 hitPoint, float radius)
        {
            // Remove positions within the specified radius
            for (int i = grassData.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(grassData[i].position, hitPoint) <= radius)
                {
                    grassData.RemoveAt(i);
                }
            }
        }

        private GrassData CreateGrassData(Vector3 position, Vector3 normal)
        {
            return new GrassData
            {
                color = GetRandomColor(),
                position = position,
                length = new Vector2(toolSettings.sizeWidth, toolSettings.sizeHeight),
                normal = normal
            };
        }

        private void AddGrass(Event e)
        {
            var paintBlockMaskValue = toolSettings.paintBlockMask.value;
            var paintMaskValue = toolSettings.paintMask.value;
            var brushSize = toolSettings.brushSize;
            var density = toolSettings.density;
            var normalLimit = toolSettings.normalLimit;

            if (Physics.Raycast(_ray, out _, 200f))
            {
                if (Physics.CheckSphere(_hitPos, toolSettings.brushSize, paintBlockMaskValue))
                {
                    return;
                }
            }

            if (Physics.Raycast(_ray, out var hit2, 200f))
            {
                var hitLayer = hit2.transform.gameObject.layer;
                if (((1 << hitLayer) & paintMaskValue) != 0)
                {
                    _hitPos = hit2.point;
                    _hitNormal = hit2.normal;
                    // paintMaskValue와 hitLayer의 AND연산으로 0보다 크다는것은 paintMaskValue가 hitLayer를 포함하고 있음을 뜻함
                    // 즉 히트된 오브젝트의 레이어가 paintMaskValue에 포함되있다면

                    if (((1 << hitLayer) & paintMaskValue) > 0 && _hitNormal.y <= 1 + normalLimit &&
                        _hitNormal.y >= 1 - normalLimit)
                    {
                        if (Vector3.Distance(_hitPos, _lastPosition) > brushSize / density)
                        {
                            for (int i = 0; i < density; i++)
                            {
                                var randomPoint = Random.insideUnitCircle * brushSize;
                                var randomPos = new Vector3(_hitPos.x + randomPoint.x, _hitPos.y,
                                    _hitPos.z + randomPoint.y);
                                var castDir = -_hitNormal;
                                if (Physics.Raycast(randomPos + _hitNormal, castDir, out var groundHit, 1f))
                                {
                                    var groundHitLayer = groundHit.collider.gameObject.layer;
                                    if (((1 << groundHitLayer) & paintMaskValue) != 0)
                                    {
                                        var newData = CreateGrassData(groundHit.point, groundHit.normal);
                                        grassData.Add(newData);
                                    }
                                }
                            }
                        }

                        _lastPosition = _hitPos;
                    }
                }
            }


            e.Use();
        }

        private void EditGrassPainting(RaycastHit[] terrainHit, Event e)
        {
            var hits = Physics.RaycastNonAlloc(_ray, terrainHit, 200f, toolSettings.paintMask.value);
            for (var i = 0; i < hits; i++)
            {
                _hitPos = terrainHit[i].point;
                _hitNormal = terrainHit[i].normal;
                for (var j = 0; j < grassData.Count; j++)
                {
                    var pos = grassData[j].position;

                    //  pos += grassObject.transform.position;
                    var dist = Vector3.Distance(terrainHit[i].point, pos);

                    // if its within the radius of the brush, remove all info
                    if (dist <= toolSettings.brushSize)
                    {
                        var falloff = Mathf.Clamp01(dist / (toolSettings.brushFalloffSize * toolSettings.brushSize));

                        //store the original color
                        var origColor = grassData[j].color;

                        // add in the new color
                        var newCol = GetRandomColor();

                        var origLength = grassData[j].length;
                        var newLength = new Vector2(toolSettings.adjustWidth, toolSettings.adjustHeight);

                        _flowTimer++;
                        if (_flowTimer > toolSettings.flow)
                        {
                            // edit colors
                            if (toolbarIntEdit is 0 or 2)
                            {
                                var newData = grassData[j];
                                newData.color = Vector3.Lerp(newCol, origColor, falloff);
                                grassData[j] = newData;
                            }

                            // edit grass length
                            if (toolbarIntEdit is 1 or 2)
                            {
                                var newData = grassData[j];
                                newData.length = Vector2.Lerp(origLength + newLength, origLength, falloff);
                                newData.length.x = Mathf.Clamp(newData.length.x, 0, toolSettings.adjustWidthMax);
                                newData.length.y = Mathf.Clamp(newData.length.y, 0, toolSettings.adjustHeightMax);
                                grassData[j] = newData;
                            }

                            _flowTimer = 0;
                        }
                    }
                }
            }

            e.Use();
        }

        private void ReprojectGrassPainting(RaycastHit[] terrainHit, Event e)
        {
            var hits = Physics.RaycastNonAlloc(_ray, terrainHit, 200f, toolSettings.paintMask.value);
            for (var i = 0; i < hits; i++)

            {
                _hitPos = terrainHit[i].point;
                _hitNormal = terrainHit[i].normal;

                for (var j = 0; j < grassData.Count; j++)
                {
                    var pos = grassData[j].position;
                    //  pos += grassObject.transform.position;
                    var dist = Vector3.Distance(terrainHit[i].point, pos);

                    // if its within the radius of the brush, raycast to a new position
                    if (dist <= toolSettings.brushSize)
                    {
                        var meshPoint = new Vector3(pos.x, pos.y + toolSettings.reprojectOffset, pos.z);
                        if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f,
                                toolSettings.paintMask.value))
                        {
                            var newData = grassData[j];
                            newData.position = hitInfo.point;
                            grassData[j] = newData;
                        }
                    }
                }
            }

            e.Use();
        }

        private bool RemovePoints(GrassData point)
        {
            if (Physics.Raycast(_ray, out var terrainHit, 100f, toolSettings.paintMask.value))
            {
                _hitPos = terrainHit.point;
                _hitNormal = terrainHit.normal;

                var pos = point.position;
                // pos += grassObject.transform.position;
                var dist = Vector3.Distance(terrainHit.point, pos);

                // if its within the radius of the brush, remove all info
                return dist <= toolSettings.brushSize;
            }

            return false;
        }

        private void RebuildMesh()
        {
            _grassAmount = grassData.Count;
            _grassComputeScript.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildMeshFast()
        {
            _grassAmount = grassData.Count;
            _grassComputeScript.ResetFaster();
        }

#endif
    }
}