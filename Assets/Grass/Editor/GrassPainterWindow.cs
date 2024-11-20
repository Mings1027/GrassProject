using System;
using System.Collections.Generic;
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
        private Vector2 _scrollPos;

        private bool _paintModeActive;
        private bool _enableGrass;
        private bool _showSpatialGrid;

        private readonly string[] _mainTabBarStrings = { "Paint/Edit", "Modify", "Generate", "General Settings" };
        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reposition" };
        private readonly string[] _editOptionStrings = { "Edit Colors", "Edit Width/Height", "Both" };
        private readonly string[] _modifyOptionStrings = { "Width/Height", "Color", "Both" };
        private readonly string[] _generateTabStrings = { "Basic", "Terrain Layers" };
        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSo toolSettings;

        // options
        private GrassEditorTab _grassEditorTab;
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
                Debug.LogWarning("Progress update was cancelled");
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
                _grassCompute.GrassSetting = CreateOrGetGrassSetting();
            }

            _grassCompute.GrassSetting = (GrassSettingSO)EditorGUILayout.ObjectField(
                "Grass Settings Object",
                _grassCompute.GrassSetting,
                typeof(GrassSettingSO),
                false
            );

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

            _grassEditorTab = (GrassEditorTab)GUILayout.Toolbar(
                (int)_grassEditorTab,
                _mainTabBarStrings,
                GUILayout.MinWidth(300),
                GUILayout.Height(30)
            );
        }

        private void DrawControlPanels()
        {
            EditorGUILayout.Separator();

            switch (_grassEditorTab)
            {
                case GrassEditorTab.PaintEdit:
                    ShowPaintPanel();
                    break;
                case GrassEditorTab.Modify:
                    ShowModifyPanel();
                    break;
                case GrassEditorTab.Generate:
                    ShowGeneratePanel();
                    break;
                case GrassEditorTab.GeneralSettings:
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
            DrawBrushToolbar();
            DrawHitSettings();
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

        private void DrawBrushToolbar()
        {
            _selectedToolOption = (BrushOption)GUILayout.Toolbar(
                (int)_selectedToolOption,
                _toolbarStrings,
                GUILayout.Height(25)
            );
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

        private void DrawCommonBrushSettings()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField("Hold Shift + Scroll to adjust Brush size", style);

            toolSettings.BrushSize = GrassPainterHelper.FloatSlider(
                "Brush Size",
                "Adjust Brush Size",
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
                        Undo.RegisterCompleteObjectUndo(_grassCompute, "Modified Size");
                        ModifySize().Forget();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Color)
                {
                    if (EditorUtility.DisplayDialog("Modify Color",
                            "Modify the color of all grass elements?",
                            "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(_grassCompute, "Modified Color");
                        ModifyColor().Forget();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Both)
                {
                    if (EditorUtility.DisplayDialog("Modify Width Height and Color",
                            "Modify the width, height and color of all grass elements?", "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(_grassCompute, "Modified Size and Color");
                        ModifySizeAndColor().Forget();
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
            }

            EditorGUILayout.Separator();
            GrassPainterHelper.DrawHorizontalLine(Color.gray);
            DrawObjectOperations();
        }

        private void DrawGeneralSettings()
        {
            toolSettings.GenerateGrassCount = EditorGUILayout.IntSlider(
                "Generate Grass Count",
                toolSettings.GenerateGrassCount,
                toolSettings.MinGrassAmountToGenerate,
                toolSettings.MaxGrassAmountToGenerate
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

        private bool _layerGuideExpanded;

        private void DrawTerrainLayerSettings()
        {
            var headerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 10, 10),
                margin = new RectOffset(0, 0, 10, 10)
            };

            // 화살표 아이콘을 위한 스타일
            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                margin = new RectOffset(5, 0, 0, 0),
            };

            EditorGUILayout.BeginVertical(headerStyle);
            EditorGUILayout.BeginHorizontal();

            // 화살표 아이콘 표시
            _layerGuideExpanded = EditorGUILayout.Foldout(_layerGuideExpanded, "", true, foldoutStyle);

            var labelStyle = new GUIStyle()
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
            };

            if (GUILayout.Button("Layer Settings Guide", labelStyle))
            {
                _layerGuideExpanded = !_layerGuideExpanded;
            }

            EditorGUILayout.EndHorizontal();

            if (_layerGuideExpanded)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var helpBoxStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    richText = true,
                    padding = new RectOffset(15, 15, 15, 0),
                    wordWrap = true
                };

                EditorGUILayout.LabelField(
                    "<b>• Grass Density:</b>\n" +
                    "  Controls grass placement in each terrain layer.\n" +
                    "  1 = Maximum grass\n" +
                    "  0 = No grass\n\n" +
                    $"<b>• Height Scale:</b>\n" +
                    $"  Controls grass height based on current 'Grass Height' setting.\n" +
                    $"  1 = Current Grass Height\n" +
                    $"  0.5 = 50% of Grass Height\n" +
                    $"  0 = No grass\n\n",
                    helpBoxStyle
                );
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

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

            // 최소 너비 설정
            const float minLayerNameWidth = 100f; // 레이어 이름 최소 너비
            const float statusWidth = 40f; // 상태 표시 (고정)
            const float minSliderWidth = 100f; // 슬라이더 최소 너비

            // Column headers
            EditorGUILayout.BeginHorizontal();

            // 레이어 이름 (늘어남)
            EditorGUILayout.LabelField(
                new GUIContent("Terrain Layer", "Terrain layer name"),
                GUILayout.MinWidth(minLayerNameWidth),
                GUILayout.ExpandWidth(true)
            );

            // 상태 (고정)
            EditorGUILayout.LabelField(
                new GUIContent("Status", "Layer will be skipped or painted with grass"),
                GUILayout.MinWidth(statusWidth),
                GUILayout.ExpandWidth(true)
            );

            // 밀도 슬라이더 (늘어남)
            EditorGUILayout.LabelField(
                new GUIContent("Density", "0: Full grass, 1: No grass"),
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );

            // 높이 슬라이더 (늘어남)
            EditorGUILayout.LabelField(
                new GUIContent("Height", "0-100% of grass height"),
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );

            EditorGUILayout.EndHorizontal();

            GrassPainterHelper.DrawHorizontalLine(Color.gray, 1, 2);

            var terrainLayers = terrain.terrainData.terrainLayers;
            for (var i = 0; i < terrainLayers.Length; i++)
            {
                DrawTerrainLayerRow(i, terrainLayers[i], minLayerNameWidth, statusWidth, minSliderWidth);
            }
        }

        private void DrawTerrainLayerRow(int index, TerrainLayer layer, float minNameWidth, float statusWidth,
                                         float minSliderWidth)
        {
            EditorGUILayout.BeginHorizontal();

            var layerName = layer != null ? layer.name : $"Layer {index}";
            EditorGUILayout.LabelField(layerName, GUILayout.MinWidth(minNameWidth), GUILayout.ExpandWidth(true));

            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(statusWidth), GUILayout.ExpandWidth(true));
            bool isSkipped = toolSettings.LayerBlocking[index] <= 0f || toolSettings.HeightFading[index] <= 0f;
            var statusStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = new GUIStyleState
                {
                    textColor = isSkipped ? new Color(0.9f, 0.2f, 0.3f) : new Color(0.1f, 0.7f, 0.1f)
                }
            };

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("●", statusStyle, GUILayout.Width(10));
            EditorGUILayout.LabelField(isSkipped ? "Skip" : "Paint", statusStyle, GUILayout.Width(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            toolSettings.LayerBlocking[index] = EditorGUILayout.Slider(
                toolSettings.LayerBlocking[index],
                0f,
                1f,
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );

            toolSettings.HeightFading[index] = EditorGUILayout.Slider(
                toolSettings.HeightFading[index],
                0f,
                1f,
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );

            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectOperations()
        {
            if (DrawObjectOperationButton("Selected Objects: Generate Grass", "Generate Grass",
                    "Generate grass to selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(_grassCompute, "Generate Grass");
                GenerateGrassOnObject(Selection.gameObjects).Forget();
            }

            if (DrawObjectOperationButton("Selected Objects: Regenerate Grass", "Regenerate Grass",
                    "Remove existing grass and regenerate on selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(_grassCompute, "Regenerate Grass");
                RegenerateGrass(Selection.gameObjects).Forget();
            }

            if (DrawObjectOperationButton("Selected Objects: Remove Grass", "Remove Grass",
                    "Remove the grass on the selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(_grassCompute, "Remove Grass");
                RemoveCurrentGrass(Selection.gameObjects).Forget();
            }

            if (DrawGrassEditButton("Remove All Grass", "Remove All Grass", "Remove all grass from the scene?"))
            {
                Undo.RegisterCompleteObjectUndo(_grassCompute, "Remove All Grass");
                RemoveAllGrass();
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

        private bool DrawGrassEditButton(string buttonText, string dialogTitle, string dialogMessage)
        {
            return GUILayout.Button(buttonText) && EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No");
        }

        private void ShowMainSettingsPanel()
        {
            EditorGUILayout.LabelField("Blade Min/Max Settings", EditorStyles.boldLabel);
            var curPresets = _grassCompute.GrassSetting;

            DrawMinMaxSection("Blade Width", ref curPresets.minWidth, ref curPresets.maxWidth,
                curPresets.MinWidthLimit, curPresets.MaxWidthLimit);
            DrawMinMaxSection("Blade Height", ref curPresets.minHeight, ref curPresets.maxHeight,
                curPresets.MinHeightLimit, curPresets.MaxHeightLimit);
            DrawMinMaxSection("Random Height", ref curPresets.randomHeightMin, ref curPresets.randomHeightMax, 0, 1);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Blade Shape Settings", EditorStyles.boldLabel);
            DrawSliderRow("Blade Radius", ref curPresets.bladeRadius, curPresets.MinBladeRadius,
                curPresets.MaxBladeRadius);
            DrawSliderRow("Blade Forward", ref curPresets.bladeForward, curPresets.MinBladeForward,
                curPresets.MaxBladeForward);
            DrawSliderRow("Blade Curve", ref curPresets.bladeCurve, curPresets.MinBladeCurve, curPresets.MaxBladeCurve);
            DrawSliderRow("Bottom Width", ref curPresets.bottomWidth, curPresets.MinBottomWidth,
                curPresets.MaxBottomWidth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Blade Amount Settings", EditorStyles.boldLabel);
            curPresets.bladesPerVertex = EditorGUILayout.IntSlider("Blades Per Vertex",
                curPresets.bladesPerVertex, curPresets.MinBladesPerVertex, curPresets.MaxBladesPerVertex);
            curPresets.segmentsPerBlade = EditorGUILayout.IntSlider("Segments Per Blade",
                curPresets.segmentsPerBlade, curPresets.MinSegmentsPerBlade, curPresets.MaxSegmentsPerBlade);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wind Settings", EditorStyles.boldLabel);
            curPresets.windSpeed = EditorGUILayout.Slider("Wind Speed",
                curPresets.windSpeed, curPresets.MinWindSpeed, curPresets.MaxWindSpeed);
            curPresets.windStrength = EditorGUILayout.Slider("Wind Strength",
                curPresets.windStrength, curPresets.MinWindStrength, curPresets.MaxWindStrength);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tinting Settings", EditorStyles.boldLabel);
            curPresets.topTint = EditorGUILayout.ColorField("Top Tint", curPresets.topTint);
            curPresets.bottomTint = EditorGUILayout.ColorField("Bottom Tint", curPresets.bottomTint);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LOD/Culling Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Culling Bounds:", EditorStyles.boldLabel);
            curPresets.drawBounds = EditorGUILayout.Toggle(curPresets.drawBounds);
            EditorGUILayout.EndHorizontal();

            DrawFadeDistanceSlider("Min Fade Distance", ref curPresets.minFadeDistance, 0, curPresets.maxFadeDistance);
            DrawFadeDistanceSlider("Max Fade Distance", ref curPresets.maxFadeDistance, curPresets.minFadeDistance,
                300);

            curPresets.cullingTreeDepth = EditorGUILayout.IntField("Culling Tree Depth", curPresets.cullingTreeDepth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Settings", EditorStyles.boldLabel);
            curPresets.interactorStrength =
                EditorGUILayout.FloatField("Interactor Strength", curPresets.interactorStrength);
            curPresets.castShadow = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                "Shadow Settings", curPresets.castShadow);
        }

        private void DrawMinMaxSection(string label, ref float min, ref float max, float minLimit, float maxLimit)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.MinMaxSlider(label, ref min, ref max, minLimit, maxLimit);
            min = EditorGUILayout.FloatField(min, GUILayout.Width(50));
            max = EditorGUILayout.FloatField(max, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSliderRow(string label, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            value = EditorGUILayout.Slider(value, min, max);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFadeDistanceSlider(string label, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            value = EditorGUILayout.Slider(value, min, max);
            GUILayout.Label($"{min} / {max}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewGrassObject()
        {
            grassObject = new GameObject
            {
                name = "Grass System - Holder"
            };
            _grassCompute = grassObject.AddComponent<GrassComputeScript>();
            _grassCompute.GrassSetting = CreateOrGetGrassSetting();

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
                    // 브러시 미리보기
                    if (Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                    {
                        _hitPos = hit.point;
                        _hitNormal = hit.normal;
                        DrawGrassBrush(_hitPos, _hitNormal, toolSettings.BrushSize);
                    }

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
                    GrassPainterHelper.DrawCellWireframe(cellCenter, cellSize);
                }
            }
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
                float angleA = 360f / segmentCount * i;
                float angleB = 360f / segmentCount * (i + 1);

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
            var newGrassData = CollectionsPool.GetList<GrassData>();
            var allTempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();
            var totalRequestedPoints = 0;

            // 먼저 총 요청된 포인트 수 계산
            foreach (var selection in selections)
            {
                if (((1 << selection.layer) & toolSettings.PaintMask.value) != 0)
                {
                    totalRequestedPoints += toolSettings.GenerateGrassCount;
                }
            }

            var currentPoint = 0;
            // 모든 오브젝트에 대해 생성 가능한 잔디 위치를 계산
            foreach (var selection in selections)
            {
                if (((1 << selection.layer) & toolSettings.PaintMask.value) == 0)
                {
                    LogLayerMismatch(selection);
                    continue;
                }

                var numPoints = toolSettings.GenerateGrassCount;

                if (selection.TryGetComponent(out MeshFilter mesh))
                {
                    var tempPoints =
                        await CalculateValidPointsForMesh(mesh, numPoints, currentPoint, totalRequestedPoints);
                    allTempPoints.AddRange(tempPoints);
                    currentPoint += numPoints;
                }
                else if (selection.TryGetComponent(out Terrain terrain))
                {
                    var tempPoints =
                        await CalculateValidPointsForTerrain(terrain, numPoints, currentPoint, totalRequestedPoints);
                    allTempPoints.AddRange(tempPoints);
                    currentPoint += numPoints;
                }
            }

            // 실제 잔디 생성
            for (var i = 0; i < allTempPoints.Count; i++)
            {
                await UpdateProgress(i + 1, allTempPoints.Count,
                    $"Generating grass: ({i + 1:N0}/{allTempPoints.Count:N0})");

                var (worldPoint, worldNormal, widthHeight) = allTempPoints[i];
                var grassData = new GrassData
                {
                    position = worldPoint,
                    normal = worldNormal,
                    color = GetRandomColor(),
                    widthHeight = widthHeight
                };

                newGrassData.Add(grassData);
            }

            var grassDataList = _grassCompute.GrassDataList;
            grassDataList.AddRange(newGrassData);

            _grassCompute.Reset();
            CollectionsPool.ReturnList(newGrassData);
        }

        private void LogLayerMismatch(GameObject selection)
        {
            var expectedLayers = string.Join(", ", toolSettings.GetPaintMaskLayerNames());
            Debug.LogWarning(
                $"'{selection.name}' layer mismatch. Expected: '{expectedLayers}', Current: {LayerMask.LayerToName(selection.layer)}");
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CalculateValidPointsForMesh(MeshFilter sourceMesh, int numPoints, int startPoint, int totalPoints)
        {
            // 메인 스레드에서 필요한 데이터 미리 준비
            var localToWorld = sourceMesh.transform.localToWorldMatrix;
            var objectUp = sourceMesh.transform.up;
            var sharedMesh = sourceMesh.sharedMesh;
            var oVertices = sharedMesh.vertices;
            var oColors = sharedMesh.colors;
            var oNormals = sharedMesh.normals;
            var triangles = sharedMesh.triangles;
            var tempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();

            const int batchSize = 1000;
            var totalBatches = (numPoints + batchSize - 1) / batchSize;
            var tasks =
                new UniTask<
                        List<(Vector3 worldPoint, Vector3 worldNormal, Vector2 widthHeight, bool needsPhysicsCheck)>>
                    [totalBatches];

            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var start = batchIndex * batchSize;
                var end = Mathf.Min(start + batchSize, numPoints);
                var localBatchIndex = batchIndex;

                tasks[batchIndex] = UniTask.RunOnThreadPool(() =>
                {
                    var localResults =
                        new List<(Vector3 worldPoint, Vector3 worldNormal, Vector2 widthHeight, bool needsPhysicsCheck
                            )>();
                    var localRandom = new System.Random(Environment.TickCount + localBatchIndex);

                    for (var i = start; i < end; i++)
                    {
                        // 1. 무작위 삼각형 선택
                        var randomTriIndex = localRandom.Next(0, triangles.Length / 3);
                        var randomBarycentricCoord = GrassPainterHelper.GetRandomBarycentricCoordWithSeed(localRandom);
                        var vertexIndex1 = triangles[randomTriIndex * 3];
                        var vertexIndex2 = triangles[randomTriIndex * 3 + 1];
                        var vertexIndex3 = triangles[randomTriIndex * 3 + 2];

                        // 2. 삼각형 내의 랜덤한 점 계산
                        var point = randomBarycentricCoord.x * oVertices[vertexIndex1] +
                                    randomBarycentricCoord.y * oVertices[vertexIndex2] +
                                    randomBarycentricCoord.z * oVertices[vertexIndex3];

                        // 3. 해당 점의 법선 벡터 계산
                        var normal = randomBarycentricCoord.x * oNormals[vertexIndex1] +
                                     randomBarycentricCoord.y * oNormals[vertexIndex2] +
                                     randomBarycentricCoord.z * oNormals[vertexIndex3];

                        // 4. 로컬 좌표를 월드 좌표로 변환 (미리 저장한 매트릭스 사용)
                        var worldPoint = localToWorld.MultiplyPoint3x4(point);
                        var worldNormal = localToWorld.MultiplyVector(normal).normalized;

                        // 5. 경사도 체크
                        var normalDot = Mathf.Abs(Vector3.Dot(worldNormal, objectUp));
                        if (normalDot >= 1 - toolSettings.NormalLimit)
                        {
                            // 6. 버텍스 컬러 기반 크기 계산
                            var color = GrassPainterHelper.GetVertexColor(oColors, vertexIndex1, vertexIndex2,
                                vertexIndex3,
                                randomBarycentricCoord);
                            var widthHeightFactor = CalculateWidthHeightFactor(color);
                            var widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight) *
                                              widthHeightFactor;

                            localResults.Add((worldPoint, worldNormal, widthHeight, true));
                        }
                    }

                    return localResults;
                });

                await UpdateProgress(startPoint + start, totalPoints,
                    "Calculating grass positions");
            }

            var batchResults = await UniTask.WhenAll(tasks);

            // 메인 스레드에서 물리 검사 수행
            foreach (var batchResult in batchResults)
            {
                foreach (var (worldPoint, worldNormal, widthHeight, needsPhysicsCheck) in batchResult)
                {
                    if (needsPhysicsCheck && !Physics.CheckSphere(worldPoint, 0.1f, toolSettings.PaintBlockMask))
                    {
                        tempPoints.Add((worldPoint, worldNormal, widthHeight));
                    }
                }
            }

            return tempPoints;
        }

        private async UniTask<List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>>
            CalculateValidPointsForTerrain(Terrain terrain, int numPoints, int startPoint, int totalPoints)
        {
            var terrainData = terrain.terrainData;
            var terrainSize = terrainData.size;
            var tempPoints = new List<(Vector3 position, Vector3 normal, Vector2 widthHeight)>();

            for (var i = 0; i < numPoints; i++)
            {
                await UpdateProgress(startPoint + i + 1, totalPoints,
                    "Calculating grass positions");

                var randomPoint = new Vector3(
                    Random.Range(0, terrainSize.x),
                    0,
                    Random.Range(0, terrainSize.z)
                );

                var worldPoint = terrain.transform.TransformPoint(randomPoint);
                worldPoint.y = terrain.SampleHeight(worldPoint);

                if (Physics.CheckSphere(worldPoint, 0.1f, toolSettings.PaintBlockMask))
                    continue;

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
                    float heightFade = 0f;
                    bool shouldSkip = true;

                    // 각 레이어별 계산
                    for (var j = 0; j < splatmapData.GetLength(2); j++)
                    {
                        // Skip if layer has max density or zero height
                        if (toolSettings.LayerBlocking[j] <= 0f || toolSettings.HeightFading[j] <= 0f)
                            continue;

                        var layerStrength = splatmapData[0, 0, j];
                        shouldSkip = false;
                        totalDensity += toolSettings.LayerBlocking[j] * layerStrength;
                        totalWeight += layerStrength;
                        heightFade += layerStrength * toolSettings.HeightFading[j];
                    }

                    // Skip this point if all applicable layers are disabled
                    if (shouldSkip)
                        continue;

                    var averageDensity = totalWeight > 0 ? totalDensity / totalWeight : 0;
                    heightFade = totalWeight > 0 ? heightFade / totalWeight : 1f;

                    // 밀도에 따른 잔디 생성 여부 결정
                    if (Random.value <= averageDensity)
                    {
                        var widthHeight = new Vector2(
                            toolSettings.GrassWidth,
                            toolSettings.GrassHeight * heightFade
                        );
                        tempPoints.Add((worldPoint, normal, widthHeight));
                    }
                }
            }

            return tempPoints;
        }

        private float CalculateWidthHeightFactor(Color vertexColor)
        {
            return toolSettings.VertexColorSettings switch
            {
                GrassToolSettingSo.VertexColorSetting.Red => 1 - vertexColor.r,
                GrassToolSettingSo.VertexColorSetting.Green => 1 - vertexColor.g,
                GrassToolSettingSo.VertexColorSetting.Blue => 1 - vertexColor.b,
                _ => 1f
            };
        }

        private async UniTask UpdateProgress(int currentPoint, int totalPoints, string progressMessage)
        {
            _objectProgress.progress = (float)currentPoint / totalPoints;
            _objectProgress.progressMessage = $"{progressMessage} {currentPoint / (float)totalPoints * 100:F1}%";

            if (currentPoint % Math.Max(1, totalPoints / 100) == 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
                Repaint();
            }
        }

        private async UniTask GenerateGrassOnObject(GameObject[] selectedObjects)
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

                await _spatialGrid.GetObjectsInRadiusAsync(center, radius, tempIndices);
                var grassData = _grassCompute.GrassDataList;

                if (obj.TryGetComponent(out MeshFilter meshFilter))
                {
                    await HandleMeshFilter(meshFilter, tempIndices, grassData, positionsToRemove);
                }
                else if (obj.TryGetComponent(out Terrain terrain))
                {
                    HandleTerrain(terrain, tempIndices, grassData, positionsToRemove);
                }

                tempIndices.Clear();

                await UpdateProgress(currentObjectIndex, totalObjects,
                    "Checking object");

                currentObjectIndex++;
            }

            CollectionsPool.ReturnList(tempIndices);
            return positionsToRemove;
        }

        private static async UniTask HandleMeshFilter(MeshFilter meshFilter, List<int> indices,
                                                      List<GrassData> grassData,
                                                      HashSet<Vector3> positionsToRemove)
        {
            var localBounds = meshFilter.sharedMesh.bounds;
            const int batchSize = 10000;
            var totalBatches = (indices.Count + batchSize - 1) / batchSize;
            var batchResults = new List<Vector3>[totalBatches];

            // 메인 스레드에서 필요한 데이터만 미리 캐시
            var worldToLocalMatrix = meshFilter.transform.worldToLocalMatrix;

            // 변환 작업을 스레드풀에서 수행
            var localPositions = await UniTask.RunOnThreadPool(() =>
            {
                var positions = new Vector3[indices.Count];

                for (var i = 0; i < indices.Count; i++)
                {
                    var index = indices[i];
                    if (index < grassData.Count)
                    {
                        positions[i] = worldToLocalMatrix.MultiplyPoint3x4(grassData[index].position);
                    }
                }

                return positions;
            });

            // 각 배치를 병렬로 처리
            var tasks = new UniTask[totalBatches];
            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var start = batchIndex * batchSize;
                var end = Mathf.Min(start + batchSize, indices.Count);
                var localBatchIndex = batchIndex;

                tasks[batchIndex] = UniTask.RunOnThreadPool(() =>
                {
                    var localPositionsToRemove = new List<Vector3>();
                    for (var i = start; i < end; i++)
                    {
                        var index = indices[i];
                        if (index >= grassData.Count) continue;

                        var localPosition = localPositions[i];
                        var grassPosition = grassData[index].position;

                        const float epsilon = 0.001f;
                        if (localPosition.x >= localBounds.min.x - epsilon &&
                            localPosition.x <= localBounds.max.x + epsilon &&
                            localPosition.y >= localBounds.min.y - epsilon &&
                            localPosition.y <= localBounds.max.y + epsilon &&
                            localPosition.z >= localBounds.min.z - epsilon &&
                            localPosition.z <= localBounds.max.z + epsilon)
                        {
                            localPositionsToRemove.Add(grassPosition);
                        }
                    }

                    // 결과를 배열에 저장
                    batchResults[localBatchIndex] = localPositionsToRemove;
                    return UniTask.CompletedTask;
                });
            }

            // 모든 배치가 완료될 때까지 대기
            await UniTask.WhenAll(tasks);

            // 메인 스레드에서 한 번에 결과 합치기
            foreach (var batchResult in batchResults)
            {
                if (batchResult == null) continue;
                foreach (var pos in batchResult)
                {
                    positionsToRemove.Add(pos);
                }
            }
        }

        private static void HandleTerrain(Terrain terrain, List<int> indices, List<GrassData> grassData,
                                          HashSet<Vector3> positionsToRemove)
        {
            var terrainPos = terrain.transform.position;
            var terrainSize = terrain.terrainData.size;
            foreach (var index in indices)
            {
                if (index >= grassData.Count) continue;

                var grassPosition = grassData[index].position;
                const float epsilon = 0.001f;
                if (grassPosition.x >= terrainPos.x - epsilon &&
                    grassPosition.x <= terrainPos.x + terrainSize.x + epsilon &&
                    grassPosition.y >= terrainPos.y - epsilon &&
                    grassPosition.y <= terrainPos.y + terrainSize.y + epsilon &&
                    grassPosition.z >= terrainPos.z - epsilon &&
                    grassPosition.z <= terrainPos.z + terrainSize.z + epsilon)
                {
                    positionsToRemove.Add(grassPosition);
                }
            }
        }

        private async UniTask RemoveGrassAtPositions(HashSet<Vector3> positionsToRemove)
        {
            if (positionsToRemove.Count == 0)
            {
                Debug.Log("No grass to remove");
                return;
            }

            const int batchSize = 10000;
            var grassData = _grassCompute.GrassDataList;
            var totalBatches = (grassData.Count + batchSize - 1) / batchSize;
            var finalGrassToKeep = new List<GrassData>();
            var totalRemoved = 0;

            // 배치별로 순차 처리
            for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var start = batchIndex * batchSize;
                var end = Mathf.Min(start + batchSize, grassData.Count);

                var result = await UniTask.RunOnThreadPool(() =>
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

                    return (localGrassToKeep, localRemovedCount);
                });

                finalGrassToKeep.AddRange(result.localGrassToKeep);
                totalRemoved += result.localRemovedCount;

                await UpdateProgress(batchIndex + 1, totalBatches,
                    $"Removing Grass");
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

        private async UniTask ModifyColor()
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.color = GetRandomColor();
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying colors");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
        }

        private async UniTask ModifySize()
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying size");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
        }

        private async UniTask ModifySizeAndColor()
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var grassData = new List<GrassData>(_grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (int i = 0; i < totalCount; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                newData.color = GetRandomColor();
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying size and color");
            }

            _grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
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
                    _grassRemovePainter.RemoveGrass(_mousePointRay, toolSettings.BrushSize);
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