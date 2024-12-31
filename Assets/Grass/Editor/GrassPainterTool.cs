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

namespace Grass.Editor
{
    public class ObjectProgress
    {
        public float progress;
        public string progressMessage;
    }

    public class GrassPainterTool : EditorWindow
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
                (GrassPainterTool)GetWindow(typeof(GrassPainterTool), false, "Grass Tool", true);
            var icon = EditorGUIUtility.FindTexture("tree_icon");
            var mToolSettings =
                (GrassToolSettingSo)AssetDatabase.LoadAssetAtPath("Assets/Grass/Settings/Grass Tool Settings.asset",
                    typeof(GrassToolSettingSo));
            if (mToolSettings == null)
            {
                Debug.Log("creating new one");
                mToolSettings = CreateInstance<GrassToolSettingSo>();

                AssetDatabase.CreateAsset(mToolSettings, "Assets/Grass/Settings/Grass Tool Settings.asset");

                var terrain = FindAnyObjectByType<Terrain>();
                int layerCount = terrain != null && terrain.terrainData != null
                    ? terrain.terrainData.terrainLayers.Length
                    : 1;

                mToolSettings.CreateNewLayers(layerCount);

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

            var manualUpdate = new GUIContent
            {
                text = "Manual Update",
                tooltip = "Update all grass data. Performance may be slower with large amounts of grass data."
            };
            if (GUILayout.Button(manualUpdate, GUILayout.Height(40)))
            {
                Init();
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

            toolSettings = (GrassToolSettingSo)EditorGUILayout.ObjectField(
                "Grass Tool Settings", toolSettings, typeof(GrassToolSettingSo), false);

            grassCompute = (GrassComputeScript)EditorGUILayout.ObjectField(
                "Grass Compute Object", grassCompute, typeof(GrassComputeScript), true);

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

            if (GrassEditorHelper.DrawToggleButton("Enable Grass", _enableGrass, out var newEnableGrass))
            {
                _enableGrass = newEnableGrass;
                grassCompute.enabled = newEnableGrass;
            }

            if (GrassEditorHelper.DrawToggleButton("Paint Mode", _paintModeActive, out var newPaintMode))
            {
                _paintModeActive = newPaintMode;
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

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainToolbar()
        {
            var totalGrassContent = new GUIContent($"Total Grass Instances: {grassCompute.GrassDataList.Count:N0}",
                "Total number of grass instances in the scene");
            EditorGUILayout.LabelField(totalGrassContent);

            var currentBuffer = grassCompute.CurrentBufferCount;
            var maxBuffer = grassCompute.MaximumBufferSize;
            var bufferUsagePercent = (float)currentBuffer / maxBuffer * 100;

            var bufferContent = new GUIContent(EditorIcons.Cube)
            {
                text = $"Buffer Usage: {currentBuffer:N0} / {maxBuffer:N0} ({bufferUsagePercent:F1}%)",
                tooltip = "Current number of elements in the draw buffer / Maximum buffer size"
            };

            if (bufferUsagePercent >= 90)
            {
                var warningStyle = new GUIStyle
                {
                    normal = { textColor = new Color(1f, 0.4f, 0.4f) }
                };
                EditorGUILayout.LabelField(bufferContent, warningStyle);
            }
            else
            {
                EditorGUILayout.LabelField(bufferContent);
            }

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
                    DrawPaintEditPanel();
                    break;
                case GrassEditorTab.Modify:
                    DrawModifyPanel();
                    break;
                case GrassEditorTab.Generate:
                    DrawGeneratePanel();
                    break;
                case GrassEditorTab.GeneralSettings:
                    DrawGeneralSettingsPanel();
                    break;
            }
        }

        private void DrawPaintEditPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPanelHeader("Paint/Edit Tools");
                DrawPaintEditToolbar();
                DrawPaintEditContent();
            }
        }

        private void DrawPaintEditToolbar()
        {
            _selectedToolOption = (BrushOption)GUILayout.Toolbar(
                (int)_selectedToolOption,
                _toolbarStrings,
                GUILayout.Height(25)
            );
            EditorGUILayout.Space(5);
        }

