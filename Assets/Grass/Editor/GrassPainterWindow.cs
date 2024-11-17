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
        private readonly string[] _generateTabStrings = { "Basic", "Terrain Layers", "Advanced" };
        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSo toolSettings;

        // options
        private BrushOption _selectedToolOption;
        private EditOption _selectedEditOption;
        private ModifyOption _selectedModifyOption;
        private GenerateTab _selectedGenerateOption;

        private Ray _mousePointRay;

        private Vector3 _mousePos;

        [SerializeField] private GameObject grassObject;

        private GrassComputeScript _grassCompute;

        private Vector3 _cachedPos;
        
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

            // grassObject가 없으면 여기서 종료
            if (grassObject == null)
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

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(toolSettings);
            EditorUtility.SetDirty(_grassCompute.GrassSetting);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;

            // 기존 grassObject가 있는지 확인
            if (grassObject == null)
            {
                grassObject = FindAnyObjectByType<GrassComputeScript>()?.gameObject;
                if (grassObject != null)
                {
                    _grassCompute = grassObject.GetComponent<GrassComputeScript>();
                }
            }

            // toolSettings가 없다면 로드 시도
            if (toolSettings == null)
            {
                toolSettings = AssetDatabase.LoadAssetAtPath<GrassToolSettingSo>(
                    "Assets/Grass/Settings/Grass Tool Settings.asset"
                );
            }
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
            // grassObject 검사를 가장 먼저 수행
            if (grassObject == null)
            {
                // grassObject를 씬에서 찾아보기
                grassObject = FindAnyObjectByType<GrassComputeScript>()?.gameObject;

                // 찾지 못했다면 _grassCompute도 null일 것이므로 여기서 리턴
                if (grassObject == null)
                {
                    if (GUILayout.Button("Create Grass Object", GUILayout.Height(45)))
                    {
                        CreateNewGrassObject();
                    }

                    EditorGUILayout.HelpBox("No Grass System Holder found, create a new one", MessageType.Info);
                    return;
                }

                // grassObject를 찾았다면 _grassCompute 설정
                _grassCompute = grassObject.GetComponent<GrassComputeScript>();
            }

            // _grassCompute가 없다면 여기서도 리턴
            if (_grassCompute == null)
            {
                _grassCompute = grassObject.GetComponent<GrassComputeScript>();
                if (_grassCompute == null)
                {
                    EditorGUILayout.HelpBox("GrassComputeScript component is missing", MessageType.Error);
                    return;
                }
            }

            // SpatialGrid 초기화 검사
            if (_spatialGrid == null)
            {
                InitSpatialGrid();
            }

            // 나머지 UI 요소들은 모든 컴포넌트가 유효할 때만 그리기
            if (GUILayout.Button("Manual Update", GUILayout.Height(45)))
            {
                Init();
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

            if (_grassCompute.GrassSetting == null)
            {
                _grassCompute.SetGrassSetting(CreateOrGetGrassSetting());
            }

            _grassCompute.SetGrassSetting((GrassSettingSO)EditorGUILayout.ObjectField(
                "Grass Settings Object",
                _grassCompute.GrassSetting,
                typeof(GrassSettingSO),
                false
            ));

            _grassCompute.GrassSetting.materialToUse = (Material)EditorGUILayout.ObjectField(
                "Grass Material",
                _grassCompute.GrassSetting.materialToUse,
                typeof(Material),
                false
            );
        }

        private GrassSettingSO CreateOrGetGrassSetting()
        {
            // 먼저 에셋을 찾아봅니다
            var grassSetting =
                AssetDatabase.LoadAssetAtPath<GrassSettingSO>("Assets/Grass/Settings/Grass Settings.asset");

            // 에셋이 없다면 새로 생성합니다
            if (grassSetting == null)
            {
                // Settings 폴더가 없다면 생성
                if (!AssetDatabase.IsValidFolder("Assets/Grass"))
                {
                    AssetDatabase.CreateFolder("Assets", "Grass");
                }

                if (!AssetDatabase.IsValidFolder("Assets/Grass/Settings"))
                {
                    AssetDatabase.CreateFolder("Assets/Grass", "Settings");
                }

                // 새로운 GrassSettingSO 생성
                grassSetting = CreateInstance<GrassSettingSO>();
                AssetDatabase.CreateAsset(grassSetting, "Assets/Grass/Settings/Grass Settings.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("Created new Grass Settings asset");
            }

            return grassSetting;
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
            _grassCompute.autoUpdate =
                GrassPainterHelper.ToggleWithLabel("Auto Update", "Slow but Always Updated", _grassCompute.autoUpdate);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPaintModeToggle()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Paint Mode", GUILayout.Width(LabelWidth));
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive, GUILayout.Width(ToggleWidth));

            _showSpatialGrid = GrassPainterHelper.ToggleWithLabel("Show Spatial Grid",
                "Show grid cells used to optimize grass editing performance", _showSpatialGrid);

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
            var buttonName = toolSettings.grassMouseButton switch
            {
                MouseButton.LeftMouse => "Left",
                MouseButton.RightMouse => "Right",
                MouseButton.MiddleMouse => "Middle",
                _ => "Unknown"
            };

            var style = new GUIStyle(EditorStyles.label) { richText = true };
            EditorGUILayout.LabelField($"Hold and drag with <b>{buttonName}</b> mouse button to paint grass",
                style);

            toolSettings.grassMouseButton =
                (MouseButton)EditorGUILayout.EnumPopup("Mouse Button", toolSettings.grassMouseButton);
        }

        private void ShowPaintPanel()
        {
            DrawHitSettings();
            DrawBrushToolbar();
            DrawCommonBrushSettings();

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    DrawAddBrushSettings();
                    break;
                case BrushOption.Remove:
                    break;
                case BrushOption.Edit:
                    DrawEditBrushSettings();
                    break;
                case BrushOption.Reposition:
                    DrawRepositionMessage();
                    break;
            }

            EditorGUILayout.Separator();
        }

        private void DrawHitSettings()
        {
            EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);

            LayerMask paintMask = EditorGUILayout.MaskField(
                "Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintMask),
                InternalEditorUtility.layers
            );
            toolSettings.PaintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintMask);

            LayerMask paintBlockMask = EditorGUILayout.MaskField(
                "Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintBlockMask),
                InternalEditorUtility.layers
            );
            toolSettings.PaintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintBlockMask);
        }

        private void DrawBrushToolbar()
        {
            EditorGUILayout.Separator();
            _selectedToolOption = (BrushOption)GUILayout.Toolbar(
                (int)_selectedToolOption,
                _toolbarStrings,
                GUILayout.Height(25)
            );
            EditorGUILayout.Separator();
        }

        private void DrawCommonBrushSettings()
        {
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField("Hold Shift + Scroll to adjust Brush size", style);

            toolSettings.BrushSize = EditorGUILayout.Slider(
                "Brush Size",
                toolSettings.BrushSize,
                toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize
            );
            
            EditorGUILayout.BeginHorizontal();
            toolSettings.NormalLimit = GrassPainterHelper.FloatSlider(
                "Normal Limit",
                "Maximum slope angle where grass can be placed. 0 = flat ground only, 1 = all slopes",
                toolSettings.NormalLimit,
                toolSettings.MinNormalLimit,
                toolSettings.MaxNormalLimit
            );
            GrassPainterHelper.ShowAngle(toolSettings.NormalLimit);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddBrushSettings()
        {
            toolSettings.Density = GrassPainterHelper.IntSlider(
                "Density",
                "Amount of grass placed in one brush stroke",
                toolSettings.Density,
                toolSettings.MinDensity,
                toolSettings.MaxDensity
            );

            DrawGrassWidthHeightSettings();
            DrawGrassColorSettings();
        }

        private void DrawGrassWidthHeightSettings()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Width and Height", EditorStyles.boldLabel);

            toolSettings.GrassWidth = GrassPainterHelper.FloatSlider(
                "Grass Width",
                "Set grass width",
                toolSettings.GrassWidth,
                toolSettings.MinSizeWidth,
                toolSettings.MaxSizeWidth
            );

            toolSettings.GrassHeight = GrassPainterHelper.FloatSlider(
                "Grass Height",
                "Set grass height",
                toolSettings.GrassHeight,
                toolSettings.MinSizeHeight,
                toolSettings.MaxSizeHeight
            );

            CheckGrassHeightWarning();
        }

        private void CheckGrassHeightWarning()
        {
            if (toolSettings.GrassHeight > _grassCompute.GrassSetting.maxHeight)
            {
                EditorGUILayout.HelpBox(
                    "Grass Height must be less than Blade Height Max in the General Settings",
                    MessageType.Warning,
                    true
                );
            }
        }

        private void DrawGrassColorSettings()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            DrawColorVariationSliders();
        }

        private void DrawColorVariationSliders()
        {
            toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
            toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
            toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
        }

        private void DrawEditBrushSettings()
        {
            _selectedEditOption = (EditOption)GUILayout.Toolbar((int)_selectedEditOption, _editOptionStrings);
            EditorGUILayout.Separator();

            toolSettings.BrushTransitionSpeed = EditorGUILayout.Slider(
                "Brush Transition Speed",
                toolSettings.BrushTransitionSpeed,
                0f,
                1f
            );

            switch (_selectedEditOption)
            {
                case EditOption.EditColors:
                    DrawGrassColorSettings();
                    break;
                case EditOption.EditWidthHeight:
                    DrawGrassAdjustmentSettings();
                    break;
                case EditOption.Both:
                    DrawGrassAdjustmentSettings();
                    DrawGrassColorSettings();
                    break;
            }
        }

        private void DrawGrassAdjustmentSettings()
        {
            EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);

            toolSettings.AdjustWidth = EditorGUILayout.Slider(
                "Grass Width Adjustment",
                toolSettings.AdjustWidth,
                toolSettings.MinAdjust,
                toolSettings.MaxAdjust
            );

            toolSettings.AdjustHeight = EditorGUILayout.Slider(
                "Grass Height Adjustment",
                toolSettings.AdjustHeight,
                toolSettings.MinAdjust,
                toolSettings.MaxAdjust
            );

            toolSettings.AdjustWidthMax = EditorGUILayout.Slider(
                "Max Width Adjustment",
                toolSettings.AdjustWidthMax,
                0.01f,
                3f
            );

            toolSettings.AdjustHeightMax = EditorGUILayout.Slider(
                "Max Height Adjustment",
                toolSettings.AdjustHeightMax,
                0.01f,
                3f
            );
        }

        private void DrawRepositionMessage()
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 10, 10)
            };

            EditorGUILayout.LabelField(
                "Increase brush size if grass is not being repositioned",
                style
            );
        }

        private void ShowModifyPanel()
        {
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
            _selectedGenerateOption = (GenerateTab)GUILayout.Toolbar((int)_selectedGenerateOption, _generateTabStrings,
                GUILayout.Height(25));
            EditorGUILayout.Separator();

            DrawGeneralSettings();

            switch (_selectedGenerateOption)
            {
                case GenerateTab.Basic:
                    DrawMaskSettings();
                    DrawGrassAppearanceSettings();
                    break;

                case GenerateTab.TerrainLayers:
                    DrawTerrainLayerSettings();
                    break;

                case GenerateTab.Advanced:
                    // Reserved for future use
                    EditorGUILayout.HelpBox("Advanced features coming soon!", MessageType.Info);
                    break;
            }

            EditorGUILayout.Separator();
            DrawObjectOperations();
            DrawRemoveAllGrassButton();
        }

        private void DrawGeneralSettings()
        {
            toolSettings.GrassAmountToGenerate = EditorGUILayout.IntSlider(
                "Grass Place Max Amount",
                toolSettings.GrassAmountToGenerate,
                toolSettings.MinGrassAmountToGenerate,
                toolSettings.MaxGrassAmountToGenerate
            );

            toolSettings.GenerationDensity = EditorGUILayout.Slider(
                "Grass Place Density",
                toolSettings.GenerationDensity,
                toolSettings.MinGenerationDensity,
                toolSettings.MaxGenerationDensity
            );

            EditorGUILayout.BeginHorizontal();
            toolSettings.NormalLimit = GrassPainterHelper.FloatSlider(
                "Normal Limit",
                "Maximum slope angle where grass can be placed. 0 = flat ground only, 1 = all slopes",
                toolSettings.NormalLimit,
                toolSettings.MinNormalLimit,
                toolSettings.MaxNormalLimit
            );
            GrassPainterHelper.ShowAngle(toolSettings.NormalLimit);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaskSettings()
        {
            LayerMask paintingMask = EditorGUILayout.MaskField(
                "Painting Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintMask),
                InternalEditorUtility.layers
            );
            toolSettings.PaintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(paintingMask);

            LayerMask blockingMask = EditorGUILayout.MaskField(
                "Blocking Mask",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.PaintBlockMask),
                InternalEditorUtility.layers
            );
            toolSettings.PaintBlockMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(blockingMask);

            toolSettings.VertexColorSettings = (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup(
                "Block On vertex Colors",
                toolSettings.VertexColorSettings
            );
            toolSettings.VertexFade = (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup(
                "Fade on Vertex Colors",
                toolSettings.VertexFade
            );
        }

        private void DrawGrassAppearanceSettings()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.GrassWidth = EditorGUILayout.Slider(
                "Grass Width",
                toolSettings.GrassWidth,
                toolSettings.MinSizeWidth,
                toolSettings.MaxSizeWidth
            );
            toolSettings.GrassHeight = EditorGUILayout.Slider(
                "Grass Height",
                toolSettings.GrassHeight,
                toolSettings.MinSizeHeight,
                toolSettings.MaxSizeHeight
            );

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);

            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
            toolSettings.RangeR = EditorGUILayout.Slider("Red", toolSettings.RangeR, 0f, 1f);
            toolSettings.RangeG = EditorGUILayout.Slider("Green", toolSettings.RangeG, 0f, 1f);
            toolSettings.RangeB = EditorGUILayout.Slider("Blue", toolSettings.RangeB, 0f, 1f);
        }

        private void DrawTerrainLayerHeader()
        {
            EditorGUILayout.HelpBox(
                "Layer Settings Guide:\n\n" +
                "• Grass Density:\n" +
                "  1 = Full grass growth\n" +
                "  0 = No grass\n\n" +
                "• Height Toggle:\n" +
                "  ON = Variable grass height\n" +
                "  OFF = Fixed grass height",
                MessageType.Info
            );
        }

        private void DrawTerrainLayerSettings()
        {
            DrawTerrainLayerHeader();

            var terrain = FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                EditorGUILayout.HelpBox(
                    "No terrain found in scene.\n" +
                    "1. Create a terrain (3D Object > Terrain)\n" +
                    "2. Add terrain layers in Paint Terrain tab",
                    MessageType.Warning
                );
                return;
            }

            EditorGUILayout.Space(10);

            // Column headers with tooltips
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Layer",
                    "Terrain layer name"),
                GUILayout.Width(150));
            EditorGUILayout.LabelField(new GUIContent("Grass Density",
                    "Controls how much grass grows in this layer (0: Full grass, 1: No grass)"),
                GUILayout.Width(200));
            EditorGUILayout.LabelField(new GUIContent("Vary Height",
                    "Adjust grass height based on layer strength"),
                GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            GrassPainterHelper.DrawHorizontalLine(Color.gray, 1, 2);

            var terrainLayers = terrain.terrainData.terrainLayers;
            for (var i = 0; i < terrainLayers.Length; i++)
            {
                DrawTerrainLayerRow(i, terrainLayers[i]);
            }
        }

        private void DrawTerrainLayerRow(int index, TerrainLayer layer)
        {
            EditorGUILayout.BeginHorizontal();

            // Layer name
            var layerName = layer != null ? layer.name : $"Layer {index}";
            EditorGUILayout.LabelField(layerName, GUILayout.Width(150));

            // Density slider
            var sliderContent = new GUIContent("",
                $"Grass density control for {layerName}\n" +
                "0: Full grass growth\n" +
                "1: No grass growth");
            toolSettings.LayerBlocking[index] = EditorGUILayout.Slider(
                sliderContent,
                toolSettings.LayerBlocking[index],
                0f,
                1f,
                GUILayout.Width(200)
            );

            // Height variation toggle
            var toggleContent = new GUIContent("",
                $"Enable to vary grass height based on {layerName} strength");
            toolSettings.LayerFading[index] = EditorGUILayout.Toggle(
                toggleContent,
                toolSettings.LayerFading[index],
                GUILayout.Width(30)
            );

            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectOperations()
        {
            EditorGUILayout.LabelField("Selected Object Operations", EditorStyles.boldLabel);

            if (DrawObjectOperationButton("Selected Objects: Add Grass", "Add Grass",
                    "Add grass to selected object(s)?"))
            {
                AddGrassOnMesh(Selection.gameObjects).Forget();
            }

            if (DrawObjectOperationButton("Selected Objects: Regenerate Grass", "Regenerate Grass",
                    "Remove existing grass and regenerate on selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(this, "Regenerate Grass");
                RegenerateGrass(Selection.gameObjects).Forget();
            }

            if (DrawObjectOperationButton("Selected Objects: Remove Grass", "Remove Grass",
                    "Remove the grass on the selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(this, "Remove Grass");
                RemoveCurrentGrass(Selection.gameObjects).Forget();
            }
        }

        private bool DrawObjectOperationButton(string buttonText, string dialogTitle, string dialogMessage)
        {
            if (!GUILayout.Button(buttonText)) return false;

            if (!EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No")) return false;

            if (Selection.gameObjects is { Length: > 0 }) return true;

            Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
            return false;
        }

        private void DrawRemoveAllGrassButton()
        {
            EditorGUILayout.Separator();
            GrassPainterHelper.DrawHorizontalLine(Color.gray);
            EditorGUILayout.Space(5);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 9, 9)
            };

            if (!GUILayout.Button("Remove All Grass", buttonStyle, GUILayout.Height(30))) return;

            if (!EditorUtility.DisplayDialog("Remove All Grass", "Remove all grass from the scene?", "Yes", "No"))
                return;

            Undo.RegisterCompleteObjectUndo(this, "Remove All Grass");
            RemoveAllGrass();
        }

        private void ShowMainSettingsPanel()
        {
            EditorGUILayout.LabelField("Blade Min/Max Settings", EditorStyles.boldLabel);
            var curPresets = _grassCompute.GrassSetting;

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

            EditorGUILayout.MinMaxSlider("Random Height Min/Max", ref curPresets.randomHeightMin,
                ref curPresets.randomHeightMax, 0, 1);

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
            _grassCompute.SetGrassSetting(CreateOrGetGrassSetting());

            InitSpatialGrid();

            EditorUtility.SetDirty(grassObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hasFocus)
            {
                if (_paintModeActive)
                {
                    if (_showSpatialGrid)
                    {
                        DrawGridHandles();
                    }
                }
            }
        }

        private void DrawGridHandles()
        {
            if (_spatialGrid == null) return;
            var cellSize = _spatialGrid.CellSize;

            if (!Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            var hitCell = _spatialGrid.WorldToCell(hit.point);
            var cellRadius = Mathf.CeilToInt(toolSettings.BrushSize / cellSize);

            // Debug point to verify ray hit
            Handles.color = Color.yellow;
            Handles.SphereHandleCap(0, hit.point, Quaternion.identity, 0.3f, EventType.Repaint);

            var notActiveCellColor = new Color(1f, 0f, 0f, 0.3f);
            var activeCellColor = new Color(0f, 1f, 0f, 1f);

            // 브러시 범위 내 모든 셀 순회
            for (var x = -cellRadius; x <= cellRadius; x++)
            for (var y = -cellRadius; y <= cellRadius; y++)
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                // 원형 브러시 범위 체크
                if (x * x + y * y + z * z > cellRadius * cellRadius)
                    continue;

                // Y축은 히트 포인트 기준으로 고정
                var checkCell = new Vector3Int(hitCell.x + x, hitCell.y + y, hitCell.z + z);
                var cellWorldPos = _spatialGrid.CellToWorld(checkCell);
                var cellCenter = cellWorldPos + Vector3.one * (cellSize * 0.5f);

                // 실제 브러시 범위 내에 있는지 체크
                if (Vector3.Distance(cellCenter, hit.point) <= toolSettings.BrushSize)
                {
                    var key = SpatialGrid.GetKey(checkCell.x, checkCell.y, checkCell.z);
                    var hasGrass = _spatialGrid.HasAnyObject(key);

                    Handles.color = hasGrass ? activeCellColor : notActiveCellColor;
                    DrawCellWireframe(cellCenter, cellSize);
                }
            }
        }

        private void DrawCellWireframe(Vector3 center, float size)
        {
            var halfSize = size * 0.5f;
            var points = new[]
            {
                center + new Vector3(-halfSize, -halfSize, -halfSize), // 0 bottom
                center + new Vector3(halfSize, -halfSize, -halfSize), // 1
                center + new Vector3(halfSize, -halfSize, halfSize), // 2
                center + new Vector3(-halfSize, -halfSize, halfSize), // 3
                center + new Vector3(-halfSize, halfSize, -halfSize), // 4 top
                center + new Vector3(halfSize, halfSize, -halfSize), // 5
                center + new Vector3(halfSize, halfSize, halfSize), // 6
                center + new Vector3(-halfSize, halfSize, halfSize) // 7
            };

            // Draw bottom square
            Handles.DrawLine(points[0], points[1]);
            Handles.DrawLine(points[1], points[2]);
            Handles.DrawLine(points[2], points[3]);
            Handles.DrawLine(points[3], points[0]);

            // Draw top square
            Handles.DrawLine(points[4], points[5]);
            Handles.DrawLine(points[5], points[6]);
            Handles.DrawLine(points[6], points[7]);
            Handles.DrawLine(points[7], points[4]);

            // Draw vertical lines
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

            _mousePointRay = scene.camera.ScreenPointToRay(_mousePos);

            // 현재 이벤트에서 modifier 키가 눌려있는지 체크
            var hasModifier = e.alt || e.control || e.command;

            // Shift + 스크롤은 브러시 크기 조절에 사용
            if (e.type == EventType.ScrollWheel && e.shift)
            {
                HandleScrollWheel(e);
                e.Use();
                return;
            }

            var isCorrectMouseButton = GrassPainterHelper.IsMouseButtonPressed(toolSettings.grassMouseButton);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (isCorrectMouseButton && !hasModifier) // modifier 없을 때만 페인팅 시작
                    {
                        _isMousePressed = true;
                        StartPainting();
                        e.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (_isMousePressed && isCorrectMouseButton && !hasModifier)
                    {
                        ContinuePainting();
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (_isMousePressed && isCorrectMouseButton)
                    {
                        _isMousePressed = false;
                        EndPainting();
                        e.Use();
                    }

                    break;
            }

            // 브러시 미리보기
            if (!hasModifier &&
                Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
            {
                _hitPos = hit.point;
                _hitNormal = hit.normal;
                DrawGrassBrush(_hitPos, _hitNormal, toolSettings.BrushSize);
            }
        }

        private void DrawGrassBrush(Vector3 center, Vector3 normal, float radius)
        {
            const float alpha = 0.3f;
            var color = _selectedToolOption switch
            {
                BrushOption.Add => new Color(0f, 0.5f, 0f, alpha), // 연한 초록색
                BrushOption.Remove => new Color(0.5f, 0f, 0f, alpha), // 연한 빨간색
                BrushOption.Edit => new Color(0.5f, 0.5f, 0f, alpha), // 연한 노란색
                BrushOption.Reposition => new Color(0f, 0.5f, 0.5f, alpha), // 연한 청록색
                _ => new Color(0.9f, 0.9f, 0.9f, alpha) // 연한 흰색
            };

            Handles.color = color;
            if (_selectedToolOption == BrushOption.Reposition)
            {
                // Reposition일 때 원기둥 그리기
                DrawTransparentCylinder(center, radius); // 높이를 1로 설정
            }
            else
            {
                Handles.DrawSolidDisc(center + normal * 0.01f, normal, radius);
            }
        }

        private void DrawTransparentCylinder(Vector3 center, float radius)
        {
            var topCenter = center + new Vector3(0, toolSettings.BrushSize);
            var bottomCenter = center;

            // 그리기 전에 현재 색상을 백업
            var originalColor = Handles.color;

            // 반투명 색상 적용
            Handles.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a);

            // 원기둥의 상단과 하단 그리기
            Handles.DrawSolidDisc(topCenter, Vector3.up, radius);
            Handles.DrawSolidDisc(bottomCenter, Vector3.up, radius);

            int segmentCount = 24;

            // 측면 그리기
            for (int i = 0; i < segmentCount; i++)
            {
                // 현재 세그먼트의 각도 계산
                float angleA = (360f / segmentCount) * i;
                float angleB = (360f / segmentCount) * (i + 1);

                // 각도에 따른 두 점 계산 (하단)
                Vector3 pointA = bottomCenter + Quaternion.Euler(0, angleA, 0) * Vector3.right * radius;
                Vector3 pointB = bottomCenter + Quaternion.Euler(0, angleB, 0) * Vector3.right * radius;

                // 각도에 따른 두 점 계산 (상단)
                Vector3 pointC = topCenter + Quaternion.Euler(0, angleA, 0) * Vector3.right * radius;
                Vector3 pointD = topCenter + Quaternion.Euler(0, angleB, 0) * Vector3.right * radius;

                // 측면을 반투명하게 그리기
                Handles.DrawAAConvexPolygon(pointA, pointB, pointD, pointC);
            }

            // 색상 복원
            Handles.color = originalColor;
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

        private void RemoveAllGrass()
        {
            _spatialGrid?.Clear();
            _grassCompute.ClearAllData();
            Init();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Removed all grass from the scene");
        }

        private async UniTask GenerateGrass(GameObject[] selections)
        {
            var totalPoints = 0;
            var terrains = new List<(Terrain terrain, int points)>();
            var meshes = new List<(MeshFilter mesh, int points)>();

            // 먼저 각 오브젝트별 생성 가능한 포인트 수를 계산
            for (var index = 0; index < selections.Length; index++)
            {
                var selection = selections[index];
                if (((1 << selection.layer) & toolSettings.PaintMask.value) == 0)
                {
                    LogLayerMismatch(selection);
                    continue;
                }

                if (selection.TryGetComponent(out MeshFilter mesh))
                {
                    var points = CalculatePointsForMesh();
                    meshes.Add((mesh, points));
                    totalPoints += points;
                }
                else if (selection.TryGetComponent(out Terrain terrain))
                {
                    var points = CalculatePointsForTerrain(terrain);
                    terrains.Add((terrain, points));
                    totalPoints += points;
                }
            }

            var currentPoint = 0;
            var newGrassData = CollectionsPool.GetList<GrassData>(totalPoints);

            // Mesh 처리
            foreach (var (mesh, points) in meshes)
            {
                await GenerateGrassForMesh(mesh, newGrassData, currentPoint, totalPoints);
                currentPoint += points;
            }

            // Terrain 처리
            foreach (var (terrain, points) in terrains)
            {
                await GenerateGrassForTerrain(terrain, newGrassData);
                currentPoint += points;
            }

            // 생성된 데이터를 grassData에 추가
            var grassData = _grassCompute.GrassDataList;
            grassData.AddRange(newGrassData);

            _grassCompute.Reset();
            CollectionsPool.ReturnList(newGrassData);
        }

        // private int CalculatePointsForObject(GameObject obj)
        // {
        //     if (obj.TryGetComponent(out MeshFilter _))
        //     {
        //         return CalculatePointsForMesh();
        //     }
        //
        //     if (obj.TryGetComponent(out Terrain _))
        //     {
        //         return CalculatePointsForTerrain();
        //     }
        //
        //     return 0;
        // }
        //
        // private int CalculateTotalPoints(GameObject[] selections)
        // {
        //     var totalPoints = 0;
        //     for (var index = 0; index < selections.Length; index++)
        //     {
        //         var selection = selections[index];
        //         if (selection.TryGetComponent(out MeshFilter _))
        //         {
        //             totalPoints += CalculatePointsForMesh();
        //         }
        //         else if (selection.TryGetComponent(out Terrain _))
        //         {
        //             totalPoints += CalculatePointsForTerrain();
        //         }
        //     }
        //
        //     return totalPoints;
        // }

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

            var numPoints = CalculatePointsForMesh();
            for (var j = 0; j < numPoints; j++)
            {
                await UpdateProgress(startPoint + j, totalPoints,
                    $"Generating grass: {startPoint + j:N0}/{totalPoints:N0}");

                var (success, grassData) = GenerateGrassDataForMesh(sourceMesh, localToWorld, sharedMesh, oVertices,
                    oColors, oNormals);
                if (success)
                {
                    newGrassData.Add(grassData);
                }
            }
        }

        private int CalculatePointsForMesh()
        {
            return Mathf.FloorToInt(toolSettings.GrassAmountToGenerate * toolSettings.GenerationDensity);
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

            // 오브젝트의 up 벡터를 기준으로 경사 체크
            var objectUp = sourceMesh.transform.up;
            var normalDot = Mathf.Abs(Vector3.Dot(worldNormal, objectUp));

            if (normalDot >= 1 - toolSettings.NormalLimit)
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

        private async UniTask GenerateGrassForTerrain(Terrain terrain, List<GrassData> newGrassData)
        {
            var numPoints = CalculatePointsForTerrain(terrain);
            var successCount = 0;
            var attemptCount = 0;

            while (successCount < numPoints)
            {
                await UpdateProgress(successCount, numPoints,
                    $"Generating grass: {successCount:N0}/{numPoints:N0}");

                var (success, grassData) = GenerateGrassDataForTerrain(terrain);
                if (success)
                {
                    newGrassData.Add(grassData);
                    successCount++;
                }
        
                attemptCount++;
        
                if (attemptCount > numPoints * 2)
                {
                    Debug.LogWarning($"Grass generation completed with {successCount}/{numPoints} instances after {attemptCount} attempts.");
                    break;
                }
            }
        }

        private int CalculatePointsForTerrain(Terrain terrain)
        {
            var terrainData = terrain.terrainData;
            var terrainSize = terrainData.size;
            var sampleCount = 100; // 샘플링 횟수
            var validPoints = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                var randomPoint = new Vector3(
                    Random.Range(0, terrainSize.x),
                    0,
                    Random.Range(0, terrainSize.z)
                );

                var splatMapCoord = new Vector2(randomPoint.x / terrainSize.x, randomPoint.z / terrainSize.z);
                var splatmapData = terrainData.GetAlphamaps(
                    Mathf.FloorToInt(splatMapCoord.x * (terrainData.alphamapWidth - 1)),
                    Mathf.FloorToInt(splatMapCoord.y * (terrainData.alphamapHeight - 1)),
                    1, 1
                );

                float totalDensity = 0f;
                float totalWeight = 0f;

                for (var j = 0; j < splatmapData.GetLength(2); j++)
                {
                    var layerStrength = splatmapData[0, 0, j];
                    totalDensity += toolSettings.LayerBlocking[j] * layerStrength;
                    totalWeight += layerStrength;
                }

                var averageDensity = totalWeight > 0 ? totalDensity / totalWeight : 0;
                if (Random.value <= averageDensity)
                {
                    validPoints++;
                }
            }

            var validRatio = (float)validPoints / sampleCount;
            var requestedPoints = Mathf.FloorToInt(toolSettings.GrassAmountToGenerate * toolSettings.GenerationDensity);
            return Mathf.FloorToInt(requestedPoints * validRatio);
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

    var normal = terrain.terrainData.GetInterpolatedNormal(
        randomPoint.x / terrainSize.x,
        randomPoint.z / terrainSize.z);

    if (normal.y <= 1 + toolSettings.NormalLimit && normal.y >= 1 - toolSettings.NormalLimit)
    {
        var splatMapCoord = new Vector2(randomPoint.x / terrainSize.x, randomPoint.z / terrainSize.z);
        var splatmapData = terrainData.GetAlphamaps(
            Mathf.FloorToInt(splatMapCoord.x * (terrainData.alphamapWidth - 1)),
            Mathf.FloorToInt(splatMapCoord.y * (terrainData.alphamapHeight - 1)),
            1, 1
        );

        float totalDensity = 0f;
        float totalWeight = 0f;
        float getFadeMap = 0f;

        for (var i = 0; i < splatmapData.GetLength(2); i++)
        {
            var layerStrength = splatmapData[0, 0, i];
            totalDensity += toolSettings.LayerBlocking[i] * layerStrength;
            totalWeight += layerStrength;
            
            if (toolSettings.LayerFading[i])
            {
                getFadeMap += layerStrength;
            }
        }

        var averageDensity = totalWeight > 0 ? totalDensity / totalWeight : 0;
        
        if (Random.value > averageDensity)
        {
            return (false, default);
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

            await GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RegenerateGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();

            await RemoveGrassFromObjects(selectedObjects);

            await GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RemoveCurrentGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            InitializeObjectProgresses();

            await RemoveGrassFromObjects(selectedObjects);

            Init();

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

        private async UniTask RemoveGrassFromObjects(GameObject[] selectedObjects)
        {
            var positionsToRemove = await GetPositionsToRemove(selectedObjects);
            await RemoveGrassAtPositions(positionsToRemove);
        }

        private async UniTask<HashSet<Vector3>> GetPositionsToRemove(GameObject[] selectedObjects)
        {
            var positionsToRemove = new HashSet<Vector3>();
            var tempIndices = CollectionsPool.GetList<int>();
            var totalObjects = selectedObjects.Length;
            var currentObjectIndex = 0;

            foreach (var obj in selectedObjects)
            {
                var bound = GrassPainterHelper.GetObjectBounds(obj);
                if (!bound.HasValue)
                {
                    currentObjectIndex++;
                    continue;
                }

                var center = bound.Value.center;
                var radius = bound.Value.extents.magnitude;

                _spatialGrid.GetObjectsInRadius(center, radius, tempIndices);

                var grassData = _grassCompute.GrassDataList;
                foreach (var index in tempIndices)
                {
                    if (index >= grassData.Count) continue;

                    var grassPosition = grassData[index].position;
                    if (IsPositionInObject(grassPosition, obj))
                    {
                        positionsToRemove.Add(grassPosition);
                    }
                }

                tempIndices.Clear();

                // 진행 상황 업데이트
                await UpdateProgress(currentObjectIndex, totalObjects,
                    $"Checking object {currentObjectIndex + 1:N0} / {totalObjects:N0}");

                currentObjectIndex++;
            }

            CollectionsPool.ReturnList(tempIndices);
            return positionsToRemove;
        }

        private bool IsPositionInObject(Vector3 worldPosition, GameObject obj)
        {
            if (obj.TryGetComponent(out MeshFilter meshFilter))
            {
                // 월드 좌표를 오브젝트의 로컬 좌표로 변환
                var localPosition = obj.transform.InverseTransformPoint(worldPosition);
                var localBounds = meshFilter.sharedMesh.bounds;

                // 로컬 공간에서 bounds 체크 (약간의 여유 추가)
                const float epsilon = 0.001f;
                return localPosition.x >= localBounds.min.x - epsilon &&
                       localPosition.x <= localBounds.max.x + epsilon &&
                       localPosition.y >= localBounds.min.y - epsilon &&
                       localPosition.y <= localBounds.max.y + epsilon &&
                       localPosition.z >= localBounds.min.z - epsilon && localPosition.z <= localBounds.max.z + epsilon;
            }

            if (obj.TryGetComponent(out Terrain terrain))
            {
                // 지형의 경우는 회전이 없으므로 월드 공간에서 직접 체크
                var terrainPos = terrain.transform.position;
                var terrainSize = terrain.terrainData.size;

                const float epsilon = 0.001f;
                return worldPosition.x >= terrainPos.x - epsilon &&
                       worldPosition.x <= terrainPos.x + terrainSize.x + epsilon &&
                       worldPosition.y >= terrainPos.y - epsilon &&
                       worldPosition.y <= terrainPos.y + terrainSize.y + epsilon &&
                       worldPosition.z >= terrainPos.z - epsilon &&
                       worldPosition.z <= terrainPos.z + terrainSize.z + epsilon;
            }

            return false;
        }

        private async UniTask RemoveGrassAtPositions(HashSet<Vector3> positionsToRemove)
        {
            const int batchSize = 10000;
            var grassData = _grassCompute.GrassDataList;
            var totalBatches = (grassData.Count + batchSize - 1) / batchSize;
            var tasks = new UniTask<List<GrassData>>[totalBatches];
            var removedCounts = new int[totalBatches];
            var totalRemoveGrassCount = positionsToRemove.Count;

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
                        // 거리 비교 대신 HashSet을 통한 빠른 확인
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
            }

            var results = await UniTask.WhenAll(tasks);
            var finalGrassToKeep = new List<GrassData>();
            var totalRemoved = 0;

            for (var index = 0; index < results.Length; index++)
            {
                finalGrassToKeep.AddRange(results[index]);
                totalRemoved += removedCounts[index];
                await UpdateProgress(index, results.Length,
                    $"Removed grass: {totalRemoved:N0} / {totalRemoveGrassCount:N0}");
            }

            _grassCompute.GrassDataList = finalGrassToKeep;
            InitSpatialGrid();
            Debug.Log($"Removed {totalRemoved} grass instances in total");
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ContinuePainting()
        {
            if (!_isPainting) return;

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    _grassAddPainter.AddGrass(_mousePointRay, toolSettings);
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter.RemoveGrass(_hitPos, toolSettings.BrushSize);
                    break;
                case BrushOption.Edit:
                    _grassEditPainter.EditGrass(_mousePointRay, toolSettings, _selectedEditOption);
                    break;
                case BrushOption.Reposition:
                    _grassRepositionPainter.RepositionGrass(_mousePointRay, toolSettings);
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

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildMesh()
        {
            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void InitSpatialGrid()
        {
            // 필요한 컴포넌트들이 없다면 초기화하지 않음
            if (_grassCompute == null || _grassCompute.GrassDataList == null)
                return;

            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            _spatialGrid = new SpatialGrid(bounds, toolSettings?.BrushSize ?? 4f);
            var grassData = _grassCompute.GrassDataList;

            if (_spatialGrid != null)
            {
                _spatialGrid.Clear();

                for (var i = 0; i < grassData.Count; i++)
                {
                    var grass = grassData[i];
                    _spatialGrid.AddObject(grass.position, i);
                }
            }
        }

        private void UpdateGrassData()
        {
            InitSpatialGrid();
            _grassCompute.Reset();
        }
    }
}