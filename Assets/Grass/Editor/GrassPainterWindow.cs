using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Grass.Editor
{
    public class ObjectProgress
    {
        public float progress;
        public string progressMessage;
    }

    public class GrassPainterWindow : EditorWindow
    {
        // main tabs
        private readonly string[] _mainTabBarStrings =
            { "Paint/Edit", "Modify", "Generate", "General Settings", "Material Settings" };

        private int _mainTabCurrent;
        private Vector2 _scrollPos;

        private bool _paintModeActive;
        private bool _enableGrass;
        private bool _showSpatialGrid;

        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reproject" };
        private readonly string[] _editOptionStrings = { "Edit Colors", "Edit Width/Height", "Both" };
        private readonly string[] _modifyOptionStrings = { "Width/Height", "Color", "Both" };

        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSo toolSettings;

        // options
        private BrushOption _selectedToolOption;
        private EditOption _selectedEditOption;
        private ModifyOption _selectedModifyOption;

        private Ray _mousePointRay;

        private Vector3 _mousePos;

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

        private List<GrassData> _grassData = new();
        private Bounds _grassBounds;

        private bool _isInit;
        private bool _isProcessing;
        private CancellationTokenSource _cts;
        private ObjectProgress _objectProgress = new();

        private bool _isMousePressed;
        private bool _isPainting;

        private GrassAddPainter _grassAddPainter;
        private GrassEditPainter _grassEditPainter;
        private GrassRemovePainter _grassRemovePainter;
        private GrassReprojectPainter _grassReprojectPainter;

        private SpatialGrid _spatialGrid;

        [MenuItem("Tools/Grass Tool")]
        private static void Open()
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
            window._enableGrass = true;
        }

        private void OnGUI()
        {
            if (_isProcessing)
            {
                DrawProcessingUI();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawBasicControls();

            if (!ValidateGrassObject())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawGrassObjectSettings();
            DrawMainToolbar();
            DrawControlPanels();
            DrawClearButtons();

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(toolSettings);
            EditorUtility.SetDirty(_grassCompute.currentPresets);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;
        }

        private void OnDisable()
        {
            Debug.Log("OnDisable");
            RemoveDelegates();
            _spatialGrid.Clear();
        }

        private void OnDestroy()
        {
            RemoveDelegates();
            if (_materialEditor != null)
            {
                DestroyImmediate(_materialEditor);
            }
        }

        private void DrawProcessingUI()
        {
            const float bottomMargin = 10f;
            const float progressBarHeight = 20f;
            const float buttonHeight = 25f;
            const float buttonWidth = 150f;
            const float spacing = 5f;

            // Kill 버튼 배치
            var buttonRect = new Rect(
                (position.width - buttonWidth) / 2,
                position.height - bottomMargin - progressBarHeight - spacing - buttonHeight,
                buttonWidth,
                buttonHeight
            );
            if (GUI.Button(buttonRect, "Kill Grass Process"))
            {
                _isProcessing = false;
                _cts.Cancel();
            }

            // 프로그레스 바 배치
            var progressRect = new Rect(
                10,
                position.height - bottomMargin - progressBarHeight,
                position.width - 20,
                progressBarHeight
            );
            EditorGUI.ProgressBar(progressRect, _objectProgress.progress, _objectProgress.progressMessage);
            Repaint();
        }

        private void DrawBasicControls()
        {
            // grassObject가 없으면 씬에서 찾기
            if (grassObject == null)
            {
                grassObject = FindFirstObjectByType<GrassComputeScript>()?.gameObject;
                if (grassObject != null)
                {
                    _grassCompute = grassObject.GetComponent<GrassComputeScript>();
                }
            }

            if (_spatialGrid == null)
            {
                InitSpatialGrid();
            }

            if (GUILayout.Button("Manual Update", GUILayout.Height(50)))
            {
                Init();
                _grassCompute.GrassDataList = _grassData;
                _grassCompute.Reset();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            toolSettings = (GrassToolSettingSo)EditorGUILayout.ObjectField(
                "Grass Tool Settings",
                toolSettings,
                typeof(GrassToolSettingSo),
                false
            );

            grassObject = (GameObject)EditorGUILayout.ObjectField(
                "Grass Compute Object",
                grassObject,
                typeof(GameObject),
                true
            );
        }

        private bool ValidateGrassObject()
        {
            if (grassObject == null)
            {
                if (GUILayout.Button("Create Grass Object"))
                {
                    if (EditorUtility.DisplayDialog(
                            "Create a new Grass Object?",
                            "No Grass Object Found, create a new Object?",
                            "Yes",
                            "No"))
                    {
                        CreateNewGrassObject();
                    }
                }

                EditorGUILayout.LabelField("No Grass System Holder found, create a new one", EditorStyles.label);
                return false;
            }

            return true;
        }

        private void DrawGrassObjectSettings()
        {
            _grassCompute.currentPresets = (GrassSettingSO)EditorGUILayout.ObjectField(
                "Grass Settings Object",
                _grassCompute.currentPresets,
                typeof(GrassSettingSO),
                false
            );

            if (_grassCompute.currentPresets == null)
            {
                EditorGUILayout.LabelField(
                    "No Grass Settings Set, create or assign one first. \n Create > Utility> Grass Settings",
                    GUILayout.Height(150)
                );
                return;
            }

            EditorGUILayout.Separator();
            _grassCompute.currentPresets.materialToUse = (Material)EditorGUILayout.ObjectField(
                "Grass Material",
                _grassCompute.currentPresets.materialToUse,
                typeof(Material),
                false
            );
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.Separator();
            ShowKeyBindingsUI();
            DrawGrassToggle();
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Total Grass Amount: " + _grassCompute.GrassDataList.Count, EditorStyles.label);

            EditorGUILayout.BeginHorizontal();
            _mainTabCurrent = GUILayout.Toolbar(
                _mainTabCurrent,
                _mainTabBarStrings,
                GUILayout.MinWidth(300),
                GUILayout.Height(30)
            );
            EditorGUILayout.EndHorizontal();

            DrawPaintModeAndAutoUpdate();
        }

        private void DrawGrassToggle()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable Grass:", EditorStyles.boldLabel, GUILayout.Width(100));
            _enableGrass = EditorGUILayout.Toggle(_enableGrass);

            if (_grassCompute == null)
                _grassCompute = grassObject.GetComponent<GrassComputeScript>();

            _grassCompute.enabled = _enableGrass;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Spatial Grid:", EditorStyles.boldLabel, GUILayout.Width(100));
            _showSpatialGrid = EditorGUILayout.Toggle(_showSpatialGrid);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPaintModeAndAutoUpdate()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel, GUILayout.Width(100));
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Update:", EditorStyles.boldLabel, GUILayout.Width(100));
            _grassCompute.autoUpdate = EditorGUILayout.Toggle(_grassCompute.autoUpdate, GUILayout.Width(20));

            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"))
            {
                tooltip = "Slow but Always Updated"
            };
            GUILayout.Label(helpIcon);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawControlPanels()
        {
            EditorGUILayout.Separator();

            switch (_mainTabCurrent)
            {
                case 0:
                    ShowPaintPanel();
                    break;
                case 1:
                    ShowModifyPanel();
                    break;
                case 2:
                    ShowGeneratePanel();
                    break;
                case 3:
                    ShowMainSettingsPanel();
                    break;
                case 4:
                    ShowMaterialSettings();
                    break;
            }
        }

        private void DrawClearButtons()
        {
            if (GUILayout.Button("Clear current mesh"))
            {
                if (EditorUtility.DisplayDialog(
                        "Clear Grass",
                        "Clear the grass on the selected object(s)?",
                        "Yes",
                        "No"))
                {
                    var selectedObjects = Selection.gameObjects;
                    if (selectedObjects is { Length: > 0 })
                    {
                        Undo.RegisterCompleteObjectUndo(this, "Cleared Grass on Selected Objects");
                        ClearCurrentGrass(selectedObjects).Forget();
                    }
                    else
                    {
                        Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
                    }
                }
            }

            if (GUILayout.Button("Clear All Grass"))
            {
                if (EditorUtility.DisplayDialog(
                        "Clear All Grass",
                        "Remove all grass from the scene?",
                        "Yes",
                        "No"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Cleared All Grass");
                    ClearMesh();
                }
            }
        }

        private static int GetUpdateInterval(int totalCount)
        {
            return totalCount switch
            {
                <= 100000 => 100000,
                <= 1000000 => 100000,
                <= 10000000 => 100000,
                <= 100000000 => 200000,
                _ => 200000
            };
        }

        private void Init()
        {
            if (_grassCompute.GrassDataList.Count > 0)
            {
                _grassData = _grassCompute.GrassDataList;
            }
            else
            {
                _grassData.Clear();
            }

            InitSpatialGrid();

            // 이미 생성된 painter가 있다면 Initialize만 호출
            _grassAddPainter?.Initialize(_grassCompute, _spatialGrid);
            _grassRemovePainter?.Initialize(_grassCompute, _spatialGrid);
            _grassEditPainter?.Initialize(_grassCompute, _spatialGrid);
            _grassReprojectPainter?.Initialize(_grassCompute, _spatialGrid);

            // painter가 없는 경우에만 새로 생성
            _grassAddPainter ??= new GrassAddPainter(_grassCompute, _spatialGrid);
            _grassRemovePainter ??= new GrassRemovePainter(_grassCompute, _spatialGrid);
            _grassEditPainter ??= new GrassEditPainter(_grassCompute, _spatialGrid);
            _grassReprojectPainter ??= new GrassReprojectPainter(_grassCompute, _spatialGrid);
        }

        private void ShowKeyBindingsUI()
        {
            EditorGUILayout.LabelField("Key Bindings", EditorStyles.boldLabel);
            toolSettings.paintKey = (KeyBinding)EditorGUILayout.MaskField("Modifier Keys",
                (int)toolSettings.paintKey, Enum.GetNames(typeof(KeyBinding)));
            toolSettings.paintButton = (MouseButton)EditorGUILayout.EnumPopup("Paint Button", toolSettings.paintButton);
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

            _selectedToolOption =
                (BrushOption)GUILayout.Toolbar((int)_selectedToolOption, _toolbarStrings, GUILayout.Height(25));
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush Size");
            toolSettings.BrushSize = EditorGUILayout.Slider(toolSettings.BrushSize,
                toolSettings.MinBrushSize, toolSettings.MaxBrushSize);
            EditorGUILayout.EndHorizontal();

            if (_selectedToolOption == BrushOption.Add)
            {
                toolSettings.NormalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.NormalLimit,
                    toolSettings.MinNormalLimit, toolSettings.MaxNormalLimit);
                toolSettings.Density = EditorGUILayout.IntSlider("Density", toolSettings.Density,
                    toolSettings.MinDensity, toolSettings.MaxDensity);

                EditorGUILayout.Separator();

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

                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
            }

            if (_selectedToolOption == BrushOption.Edit)
            {
                _selectedEditOption = (EditOption)GUILayout.Toolbar((int)_selectedEditOption, _editOptionStrings);
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Soft Falloff Settings", EditorStyles.boldLabel);
                toolSettings.FalloffOuterSpeed =
                    EditorGUILayout.Slider("Falloff Outer Speed", toolSettings.FalloffOuterSpeed, 0f, 1f);
                EditorGUILayout.Separator();

                if (_selectedEditOption == EditOption.EditColors)
                {
                    EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                    toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
                    EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                    toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                    toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                    toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
                }
                else if (_selectedEditOption == EditOption.EditWidthHeight)
                {
                    EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);
                    toolSettings.AdjustWidth =
                        EditorGUILayout.Slider("Grass Width Adjustment", toolSettings.AdjustWidth,
                            toolSettings.MinAdjust,
                            toolSettings.MaxAdjust);
                    toolSettings.AdjustHeight =
                        EditorGUILayout.Slider("Grass Height Adjustment", toolSettings.AdjustHeight,
                            toolSettings.MinAdjust,
                            toolSettings.MaxAdjust);

                    toolSettings.AdjustWidthMax =
                        EditorGUILayout.Slider("Max Width Adjustment", toolSettings.AdjustWidthMax, 0.01f, 3f);
                    toolSettings.AdjustHeightMax =
                        EditorGUILayout.Slider("Max Height Adjustment", toolSettings.AdjustHeightMax, 0.01f, 3f);
                }
                else if (_selectedEditOption == EditOption.Both)
                {
                    EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);
                    toolSettings.AdjustWidth =
                        EditorGUILayout.Slider("Grass Width Adjustment", toolSettings.AdjustWidth,
                            toolSettings.MinAdjust,
                            toolSettings.MaxAdjust);
                    toolSettings.AdjustHeight =
                        EditorGUILayout.Slider("Grass Height Adjustment", toolSettings.AdjustHeight,
                            toolSettings.MinAdjust,
                            toolSettings.MaxAdjust);

                    toolSettings.AdjustWidthMax =
                        EditorGUILayout.Slider("Max Width Adjustment", toolSettings.AdjustWidthMax, 0.01f, 3f);
                    toolSettings.AdjustHeightMax =
                        EditorGUILayout.Slider("Max Height Adjustment", toolSettings.AdjustHeightMax, 0.01f, 3f);

                    EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                    toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
                    EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                    toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                    toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                    toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
                }

                EditorGUILayout.Separator();
            }

            if (_selectedToolOption == BrushOption.Reproject)
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
            _selectedModifyOption = (ModifyOption)GUILayout.Toolbar((int)_selectedModifyOption, _modifyOptionStrings,
                GUILayout.Height(25));

            EditorGUILayout.Separator();

            switch (_selectedModifyOption)
            {
                case ModifyOption.WidthHeight:
                    EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
                    toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                        toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
                    toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                        toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
                    break;
                case ModifyOption.Color:
                    EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                    toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
                    EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                    toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                    toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                    toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
                    break;
                case ModifyOption.Both:
                    EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
                    toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                        toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
                    toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                        toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
                    EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                    toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
                    EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                    toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
                    toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
                    toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
                    break;
            }

            EditorGUILayout.Separator();

            if (GUILayout.Button("Apply Modify Options", GUILayout.Height(25)))
            {
                if (_selectedModifyOption == ModifyOption.WidthHeight)
                {
                    if (EditorUtility.DisplayDialog("Modify Width and Height",
                            "Modify the width and height of all grass elements?",
                            "Yes", "No"))
                    {
                        ModifyWidthAndHeight();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Color)
                {
                    if (EditorUtility.DisplayDialog("Modify Color",
                            "Modify the color of all grass elements?",
                            "Yes", "No"))
                    {
                        ModifyColor();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Both)
                {
                    if (EditorUtility.DisplayDialog("Modify Width Height and Color",
                            "Modify the width, height and color of all grass elements?", "Yes", "No"))
                    {
                        ModifyWidthAndHeight();
                        ModifyColor();
                    }
                }
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
        }

        private void ShowGeneratePanel()
        {
            toolSettings.GrassAmountToGenerate =
                (int)EditorGUILayout.Slider("Grass Place Max Amount", toolSettings.GrassAmountToGenerate,
                    toolSettings.MinGrassAmountToGenerate, toolSettings.MaxGrassAmountToGenerate);
            toolSettings.GenerationDensity =
                EditorGUILayout.Slider("Grass Place Density", toolSettings.GenerationDensity,
                    toolSettings.MinGenerationDensity, toolSettings.MaxGenerationDensity);

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
            toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
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

            if (GUILayout.Button("Add Grass to Selected Objects"))
            {
                if (EditorUtility.DisplayDialog("Add Grass", "Add grass to selected object(s)?",
                        "Yes", "No"))
                {
                    var selectedObjects = Selection.gameObjects;
                    if (selectedObjects is { Length: > 0 })
                    {
                        Undo.RegisterCompleteObjectUndo(this, "Add new Positions from Mesh(es)");
                        AddGrassOnMesh(selectedObjects).Forget();
                    }
                    else
                    {
                        Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
                    }
                }
            }

            if (GUILayout.Button("Regenerate Grass on Selection"))
            {
                if (EditorUtility.DisplayDialog("Regenerate Grass",
                        "Clear existing grass and regenerate on selected object(s)?",
                        "Yes", "No"))
                {
                    var selectedObjects = Selection.gameObjects;
                    if (selectedObjects is { Length: > 0 })
                    {
                        Undo.RegisterCompleteObjectUndo(this, "Regenerated Positions on Mesh(es)");
                        RegenerateGrass(selectedObjects).Forget();
                    }
                    else
                    {
                        Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
                    }
                }
            }
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
            if (hasFocus)
            {
                if (_paintModeActive)
                {
                    DrawHandles();

                    if (_showSpatialGrid)
                    {
                        DrawGridHandles();
                    }
                }
            }
        }

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
            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    discColor = Color.green;
                    discColor2 = new Color(0, 0.5f, 0, 0.4f);
                    break;
                case BrushOption.Remove:
                    discColor = Color.red;
                    discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                    break;
                case BrushOption.Edit:
                    discColor = Color.yellow;
                    discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);
                    break;
                case BrushOption.Reproject:
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

        private void DrawGridHandles()
        {
            if (_spatialGrid == null) return;
            var cellSize = _spatialGrid.CellSize;

            // 마우스 근처의 셀만 표시하기 위한 범위 계산
            var hitCellCenter = Vector3.zero;
            if (Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
            {
                var hitCell = _spatialGrid.WorldToCell(hit.point);
                hitCellCenter = _spatialGrid.CellToWorld(hitCell);
            }

            // 브러시 크기를 기준으로 표시할 셀 범위 계산
            var cellRadius = Mathf.CeilToInt(toolSettings.BrushSize / cellSize);
            var centerCell = _spatialGrid.WorldToCell(hitCellCenter);

            // 그리드 색상 설정
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.2f); // 기본 셀 색상
            var activeCellColor = new Color(0.3f, 1f, 0.3f, 0.4f); // 활성 셀 색상 (풀이 있는 셀)

            // 브러시 범위 내의 셀들만 표시
            for (var x = -cellRadius; x <= cellRadius; x++)
            for (var y = -cellRadius; y <= cellRadius; y++)
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                var checkCell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z + z);
                var cellWorldPos = _spatialGrid.CellToWorld(checkCell);
                var cellCenter = cellWorldPos + new Vector3(cellSize * 0.5f, cellSize * 0.5f, cellSize * 0.5f);

                // 셀에 풀이 있는지 확인하고 색상 설정
                var key = SpatialGrid.GetKey(checkCell.x, checkCell.y, checkCell.z);
                var hasGrass = _spatialGrid.HasAnyObject(key);
                Handles.color = hasGrass ? activeCellColor : new Color(0.2f, 0.8f, 1f, 0.1f);

                // 셀 그리기
                DrawCellCube(cellCenter, cellSize);
            }
        }

        private void DrawCellCube(Vector3 center, float size)
        {
            var halfSize = size * 0.5f;
            var points = new[]
            {
                center + new Vector3(-halfSize, -halfSize, -halfSize),
                center + new Vector3(halfSize, -halfSize, -halfSize),
                center + new Vector3(halfSize, -halfSize, halfSize),
                center + new Vector3(-halfSize, -halfSize, halfSize),
                center + new Vector3(-halfSize, halfSize, -halfSize),
                center + new Vector3(halfSize, halfSize, -halfSize),
                center + new Vector3(halfSize, halfSize, halfSize),
                center + new Vector3(-halfSize, halfSize, halfSize)
            };

            // 아래쪽 면
            Handles.DrawLine(points[0], points[1]);
            Handles.DrawLine(points[1], points[2]);
            Handles.DrawLine(points[2], points[3]);
            Handles.DrawLine(points[3], points[0]);

            // 위쪽 면
            Handles.DrawLine(points[4], points[5]);
            Handles.DrawLine(points[5], points[6]);
            Handles.DrawLine(points[6], points[7]);
            Handles.DrawLine(points[7], points[4]);

            // 수직선
            Handles.DrawLine(points[0], points[4]);
            Handles.DrawLine(points[1], points[5]);
            Handles.DrawLine(points[2], points[6]);
            Handles.DrawLine(points[3], points[7]);
        }

        private void OnScene(SceneView scene)
        {
            if (this == null || !_paintModeActive) return;

            var e = Event.current;
            _mousePos = e.mousePosition;
            var ppp = EditorGUIUtility.pixelsPerPoint;
            _mousePos.y = scene.camera.pixelHeight - _mousePos.y * ppp;
            _mousePos.x *= ppp;
            _mousePos.z = 0;

            // ray for gizmo(disc)
            _mousePointRay = scene.camera.ScreenPointToRay(_mousePos);

            var areModifierKeysPressed = GrassPainterHelper.AreModifierKeysPressed(toolSettings.paintKey);
            var isCorrectMouseButton = GrassPainterHelper.IsMouseButtonPressed(toolSettings.paintButton);

            if (e.type == EventType.ScrollWheel && areModifierKeysPressed)
            {
                HandleScrollWheel(e);
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (isCorrectMouseButton && areModifierKeysPressed)
                    {
                        _isMousePressed = true;
                        StartPainting();
                    }

                    break;
                case EventType.MouseDrag:
                    if (isCorrectMouseButton && areModifierKeysPressed)
                    {
                        ContinuePainting();
                    }

                    break;
                case EventType.MouseUp:
                    if (isCorrectMouseButton)
                    {
                        _isMousePressed = false;
                        EndPainting();
                    }

                    break;

                case EventType.KeyUp:
                    if (!areModifierKeysPressed)
                    {
                        EndPainting();
                    }

                    break;
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
            _grassData.Clear();
            _grassCompute.GrassDataList = _grassData;
            _grassCompute.Reset();

            // Clear the spatial grid
            _spatialGrid?.Clear();

            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Cleared all grass from the scene");
        }

        private async UniTask GeneratePositions(GameObject[] selections)
        {
            var totalPoints = CalculateTotalPoints(selections);
            var currentPoint = 0;
            var newGrassData = new List<GrassData>();

            for (var index = 0; index < selections.Length; index++)
            {
                var selection = selections[index];
                if (((1 << selection.layer) & toolSettings.PaintMask.value) == 0)
                {
                    LogLayerMismatch(selection);
                    continue;
                }

                if (selection.TryGetComponent(out MeshFilter sourceMesh))
                {
                    await GenerateGrassForMesh(sourceMesh, newGrassData, currentPoint, totalPoints);
                }
                else if (selection.TryGetComponent(out Terrain terrain))
                {
                    await GenerateGrassForTerrain(terrain, newGrassData, currentPoint, totalPoints);
                }

                currentPoint += CalculatePointsForObject(selection);
            }

            for (var i = 0; i < newGrassData.Count; i++)
            {
                _grassData.Add(newGrassData[i]);
            }

            _grassCompute.GrassDataList = _grassData;
            _grassCompute.ResetFaster();
        }

        private int CalculatePointsForObject(GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter sourceMesh))
            {
                return CalculatePointsForMesh(sourceMesh);
            }

            if (obj.TryGetComponent(out Terrain terrain))
            {
                return CalculatePointsForTerrain(terrain);
            }

            return 0;
        }

        private int CalculateTotalPoints(GameObject[] selections)
        {
            var totalPoints = 0;
            for (var index = 0; index < selections.Length; index++)
            {
                var selection = selections[index];
                if (selection.TryGetComponent(out MeshFilter sourceMesh))
                {
                    totalPoints += CalculatePointsForMesh(sourceMesh);
                }
                else if (selection.TryGetComponent(out Terrain terrain))
                {
                    totalPoints += CalculatePointsForTerrain(terrain);
                }
            }

            return totalPoints;
        }

        private void LogLayerMismatch(GameObject selection)
        {
            var expectedLayers = string.Join(", ", toolSettings.GetPaintMaskLayerNames());
            Debug.LogWarning(
                $"'{selection.name}' layer mismatch. Expected: '{expectedLayers}', Current: {LayerMask.LayerToName(selection.layer)}");
        }

        private async UniTask GenerateGrassForMesh(MeshFilter sourceMesh, List<GrassData> newGrassData,
                                                   int startPoint, int totalPoints)
        {
            var localToWorld = sourceMesh.transform.localToWorldMatrix;
            var sharedMesh = sourceMesh.sharedMesh;
            var oVertices = sharedMesh.vertices;
            var oColors = sharedMesh.colors;
            var oNormals = sharedMesh.normals;

            var numPoints = CalculatePointsForMesh(sourceMesh);
            var updateInterval = GetUpdateInterval(numPoints);
            for (var j = 0; j < numPoints; j++)
            {
                await UpdateProgress(startPoint + j, totalPoints, updateInterval,
                    $"Generating grass: {startPoint + j}/{totalPoints}");

                var (success, grassData) = GenerateGrassDataForMesh(sourceMesh, localToWorld, sharedMesh, oVertices,
                    oColors, oNormals);
                if (success)
                {
                    newGrassData.Add(grassData);
                }
            }
        }

        private int CalculatePointsForMesh(MeshFilter sourceMesh)
        {
            var bounds = sourceMesh.sharedMesh.bounds;
            var meshSize = Vector3.Scale(bounds.size, sourceMesh.transform.lossyScale) + Vector3.one;
            var meshVolume = meshSize.x * meshSize.y * meshSize.z;
            return Mathf.Min(Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity),
                toolSettings.GrassAmountToGenerate);
        }

        private (bool success, GrassData grassData) GenerateGrassDataForMesh(
            MeshFilter sourceMesh, Matrix4x4 localToWorld, Mesh sharedMesh, Vector3[] oVertices, Color[] oColors,
            Vector3[] oNormals)
        {
            var randomTriIndex = Random.Range(0, sharedMesh.triangles.Length / 3);
            var randomBarycentricCoord = GrassPainterHelper.GetRandomBarycentricCoord();
            var vertexIndex1 = sharedMesh.triangles[randomTriIndex * 3];
            var vertexIndex2 = sharedMesh.triangles[randomTriIndex * 3 + 1];
            var vertexIndex3 = sharedMesh.triangles[randomTriIndex * 3 + 2];

            var point = randomBarycentricCoord.x * oVertices[vertexIndex1] +
                        randomBarycentricCoord.y * oVertices[vertexIndex2] +
                        randomBarycentricCoord.z * oVertices[vertexIndex3];

            var normal = randomBarycentricCoord.x * oNormals[vertexIndex1] +
                         randomBarycentricCoord.y * oNormals[vertexIndex2] +
                         randomBarycentricCoord.z * oNormals[vertexIndex3];

            var worldPoint = localToWorld.MultiplyPoint3x4(point);
            var worldNormal = sourceMesh.transform.TransformDirection(normal);

            if (Physics.CheckSphere(worldPoint, 0.1f, toolSettings.PaintBlockMask))
            {
                return (false, default);
            }

            if (worldNormal.y <= 1 + toolSettings.NormalLimit &&
                worldNormal.y >= 1 - toolSettings.NormalLimit)
            {
                var color = GrassPainterHelper.GetVertexColor(oColors, vertexIndex1, vertexIndex2, vertexIndex3,
                    randomBarycentricCoord);
                var widthHeightFactor = CalculateWidthHeightFactor(color);

                var grassData = new GrassData
                {
                    position = worldPoint,
                    normal = worldNormal,
                    color = GetRandomColor(),
                    widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight) * widthHeightFactor
                };

                return (true, grassData);
            }

            return (false, default);
        }

        private async UniTask GenerateGrassForTerrain(Terrain terrain, List<GrassData> newGrassData, int startPoint,
                                                      int totalPoints)
        {
            var numPoints = CalculatePointsForTerrain(terrain);
            var updateInterval = GetUpdateInterval(numPoints);
            for (var j = 0; j < numPoints; j++)
            {
                await UpdateProgress(startPoint + j, totalPoints, updateInterval,
                    $"Generating grass: {startPoint + j}/{totalPoints}");

                var (success, grassData) = GenerateGrassDataForTerrain(terrain);
                if (success)
                {
                    newGrassData.Add(grassData);
                }
            }
        }

        private int CalculatePointsForTerrain(Terrain terrain)
        {
            var terrainData = terrain.terrainData;
            var terrainSize = terrainData.size;
            var terrainVolume = terrainSize.x * terrainSize.y * terrainSize.z;
            return Mathf.Min(Mathf.FloorToInt(terrainVolume * toolSettings.GenerationDensity),
                toolSettings.GrassAmountToGenerate);
        }

        private (bool success, GrassData grassData) GenerateGrassDataForTerrain(Terrain terrain)
        {
            var terrainData = terrain.terrainData;
            var terrainSize = terrainData.size;
            var randomPoint = new Vector3(
                Random.Range(0, terrainSize.x),
                0,
                Random.Range(0, terrainSize.z)
            );

            var worldPoint = terrain.transform.TransformPoint(randomPoint);
            worldPoint.y = terrain.SampleHeight(worldPoint);

            if (Physics.CheckSphere(worldPoint, 0.1f, toolSettings.PaintBlockMask))
            {
                return (false, default);
            }

            var normal =
                terrain.terrainData.GetInterpolatedNormal(randomPoint.x / terrainSize.x, randomPoint.z / terrainSize.z);

            if (normal.y <= 1 + toolSettings.NormalLimit && normal.y >= 1 - toolSettings.NormalLimit)
            {
                var splatMapCoord = new Vector2(randomPoint.x / terrainSize.x, randomPoint.z / terrainSize.z);
                var splatmapData = terrainData.GetAlphamaps(
                    Mathf.FloorToInt(splatMapCoord.x * (terrainData.alphamapWidth - 1)),
                    Mathf.FloorToInt(splatMapCoord.y * (terrainData.alphamapHeight - 1)),
                    1, 1
                );

                float getFadeMap = 0;
                for (var i = 0; i < splatmapData.GetLength(2); i++)
                {
                    getFadeMap += Convert.ToInt32(toolSettings.LayerFading[i]) * splatmapData[0, 0, i];
                    if (splatmapData[0, 0, i] > toolSettings.LayerBlocking[i])
                    {
                        return (false, default);
                    }
                }

                var fade = Mathf.Clamp(getFadeMap, 0, 1f);
                var grassData = new GrassData
                {
                    position = worldPoint,
                    normal = normal,
                    color = GetRandomColor(),
                    widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight * fade)
                };

                return (true, grassData);
            }

            return (false, default);
        }

        private float CalculateWidthHeightFactor(Color vertexColor)
        {
            switch (toolSettings.VertexColorSettings)
            {
                case GrassToolSettingSo.VertexColorSetting.Red:
                    return 1 - vertexColor.r;
                case GrassToolSettingSo.VertexColorSetting.Green:
                    return 1 - vertexColor.g;
                case GrassToolSettingSo.VertexColorSetting.Blue:
                    return 1 - vertexColor.b;
                default:
                    return 1f;
            }
        }

        private async UniTask UpdateProgress(int currentPoint, int totalPoints, int updateInterval,
                                             string progressMessage)
        {
            if (currentPoint % updateInterval == 0)
            {
                _objectProgress.progress = (float)currentPoint / totalPoints;
                _objectProgress.progressMessage = progressMessage;
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }
        }

        private async UniTask AddGrassOnMesh(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();
            await GeneratePositions(selectedObjects);

            UpdateGrassData(false);
            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RegenerateGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();

            var removeObjects = selectedObjects.Select(GrassPainterHelper.CreateRemoveObject).Where(obj => obj != null)
                                               .ToArray();
            await RemoveGrassForMultipleObjectsAsync(removeObjects);

            await GeneratePositions(selectedObjects);

            UpdateGrassData(false);
            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask ClearCurrentGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();

            var removeObjects = selectedObjects.Select(GrassPainterHelper.CreateRemoveObject).Where(obj => obj != null)
                                               .ToArray();
            await RemoveGrassForMultipleObjectsAsync(removeObjects);

            UpdateGrassData(false);
            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void InitializeObjectProgresses()
        {
            _objectProgress = new ObjectProgress
            {
                progress = 0, progressMessage = "Initializing..."
            };
        }

        private async UniTask RemoveGrassForMultipleObjectsAsync(RemoveGrass[] removeGrasses)
        {
            var tasks = new UniTask<HashSet<Vector3>>[removeGrasses.Length];
            for (var i = 0; i < removeGrasses.Length; i++)
            {
                var index = i;
                tasks[i] = UniTask.RunOnThreadPool(() =>
                    GetPositionsToRemoveAsync(removeGrasses[index], index));
            }

            var results = await UniTask.WhenAll(tasks);

            var allPositionsToRemove = new HashSet<Vector3>();
            for (var i = 0; i < results.Length; i++)
            {
                var result = results[i];
                allPositionsToRemove.UnionWith(result);
            }

            await RemoveGrassAtPositionsAsync(allPositionsToRemove);
        }

        private HashSet<Vector3> GetPositionsToRemoveAsync(RemoveGrass removeGrass, int objIndex)
        {
            var worldBounds = removeGrass.GetBounds();
            if (worldBounds.size == Vector3.zero)
            {
                Debug.LogWarning($"Unable to determine bounds for object");
                return new HashSet<Vector3>();
            }

            var positionsToRemove = new HashSet<Vector3>();

            for (var i = 0; i < _grassData.Count; i++)
            {
                if (worldBounds.Contains(_grassData[i].position))
                {
                    positionsToRemove.Add(_grassData[i].position);
                }
            }

            Debug.Log($"Found {positionsToRemove.Count} grass instances to remove for object {objIndex}");
            return positionsToRemove;
        }

        private async UniTask RemoveGrassAtPositionsAsync(HashSet<Vector3> positionsToRemove)
        {
            const int batchSize = 10000;
            var totalBatches = (_grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask<List<GrassData>>[totalBatches];
            var removedCounts = new int[totalBatches];
            var totalRemoveGrassCount = positionsToRemove.Count;
            var removeUpdateInterval = GetUpdateInterval(totalBatches);

            // 각 배치별로 병렬 처리
            for (var index = 0; index < totalBatches; index++)
            {
                var start = index * batchSize;
                var end = Mathf.Min(start + batchSize, _grassData.Count);
                var localBatchIndex = index;

                tasks[index] = UniTask.RunOnThreadPool(() =>
                {
                    var localGrassToKeep = new List<GrassData>();
                    var localRemovedCount = 0;

                    for (var i = start; i < end; i++)
                    {
                        if (!positionsToRemove.Contains(_grassData[i].position))
                        {
                            localGrassToKeep.Add(_grassData[i]);
                        }
                        else
                        {
                            localRemovedCount++;
                        }
                    }

                    removedCounts[localBatchIndex] = localRemovedCount;
                    return localGrassToKeep;
                });

                await UpdateProgress(index, totalRemoveGrassCount, removeUpdateInterval,
                    $"Removing grass - {index}/{totalRemoveGrassCount}");
            }

            // Collect results from all batches
            var results = await UniTask.WhenAll(tasks);
            var finalGrassToKeep = new List<GrassData>();
            var updateInterval = GetUpdateInterval(results.Length);

            // Merge results
            for (var index = 0; index < results.Length; index++)
            {
                var result = results[index];
                finalGrassToKeep.AddRange(result);
                await UpdateProgress(index, results.Length, updateInterval,
                    $"Merging results - {index}/{results.Length}");
            }

            _grassData = finalGrassToKeep;
            InitSpatialGrid();
            Debug.Log($"Removed {totalRemoveGrassCount} grass instances in total");
        }

        private Vector3 GetRandomColor()
        {
            var baseColor = toolSettings.BrushColor;
            var newRandomCol = new Color(
                baseColor.r + Random.Range(0, toolSettings.RangeR),
                baseColor.g + Random.Range(0, toolSettings.RangeG),
                baseColor.b + Random.Range(0, toolSettings.RangeB),
                1
            );
            return new Vector3(newRandomCol.r, newRandomCol.g, newRandomCol.b);
        }

        private void ModifyColor()
        {
            Undo.RegisterCompleteObjectUndo(this, "Modified Color");

            for (var i = 0; i < _grassData.Count; i++)
            {
                var newData = _grassData[i];
                newData.color = GetRandomColor();
                _grassData[i] = newData;
            }

            RebuildMesh();
        }

        private void ModifyWidthAndHeight()
        {
            Undo.RegisterCompleteObjectUndo(this, "Modified Length/Width");

            for (var i = 0; i < _grassData.Count; i++)
            {
                var newData = _grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                _grassData[i] = newData;
            }

            RebuildMesh();
        }

        private void HandleScrollWheel(Event e)
        {
            toolSettings.BrushSize += e.delta.y;
            toolSettings.BrushSize = Mathf.Clamp(toolSettings.BrushSize, toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize);
            e.Use();
        }

        private void StartPainting()
        {
            _isPainting = true;

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    _grassAddPainter ??= new GrassAddPainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(this, "Added Grass");
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter ??= new GrassRemovePainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(this, "Removed Grass");
                    break;
                case BrushOption.Edit:
                    _grassEditPainter ??= new GrassEditPainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(this, "Edited Grass");
                    break;
                case BrushOption.Reproject:
                    _grassReprojectPainter ??= new GrassReprojectPainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(this, "Reprojected Grass");
                    break;
            }
        }

        private void ContinuePainting()
        {
            if (!_isPainting) return;

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    _grassAddPainter.AddGrass(_hitPos, toolSettings);
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter.RemoveGrass(_hitPos, toolSettings.BrushSize);
                    break;
                case BrushOption.Edit:
                    _grassEditPainter.EditGrass(_mousePointRay, toolSettings, _selectedEditOption);
                    break;
                case BrushOption.Reproject:
                    _grassReprojectPainter.ReprojectGrass(_mousePointRay, toolSettings.PaintMask.value,
                        toolSettings.BrushSize, toolSettings.ReprojectOffset);
                    break;
            }
        }

        private void EndPainting()
        {
            if (!_isPainting) return;

            _isPainting = false;
            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    _grassAddPainter.Clear();
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter.Clear();
                    break;
                case BrushOption.Edit:
                    _grassEditPainter.Clear();
                    break;
                case BrushOption.Reproject:
                    _grassReprojectPainter.Clear();
                    break;
            }
        }

        private void RebuildMesh()
        {
            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void InitSpatialGrid()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            _spatialGrid = new SpatialGrid(bounds, toolSettings.BrushSize * 2);

            _spatialGrid.Clear();

            for (var i = 0; i < _grassData.Count; i++)
            {
                var grass = _grassData[i];
                _spatialGrid.AddObject(grass.position, i);
            }

            // 유효성 검사
            Debug.Assert(_spatialGrid.TotalObjectCount == _grassData.Count,
                "SpatialGrid object count doesn't match grass data count!");
        }

        private void UpdateGrassData(bool fullReset = true)
        {
            _grassCompute.GrassDataList = _grassData;
            InitSpatialGrid();
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