        private void DrawPaintEditContent()
        {
            DrawCommonBrushSettings();

            switch (_selectedToolOption)
            {
                case BrushOption.Add:
                    DrawHitSettings();
                    DrawAddBrushSettings();
                    break;
                case BrushOption.Remove:
                    break;
                case BrushOption.Edit:
                    DrawHitSettings();
                    DrawEditBrushSettings();
                    break;
                case BrushOption.Reposition:
                    DrawHitSettings();
                    DrawRepositionBrushSettings();
                    break;
            }
        }

        private void DrawModifyPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPanelHeader("Modify Tools");
                DrawModifyToolbar();
                DrawModifyContent();
                DrawModifyApplyButton();

                var warningStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(EditorIcons.Warning), GUILayout.Width(25), GUILayout.Height(25));
                EditorGUILayout.LabelField(
                    "Warning: This will modify ALL grass instances in the current scene at once.",
                    warningStyle
                );
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawModifyToolbar()
        {
            _selectedModifyOption = (ModifyOption)GUILayout.Toolbar(
                (int)_selectedModifyOption,
                _modifyOptionStrings,
                GUILayout.Height(25)
            );
            EditorGUILayout.Space(5);
        }

        private void DrawModifyContent()
        {
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
        }

        private void DrawModifyApplyButton()
        {
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Apply Modify Options", GUILayout.Height(25)))
            {
                HandleModifyOptionsButton();
            }
        }

        private void DrawGeneratePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPanelHeader("Generate Tools");
                DrawGenerateToolbar();
                DrawGenerateContent();

                GrassEditorHelper.DrawHorizontalLine(Color.gray);
                DrawObjectOperations();
            }
        }

        private void DrawGenerateToolbar()
        {
            _selectedGenerateOption = (GenerateTab)GUILayout.Toolbar(
                (int)_selectedGenerateOption,
                _generateTabStrings,
                GUILayout.Height(25)
            );
            EditorGUILayout.Space(5);
        }

        private void DrawGenerateContent()
        {
            DrawGeneralSettings();
            DrawSurfaceAngleSettings();

            switch (_selectedGenerateOption)
            {
                case GenerateTab.Basic:
                    DrawBasicGenerateSettings();
                    break;
                case GenerateTab.TerrainLayers:
                    DrawTerrainLayerSettings();
                    break;
            }
        }

        private void DrawBasicGenerateSettings()
        {
            DrawHitSettings();
            DrawVertexSettings();
            DrawWidthHeightSliders();
            DrawBrushColorField();
            DrawColorVariationSliders();
        }

        private static void DrawPanelHeader(string toolName)
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            EditorGUILayout.LabelField(toolName, headerStyle, GUILayout.Height(25));
        }

        private void HandleModifyOptionsButton()
        {
            var grassModifyOperation = new GrassModifyOperation(this, grassCompute, toolSettings);

            var operation = _selectedModifyOption switch
            {
                ModifyOption.WidthHeight => new ModifyOperation(
                    "Modify Width and Height",
                    "Modify the width and height of all grass elements?",
                    "Modified Size",
                    grassModifyOperation.ModifySize),
                ModifyOption.Color => new ModifyOperation(
                    "Modify Color",
                    "Modify the color of all grass elements?",
                    "Modified Color",
                    grassModifyOperation.ModifyColor),
                ModifyOption.Both => new ModifyOperation(
                    "Modify Width Height and Color",
                    "Modify the width, height and color of all grass elements?",
                    "Modified Size and Color",
                    grassModifyOperation.ModifySizeAndColor),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (EditorUtility.DisplayDialog(operation.title, operation.message, "Yes", "No"))
            {
                Undo.RegisterCompleteObjectUndo(grassCompute, operation.undoMessage);
                _isProcessing = true;
                _cts = new CancellationTokenSource();

                operation.action().ContinueWith(() => { _isProcessing = false; }).Forget();
            }
        }

        private readonly struct ModifyOperation
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

            toolSettings.brushSizeShortcut =
                (KeyType)EditorGUILayout.EnumPopup(
                    new GUIContent("Brush Size Shortcut",
                        $"Hold {GrassEditorHelper.GetShortcutName(toolSettings.brushSizeShortcut)} + scroll to adjust size"),
                    toolSettings.brushSizeShortcut);
        }

        private void DrawSurfaceAngleSettings()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Surface Settings", EditorStyles.boldLabel);

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
            toolSettings.allowUndersideGrass = EditorGUILayout.Toggle(
                new GUIContent("Allow Underside Grass",
                    "When enabled, grass will also be generated on the bottom surfaces of objects (except Terrain)"),
                toolSettings.allowUndersideGrass);
        }

        private void DrawAddBrushSettings()
        {
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Brush Placement", EditorStyles.boldLabel);
            toolSettings.Density = GrassEditorHelper.IntSlider(
                "Density",
                "Amount of grass placed in one brush stroke",
                toolSettings.Density,
                toolSettings.MinDensity, toolSettings.MaxDensity
            );
            DrawGrassSpacingSlider();
            DrawSurfaceAngleSettings();
            DrawWidthHeightSliders();
            DrawGrassColorSettings();
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

            EditorGUILayout.LabelField("Width / Height", EditorStyles.boldLabel);
            toolSettings.GrassWidth = EditorGUILayout.Slider("Grass Width", toolSettings.GrassWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);

            if (toolSettings.GrassWidth > grassCompute.GrassSetting.maxWidth)
            {
                EditorGUILayout.HelpBox("Grass Width must be less than Blade Width Max in the General Settings",
                    MessageType.Warning, true);
            }

            toolSettings.GrassHeight = EditorGUILayout.Slider("Grass Height", toolSettings.GrassHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);

            if (toolSettings.GrassHeight > grassCompute.GrassSetting.maxHeight)
            {
                EditorGUILayout.HelpBox("Grass Height must be less than Blade Height Max in the General Settings",
                    MessageType.Warning, true);
            }
        }

        private void DrawEditBrushSettings()
        {
            EditorGUILayout.Separator();

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
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Brush Height", EditorStyles.boldLabel);

            toolSettings.BrushHeight = GrassEditorHelper.FloatSlider(
                "Brush Height",
                "Height limit for repositioning grass",
                toolSettings.BrushHeight,
                toolSettings.MinBrushSize,
                toolSettings.MaxBrushSize
            );

            toolSettings.brushHeightShortcut =
                (KeyType)EditorGUILayout.EnumPopup(
                    new GUIContent("Brush Height Shortcut",
                        $"Hold {GrassEditorHelper.GetShortcutName(toolSettings.brushHeightShortcut)} + scroll to adjust height"),
                    toolSettings.brushHeightShortcut);
        }

        private void DrawGeneralSettings()
        {
            toolSettings.GenerateGrassCount = EditorGUILayout.IntSlider(
                "Generate Grass Count",
                toolSettings.GenerateGrassCount,
                toolSettings.MinGrassAmountToGenerate,
                toolSettings.MaxGrassAmountToGenerate
            );
            DrawGrassSpacingSlider();
        }

        private void DrawGrassSpacingSlider()
        {
            toolSettings.GrassSpacing = GrassEditorHelper.FloatSlider(
                "Grass Spacing",
                "Minimum distance between grass placements",
                toolSettings.GrassSpacing,
                0.1f, 1f
            );
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

        private void DrawTerrainLayerSettings()
        {
            // Foldout을 DrawToggleButton으로 교체
            GrassEditorHelper.DrawFoldoutSection("Layer Settings Guide", () =>
            {
                EditorGUILayout.LabelField("• If Active is unchecked, this layer is ignored.");
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("• Height = 1: No change to GrassHeight, < 1: Scales down, > 1: Scales up",
                    EditorStyles.wordWrappedLabel);
            });

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

            if (terrain.terrainData == null)
            {
                EditorGUILayout.HelpBox("Terrain has no terrain data assigned.", MessageType.Warning);
                return;
            }

            var terrainLayers = terrain.terrainData.terrainLayers;
            if (terrainLayers == null || terrainLayers.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No terrain layers found. Click the button below to create a default layer.",
                    MessageType.Info
                );

                if (GUILayout.Button("Create Default Terrain Layer", GUILayout.Height(30)))
                {
                    CreateDefaultTerrainLayer(terrain);
                }

                return;
            }

            toolSettings.UpdateLayerCount(terrainLayers.Length);

            EditorGUILayout.Space(10);

            // 최소 너비 설정
            const float statusWidth = 40f;
            const float toggleWidth = 30f;
            const float minLayerNameWidth = 100f;
            const float minSliderWidth = 100f;

            // Column headers
            // Column headers
            EditorGUILayout.BeginHorizontal();

            // Status header
            EditorGUILayout.LabelField(
                new GUIContent("Status", "Layer will be skipped or painted with grass"),
                GUILayout.Width(statusWidth + 10)
            );

            // Layer Enable header
            EditorGUILayout.LabelField(
                new GUIContent("Active", "When enabled, grass will be generated on this layer"),
                GUILayout.Width(toggleWidth + 20)
            );

            // Terrain Layer header
            EditorGUILayout.LabelField(
                new GUIContent("Layer Name", "Terrain layer name"),
                GUILayout.MinWidth(minLayerNameWidth)
            );

            // Height header
            EditorGUILayout.LabelField(
                new GUIContent("Height", "0-100% of grass height"),
                GUILayout.Width(minSliderWidth)
            );

            EditorGUILayout.EndHorizontal();

            GrassEditorHelper.DrawHorizontalLine(Color.gray, 1, 2);

            for (var i = 0; i < terrainLayers.Length; i++)
            {
                DrawTerrainLayerRow(i, terrainLayers[i], statusWidth, toggleWidth, minLayerNameWidth, minSliderWidth);
            }
        }

        private static void CreateDefaultTerrainLayer(Terrain terrain)
        {
            // 기본 TerrainLayer 에셋 생성
            var defaultLayer = new TerrainLayer
            {
                name = "Default Layer",
                tileSize = new Vector2(15, 15)
            };

            // TerrainLayer 에셋 저장을 위한 폴더 생성
            if (!AssetDatabase.IsValidFolder("Assets/Terrain"))
            {
                AssetDatabase.CreateFolder("Assets", "Terrain");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Terrain/Layers"))
            {
                AssetDatabase.CreateFolder("Assets/Terrain", "Layers");
            }

            // 저장될 에셋 경로
            var assetPath = "Assets/Terrain/Layers/DefaultTerrainLayer.terrainlayer";

            // 에셋 저장
            AssetDatabase.CreateAsset(defaultLayer, assetPath);
            AssetDatabase.SaveAssets();

            // 테라인에 레이어 추가
            var terrainData = terrain.terrainData;
            var layers = new TerrainLayer[1];
            layers[0] = defaultLayer;
            terrainData.terrainLayers = layers;

            // 테라인 데이터 업데이트
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.Refresh();

            // 상세한 로그 메시지 출력
            Debug.Log($"Default terrain layer created at {assetPath}");
        }

        private void DrawTerrainLayerRow(int index, TerrainLayer layer, float statusWidth, float toggleWidth,
                                         float minNameWidth, float minSliderWidth)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(statusWidth + 10), GUILayout.ExpandWidth(true));
            var isSkipped = toolSettings.LayerEnabled[index] == false || toolSettings.HeightFading[index] <= 0f;
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
            EditorGUILayout.LabelField("●", statusStyle, GUILayout.MinWidth(10), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(isSkipped ? "Skip" : "Paint", statusStyle, GUILayout.Width(30),
                GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            toolSettings.LayerEnabled[index] = EditorGUILayout.Toggle(
                toolSettings.LayerEnabled[index], GUILayout.MinWidth(toggleWidth), GUILayout.ExpandWidth(true));

            var layerName = layer != null ? layer.name : $"Layer {index}";
            EditorGUILayout.LabelField(layerName, GUILayout.MinWidth(minNameWidth), GUILayout.ExpandWidth(true));

            toolSettings.HeightFading[index] = EditorGUILayout.Slider(
                toolSettings.HeightFading[index],
                0f,
                2f,
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

        private void DrawGeneralSettingsPanel()
        {
            var grassSetting = grassCompute.GrassSetting;

            GrassEditorHelper.DrawFoldoutSection("Global Season Settings", () => { DrawSeasonSettings(grassSetting); });

            GrassEditorHelper.DrawFoldoutSection("Blade Min/Max Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Blade Min/Max Settings");

                GrassEditorHelper.DrawMinMaxSection("Blade Width", ref grassSetting.minWidth,
                    ref grassSetting.maxWidth,
                    toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
                GrassEditorHelper.DrawMinMaxSection("Blade Height", ref grassSetting.minHeight,
                    ref grassSetting.maxHeight,
                    toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
                GrassEditorHelper.DrawMinMaxSection("Random Height", ref grassSetting.randomHeightMin,
                    ref grassSetting.randomHeightMax, 0, 1);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.BladeMinMaxSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Blade Shape Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Blade Shape Settings");

                GrassEditorHelper.FloatSlider(ref grassSetting.bladeRadius, "Blade Radius", "",
                    grassSetting.MinBladeRadius, grassSetting.MaxBladeRadius);
                GrassEditorHelper.FloatSlider(ref grassSetting.bladeForward, "Blade Forward", "",
                    grassSetting.MinBladeForward, grassSetting.MaxBladeForward);
                GrassEditorHelper.FloatSlider(ref grassSetting.bladeCurve, "Blade Curve", "",
                    grassSetting.MinBladeCurve, grassSetting.MaxBladeCurve);
                GrassEditorHelper.FloatSlider(ref grassSetting.bottomWidth, "Bottom Width", "",
                    grassSetting.MinBottomWidth, grassSetting.MaxBottomWidth);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.BladeShapeSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Wind Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Wind Settings");

                grassSetting.windSpeed = EditorGUILayout.Slider("Wind Speed",
                    grassSetting.windSpeed, grassSetting.MinWindSpeed, grassSetting.MaxWindSpeed);
                grassSetting.windStrength = EditorGUILayout.Slider("Wind Strength",
                    grassSetting.windStrength, grassSetting.MinWindStrength, grassSetting.MaxWindStrength);
                grassSetting.WindDirection =
                    EditorGUILayout.Slider("Wind Direction", grassSetting.WindDirection, 0f, 360f);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.WindSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Tint Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Tint Settings");

                grassSetting.topTint = EditorGUILayout.ColorField("Top Tint", grassSetting.topTint);
                grassSetting.bottomTint = EditorGUILayout.ColorField("Bottom Tint", grassSetting.bottomTint);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.TintSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Blend Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Blend Settings");

                grassSetting.ambientStrength =
                    GrassEditorHelper.FloatSlider("Ambient Strength", "", grassSetting.ambientStrength, 0f, 1f);
                grassSetting.blendMultiply =
                    GrassEditorHelper.FloatSlider("Blend Multiply", "", grassSetting.blendMultiply, 0f, 5f);
                grassSetting.blendOffset =
                    GrassEditorHelper.FloatSlider("Blend Offset", "", grassSetting.blendOffset, 0f, 1f);
                if (grassSetting.materialToUse.HasProperty(GrassShaderPropertyID.AmbientAdjustmentColor))
                {
                    grassSetting.ambientAdjustmentColor = EditorGUILayout.ColorField("Ambient Adjustment Color",
                        grassSetting.ambientAdjustmentColor);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.BlendSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Shadow Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Shadow Settings");

                grassSetting.shadowDistance =
                    GrassEditorHelper.FloatSlider("Shadow Distance", "", grassSetting.shadowDistance, 0f, 300f);
                grassSetting.shadowFadeRange =
                    GrassEditorHelper.FloatSlider("Shadow Fade Range", "", grassSetting.shadowFadeRange, 0.1f, 30f);
                grassSetting.shadowBrightness =
                    GrassEditorHelper.FloatSlider("Shadow Brightness", "", grassSetting.shadowBrightness, 0f, 1f);
                grassSetting.shadowColor = EditorGUILayout.ColorField("Shadow Color", grassSetting.shadowColor);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.ShadowSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Additional Light Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Additional Light Settings");

                grassSetting.additionalLightIntensity =
                    GrassEditorHelper.FloatSlider("Light Intensity", "", grassSetting.additionalLightIntensity, 0f, 1f);
                grassSetting.additionalLightShadowStrength =
                    GrassEditorHelper.FloatSlider("Shadow Strength", "", grassSetting.additionalLightShadowStrength, 0f,
                        1f);
                grassSetting.additionalLightShadowColor =
                    EditorGUILayout.ColorField("Shadow Color", grassSetting.additionalLightShadowColor);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.AdditionalLightSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection("Specular Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Specular Settings");

                grassSetting.glossiness =
                    GrassEditorHelper.FloatSlider("Glossiness", "", grassSetting.glossiness, 0f, 10f);
                grassSetting.specularStrength =
                    GrassEditorHelper.FloatSlider("Specular Strength", "", grassSetting.specularStrength, 0f, 1f);
                grassSetting.specularHeight =
                    GrassEditorHelper.FloatSlider("Specular Height", "", grassSetting.specularHeight, 0f, 1f);

                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.SpecularSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection(new GUIContent(EditorIcons.Cube) { text = "Blade Amount Settings" },
                () =>
                {
                    EditorGUI.BeginChangeCheck();
                    Undo.RecordObject(grassSetting, "Blade Amount Settings");

                    grassSetting.bladesPerVertex = EditorGUILayout.IntSlider("Blades Per Vertex",
                        grassSetting.bladesPerVertex, grassSetting.MinBladesPerVertex, grassSetting.MaxBladesPerVertex);
                    grassSetting.segmentsPerBlade = EditorGUILayout.IntSlider("Segments Per Blade",
                        grassSetting.segmentsPerBlade, grassSetting.MinSegmentsPerBlade,
                        grassSetting.MaxSegmentsPerBlade);
                    if (EditorGUI.EndChangeCheck())
                    {
                        grassCompute.BladeAmountSetting();
                        EditorUtility.SetDirty(grassSetting);
                    }
                });
            GrassEditorHelper.DrawFoldoutSection(new GUIContent(EditorIcons.Cube) { text = "LOD Settings" }, () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "LOD Settings");

                GrassLODSettingEditor.DrawLODSettingsPanel(grassSetting);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.LODSetting();
                    EditorUtility.SetDirty(grassSetting);
                }
            });
            GrassEditorHelper.DrawFoldoutSection(new GUIContent(EditorIcons.Cube) { text = "Culling Settings" },
                () =>
                {
                    EditorGUILayout.LabelField(
                        "• Higher values create smaller grid cells for more precise culling, but may impact performance.",
                        EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField(
                        "• Lower values create larger grid cells for faster culling, but with less accuracy.",
                        EditorStyles.wordWrappedLabel);

                    // Visualization toggle

                    EditorGUILayout.BeginHorizontal();
                    var showCullingBounds = new GUIContent(EditorIcons.Gizmos)
                    {
                        text = "Show Culling Bounds",
                    };
                    if (GrassEditorHelper.DrawToggleButton(showCullingBounds, grassSetting.drawBounds,
                            out var showDrawBounds))
                    {
                        grassSetting.drawBounds = showDrawBounds;
                    }

                    var showAllCullingBoundsText = new GUIContent(EditorIcons.Gizmos)
                    {
                        text = "Show All Bounds",
                    };
                    if (GrassEditorHelper.DrawToggleButton(showAllCullingBoundsText, grassSetting.drawAllBounds,
                            out var showAllCullingBounds))
                    {
                        grassSetting.drawAllBounds = showAllCullingBounds;
                    }

                    EditorGUILayout.EndHorizontal();

                    grassSetting.cullingTreeDepth =
                        EditorGUILayout.IntSlider("Depth", grassSetting.cullingTreeDepth, 1, 10);
                });
            GrassEditorHelper.DrawFoldoutSection("Other Settings", () =>
            {
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(grassSetting, "Interactor Strength");

                grassSetting.interactorStrength = GrassEditorHelper.FloatSlider("Interactor Strength", "",
                    grassSetting.interactorStrength, 0f, 1f);

                grassSetting.castShadow = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                    "Shadow Settings", grassSetting.castShadow);
                if (EditorGUI.EndChangeCheck())
                {
                    grassCompute.InteractorStrengthSetting();
                    EditorUtility.SetDirty(grassSetting);
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
                if (GUILayout.Button("Create Season Manager"))
                {
                    grassSetting.seasonSettings.Clear();
                    CreateSeasonManager();
                    Init();
                }

                return;
            }

            EditorGUI.BeginChangeCheck();

            // Note: This max value (20) must match the MAX_ZONES define in the grass compute shader.
            // Search #define MAX_ZONES 20
            grassSetting.maxZoneCount = GrassEditorHelper.IntSlider("Max Zone Count",
                "Maximum number of Season Zones allowed in the scene.", grassSetting.maxZoneCount, 1, 20);

            GrassEditorHelper.DrawSeasonSettings(grassSetting.seasonSettings, grassSetting);
            if (EditorGUI.EndChangeCheck())
            {
                _seasonManager.Init();
                EditorUtility.SetDirty(grassSetting);
            }
        }

        private static void CreateSeasonManager()
        {
            // Create Season Controller
            var seasonManager = new GameObject("Grass Season Manager");
            var zoneObject = new GameObject("Grass Season Zone");
            zoneObject.transform.SetParent(seasonManager.transform);

            zoneObject.AddComponent<GrassSeasonZone>();
            seasonManager.AddComponent<GrassSeasonManager>();
            // Set initial transform values for zone
            zoneObject.transform.localPosition = Vector3.zero;
            zoneObject.transform.localScale = new Vector3(10f, 10f, 10f);

            // Register both objects for undo
            Undo.RegisterCreatedObjectUndo(seasonManager, "Create Season Manager");
            Selection.activeGameObject = seasonManager;
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

            if (currentPoint % Mathf.Max(1, totalPoints / 100) == 0)
            {
                await UniTask.Yield(_cts.Token);
                Repaint();
            }
        }

        private async UniTask GenerateGrassOnObject(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            await GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RegenerateGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            await RemoveGrass(selectedObjects);
            InitSpatialGrid();
            await GenerateGrass(selectedObjects);

            Init();

            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask RemoveCurrentGrass(GameObject[] selectedObjects)
        {
            _isProcessing = true;
            _cts = new CancellationTokenSource();

            await RemoveGrass(selectedObjects);

            Init();
            _isProcessing = false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private async UniTask GenerateGrass(GameObject[] selectedObjects)
        {
            // MeshFilter와 Terrain 객체 분리
            var meshObjects = new List<MeshFilter>();
            var terrainObjects = new List<Terrain>();

            foreach (var obj in selectedObjects)
            {
                if (obj.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    meshObjects.Add(meshFilter);
                }

                if (obj.TryGetComponent<Terrain>(out var terrain))
                {
                    terrainObjects.Add(terrain);
                }
            }

            // 병렬로 실행할 Task 생성
            var generationTasks = new List<UniTask>();

            // MeshFilter 오브젝트가 있을 때만 generator 생성
            if (meshObjects.Count > 0)
            {
                var meshGenerator = new GrassGenerationOperation(this, grassCompute, toolSettings, _spatialGrid);
                generationTasks.Add(meshGenerator.GenerateGrass(meshObjects));
            }

            // Terrain 오브젝트가 있을 때만 generator 생성
            if (terrainObjects.Count > 0)
            {
                var terrainGenerator =
                    new TerrainGrassGenerationOperation(this, grassCompute, toolSettings, _spatialGrid);
                generationTasks.Add(terrainGenerator.GenerateGrass(terrainObjects));
            }

            // 생성할 작업이 있을 때만 WhenAll 실행
            if (generationTasks.Count > 0)
            {
                await UniTask.WhenAll(generationTasks);
            }
        }

        private async UniTask RemoveGrass(GameObject[] selectedObjects)
        {
            // MeshFilter와 Terrain 객체 분리
            var meshObjects = new List<MeshFilter>();
            var terrainObjects = new List<Terrain>();

            foreach (var obj in selectedObjects)
            {
                if (obj.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    meshObjects.Add(meshFilter);
                }

                if (obj.TryGetComponent<Terrain>(out var terrain))
                {
                    terrainObjects.Add(terrain);
                }
            }

            // 병렬로 실행할 Task 생성
            var removalTasks = new List<UniTask>();

            // MeshFilter 오브젝트가 있을 때만 removal operation 생성
            if (meshObjects.Count > 0)
            {
                var meshRemoval =
                    new GrassRemovalOperation(this, grassCompute, _spatialGrid);
                removalTasks.Add(meshRemoval.RemoveGrass(meshObjects));
            }

            // Terrain 오브젝트가 있을 때만 removal operation 생성
            if (terrainObjects.Count > 0)
            {
                var terrainRemoval = new TerrainGrassRemovalOperation(this, grassCompute, _spatialGrid);
                removalTasks.Add(terrainRemoval.RemoveGrass(terrainObjects));
            }

            // 제거할 작업이 있을 때만 WhenAll 실행
            if (removalTasks.Count > 0)
            {
                await UniTask.WhenAll(removalTasks);
            }
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
                    Init();
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