using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Grass.Editor
{
    public class GrassPainterWindow : EditorWindow
    {
        private enum ToolBarOption
        {
            Add,
            Remove,
            Edit,
            Reproject
        }

        private enum EditOption
        {
            EditColors,
            EditWidthHeight,
            Both
        }

        // main tabs
        private readonly string[] _mainTabBarStrings =
            { "Paint/Edit", "Modify", "Generate", "General Settings", "Material Settings" };

        private int _mainTabCurrent;
        private Vector2 _scrollPos;

        private bool _paintModeActive;
        private bool _enableGrass;

        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reproject" };

        private readonly string[] _toolbarStringsEdit = { "Edit Colors", "Edit Width/Height", "Both" };

        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSo toolSettings;

        // options
        private ToolBarOption _selectedTool;
        private EditOption _selectedEditOption;

        private int GrassAmount { get; set; }

        private Ray _mousePointRay;

        private Vector3 _mousePos;

        private Vector3 _lastPosition = Vector3.zero;

        [SerializeField] private GameObject grassObject;

        private GrassComputeScript _grassCompute;

        private NativeArray<float> _sizes;
        private NativeArray<float> _cumulativeSizes;
        private NativeArray<float> _total;

        private Vector3 _cachedPos;

        private bool _showLayers;

        private MaterialEditor _materialEditor;
        private Material _currentMaterial;
        private MaterialProperty[] _materialProperties;

        [SerializeField] private List<int> grassToRemove = new();
        private bool _isDragging;

        private OptimizedGrassDataStructure _grassDataStructure;
        private BatchGrassRemoval _batchRemoval;
        private List<GrassData> _grassData = new();
        private Bounds _grassBounds;

        private readonly List<Vector3> _brushPath = new();
        private const float BrushPathDensity = 1f; // 브러시 경로의 밀도 조절
        private readonly List<int> _grassIndicesToRemove = new();
        private readonly List<int> _grassIndicesToEdit = new();

        [MenuItem("Tools/Grass Tool")]
        private static void Init()
        {
            Debug.Log("init");
            // Get existing open window or if none, make a new one:
            var window =
                (GrassPainterWindow)GetWindow(typeof(GrassPainterWindow), false, "Grass Tool", true);
            var icon = EditorGUIUtility.FindTexture("tree_icon");
            var mToolSettings =
                (GrassToolSettingSo)AssetDatabase.LoadAssetAtPath("Assets/Grass/Settings/Grass Tool Settings.asset",
                    typeof(GrassToolSettingSo));
            if (mToolSettings == null)
            {
                Debug.Log("creating new one");
                mToolSettings = CreateInstance<GrassToolSettingSo>();

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
                _grassCompute.GrassDataList = _grassData;
                _grassCompute.Reset();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            toolSettings = (GrassToolSettingSo)EditorGUILayout.ObjectField("Grass Tool Settings", toolSettings,
                typeof(GrassToolSettingSo), false);

            grassObject =
                (GameObject)EditorGUILayout.ObjectField("Grass Compute Object", grassObject, typeof(GameObject), true);

            if (grassObject == null)
            {
                grassObject = FindFirstObjectByType<GrassComputeScript>()?.gameObject;
            }

            if (grassObject != null)
            {
                _grassCompute = grassObject.GetComponent<GrassComputeScript>();
                _grassCompute.currentPresets = (GrassSettingSO)EditorGUILayout.ObjectField(
                    "Grass Settings Object",
                    _grassCompute.currentPresets, typeof(GrassSettingSO), false);

                if (_grassCompute.GrassDataList.Count > 0)
                {
                    _grassData = _grassCompute.GrassDataList;
                    GrassAmount = _grassData.Count;
                }
                else
                {
                    _grassData.Clear();
                }

                if (_grassCompute.currentPresets == null)
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

            _grassCompute.currentPresets.materialToUse = (Material)EditorGUILayout.ObjectField("Grass Material",
                _grassCompute.currentPresets.materialToUse, typeof(Material), false);

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable Grass:", EditorStyles.boldLabel, GUILayout.Width(100));
            _enableGrass = EditorGUILayout.Toggle(_enableGrass);
            if (_enableGrass && !_grassCompute.enabled)
            {
                _grassCompute.enabled = true;
            }
            else if (!_enableGrass && _grassCompute.enabled)
            {
                _grassCompute.enabled = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Total Grass Amount: " + GrassAmount, EditorStyles.label);

            EditorGUILayout.BeginHorizontal();
            _mainTabCurrent = GUILayout.Toolbar(_mainTabCurrent, _mainTabBarStrings, GUILayout.MinWidth(300),
                GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel, GUILayout.Width(100));
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Update:", EditorStyles.boldLabel, GUILayout.Width(100));
            _grassCompute.autoUpdate =
                EditorGUILayout.Toggle(_grassCompute.autoUpdate, GUILayout.Width(20));
            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"));
            helpIcon.tooltip = "Slow but Always Updated";
            GUILayout.Label(helpIcon);
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
                case 4:
                    ShowMaterialSettings();
                    break;
            }

            if (GUILayout.Button("Clear current mesh"))
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects.Length > 0)
                {
                    if (EditorUtility.DisplayDialog("Clear Selected Mesh Grass?",
                            "Clear grass from the selected object?",
                            "Clear", "Don't Clear"))
                    {
                        for (var i = 0; i < selectedObjects.Length; i++)
                        {
                            RemoveGrassOnSelectObject(selectedObjects[i]);
                        }

                        RebuildMesh();
                    }
                }
            }

            if (GUILayout.Button("Clear All Grass"))
            {
                if (EditorUtility.DisplayDialog("Clear All Grass?",
                        "Clear all existing grass?", "Clear", "Don't Clear"))
                {
                    ClearMesh();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(toolSettings);
            EditorUtility.SetDirty(_grassCompute.currentPresets);
        }

        private void OnEnable()
        {
            ResetMaterialEditor();
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;

            // 초기 바운드 설정 (나중에 동적으로 업데이트 필요)
            _grassBounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            _grassDataStructure = new OptimizedGrassDataStructure(_grassBounds, 5f); // 셀 크기는 적절히 조정
            _batchRemoval = new BatchGrassRemoval();

            // 기존 잔디 데이터를 새 구조로 이동
            foreach (var grass in _grassData)
            {
                _grassDataStructure.AddGrass(grass);
            }
        }

        private void OnFocus()
        {
            ResetMaterialEditor();
        }

        private void OnDisable()
        {
            RemoveDelegates();
        }

        private void OnDestroy()
        {
            RemoveDelegates();
            if (_materialEditor != null)
            {
                DestroyImmediate(_materialEditor);
            }
        }

        private void ShowPaintPanel()
        {
            EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
            LayerMask tempMask2 = EditorGUILayout.MaskField("Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintMask),
                InternalEditorUtility.layers);
            toolSettings.PaintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask2);
            LayerMask tempMask0 = EditorGUILayout.MaskField("Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintBlockMask),
                InternalEditorUtility.layers);
            toolSettings.PaintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask0);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Paint Status (Control + Alt + Left Mouse to paint)", EditorStyles.boldLabel);

            _selectedTool = (ToolBarOption)GUILayout.Toolbar((int)_selectedTool, _toolbarStrings, GUILayout.Height(25));
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush Size");
            toolSettings.BrushSize = EditorGUILayout.Slider(toolSettings.BrushSize,
                toolSettings.MinBrushSize, toolSettings.MaxBrushSize);
            EditorGUILayout.EndHorizontal();

            if (_selectedTool == ToolBarOption.Add)
            {
                toolSettings.NormalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.NormalLimit,
                    toolSettings.MinNormalLimit, toolSettings.MaxNormalLimit);
                toolSettings.Density = EditorGUILayout.IntSlider("Density", toolSettings.Density,
                    toolSettings.MinDensity, toolSettings.MaxDensity);
            }

            if (_selectedTool == ToolBarOption.Edit)
            {
                _selectedEditOption = (EditOption)GUILayout.Toolbar((int)_selectedEditOption, _toolbarStringsEdit);
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Soft Falloff Settings", EditorStyles.boldLabel);
                toolSettings.BrushFalloffSize =
                    EditorGUILayout.Slider("Brush Falloff Size", toolSettings.BrushFalloffSize,
                        toolSettings.MinBrushFalloffSize, toolSettings.MaxBrushFalloffSize);
                toolSettings.FalloffOuterSpeed =
                    EditorGUILayout.Slider("Falloff Outer Speed", toolSettings.FalloffOuterSpeed, 0f, 1f);
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);
                toolSettings.AdjustWidth =
                    EditorGUILayout.Slider("Grass Width Adjustment", toolSettings.AdjustWidth, toolSettings.MinAdjust,
                        toolSettings.MaxAdjust);
                toolSettings.AdjustHeight =
                    EditorGUILayout.Slider("Grass Height Adjustment", toolSettings.AdjustHeight, toolSettings.MinAdjust,
                        toolSettings.MaxAdjust);

                toolSettings.AdjustWidthMax =
                    EditorGUILayout.Slider("Max Width Adjustment", toolSettings.AdjustWidthMax, 0.01f, 3f);
                toolSettings.AdjustHeightMax =
                    EditorGUILayout.Slider("Max Height Adjustment", toolSettings.AdjustHeightMax, 0.01f, 3f);

                EditorGUILayout.Separator();
            }

            if (_selectedTool is ToolBarOption.Add or ToolBarOption.Edit)
            {
                EditorGUILayout.Separator();

                if (_selectedTool == ToolBarOption.Add)
                {
                    EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
                    toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                        toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
                    toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                        toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);

                    var curPresets = _grassCompute.currentPresets;

                    if (toolSettings.GrassHeight > curPresets.maxHeight)
                    {
                        EditorGUILayout.HelpBox(
                            "Grass Height must be less than Blade Height Max in the General Settings",
                            MessageType.Warning, true);
                    }
                }

                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                toolSettings.AdjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.AdjustedColor);
                EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
            }

            if (_selectedTool == ToolBarOption.Reproject)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Reprojection Y Offset", EditorStyles.boldLabel);

                toolSettings.ReprojectOffset = EditorGUILayout.FloatField(toolSettings.ReprojectOffset);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
        }

        private void ShowModifyPanel()
        {
            EditorGUILayout.LabelField("Modify Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Modify Width/Height"))
            {
                if (EditorUtility.DisplayDialog("Confirm Modification",
                        "Are you sure you want to modify the width and height of all grass elements?",
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
            toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
            toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.AdjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.AdjustedColor);
            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
            toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
            toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
        }

        private void ShowGeneratePanel()
        {
            toolSettings.GrassAmountToGenerate =
                (int)EditorGUILayout.Slider("Grass Place Max Amount", toolSettings.GrassAmountToGenerate, 0, 100000);
            toolSettings.GenerationDensity =
                EditorGUILayout.Slider("Grass Place Density", toolSettings.GenerationDensity, 0.01f, 1f);

            EditorGUILayout.Separator();
            LayerMask paintingMask = EditorGUILayout.MaskField("Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintMask),
                InternalEditorUtility.layers);
            toolSettings.PaintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintingMask);

            LayerMask blockingMask = EditorGUILayout.MaskField("Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintBlockMask),
                InternalEditorUtility.layers);
            toolSettings.PaintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(blockingMask);

            toolSettings.VertexColorSettings =
                (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup("Block On vertex Colors",
                    toolSettings.VertexColorSettings);
            toolSettings.VertexFade =
                (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup("Fade on Vertex Colors",
                    toolSettings.VertexFade);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
            toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.AdjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.AdjustedColor);
            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
            toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
            toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Normal Limit", EditorStyles.boldLabel);
            toolSettings.NormalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.NormalLimit, 0f, 1f);

            EditorGUILayout.Separator();
            _showLayers = EditorGUILayout.Foldout(_showLayers, "Layer Settings(Cutoff Value, Fade Height Toggle", true);

            if (_showLayers)
            {
                for (var i = 0; i < toolSettings.LayerBlocking.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    toolSettings.LayerBlocking[i] =
                        EditorGUILayout.Slider(i.ToString(), toolSettings.LayerBlocking[i], 0f, 1f);
                    toolSettings.LayerFading[i] = EditorGUILayout.Toggle(toolSettings.LayerFading[i]);
                    EditorGUILayout.EndHorizontal();
                }
            }

            var selection = Selection.gameObjects;

            if (GUILayout.Button("Add Positions From Mesh"))
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects != null && selectedObjects.Length > 0)
                {
                    Undo.RegisterCompleteObjectUndo(this, "Add new Positions from Mesh(es)");

                    // 기존 데이터 모두 지우기
                    // ClearMesh();

                    int totalNewGrass = 0;
                    for (var i = 0; i < selectedObjects.Length; i++)
                    {
                        int previousCount = GrassAmount;
                        GeneratePositions(selectedObjects[i]);
                        totalNewGrass += GrassAmount - previousCount;
                    }

                    Debug.Log(
                        $"Added {totalNewGrass} grass instances to {selectedObjects.Length} object(s). Total grass count: {GrassAmount}");
                }
                else
                {
                    Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
                }
            }

            if (GUILayout.Button("Regenerate on current Mesh (Clears Current)"))
            {
                if (selection == null) return; // no object selected

                // ClearMesh();
                for (var i = 0; i < selection.Length; i++)
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

        private void ShowMainSettingsPanel()
        {
            EditorGUILayout.LabelField("Blade Min/Max Settings", EditorStyles.boldLabel);
            var curPresets = _grassCompute.currentPresets;

            {
                EditorGUILayout.MinMaxSlider("Blade Width Min/Max", ref curPresets.minWidth,
                    ref curPresets.maxWidth, curPresets.MinWidthLimit, curPresets.MaxWidthLimit);

                EditorGUILayout.BeginHorizontal();
                curPresets.minWidth = EditorGUILayout.FloatField(curPresets.minWidth);
                curPresets.maxWidth = EditorGUILayout.FloatField(curPresets.maxWidth);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();

            {
                EditorGUILayout.MinMaxSlider("Blade Height Min/Max", ref curPresets.minHeight,
                    ref curPresets.maxHeight, curPresets.MinHeightLimit, curPresets.MaxHeightLimit);

                EditorGUILayout.BeginHorizontal();
                curPresets.minHeight = EditorGUILayout.FloatField(curPresets.minHeight);
                curPresets.maxHeight = EditorGUILayout.FloatField(curPresets.maxHeight);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Random Height Min/Max");
            EditorGUILayout.MinMaxSlider(ref curPresets.randomHeightMin, ref curPresets.randomHeightMax,
                curPresets.RandomHeightMinLimit, curPresets.RandomHeightMaxLimit);

            EditorGUILayout.BeginHorizontal();

            curPresets.randomHeightMin = EditorGUILayout.FloatField(curPresets.randomHeightMin);
            curPresets.randomHeightMax = EditorGUILayout.FloatField(curPresets.randomHeightMax);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Blade Shape Settings", EditorStyles.boldLabel);
            curPresets.bladeRadius = EditorGUILayout.Slider("Blade Radius",
                curPresets.bladeRadius, curPresets.MinBladeRadius, curPresets.MaxBladeRadius);
            curPresets.bladeForward = EditorGUILayout.Slider("Blade Forward",
                curPresets.bladeForward, curPresets.MinBladeForward, curPresets.MaxBladeForward);
            curPresets.bladeCurve = EditorGUILayout.Slider("Blade Curve",
                curPresets.bladeCurve, curPresets.MinBladeCurve, curPresets.MaxBladeCurve);
            curPresets.bottomWidth = EditorGUILayout.Slider("Bottom Width",
                curPresets.bottomWidth, curPresets.MinBottomWidth, curPresets.MaxBottomWidth);

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Blade Amount Settings", EditorStyles.boldLabel);
            curPresets.bladesPerVertex = EditorGUILayout.IntSlider(
                "Blades Per Vertex", curPresets.bladesPerVertex, curPresets.MinBladesPerVertex,
                curPresets.MaxBladesPerVertex);
            curPresets.segmentsPerBlade = EditorGUILayout.IntSlider(
                "Segments Per Blade", curPresets.segmentsPerBlade, curPresets.MinSegmentsPerBlade,
                curPresets.MaxSegmentsPerBlade);

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Wind Settings", EditorStyles.boldLabel);
            curPresets.windSpeed =
                EditorGUILayout.Slider("Wind Speed", curPresets.windSpeed, curPresets.MinWindSpeed,
                    curPresets.MaxWindSpeed);
            curPresets.windStrength = EditorGUILayout.Slider("Wind Strength",
                curPresets.windStrength, curPresets.MinWindStrength, curPresets.MaxWindStrength);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Tinting Settings", EditorStyles.boldLabel);
            curPresets.topTint =
                EditorGUILayout.ColorField("Top Tint", curPresets.topTint);
            curPresets.bottomTint =
                EditorGUILayout.ColorField("Bottom Tint", curPresets.bottomTint);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("LOD/Culling Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Culling Bounds:", EditorStyles.boldLabel);
            curPresets.drawBounds =
                EditorGUILayout.Toggle(curPresets.drawBounds);
            EditorGUILayout.EndHorizontal();

            // Min Fade Distance 슬라이더
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Fade Distance", GUILayout.Width(120)); // 레이블 표시
            curPresets.minFadeDistance =
                EditorGUILayout.Slider(curPresets.minFadeDistance, 0, curPresets.maxFadeDistance); // 슬라이더
            GUILayout.Label($"0 / {curPresets.maxFadeDistance}", GUILayout.Width(80)); // 최소값과 최대값 텍스트로 표시
            GUILayout.EndHorizontal();

            // Max Fade Distance 슬라이더
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Fade Distance", GUILayout.Width(120)); // 레이블 표시
            curPresets.maxFadeDistance =
                EditorGUILayout.Slider(curPresets.maxFadeDistance, curPresets.minFadeDistance, 300); // 슬라이더
            GUILayout.Label($"{curPresets.minFadeDistance} / 300", GUILayout.Width(80)); // 최소값과 최대값 텍스트로 표시
            GUILayout.EndHorizontal();

            curPresets.cullingTreeDepth = EditorGUILayout.IntField("Culling Tree Depth",
                curPresets.cullingTreeDepth);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Other Settings", EditorStyles.boldLabel);
            curPresets.interactorStrength = EditorGUILayout.FloatField("Interactor Strength",
                curPresets.interactorStrength);
            curPresets.castShadow =
                (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup("Shadow Settings",
                    curPresets.castShadow);
        }

        private void ShowMaterialSettings()
        {
            if (_grassCompute == null || _grassCompute.currentPresets == null)
            {
                EditorGUILayout.HelpBox("Grass Compute Script or Current Presets is not set.", MessageType.Warning);
                return;
            }

            if (_grassCompute.currentPresets.materialToUse != _currentMaterial || _materialEditor == null)
            {
                ResetMaterialEditor();
                _currentMaterial = _grassCompute.currentPresets.materialToUse;
                if (_currentMaterial != null)
                {
                    _materialEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(_currentMaterial);
                    _materialProperties = MaterialEditor.GetMaterialProperties(new Object[] { _currentMaterial });
                }
            }

            if (_materialEditor != null && _currentMaterial != null && _materialProperties != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Material Properties", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();

                _materialEditor.DrawHeader();

                for (var i = 0; i < _materialProperties.Length; i++)
                {
                    var prop = _materialProperties[i];
                    if ((prop.flags & MaterialProperty.PropFlags.HideInInspector) == 0 &&
                        !prop.name.StartsWith("unity_") &&
                        !prop.name.StartsWith("_Quarter"))
                    {
                        _materialEditor.ShaderProperty(prop, prop.displayName);
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _materialEditor.PropertiesChanged();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No material selected or material is invalid.", MessageType.Info);
            }
        }

        private void ResetMaterialEditor()
        {
            _currentMaterial = null;
            if (_materialEditor != null)
            {
                DestroyImmediate(_materialEditor);
                _materialEditor = null;
            }

            _materialProperties = null;
        }

        private void CreateNewGrassObject()
        {
            grassObject = new GameObject
            {
                name = "Grass System - Holder"
            };
            _grassCompute = grassObject.AddComponent<GrassComputeScript>();

            // setup object
            _grassData = new List<GrassData>();
            _grassCompute.GrassDataList = _grassData;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hasFocus && _paintModeActive)
            {
                DrawHandles();
                if (_isDragging && _selectedTool == ToolBarOption.Remove)
                {
                    DrawRemovalGizmos();
                }
            }
        }

        // draw the painter handles
        private void DrawHandles()
        {
            if (Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
            {
                _hitPos = hit.point;
                _hitNormal = hit.normal;
            }

            //base
            Color discColor;
            Color discColor2;
            switch (_selectedTool)
            {
                case ToolBarOption.Add:
                    discColor = Color.green;
                    discColor2 = new Color(0, 0.5f, 0, 0.4f);
                    break;
                case ToolBarOption.Remove:
                    discColor = Color.red;
                    discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                    // 브러시 경로 그리기
                    Handles.color = new Color(1, 0, 0, 0.2f); // 반투명 빨간색
                    for (int i = 0; i < _brushPath.Count; i++)
                    {
                        Handles.DrawSolidDisc(_brushPath[i], Vector3.up, toolSettings.BrushSize);
                    }

                    // 현재 브러시 위치 그리기
                    Handles.color = new Color(1, 0, 0, 0.5f); // 좀 더 진한 빨간색
                    Handles.DrawWireDisc(_hitPos, _hitNormal, toolSettings.BrushSize);
                    break;
                case ToolBarOption.Edit:
                    discColor = Color.yellow;
                    discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);

                    Handles.color = discColor;
                    Handles.DrawWireDisc(_hitPos, _hitNormal, toolSettings.BrushFalloffSize * toolSettings.BrushSize);
                    Handles.color = discColor2;
                    Handles.DrawSolidDisc(_hitPos, _hitNormal, toolSettings.BrushFalloffSize * toolSettings.BrushSize);

                    break;
                case ToolBarOption.Reproject:
                    discColor = Color.cyan;
                    discColor2 = new Color(0, 0.5f, 0.5f, 0.4f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Handles.color = discColor;
            Handles.DrawWireDisc(_hitPos, _hitNormal, toolSettings.BrushSize);
            Handles.color = discColor2;
            Handles.DrawSolidDisc(_hitPos, _hitNormal, toolSettings.BrushSize);

            if (_hitPos != _cachedPos)
            {
                SceneView.RepaintAll();
                _cachedPos = _hitPos;
            }
        }

        private void DrawRemovalGizmos()
        {
            Handles.color = new Color(1, 0, 0); // 반투명 빨간색
            for (var i = 0; i < grassToRemove.Count; i++)
            {
                int index = grassToRemove[i];
                if (index < _grassData.Count)
                {
                    Handles.SphereHandleCap(
                        0,
                        _grassData[index].position,
                        Quaternion.identity,
                        0.2f,
                        EventType.Repaint
                    );
                }
            }
        }

        private void HandleUndo()
        {
            if (_grassCompute != null)
            {
                SceneView.RepaintAll();
                _grassCompute.Reset();
            }
        }

        private void RemoveDelegates()
        {
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui -= OnScene;
            Undo.undoRedoPerformed -= HandleUndo;
        }

        private void ClearMesh()
        {
            Undo.RegisterCompleteObjectUndo(this, "Cleared Grass");
            GrassAmount = 0;
            _grassData.Clear();
            _grassCompute.GrassDataList = _grassData;
            _grassCompute.Reset();

            // OptimizedGrassDataStructure 초기화
            _grassBounds = new Bounds(Vector3.zero, Vector3.one * 1000); // 기본 바운드 설정
            _grassDataStructure = new OptimizedGrassDataStructure(_grassBounds, 5f); // 새로운 인스턴스 생성

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private readonly Collider[] _generateColliders = new Collider[1];

        private void GeneratePositions(GameObject selection)
        {
            var selectionLayer = selection.layer;
            var paintMask = toolSettings.PaintMask.value;
            var paintBlockMask = toolSettings.PaintBlockMask.value;

            // Check if the selected object's layer is in the paint mask
            if (((1 << selectionLayer) & paintMask) == 0) return;

            List<GrassData> newGrassData = new List<GrassData>();

            if (selection.TryGetComponent(out MeshFilter sourceMesh))
            {
                CalcAreas(sourceMesh.sharedMesh);
                var localToWorld = sourceMesh.transform.localToWorldMatrix;

                var oTriangles = sourceMesh.sharedMesh.triangles;
                var oVertices = sourceMesh.sharedMesh.vertices;
                var oColors = sourceMesh.sharedMesh.colors;
                var oNormals = sourceMesh.sharedMesh.normals;

                // Create native arrays for mesh data
                var meshTriangles = new NativeArray<int>(oTriangles, Allocator.Temp);
                var meshVertices = new NativeArray<Vector4>(oVertices.Length, Allocator.Temp);
                var meshColors = new NativeArray<Color>(oVertices.Length, Allocator.Temp);
                var meshNormals = new NativeArray<Vector3>(oNormals, Allocator.Temp);

                for (var i = 0; i < meshVertices.Length; i++)
                {
                    meshVertices[i] = oVertices[i];
                    meshColors[i] = oColors.Length > 0 ? oColors[i] : Color.black;
                }

                // Setup job data
                var point = new NativeArray<Vector3>(1, Allocator.Temp);
                var normals = new NativeArray<Vector3>(1, Allocator.Temp);
                var widthHeight = new NativeArray<float>(1, Allocator.Temp);
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
                    vertexColorSettings = toolSettings.VertexColorSettings,
                    vertexFade = toolSettings.VertexFade,
                    widthHeight = widthHeight,
                };

                // Calculate the number of grass instances to generate
                var bounds = sourceMesh.sharedMesh.bounds;
                var meshSize = Vector3.Scale(bounds.size, sourceMesh.transform.lossyScale) + Vector3.one;
                var meshVolume = meshSize.x * meshSize.y * meshSize.z;
                var numPoints = Mathf.Min(Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity),
                    toolSettings.GrassAmountToGenerate);

                // Generate grass instances
                for (var j = 0; j < numPoints; j++)
                {
                    job.Execute();
                    var newData = new GrassData();
                    var newPoint = point[0];
                    newData.position = localToWorld.MultiplyPoint3x4(newPoint);

                    // Check if the position is blocked
                    var size = Physics.OverlapBoxNonAlloc(newData.position, Vector3.one * 0.2f, _generateColliders,
                        Quaternion.identity, paintBlockMask);
                    if (size > 0) continue;

                    var worldNormal = selection.transform.TransformDirection(normals[0]);

                    // Check normal limit
                    if (worldNormal.y <= 1 + toolSettings.NormalLimit &&
                        worldNormal.y >= 1 - toolSettings.NormalLimit)
                    {
                        newData.color = GetRandomColor();
                        newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight) *
                                              widthHeight[0];
                        newData.normal = worldNormal;
                        newGrassData.Add(newData);
                    }
                }

                // Dispose native arrays
                _sizes.Dispose();
                _cumulativeSizes.Dispose();
                _total.Dispose();
                meshColors.Dispose();
                meshTriangles.Dispose();
                meshVertices.Dispose();
                meshNormals.Dispose();
                point.Dispose();
                widthHeight.Dispose();
            }
            else if (selection.TryGetComponent(out Terrain terrain))
            {
                var terrainData = terrain.terrainData;
                var meshVolume = terrainData.size.x * terrainData.size.y * terrainData.size.z;
                var numPoints = Mathf.Min(Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity),
                    toolSettings.GrassAmountToGenerate);

                var localToWorld = terrain.transform.localToWorldMatrix;

                for (var j = 0; j < numPoints; j++)
                {
                    var newData = new GrassData();
                    var newPoint = Vector3.zero;
                    var newNormal = Vector3.zero;
                    var maps = new float[0, 0, 0];
                    GetRandomPointOnTerrain(localToWorld, ref maps, terrain, terrainData.size, ref newPoint,
                        ref newNormal);
                    newData.position = newPoint;

                    // Check if the position is blocked
                    var size = Physics.OverlapBoxNonAlloc(newData.position, Vector3.one * 0.2f, _generateColliders,
                        Quaternion.identity, paintBlockMask);
                    if (size > 0) continue;

                    float getFadeMap = 0;
                    // Check map layers
                    for (var i = 0; i < maps.Length; i++)
                    {
                        getFadeMap += Convert.ToInt32(toolSettings.LayerFading[i]) * maps[0, 0, i];
                        if (maps[0, 0, i] > toolSettings.LayerBlocking[i])
                        {
                            newPoint = Vector3.zero;
                            break;
                        }
                    }

                    if (newPoint != Vector3.zero && newNormal.y <= 1 + toolSettings.NormalLimit &&
                        newNormal.y >= 1 - toolSettings.NormalLimit)
                    {
                        var fade = Mathf.Clamp(getFadeMap, 0, 1f);
                        newData.color = GetRandomColor();
                        newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight * fade);
                        newData.normal = newNormal;
                        newGrassData.Add(newData);
                    }
                }
            }

            // Add new grass data to OptimizedGrassDataStructure
            foreach (var newGrass in newGrassData)
            {
                _grassDataStructure.AddGrass(newGrass);
            }

            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Debug.Log($"Generated {newGrassData.Count} new grass instances. Total grass count: {_grassAmount}");
        }

        private void RemoveGrassOnSelectObject(GameObject selection)
        {
            Bounds worldBounds;
            if (selection.TryGetComponent(out MeshFilter sourceMesh))
            {
                var localToWorld = selection.transform.localToWorldMatrix;
                var bounds = sourceMesh.sharedMesh.bounds;

                worldBounds = new Bounds(
                    localToWorld.MultiplyPoint3x4(bounds.center),
                    Vector3.Scale(bounds.size, selection.transform.lossyScale));
            }
            else if (selection.TryGetComponent(out Terrain terrain))
            {
                var terrainData = terrain.terrainData;
                var terrainPos = terrain.transform.position;

                worldBounds = new Bounds(terrainPos +
                                         new Vector3(terrainData.size.x * 0.5f, terrainData.size.y * 0.5f,
                                             terrainData.size.z * 0.5f), terrainData.size);
            }
            else
            {
                Debug.LogWarning("Selected object does not have a MeshFilter or Terrain component");
                return;
            }

            // 제거할 잔디의 ID를 저장할 리스트
            List<int> grassToRemove = new List<int>();

            // OptimizedGrassDataStructure에서 제거할 잔디 찾기
            var allGrassData = _grassDataStructure.GetAllGrassData();
            for (int i = 0; i < allGrassData.Count; i++)
            {
                if (worldBounds.Contains(allGrassData[i].position))
                {
                    grassToRemove.Add(i); // GetHashCode() 대신 인덱스 사용
                }
            }

            // OptimizedGrassDataStructure에서 잔디 제거
            foreach (var id in grassToRemove)
            {
                if (_grassDataStructure.TryGetGrassData(id, out var grassData))
                {
                    _grassDataStructure.RemoveGrass(grassData.position, 0.01f);
                }
            }

            // _grassData 업데이트
            _grassData = _grassDataStructure.GetAllGrassData();
            GrassAmount = _grassData.Count;

            // GrassComputeScript 업데이트
            if (_grassCompute != null)
            {
                _grassCompute.GrassDataList = _grassData;
                _grassCompute.Reset();
            }

            // 씬 갱신
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private Vector3 GetRandomColor()
        {
            Color baseColor = toolSettings.AdjustedColor;
            Color newRandomCol = new Color(
                baseColor.r + Random.Range(0, toolSettings.RangeR),
                baseColor.g + Random.Range(0, toolSettings.RangeG),
                baseColor.b + Random.Range(0, toolSettings.RangeB),
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
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
            [WriteOnly] public NativeArray<float> widthHeight;
            [WriteOnly] public NativeArray<Vector3> normals;

            public GrassToolSettingSo.VertexColorSetting vertexColorSettings;
            public GrassToolSettingSo.VertexColorSetting vertexFade;

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
                    case GrassToolSettingSo.VertexColorSetting.Red:
                        if (meshColors[meshTriangles[triIndex * 3]].r > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case GrassToolSettingSo.VertexColorSetting.Green:
                        if (meshColors[meshTriangles[triIndex * 3]].g > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case GrassToolSettingSo.VertexColorSetting.Blue:
                        if (meshColors[meshTriangles[triIndex * 3]].b > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                }

                switch (vertexFade)
                {
                    case GrassToolSettingSo.VertexColorSetting.Red:
                        var red = meshColors[meshTriangles[triIndex * 3]].r;
                        var red2 = meshColors[meshTriangles[triIndex * 3 + 1]].r;
                        var red3 = meshColors[meshTriangles[triIndex * 3 + 2]].r;

                        widthHeight[0] = 1.0f - (red + red2 + red3) * 0.3f;
                        break;
                    case GrassToolSettingSo.VertexColorSetting.Green:
                        var green = meshColors[meshTriangles[triIndex * 3]].g;
                        var green2 = meshColors[meshTriangles[triIndex * 3 + 1]].g;
                        var green3 = meshColors[meshTriangles[triIndex * 3 + 2]].g;

                        widthHeight[0] = 1.0f - (green + green2 + green3) * 0.3f;
                        break;
                    case GrassToolSettingSo.VertexColorSetting.Blue:
                        var blue = meshColors[meshTriangles[triIndex * 3]].b;
                        var blue2 = meshColors[meshTriangles[triIndex * 3 + 1]].b;
                        var blue3 = meshColors[meshTriangles[triIndex * 3 + 2]].b;

                        widthHeight[0] = 1.0f - (blue + blue2 + blue3) * 0.3f;
                        break;
                    case GrassToolSettingSo.VertexColorSetting.None:
                        widthHeight[0] = 1.0f;
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
            var allGrassData = _grassDataStructure.GetAllGrassData();
            var updatedGrassData = new List<GrassData>();

            for (var i = 0; i < allGrassData.Count; i++)
            {
                var newData = allGrassData[i];
                newData.color = GetRandomColor();
                updatedGrassData.Add(newData);
            }

            _grassDataStructure.UpdateMultipleGrass(updatedGrassData);
            UpdateGrassData();
            RebuildMesh();
        }

        private void ModifyLengthAndWidth()
        {
            Undo.RegisterCompleteObjectUndo(this, "Modified Length/Width");
            var allGrassData = _grassDataStructure.GetAllGrassData();
            var updatedGrassData = new List<GrassData>();

            for (var i = 0; i < allGrassData.Count; i++)
            {
                var newData = allGrassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                updatedGrassData.Add(newData);
            }

            _grassDataStructure.UpdateMultipleGrass(updatedGrassData);
            UpdateGrassData();
            RebuildMesh();
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
                _mousePointRay = scene.camera.ScreenPointToRay(_mousePos);

                if (e.type == EventType.ScrollWheel && e.control && e.alt)
                {
                    toolSettings.BrushSize += e.delta.y;
                    toolSettings.BrushSize = Mathf.Clamp(toolSettings.BrushSize, toolSettings.MinBrushSize,
                        toolSettings.MaxBrushSize);
                    e.Use();
                    return;
                }

                // undo system
                if (e.type == EventType.MouseDown && e.button == 0 && e.control && e.alt)
                {
                    _isDragging = true;
                    _brushPath.Clear(); // 새로운 드래그 시작 시 브러시 경로 초기화
                    grassToRemove.Clear();
                    _cumulativeChanges.Clear();

                    switch (_selectedTool)
                    {
                        case ToolBarOption.Add:
                            Undo.RegisterCompleteObjectUndo(this, "Added Grass");
                            break;
                        case ToolBarOption.Remove:
                            Undo.RegisterCompleteObjectUndo(this, "Removed Grass");
                            break;
                        case ToolBarOption.Edit:
                            Undo.RegisterCompleteObjectUndo(this, "Edited Grass");
                            break;
                        case ToolBarOption.Reproject:
                            Undo.RegisterCompleteObjectUndo(this, "Reprojected Grass");
                            break;
                    }
                }

                if (e.type == EventType.MouseDrag && e.button == 0 && e.control && e.alt)
                {
                    switch (_selectedTool)
                    {
                        case ToolBarOption.Add:
                            AddGrass(e);
                            break;
                        case ToolBarOption.Remove:
                            MarkGrassForRemove(_hitPos, toolSettings.BrushSize);
                            break;
                        case ToolBarOption.Edit:
                            EditGrassPainting(e);
                            break;
                        case ToolBarOption.Reproject:
                            ReprojectGrassPainting(e);
                            break;
                    }

                    RebuildMeshFast();
                }

                // on up
                if (e.type == EventType.MouseUp && e.button == 0 && e.control && e.alt)
                {
                    _isDragging = false;
                    if (_selectedTool == ToolBarOption.Remove)
                    {
                        RemoveMarkedGrass();
                        _brushPath.Clear(); // 제거 작업 완료 후 브러시 경로 초기화
                    }
                }
            }
        }

        private void MarkGrassForRemove(Vector3 hitPoint, float radius)
        {
            _grassDataStructure.GetGrassIndicesInRadius(hitPoint, radius, _grassIndicesToRemove);
            for (var i = 0; i < _grassIndicesToRemove.Count; i++)
            {
                int index = _grassIndicesToRemove[i];
                _batchRemoval.MarkGrassForRemoval(index);
            }

            // 브러시 경로 추가
            if (_brushPath.Count == 0 ||
                Vector3.Distance(_brushPath[_brushPath.Count - 1], hitPoint) > BrushPathDensity)
            {
                _brushPath.Add(hitPoint);
            }
        }

        private void AddGrass(Event e)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var normalLimit = toolSettings.NormalLimit;

            var startPos = _hitPos + Vector3.up * 3f;

            var distanceMoved = Vector3.Distance(_lastPosition, startPos);
            if (distanceMoved >= brushSize)
            {
                for (var i = 0; i < density; i++)
                {
                    var randomPoint = Random.insideUnitCircle * brushSize;
                    var randomPos = new Vector3(startPos.x + randomPoint.x, startPos.y, startPos.z + randomPoint.y);

                    if (Physics.Raycast(randomPos + Vector3.up * 3f, Vector3.down, out var hit, float.MaxValue,
                            paintMaskValue))
                    {
                        var hitLayer = hit.collider.gameObject.layer;
                        if (((1 << hitLayer) & paintMaskValue) != 0 && hit.normal.y <= 1 + normalLimit &&
                            hit.normal.y >= 1 - normalLimit)
                        {
                            var newData = CreateGrassData(hit.point, hit.normal);
                            _grassDataStructure.AddGrass(newData);
                            GrassAmount++;
                        }
                    }
                }

                _lastPosition = startPos;
            }

            e.Use();
        }

        private GrassData CreateGrassData(Vector3 position, Vector3 normal)
        {
            return new GrassData
            {
                color = GetRandomColor(),
                position = position,
                widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight),
                normal = normal
            };
        }

        private void RemoveMarkedGrass()
        {
            _batchRemoval.StartBatchRemoval(_grassDataStructure, () =>
            {
                // 제거 완료 후 실행될 콜백
                EditorUtility.ClearProgressBar(); // 진행 상황 바 제거
                UpdateGrassData();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Grass removal completed. Remaining grass count: {GrassAmount}");
            });
        }

        private readonly Dictionary<int, float> _cumulativeChanges = new();

        private void EditGrassPainting(Event e)
        {
            if (!Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            Vector3 hitPos = hit.point;
            float brushSize = toolSettings.BrushSize;
            float brushSizeSqr = brushSize * brushSize;
            float brushFalloffSize = toolSettings.BrushFalloffSize * brushSize;
            float brushFalloffSizeSqr = brushFalloffSize * brushFalloffSize;
            Vector2 adjustSize = new Vector2(toolSettings.AdjustWidth, toolSettings.AdjustHeight);
            Color adjustColor = toolSettings.AdjustedColor;
            Vector3 colorRange = new Vector3(toolSettings.RangeR, toolSettings.RangeG, toolSettings.RangeB);

            bool editColor = _selectedEditOption is EditOption.EditColors or EditOption.Both;
            bool editSize = _selectedEditOption is EditOption.EditWidthHeight or EditOption.Both;

            _grassDataStructure.GetGrassIndicesInRadius(hitPos, brushSize, _grassIndicesToEdit);

            foreach (int id in _grassIndicesToEdit)
            {
                if (_grassDataStructure.TryGetGrassData(id, out GrassData currentData))
                {
                    Vector3 grassPos = currentData.position;
                    float distSqr = (hitPos - grassPos).sqrMagnitude;

                    if (distSqr <= brushSizeSqr)
                    {
                        GrassData targetData = currentData;

                        if (editColor)
                        {
                            Color newCol = new Color(
                                adjustColor.r + Random.value * colorRange.x,
                                adjustColor.g + Random.value * colorRange.y,
                                adjustColor.b + Random.value * colorRange.z
                            );
                            targetData.color = new Vector3(newCol.r, newCol.g, newCol.b);
                        }

                        if (editSize)
                        {
                            targetData.widthHeight = new Vector2(
                                Mathf.Clamp(currentData.widthHeight.x + adjustSize.x, 0, toolSettings.AdjustWidthMax),
                                Mathf.Clamp(currentData.widthHeight.y + adjustSize.y, 0, toolSettings.AdjustHeightMax)
                            );
                        }

                        if (!_cumulativeChanges.ContainsKey(id))
                            _cumulativeChanges[id] = 0f;

                        float changeSpeed = (distSqr <= brushFalloffSizeSqr) ? 1f : toolSettings.FalloffOuterSpeed;
                        _cumulativeChanges[id] = Mathf.Clamp01(_cumulativeChanges[id] + changeSpeed * Time.deltaTime);

                        GrassData newData = new GrassData
                        {
                            position = currentData.position,
                            normal = currentData.normal,
                            color = Vector3.Lerp(currentData.color, targetData.color, _cumulativeChanges[id]),
                            widthHeight = Vector2.Lerp(currentData.widthHeight, targetData.widthHeight,
                                _cumulativeChanges[id])
                        };

                        _grassDataStructure.UpdateGrass(id, newData);
                    }
                }
            }

            e.Use();
        }

        private void ReprojectGrassPainting(Event e)
        {
            if (Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
            {
                _hitPos = hit.point;
                _hitNormal = hit.normal;

                for (var j = 0; j < _grassData.Count; j++)
                {
                    var pos = _grassData[j].position;
                    //  pos += grassObject.transform.position;
                    var dist = Vector3.Distance(hit.point, pos);

                    // if its within the radius of the brush, raycast to a new position
                    if (dist <= toolSettings.BrushSize)
                    {
                        var meshPoint = new Vector3(pos.x, pos.y + toolSettings.ReprojectOffset, pos.z);
                        if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f,
                                toolSettings.PaintMask.value))
                        {
                            var newData = _grassData[j];
                            newData.position = hitInfo.point;
                            _grassData[j] = newData;
                        }
                    }
                }
            }

            e.Use();
        }

        private void RebuildMesh()
        {
            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildMeshFast()
        {
            UpdateGrassData(false);
        }

        private void UpdateGrassData(bool fullReset = true)
        {
            _grassData = _grassDataStructure.GetAllGrassData();
            GrassAmount = _grassData.Count;

            if (_grassCompute != null)
            {
                _grassCompute.GrassDataList = _grassData;
                if (fullReset)
                {
                    _grassCompute.Reset();
                }
                else
                {
                    _grassCompute.ResetFaster();
                }
            }
        }
    }
}