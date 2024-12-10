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
        public Vector2 ScrollPos
        {
            get => _scrollPos;
            set => _scrollPos = value;
        }
        private bool _paintModeActive;
        private bool _enableGrass;
        private bool _autoUpdate;

        private readonly string[] _mainTabBarStrings = { "Paint/Edit", "Modify", "Generate", "General Settings" };
        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reposition" };
        private readonly string[] _editOptionStrings = { "Edit Colors", "Edit Width/Height", "Both" };
        private readonly string[] _modifyOptionStrings = { "Color", "Width/Height", "Both" };
        private readonly string[] _generateTabStrings = { "Basic", "Terrain Layers" };
        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSo toolSettings;

        // options
        public GrassEditorTab GrassEditorTab
        {
            set => _grassEditorTab = value;
        }
        private GrassEditorTab _grassEditorTab;
        private BrushOption _selectedToolOption;
        private EditOption _selectedEditOption;
        private ModifyOption _selectedModifyOption;
        private GenerateTab _selectedGenerateOption;

        private Ray _mousePointRay;
        private Vector3 _mousePos;
        private Vector3 _cachedPos;
        private Bounds _grassBounds;

        [SerializeField] private GrassComputeScript grassCompute;

        private GrassSeasonManager _seasonManager;

        private bool _isInit;
        private bool _isProcessing;
        private CancellationTokenSource _cts;
        private readonly ObjectProgress _objectProgress = new();

        private bool _isMousePressed;
        private bool _isPainting;

        private GrassAddPainter _grassAddPainter;
        private GrassEditPainter _grassEditPainter;
        private GrassRemovePainter _grassRemovePainter;
        private GrassRepositionPainter _grassRepositionPainter;

        private SpatialGrid _spatialGrid;

        [MenuItem("Tools/Grass Tool")]
        public static void Open()
        {
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

            toolSettings = (GrassToolSettingSo)EditorGUILayout.ObjectField(
                "Grass Tool Settings", toolSettings, typeof(GrassToolSettingSo), false);

            grassCompute = (GrassComputeScript)EditorGUILayout.ObjectField(
                "Grass Compute Object", grassCompute, typeof(GrassComputeScript), true);

            var manualUpdate = new GUIContent
            {
                text = "Manual Update",
                tooltip = "Update all grass data. Performance may be slower with large amounts of grass data."
            };
            if (GUILayout.Button(manualUpdate, GUILayout.Height(40)))
            {
                Init();
            }

            if (grassCompute == null)
            {
                grassCompute = FindAnyObjectByType<GrassComputeScript>();
            }

            if (grassCompute == null)
            {
                if (GUILayout.Button("Create Grass Compute Object", GUILayout.Height(30)))
                {
                    CreateNewGrassObject();
                }

                EditorGUILayout.HelpBox("Please Create Grass Compute Object.", MessageType.Warning);
                return;
            }

            if (grassCompute != null && _enableGrass != grassCompute.enabled)
            {
                _enableGrass = grassCompute.enabled;
            }

            if (GUILayout.Button("Update All Shader Data", GUILayout.Height(30)))
            {
                InitSeasonManager();
                UpdateSeasonZones();
                grassCompute.SetShaderData();
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawBasicControls();

            GrassEditorHelper.DrawHorizontalLine(Color.gray);
            DrawKeyBindingsUI();
            DrawToggles();
            GrassEditorHelper.DrawHorizontalLine(Color.gray);
            DrawMainToolbar();
            DrawControlPanels();

            EditorGUILayout.EndScrollView();

            EditorUtility.SetDirty(toolSettings);
            EditorUtility.SetDirty(grassCompute.GrassSetting);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;

            // toolSettings가 없다면 로드 시도
            if (toolSettings == null)
            {
                toolSettings = AssetDatabase.LoadAssetAtPath<GrassToolSettingSo>(
                    "Assets/Grass/Settings/Grass Tool Settings.asset"
                );
            }

            if (grassCompute != null)
            {
                _enableGrass = grassCompute.enabled;
            }
        }

        private void OnDisable()
        {
            Debug.Log("OnDisable");
            RemoveDelegates();
            _spatialGrid?.Clear();
            if (grassCompute != null)
            {
                EditorApplication.update -= grassCompute.Reset;
            }
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
            // SpatialGrid 초기화 검사
            if (_spatialGrid == null)
            {
                InitSpatialGrid();
            }

            if (grassCompute.GrassSetting == null)
            {
                grassCompute.GrassSetting = CreateOrGetGrassSetting();
            }

            grassCompute.GrassSetting = (GrassSettingSO)EditorGUILayout.ObjectField(
                "Grass Settings Object",
                grassCompute.GrassSetting,
                typeof(GrassSettingSO),
                false
            );

            grassCompute.GrassSetting.materialToUse = (Material)EditorGUILayout.ObjectField(
                "Grass Material",
                grassCompute.GrassSetting.materialToUse,
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

        private void DrawToggles()
        {
            EditorGUILayout.BeginHorizontal();

            if (GrassEditorHelper.DrawToggleButton("Enable Grass", _enableGrass, out var newEnagleGrass))
            {
                _enableGrass = newEnagleGrass;
                grassCompute.enabled = newEnagleGrass;
            }

            if (GrassEditorHelper.DrawToggleButton("Auto Update", "Slow but always update",
                    _autoUpdate, out var newAutoUpdate))
            {
                _autoUpdate = newAutoUpdate;
                if (_autoUpdate)
                {
                    EditorApplication.update += grassCompute.Reset;
                }
                else
                {
                    EditorApplication.update -= grassCompute.Reset;
                }
            }

            if (GrassEditorHelper.DrawToggleButton("Paint Mode", _paintModeActive, out var newPaintMode))
            {
                _paintModeActive = newPaintMode;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Total Grass Amount: " + grassCompute.GrassDataList.Count, EditorStyles.label);

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
            InitSeasonManager();
            UpdateSeasonZones();
            InitPainters();
        }

        private void InitSeasonManager()
        {
            if (_seasonManager == null) _seasonManager = FindAnyObjectByType<GrassSeasonManager>();
        }

        private void UpdateSeasonZones()
        {
            if (_seasonManager != null) _seasonManager.UpdateSeasonZones();
        }

        private void InitPainters()
        {
            _grassAddPainter = new GrassAddPainter(grassCompute, _spatialGrid);
            _grassRemovePainter = new GrassRemovePainter(grassCompute, _spatialGrid);
            _grassEditPainter = new GrassEditPainter(grassCompute, _spatialGrid);
            _grassRepositionPainter = new GrassRepositionPainter(grassCompute, _spatialGrid);
        }

        private void DrawKeyBindingsUI()
        {
            toolSettings.grassMouseButton =
                (MouseButton)EditorGUILayout.EnumPopup(
                    new GUIContent("Mouse Button",
                        $"Hold and drag <b>{GrassEditorHelper.GetMouseButtonName(toolSettings.grassMouseButton)}</b> mouse button to paint grass"),
                    toolSettings.grassMouseButton);

            if (_paintModeActive)
            {
                toolSettings.brushSizeShortcut =
                    (KeyType)EditorGUILayout.EnumPopup(
                        new GUIContent("Brush Size Shortcut",
                            $"Hold {GrassEditorHelper.GetShortcutName(toolSettings.brushSizeShortcut)} + scroll to adjust size"),
                        toolSettings.brushSizeShortcut);

                toolSettings.brushHeightShortcut =
                    (KeyType)EditorGUILayout.EnumPopup(
                        new GUIContent("Brush Height Shortcut",
                            $"Hold {GrassEditorHelper.GetShortcutName(toolSettings.brushHeightShortcut)} + scroll to adjust height"),
                        toolSettings.brushHeightShortcut);
            }
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
                    DrawRepositionBrushSettings();
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
            EditorGUILayout.Separator();

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

            toolSettings.BrushSize = GrassEditorHelper.FloatSlider(
                "Brush Size",
                "Adjust Brush Size",
                toolSettings.BrushSize,
                toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize
            );

            EditorGUILayout.BeginHorizontal();
            toolSettings.NormalLimit = GrassEditorHelper.FloatSlider(
                "Normal Limit",
                "Maximum slope angle where grass can be placed. 0 = flat ground only, 1 = all slopes",
                toolSettings.NormalLimit,
                toolSettings.MinNormalLimit,
                toolSettings.MaxNormalLimit
            );
            GrassEditorHelper.ShowAngle(toolSettings.NormalLimit);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddBrushSettings()
        {
            toolSettings.Density = GrassEditorHelper.IntSlider(
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

            toolSettings.GrassWidth = GrassEditorHelper.FloatSlider(
                "Grass Width",
                "Set grass width",
                toolSettings.GrassWidth,
                toolSettings.MinSizeWidth,
                toolSettings.MaxSizeWidth
            );

            toolSettings.GrassHeight = GrassEditorHelper.FloatSlider(
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
            if (toolSettings.GrassHeight > grassCompute.GrassSetting.maxHeight)
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
            DrawBrushColorField();
            DrawColorVariationSliders();
        }

        private void DrawBrushColorField()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            toolSettings.BrushColor = EditorGUILayout.ColorField("Brush Color", toolSettings.BrushColor);
        }

        private void DrawColorVariationSliders()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);

            toolSettings.RangeR = GrassEditorHelper.FloatSlider("Red", "", toolSettings.RangeR, 0f, 1f);
            toolSettings.RangeG = GrassEditorHelper.FloatSlider("Green", "", toolSettings.RangeG, 0f, 1f);
            toolSettings.RangeB = GrassEditorHelper.FloatSlider("Blue", "", toolSettings.RangeB, 0f, 1f);
        }

        private void DrawWidthHeightSliders()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
            toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
        }

        private void DrawEditBrushSettings()
        {
            _selectedEditOption =
                (EditOption)GUILayout.Toolbar((int)_selectedEditOption, _editOptionStrings, GUILayout.Height(25));
            EditorGUILayout.Separator();

            toolSettings.BrushTransitionSpeed = GrassEditorHelper.FloatSlider(
                "Brush Transition Speed",
                "Speed of grass modifications",
                toolSettings.BrushTransitionSpeed,
                0f, 1f
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
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);

            toolSettings.AdjustWidth = GrassEditorHelper.FloatSlider(
                "Grass Width Adjustment",
                "Grass Width Adjustment",
                toolSettings.AdjustWidth,
                toolSettings.MinAdjust,
                toolSettings.MaxAdjust
            );

            toolSettings.AdjustHeight = GrassEditorHelper.FloatSlider(
                "Grass Height Adjustment",
                "Grass Height Adjustment",
                toolSettings.AdjustHeight,
                toolSettings.MinAdjust,
                toolSettings.MaxAdjust
            );

            toolSettings.AdjustWidthMax = GrassEditorHelper.FloatSlider(
                "Max Width Adjustment",
                "Max Width Adjustment",
                toolSettings.AdjustWidthMax,
                0.01f,
                3f
            );

            toolSettings.AdjustHeightMax = GrassEditorHelper.FloatSlider(
                "Max Height Adjustment",
                "Max Height Adjustment",
                toolSettings.AdjustHeightMax,
                0.01f,
                3f
            );
        }

        private void DrawRepositionBrushSettings()
        {
            toolSettings.BrushHeight = GrassEditorHelper.FloatSlider(
                "Brush Height",
                "Height limit for repositioning grass",
                toolSettings.BrushHeight,
                toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize
            );
        }

        private void ShowModifyPanel()
        {
            _selectedModifyOption = (ModifyOption)GUILayout.Toolbar((int)_selectedModifyOption, _modifyOptionStrings,
                GUILayout.Height(25));

            EditorGUILayout.Separator();

            switch (_selectedModifyOption)
            {
                case ModifyOption.Color:
                    DrawBrushColorField();
                    DrawColorVariationSliders();
                    break;
                case ModifyOption.WidthHeight:
                    DrawWidthHeightSliders();
                    break;
                case ModifyOption.Both:
                    DrawWidthHeightSliders();
                    DrawBrushColorField();
                    DrawColorVariationSliders();
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
                        Undo.RegisterCompleteObjectUndo(grassCompute, "Modified Size");
                        ModifySize().Forget();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Color)
                {
                    if (EditorUtility.DisplayDialog("Modify Color",
                            "Modify the color of all grass elements?",
                            "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(grassCompute, "Modified Color");
                        ModifyColor().Forget();
                    }
                }

                if (_selectedModifyOption == ModifyOption.Both)
                {
                    if (EditorUtility.DisplayDialog("Modify Width Height and Color",
                            "Modify the width, height and color of all grass elements?", "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(grassCompute, "Modified Size and Color");
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
                    DrawHitSettings();
                    DrawVertexSettings();
                    DrawWidthHeightSliders();
                    DrawBrushColorField();
                    DrawColorVariationSliders();
                    break;
                // case GenerateTab.Mesh:
                //     DrawMeshSettings();
                //     break;
                case GenerateTab.TerrainLayers:
                    DrawTerrainLayerSettings();
                    break;
            }

            EditorGUILayout.Separator();
            GrassEditorHelper.DrawHorizontalLine(Color.gray);
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
            toolSettings.NormalLimit = GrassEditorHelper.FloatSlider(
                "Normal Limit",
                "Maximum slope angle where grass can be placed. 0 = flat ground only, 1 = all slopes",
                toolSettings.NormalLimit,
                toolSettings.MinNormalLimit,
                toolSettings.MaxNormalLimit
            );
            GrassEditorHelper.ShowAngle(toolSettings.NormalLimit);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVertexSettings()
        {
            EditorGUILayout.Separator();

            toolSettings.VertexColorSettings = (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup(
                "Block On vertex Colors",
                toolSettings.VertexColorSettings
            );
            toolSettings.VertexFade = (GrassToolSettingSo.VertexColorSetting)EditorGUILayout.EnumPopup(
                "Fade on Vertex Colors",
                toolSettings.VertexFade
            );
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

            EditorGUILayout.BeginVertical(headerStyle);

            // Foldout을 DrawToggleButton으로 교체
            if (GrassEditorHelper.DrawToggleButton("Layer Settings Guide",
                    "Display information about terrain layer settings",
                    _layerGuideExpanded, out var expanded))
            {
                _layerGuideExpanded = expanded;
            }

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
                    "<b>• Height Scale:</b>\n" +
                    "  Controls grass height based on current 'Grass Height' setting.\n" +
                    "  1 = Current Grass Height\n" +
                    "  0.5 = 50% of Grass Height\n" +
                    "  0 = No grass\n\n",
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
            const float minLayerNameWidth = 100f;
            const float statusWidth = 40f;
            const float minSliderWidth = 100f;

            // Column headers
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Terrain Layer", "Terrain layer name"),
                GUILayout.MinWidth(minLayerNameWidth),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.LabelField(
                new GUIContent("Status", "Layer will be skipped or painted with grass"),
                GUILayout.MinWidth(statusWidth),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.LabelField(
                new GUIContent("Density", "0: Full grass, 1: No grass"),
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.LabelField(
                new GUIContent("Height", "0-100% of grass height"),
                GUILayout.MinWidth(minSliderWidth),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.EndHorizontal();

            GrassEditorHelper.DrawHorizontalLine(Color.gray, 1, 2);

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
            var isSkipped = toolSettings.LayerBlocking[index] <= 0f || toolSettings.HeightFading[index] <= 0f;
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
            if (DrawButtonForSelectedObject("Selected Objects: Generate Grass", "Generate Grass",
                    "Generate grass to selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(grassCompute, "Generate Grass");
                GenerateGrassOnObject(Selection.gameObjects).Forget();
            }

            if (DrawButtonForSelectedObject("Selected Objects: Regenerate Grass", "Regenerate Grass",
                    "Remove existing grass and regenerate on selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(grassCompute, "Regenerate Grass");
                RegenerateGrass(Selection.gameObjects).Forget();
            }

            if (DrawButtonForSelectedObject("Selected Objects: Remove Grass", "Remove Grass",
                    "Remove the grass on the selected object(s)?"))
            {
                Undo.RegisterCompleteObjectUndo(grassCompute, "Remove Grass");
                RemoveCurrentGrass(Selection.gameObjects).Forget();
            }

            if (DrawButtonForGrass("Remove All Grass", "Remove All Grass", "Remove all grass from the scene?"))
            {
                Undo.RegisterCompleteObjectUndo(grassCompute, "Remove All Grass");
                RemoveAllGrass();
            }
        }

        private bool DrawButtonForSelectedObject(string buttonText, string dialogTitle, string dialogMessage)
        {
            if (!GUILayout.Button(buttonText)) return false;

            if (!EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No")) return false;

            if (Selection.gameObjects is { Length: > 0 }) return true;

            Debug.LogWarning("No objects selected. Please select one or more objects in the scene.");
            return false;
        }

        private bool DrawButtonForGrass(string buttonText, string dialogTitle, string dialogMessage)
        {
            return GUILayout.Button(buttonText) && EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No");
        }

        private void ShowMainSettingsPanel()
        {
            var grassSetting = grassCompute.GrassSetting;

            GrassEditorHelper.DrawFoldoutSection("Global Season Settings", () => DrawSeasonSettings(grassSetting));

            GrassEditorHelper.DrawFoldoutSection("Blade Min/Max Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                GrassEditorHelper.DrawMinMaxSection("Blade Width", ref grassSetting.minWidth,
                    ref grassSetting.maxWidth,
                    grassSetting.MinWidthLimit, grassSetting.MaxWidthLimit);
                GrassEditorHelper.DrawMinMaxSection("Blade Height", ref grassSetting.minHeight,
                    ref grassSetting.maxHeight,
                    grassSetting.MinHeightLimit, grassSetting.MaxHeightLimit);
                GrassEditorHelper.DrawMinMaxSection("Random Height", ref grassSetting.randomHeightMin,
                    ref grassSetting.randomHeightMax, 0, 1);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetBladeMinMax();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Blade Shape Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                DrawSliderRow("Blade Radius", ref grassSetting.bladeRadius, grassSetting.MinBladeRadius,
                    grassSetting.MaxBladeRadius);
                DrawSliderRow("Blade Forward", ref grassSetting.bladeForward, grassSetting.MinBladeForward,
                    grassSetting.MaxBladeForward);
                DrawSliderRow("Blade Curve", ref grassSetting.bladeCurve, grassSetting.MinBladeCurve,
                    grassSetting.MaxBladeCurve);
                DrawSliderRow("Bottom Width", ref grassSetting.bottomWidth, grassSetting.MinBottomWidth,
                    grassSetting.MaxBottomWidth);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetBladeShape();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Blade Amount Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                grassSetting.bladesPerVertex = EditorGUILayout.IntSlider("Blades Per Vertex",
                    grassSetting.bladesPerVertex, grassSetting.MinBladesPerVertex, grassSetting.MaxBladesPerVertex);
                grassSetting.segmentsPerBlade = EditorGUILayout.IntSlider("Segments Per Blade",
                    grassSetting.segmentsPerBlade, grassSetting.MinSegmentsPerBlade, grassSetting.MaxSegmentsPerBlade);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetBladeAmount();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Wind Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                grassSetting.windSpeed = EditorGUILayout.Slider("Wind Speed",
                    grassSetting.windSpeed, grassSetting.MinWindSpeed, grassSetting.MaxWindSpeed);
                grassSetting.windStrength = EditorGUILayout.Slider("Wind Strength",
                    grassSetting.windStrength, grassSetting.MinWindStrength, grassSetting.MaxWindStrength);
                grassSetting.WindDirection =
                    EditorGUILayout.Slider("Wind Direction", grassSetting.WindDirection, 0f, 360f);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetWindSetting();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Tinting Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                grassSetting.topTint = EditorGUILayout.ColorField("Top Tint", grassSetting.topTint);
                grassSetting.bottomTint = EditorGUILayout.ColorField("Bottom Tint", grassSetting.bottomTint);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetTint();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("LOD Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                GrassLODSettingEditor.DrawLODSettingsPanel(grassSetting);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetLODSetting();
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Culling Settings", () =>
            {
                EditorGUILayout.HelpBox(
                    "Divides the world into grid cells for grass culling.\n" +
                    "Smaller cells = More precise culling area\n" +
                    "Larger cells = Larger culling area",
                    MessageType.Info
                );

                // Visualization toggle
                var gizmosContent = new GUIContent(EditorIcons.Gizmos)
                {
                    text = " Visualize Culling Bounds",
                    tooltip = "Green: Active culling zones\nRed: Total grass bounds"
                };

                if (GrassEditorHelper.DrawToggleButton(gizmosContent, grassSetting.drawBounds, out var newState))
                {
                    grassSetting.drawBounds = newState;
                }

                grassSetting.cullingTreeDepth =
                    EditorGUILayout.IntSlider("Cell Size", grassSetting.cullingTreeDepth, 1, 10);
            });
            GrassEditorHelper.DrawFoldoutSection("Other Settings", () =>
            {
                EditorGUI.BeginChangeCheck();

                grassSetting.interactorStrength =
                    EditorGUILayout.Slider("Interactor Strength", grassSetting.interactorStrength, 0f, 10f);
                grassSetting.castShadow = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                    "Shadow Settings", grassSetting.castShadow);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SetInteractorStrength();
                }
            });
        }

        private void DrawSeasonSettings(GrassSettingSO grassSetting)
        {
            InitSeasonManager();
            if (_seasonManager == null)
            {
                EditorGUILayout.HelpBox("No GrassSeasonManager found in scene. Season effects are disabled.",
                    MessageType.Info);
                if (GUILayout.Button("Create Season Controller"))
                {
                    CreateSeasonController();
                }

                return;
            }

            SerializedObject serializedSettings = new SerializedObject(grassSetting);
            var seasonRange = serializedSettings.FindProperty("seasonRange");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(seasonRange);
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(grassSetting);
                _seasonManager.UpdateSeasonZones();
            }

            EditorGUILayout.Space(10);

            var rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.helpBox, GUILayout.Height(100));
            GrassEditorHelper.DrawSeasonSettingsTable(rect, grassSetting, _seasonManager);
        }

        private static void CreateSeasonController()
        {
            // Create Season Controller
            var controllerObject = new GameObject("Grass Season Manager");

            // Create Grass Season Zone as child
            var zoneObject = new GameObject("Grass Season Zone");
            zoneObject.transform.SetParent(controllerObject.transform);
            zoneObject.AddComponent<GrassSeasonZone>();

            // Set initial transform values for zone
            zoneObject.transform.localPosition = Vector3.zero;
            zoneObject.transform.localScale = new Vector3(10f, 10f, 10f);

            // Register both objects for undo
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create Season Controller");
            Selection.activeGameObject = controllerObject;
        }

        private static void DrawSliderRow(string label, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            value = EditorGUILayout.Slider(value, min, max);
            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewGrassObject()
        {
            var grassObject = new GameObject
            {
                name = "Grass Compute System"
            };
            grassCompute = grassObject.AddComponent<GrassComputeScript>();
            grassCompute.GrassSetting = CreateOrGetGrassSetting();

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

            if (e.type == EventType.ScrollWheel && GrassEditorHelper.IsShortcutPressed(toolSettings.brushSizeShortcut))
            {
                HandleBrushSize(e);
                e.Use();
                return;
            }

            if (e.type == EventType.ScrollWheel &&
                GrassEditorHelper.IsShortcutPressed(toolSettings.brushHeightShortcut))
            {
                HandleBrushHeight(e);
                e.Use();
                return;
            }

            var isCorrectMouseButton = GrassEditorHelper.IsMouseButtonPressed(toolSettings.grassMouseButton);

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
            var topCenter = center + new Vector3(0, toolSettings.BrushHeight);
            var bottomCenter = center;

            // 그리기 전에 현재 색상을 백업
            var originalColor = Handles.color;

            // 반투명 색상 적용
            Handles.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a);

            // 원기둥의 상단과 하단 그리기
            Handles.DrawSolidDisc(topCenter, Vector3.up, radius);
            Handles.DrawSolidDisc(bottomCenter, Vector3.up, radius);

            var segmentCount = 24;

            // 측면 그리기
            for (var i = 0; i < segmentCount; i++)
            {
                // 현재 세그먼트의 각도 계산
                var angleA = 360f / segmentCount * i;
                var angleB = 360f / segmentCount * (i + 1);

                // 각도에 따른 두 점 계산 (하단)
                var pointA = bottomCenter + Quaternion.Euler(0, angleA, 0) * Vector3.right * radius;
                var pointB = bottomCenter + Quaternion.Euler(0, angleB, 0) * Vector3.right * radius;

                // 각도에 따른 두 점 계산 (상단)
                var pointC = topCenter + Quaternion.Euler(0, angleA, 0) * Vector3.right * radius;
                var pointD = topCenter + Quaternion.Euler(0, angleB, 0) * Vector3.right * radius;

                // 측면을 반투명하게 그리기
                Handles.DrawAAConvexPolygon(pointA, pointB, pointD, pointC);
            }

            // 색상 복원
            Handles.color = originalColor;
        }

        private void HandleUndo()
        {
            if (grassCompute != null)
            {
                SceneView.RepaintAll();
                grassCompute.Reset();
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
            grassCompute.ClearAllData();
            Init();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Removed all grass from the scene");
        }

        public async UniTask UpdateProgress(int currentPoint, int totalPoints, string progressMessage)
        {
            _objectProgress.progress = (float)currentPoint / totalPoints;
            _objectProgress.progressMessage = $"{progressMessage} {currentPoint / (float)totalPoints * 100:F1}%";

            if (currentPoint % Math.Max(1, totalPoints / 100) == 0)
            {
                await UniTask.Yield(_cts.Token);
                Repaint();
            }
        }

        private async UniTask GenerateGrassOnObject(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var generator = new GrassGenerationOperation(this, grassCompute, toolSettings);
            await generator.GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RegenerateGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var removalOperation =
                new GrassRemovalOperation(this, grassCompute, _spatialGrid, toolSettings.PaintMask.value);
            await removalOperation.RemoveGrassFromObjects(selectedObjects);

            var generator = new GrassGenerationOperation(this, grassCompute, toolSettings);
            await generator.GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RemoveCurrentGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var removalOperation =
                new GrassRemovalOperation(this, grassCompute, _spatialGrid, toolSettings.PaintMask.value);
            await removalOperation.RemoveGrassFromObjects(selectedObjects);

            Init();
            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
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

            var grassData = new List<GrassData>(grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.color = GetRandomColor();
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying colors");
            }

            grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
        }

        private async UniTask ModifySize()
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var grassData = new List<GrassData>(grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < grassData.Count; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying size");
            }

            grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
        }

        private async UniTask ModifySizeAndColor()
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            var grassData = new List<GrassData>(grassCompute.GrassDataList);
            var totalCount = grassData.Count;

            for (var i = 0; i < totalCount; i++)
            {
                var newData = grassData[i];
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                newData.color = GetRandomColor();
                grassData[i] = newData;

                await UpdateProgress(i + 1, totalCount, "Modifying size and color");
            }

            grassCompute.GrassDataList = grassData;
            RebuildMesh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            _isProcessing = false;
        }

        private void HandleBrushSize(Event e)
        {
            toolSettings.BrushSize += e.delta.y;
            toolSettings.BrushSize = Mathf.Clamp(toolSettings.BrushSize, toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize);
        }

        private void HandleBrushHeight(Event e)
        {
            toolSettings.BrushHeight += e.delta.y;
            toolSettings.BrushHeight = Mathf.Clamp(toolSettings.BrushHeight, toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize);
        }

        private void StartPainting()
        {
            _isPainting = true;

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    _grassAddPainter ??= new GrassAddPainter(grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(grassCompute, "Add Grass");
                    break;
                case BrushOption.Remove:
                    _grassRemovePainter ??= new GrassRemovePainter(grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(grassCompute, "Remove Grass");
                    break;
                case BrushOption.Edit:
                    _grassEditPainter ??= new GrassEditPainter(grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(grassCompute, "Edit Grass");
                    break;
                case BrushOption.Reposition:
                    _grassRepositionPainter ??= new GrassRepositionPainter(grassCompute, _spatialGrid);
                    Undo.RegisterCompleteObjectUndo(grassCompute, "Reposition Grass");
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

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildMesh()
        {
            UpdateGrassData();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public void InitSpatialGrid()
        {
            // 필요한 컴포넌트들이 없다면 초기화하지 않음
            if (grassCompute == null || grassCompute.GrassDataList == null)
                return;

            var bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000));
            _spatialGrid = new SpatialGrid(bounds, toolSettings.BrushSize);
            var grassData = grassCompute.GrassDataList;

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
            grassCompute.Reset();
        }
    }
}