using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            { "Paint/Edit", "Modify", "Generate", "General Settings" };

        private int _mainTabCurrent;
        private Vector2 _scrollPos;

        private bool _paintModeActive;
        private bool _enableGrass;
        private bool _showSpatialGrid;

        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reposition" };
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

        private Vector3 _cachedPos;

        private bool _showLayers;

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
        private GrassRepositionPainter _grassRepositionPainter;

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

            GrassPainterHelper.DrawHorizontalLine(Color.gray);
            DrawKeyBindingsUI();
            DrawEnableGrassToggle();
            DrawPaintModeToggle();
            GrassPainterHelper.DrawHorizontalLine(Color.gray);
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
                grassObject = FindAnyObjectByType<GrassComputeScript>()?.gameObject;
                if (grassObject != null)
                {
                    _grassCompute = grassObject.GetComponent<GrassComputeScript>();
                }
            }

            if (_spatialGrid == null)
            {
                InitSpatialGrid();
            }

            if (GUILayout.Button("Manual Update", GUILayout.Height(45)))
            {
                Init();
            }

            toolSettings =
                (GrassToolSettingSo)EditorGUILayout.ObjectField("Grass Tool Settings", toolSettings,
                    typeof(GrassToolSettingSo), false);

            grassObject =
                (GameObject)EditorGUILayout.ObjectField("Grass Compute Object", grassObject,
                    typeof(GameObject), true);

            _grassCompute.currentPresets =
                (GrassSettingSO)EditorGUILayout.ObjectField("Grass Settings Object",
                    _grassCompute.currentPresets, typeof(GrassSettingSO), false);

            if (_grassCompute.currentPresets == null)
            {
                EditorGUILayout.LabelField(
                    "No Grass Settings Set, create or assign one first. \n Create > Utility> Grass Settings",
                    GUILayout.Height(150));
                return;
            }

            _grassCompute.currentPresets.materialToUse =
                (Material)EditorGUILayout.ObjectField("Grass Material", _grassCompute.currentPresets.materialToUse,
                    typeof(Material), false);
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
                            "Yes", "No"))
                    {
                        CreateNewGrassObject();
                    }
                }

                EditorGUILayout.LabelField("No Grass System Holder found, create a new one", EditorStyles.label);
                return false;
            }

            return true;
        }

        private const float LabelWidth = 150f;
        private const float ToggleWidth = 20f;

        private void DrawEnableGrassToggle()
        {
            EditorGUILayout.BeginHorizontal();

            // 왼쪽 그룹
            EditorGUILayout.LabelField("Enable Grass", GUILayout.Width(LabelWidth));
            _enableGrass = EditorGUILayout.Toggle(_enableGrass, GUILayout.Width(ToggleWidth));

            if (_grassCompute == null)
                _grassCompute = grassObject.GetComponent<GrassComputeScript>();
            _grassCompute.enabled = _enableGrass;

            // 오른쪽 그룹 
            EditorGUILayout.LabelField("Auto Update", GUILayout.Width(LabelWidth));
            _grassCompute.autoUpdate = EditorGUILayout.Toggle(_grassCompute.autoUpdate, GUILayout.Width(ToggleWidth));

            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"))
            {
                tooltip = "Slow but Always Updated"
            };
            GUILayout.Label(helpIcon);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPaintModeToggle()
        {
            EditorGUILayout.BeginHorizontal();

            // 왼쪽 그룹
            EditorGUILayout.LabelField("Paint Mode", GUILayout.Width(LabelWidth));
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive, GUILayout.Width(ToggleWidth));

            // 오른쪽 그룹
            EditorGUILayout.LabelField("Show Spatial Grid", GUILayout.Width(LabelWidth));
            _showSpatialGrid = EditorGUILayout.Toggle(_showSpatialGrid, GUILayout.Width(ToggleWidth));

            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"))
            {
                tooltip = "Show grid cells used to optimize grass editing performance"
            };
            GUILayout.Label(helpIcon);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainToolbar()
        {
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
            }
        }

        private void DrawClearButtons()
        {
            if (GUILayout.Button("Clear current mesh"))
            {
                if (EditorUtility.DisplayDialog(
                        "Clear Grass", "Clear the grass on the selected object(s)?",
                        "Yes", "No"))
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
                        "Clear All Grass", "Remove all grass from the scene?",
                        "Yes", "No"))
                {
                    Undo.RegisterCompleteObjectUndo(this, "Cleared All Grass");
                    ClearAllGrass();
                }
            }
        }

        private void Init()
        {
            UpdateGrassData();
            _grassAddPainter = new GrassAddPainter(_grassCompute, _spatialGrid);
            _grassRemovePainter = new GrassRemovePainter(_grassCompute, _spatialGrid);
            _grassEditPainter = new GrassEditPainter(_grassCompute, _spatialGrid);
            _grassRepositionPainter = new GrassRepositionPainter(_grassCompute, _spatialGrid);
        }

        private void DrawKeyBindingsUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key Bindings", EditorStyles.boldLabel, GUILayout.Width(82));
            var tooltipIcon = new GUIContent(EditorGUIUtility.IconContent("_Help").image,
                "Hold Modifier key(s) + drag mouse button to paint grass");
            GUILayout.Label(tooltipIcon, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            toolSettings.grassModifierKey = (KeyBinding)EditorGUILayout.MaskField("Modifier Key(s)",
                (int)toolSettings.grassModifierKey, Enum.GetNames(typeof(KeyBinding)));
            toolSettings.grassMouseButton =
                (MouseButton)EditorGUILayout.EnumPopup("Mouse Button", toolSettings.grassMouseButton);
        }

        private void ShowPaintPanel()
        {
            EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
            LayerMask paintMask = EditorGUILayout.MaskField("Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintMask),
                InternalEditorUtility.layers);
            toolSettings.PaintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintMask);
            LayerMask paintBlockMask = EditorGUILayout.MaskField("Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintBlockMask),
                InternalEditorUtility.layers);
            toolSettings.PaintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintBlockMask);

            EditorGUILayout.Separator();

            _selectedToolOption =
                (BrushOption)GUILayout.Toolbar((int)_selectedToolOption, _toolbarStrings, GUILayout.Height(25));
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            toolSettings.BrushSize = EditorGUILayout.Slider("Brush Size", toolSettings.BrushSize,
                toolSettings.MinBrushSize, toolSettings.MaxBrushSize);
            EditorGUILayout.EndHorizontal();

            if (_selectedToolOption == BrushOption.Add)
            {
                toolSettings.NormalLimit = DrawSliderWithTooltip("Normal Limit", toolSettings.NormalLimit,
                    toolSettings.MinNormalLimit, toolSettings.MaxNormalLimit,
                    "Higher values allow painting on steeper surfaces");

                toolSettings.BrushHeight = DrawSliderWithTooltip("Brush Height", toolSettings.BrushHeight,
                    toolSettings.MinBrushHeight, toolSettings.MaxBrushHeight,
                    "The height from where grass painting begins checking for valid surfaces");

                toolSettings.Density = DrawIntSliderWithTooltip("Density", toolSettings.Density,
                    toolSettings.MinDensity, toolSettings.MaxDensity,
                    "Number of grass instances created per brush stroke");

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
                EditorGUILayout.LabelField("Brush Transition Speed", EditorStyles.boldLabel);
                toolSettings.BrushTransitionSpeed =
                    EditorGUILayout.Slider(toolSettings.BrushTransitionSpeed, 0f, 1f);
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
                            toolSettings.MinAdjust, toolSettings.MaxAdjust);
                    toolSettings.AdjustHeight =
                        EditorGUILayout.Slider("Grass Height Adjustment", toolSettings.AdjustHeight,
                            toolSettings.MinAdjust, toolSettings.MaxAdjust);

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

            if (_selectedToolOption == BrushOption.Reposition)
            {
                EditorGUILayout.Separator();
                toolSettings.NormalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.NormalLimit,
                    toolSettings.MinNormalLimit, toolSettings.MaxNormalLimit);

                toolSettings.RepositionOffset =
                    EditorGUILayout.FloatField("Reposition Y Offset", toolSettings.RepositionOffset);
            }

            EditorGUILayout.Separator();
        }

        private float DrawSliderWithTooltip(string label, float value, float minValue, float maxValue,
                                            string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            var newValue = EditorGUILayout.Slider(label, value, minValue, maxValue);
            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"))
            {
                tooltip = tooltip
            };
            GUILayout.Label(helpIcon, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
            return newValue;
        }

        private int DrawIntSliderWithTooltip(string label, int value, int minValue, int maxValue, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            var newValue = EditorGUILayout.IntSlider(label, value, minValue, maxValue);
            var helpIcon = new GUIContent(EditorGUIUtility.IconContent("_Help"))
            {
                tooltip = tooltip
            };
            GUILayout.Label(helpIcon, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
            return newValue;
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
                curPresets.MinHeightLimit, curPresets.MaxHeightLimit);

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

        private void CreateNewGrassObject()
        {
            grassObject = new GameObject
            {
                name = "Grass System - Holder"
            };
            _grassCompute = grassObject.AddComponent<GrassComputeScript>();
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

            Color discColor2;
            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    discColor2 = new Color(0, 0.5f, 0, 0.4f);
                    DrawBrushHeightHandles(_hitPos);
                    break;
                case BrushOption.Remove:
                    discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                    break;
                case BrushOption.Edit:
                    discColor2 = new Color(0.5f, 0.5f, 0f, 0.4f);
                    break;
                case BrushOption.Reposition:
                    discColor2 = new Color(0, 0.5f, 0.5f, 0.4f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Handles.color = discColor2;
            Handles.DrawSolidDisc(_hitPos, _hitNormal, toolSettings.BrushSize);

            if (_hitPos != _cachedPos)
            {
                SceneView.RepaintAll();
                _cachedPos = _hitPos;
            }
        }

        private void DrawBrushHeightHandles(Vector3 basePosition)
        {
            var height = toolSettings.BrushHeight;
            var radius = toolSettings.BrushSize;

            Handles.color = Color.yellow;

            // Always use Vector3.up instead of normal for the height direction
            var topCenter = basePosition + Vector3.up * height;

            // Draw top and bottom circles using Vector3.up as normal
            Handles.DrawWireDisc(topCenter, Vector3.up, radius);
            Handles.DrawWireDisc(basePosition, Vector3.up, radius);

            // Use fixed directions for the connecting lines
            var forward = Vector3.forward;
            var right = Vector3.right;

            // Calculate four points on bottom circle
            var bottomPoints = new[]
            {
                basePosition + forward * radius,
                basePosition - forward * radius,
                basePosition + right * radius,
                basePosition - right * radius
            };

            // Calculate corresponding points on top circle
            var topPoints = new[]
            {
                topCenter + forward * radius,
                topCenter - forward * radius,
                topCenter + right * radius,
                topCenter - right * radius
            };

            // Draw vertical lines connecting top and bottom circles
            for (int i = 0; i < 4; i++)
            {
                Handles.DrawLine(bottomPoints[i], topPoints[i]);
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

            var areModifierKeysPressed = GrassPainterHelper.AreModifierKeysPressed(toolSettings.grassModifierKey);
            var isCorrectMouseButton = GrassPainterHelper.IsMouseButtonPressed(toolSettings.grassMouseButton);

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

        private void ClearAllGrass()
        {
            Undo.RegisterCompleteObjectUndo(this, "Cleared Grass");

            // Clear the spatial grid
            _spatialGrid?.Clear();
            _grassCompute.ClearAllData();
            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Cleared all grass from the scene");
        }

        private async UniTask GeneratePositions(GameObject[] selections)
        {
            var totalPoints = CalculateTotalPoints(selections);
            var currentPoint = 0;
            var newGrassData = CollectionsPool.GetList<GrassData>((int)(toolSettings.BrushSize + 1));

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

            var grassData = _grassCompute.GrassDataList;
            for (var i = 0; i < newGrassData.Count; i++)
            {
                grassData.Add(newGrassData[i]);
            }

            _grassCompute.Reset();
            CollectionsPool.ReturnList(newGrassData);
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
            for (var j = 0; j < numPoints; j++)
            {
                await UpdateProgress(startPoint + j, totalPoints,
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
            return Mathf.Max(Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity),
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
            for (var j = 0; j < numPoints; j++)
            {
                await UpdateProgress(startPoint + j, totalPoints,
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
            return Mathf.Max(Mathf.FloorToInt(terrainVolume * toolSettings.GenerationDensity),
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

        private const int UpdateInterval = 10000;
        private float _lastProgressUpdate;

        private async UniTask UpdateProgress(int currentPoint, int totalPoints, string progressMessage)
        {
            var currentProgress = (float)currentPoint / totalPoints;

            // 진행도가 1% 이상 변했을 때만 UI 업데이트
            if (currentProgress - _lastProgressUpdate >= 0.01f || currentPoint % UpdateInterval == 0)
            {
                _objectProgress.progress = currentProgress;
                _objectProgress.progressMessage = progressMessage;
                _lastProgressUpdate = currentProgress;
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }
        }

        private async UniTask AddGrassOnMesh(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();
            await GeneratePositions(selectedObjects);

            UpdateGrassData();
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

            UpdateGrassData();
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

            UpdateGrassData();
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
            var grassData = _grassCompute.GrassDataList;

            for (var i = 0; i < grassData.Count; i++)
            {
                if (worldBounds.Contains(grassData[i].position))
                {
                    positionsToRemove.Add(grassData[i].position);
                }
            }

            Debug.Log($"Found {positionsToRemove.Count} grass instances to remove for object {objIndex}");
            return positionsToRemove;
        }

        private async UniTask RemoveGrassAtPositionsAsync(HashSet<Vector3> positionsToRemove)
        {
            const int batchSize = 10000;
            var grassData = _grassCompute.GrassDataList;
            var totalBatches = (grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask<List<GrassData>>[totalBatches];
            var removedCounts = new int[totalBatches];
            var totalRemoveGrassCount = positionsToRemove.Count;

            // 각 배치별로 병렬 처리
            for (var index = 0; index < totalBatches; index++)
            {
                var start = index * batchSize;
                var end = Mathf.Min(start + batchSize, grassData.Count);
                var localBatchIndex = index;

                tasks[index] = UniTask.RunOnThreadPool(() =>
                {
                    var localGrassToKeep = new List<GrassData>();
                    var localRemovedCount = 0;

                    for (var i = start; i < end; i++)
                    {
                        if (!positionsToRemove.Contains(grassData[i].position))
                        {
                            localGrassToKeep.Add(grassData[i]);
                        }
                        else
                        {
                            localRemovedCount++;
                        }
                    }

                    removedCounts[localBatchIndex] = localRemovedCount;
                    return localGrassToKeep;
                });

                // await UpdateProgress(index, totalRemoveGrassCount,
                //     $"Removing grass - {index}/{totalRemoveGrassCount}");
            }

            // Collect results from all batches
            var results = await UniTask.WhenAll(tasks);
            var finalGrassToKeep = new List<GrassData>();
            var totalRemoved = 0;
            // 처리된 결과 합치기 및 제거된 잔디 수 업데이트
            for (var index = 0; index < results.Length; index++)
            {
                var result = results[index];
                finalGrassToKeep.AddRange(result);
                totalRemoved += removedCounts[index];
        
                await UpdateProgress(index, results.Length,
                    $"Removed grass: {totalRemoved:N0} / {totalRemoveGrassCount:N0}");
            }

            _grassCompute.GrassDataList = finalGrassToKeep;
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
            // 현재 상태를 Undo 시스템에 등록
            Undo.RegisterCompleteObjectUndo(_grassCompute, "Modified Color");

            // 기존 데이터의 깊은 복사본 생성 
            var grassData = new List<GrassData>(_grassCompute.GrassDataList);

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.color = GetRandomColor();
                grassData[i] = newData;
            }

            // 수정된 데이터를 _grassCompute에 할당
            _grassCompute.GrassDataList = grassData;
            // 변경사항 적용
            RebuildMesh();
            // Scene을 더티 상태로 표시
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void ModifyWidthAndHeight()
        {
            // 현재 상태를 Undo 시스템에 등록
            Undo.RegisterCompleteObjectUndo(_grassCompute, "Modified Length/Width");

            // 기존 데이터의 깊은 복사본 생성
            var grassData = new List<GrassData>(_grassCompute.GrassDataList);

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                grassData[i] = newData;
            }

            // 수정된 데이터를 _grassCompute에 할당
            _grassCompute.GrassDataList = grassData;

            // 변경사항 적용
            RebuildMesh();
            // Scene을 더티 상태로 표시
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
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
                    Undo.RegisterCompleteObjectUndo(_grassCompute, "Add Grass");
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter ??= new GrassRemovePainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(_grassCompute, "Remove Grass");
                    break;
                case BrushOption.Edit:
                    _grassEditPainter ??= new GrassEditPainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(_grassCompute, "Edit Grass");
                    break;
                case BrushOption.Reposition:
                    _grassRepositionPainter ??= new GrassRepositionPainter(_grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(_grassCompute, "Reposition Grass");
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
                    _grassEditPainter.EditGrass(_hitPos, toolSettings, _selectedEditOption);
                    break;
                case BrushOption.Reposition:
                    _grassRepositionPainter.RepositionGrass(_hitPos, toolSettings);
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
                case BrushOption.Reposition:
                    _grassRepositionPainter.Clear();
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
            _spatialGrid = new SpatialGrid(bounds, toolSettings.BrushSize);
            var grassData = _grassCompute.GrassDataList;

            _spatialGrid.Clear();

            for (var i = 0; i < grassData.Count; i++)
            {
                var grass = grassData[i];
                _spatialGrid.AddObject(grass.position, i);
            }
        }

        private void UpdateGrassData()
        {
            InitSpatialGrid();
            _grassCompute.Reset();
        }
    }
}