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

        public List<GrassData> grassData = new();

        private int _grassAmount;

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
                _grassCompute.GrassDataList = grassData;
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
                    grassData = _grassCompute.GrassDataList;
                    _grassAmount = grassData.Count;
                }
                else
                {
                    grassData.Clear();
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

            EditorGUILayout.LabelField("Total Grass Amount: " + _grassAmount, EditorStyles.label);

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
                // no objects selected
                if (selection != null)
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


        private void CreateNewGrassObject()
        {
            grassObject = new GameObject
            {
                name = "Grass System - Holder"
            };
            _grassCompute = grassObject.AddComponent<GrassComputeScript>();

            // setup object
            grassData = new List<GrassData>();
            _grassCompute.GrassDataList = grassData;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (hasFocus && _paintModeActive)
            {
                DrawHandles();
            }
        }
        
        // draw the painter handles
        private void DrawHandles()
        {
            if(Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
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

#if UNITY_EDITOR
        private void HandleUndo()
        {
            if (_grassCompute != null)
            {
                SceneView.RepaintAll();
                _grassCompute.Reset();
            }
        }

        private void OnEnable()
        {
            ResetMaterialEditor();
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;
        }

        private void OnFocus()
        {
            ResetMaterialEditor();
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
            if (_materialEditor != null)
            {
                DestroyImmediate(_materialEditor);
            }
        }

        private void ClearMesh()
        {
            Undo.RegisterCompleteObjectUndo(this, "Cleared Grass");
            _grassAmount = 0;
            grassData.Clear();
            _grassCompute.GrassDataList = grassData;
            _grassCompute.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private readonly Collider[] _generateColliders = new Collider[1];

        private void GeneratePositions(GameObject selection)
        {
            var selectionLayer = selection.layer;
            var paintMask = toolSettings.PaintMask.value;
            var paintBlockMask = toolSettings.PaintBlockMask.value;

            if (((1 << selectionLayer) & paintMask) == 0) return;

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

                var bounds = sourceMesh.sharedMesh.bounds;

                var meshSize = new Vector3(
                    bounds.size.x * sourceMesh.transform.lossyScale.x,
                    bounds.size.y * sourceMesh.transform.lossyScale.y,
                    bounds.size.z * sourceMesh.transform.lossyScale.z
                );
                meshSize += Vector3.one;

                var meshVolume = meshSize.x * meshSize.y * meshSize.z;
                var floorToInt = Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity);
                var numPoints = Mathf.Min(floorToInt, toolSettings.GrassAmountToGenerate);
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

                    if (worldNormal.y <= 1 + toolSettings.NormalLimit &&
                        worldNormal.y >= 1 - toolSettings.NormalLimit)
                    {
                        if (newPoint != Vector3.zero)
                        {
                            newData.color = GetRandomColor();
                            newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight) *
                                             widthHeight[0];
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
                widthHeight.Dispose();
            }

            else if (selection.TryGetComponent(out Terrain terrain))
            {
                var meshVolume = terrain.terrainData.size.x * terrain.terrainData.size.y *
                                 terrain.terrainData.size.z;
                var numPoints = Mathf.Min(Mathf.FloorToInt(meshVolume * toolSettings.GenerationDensity),
                    toolSettings.GrassAmountToGenerate);

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
                        getFadeMap += Convert.ToInt32(toolSettings.LayerFading[i]) * maps[0, 0, i];
                        if (maps[0, 0, i] > toolSettings.LayerBlocking[i])
                        {
                            newPoint = Vector3.zero;
                        }
                    }

                    if (newNormal.y <= 1 + toolSettings.NormalLimit && newNormal.y >= 1 - toolSettings.NormalLimit)
                    {
                        var fade = Mathf.Clamp(getFadeMap, 0, 1f);
                        newData.color = GetRandomColor();
                        newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight * fade);
                        newData.normal = newNormal;
                        if (newPoint != Vector3.zero)
                        {
                            grassData.Add(newData);
                        }
                    }
                }
            }

            RebuildMesh();
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

            grassData.RemoveAll(g => worldBounds.Contains(g.position));
        }

        private Vector3 GetRandomColor()
        {
            Color newRandomCol = new(toolSettings.AdjustedColor.r + Random.Range(0, 1.0f) * toolSettings.RangeR,
                toolSettings.AdjustedColor.g + Random.Range(0, 1.0f) * toolSettings.RangeG,
                toolSettings.AdjustedColor.b + Random.Range(0, 1.0f) * toolSettings.RangeB, 1);
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
                newData.widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight);
                grassData[i] = newData;
            }

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
                            RemoveAtPoint(e);
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
                    RebuildMesh();
                }
            }
        }
        
        private void RemoveAtPoint(Event e)
        {
            if(Physics.Raycast(_mousePointRay,out var hit,float.MaxValue, toolSettings.PaintMask.value))
            {
                _hitPos = hit.point;
                _hitNormal = hit.normal;
                RemovePositionsNearRayCastHit(_hitPos, toolSettings.BrushSize);
            }

            e.Use();
        }

        private void RemovePositionsNearRayCastHit(Vector3 hitPoint, float radius)
        {
            // Remove positions within the specified radius
            for (var i = grassData.Count - 1; i >= 0; i--)
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
                widthHeight = new Vector2(toolSettings.GrassWidth, toolSettings.GrassHeight),
                normal = normal
            };
        }

        private void AddGrass(Event e)
        {
            var paintMaskValue = toolSettings.PaintMask.value;
            var brushSize = toolSettings.BrushSize;
            var density = toolSettings.Density;
            var normalLimit = toolSettings.NormalLimit;

            // Position to shoot rays from (you can modify this as needed)
            var startPos = _hitPos + Vector3.up * 3f; // Starting a bit above the hit position

            // Check if we've moved far enough to paint again
            var distanceMoved = Vector3.Distance(_lastPosition, startPos);
            if (distanceMoved >= brushSize)
            {
                for (var i = 0; i < density; i++)
                {
                    // Random point within the brush size to shoot rays
                    var randomPoint = Random.insideUnitCircle * brushSize;
                    var randomPos = new Vector3(startPos.x + randomPoint.x, startPos.y, startPos.z + randomPoint.y);

                    if (Physics.Raycast(_mousePointRay, out var hit2, 200f))
                    {
                        var hitLayer = hit2.transform.gameObject.layer;
                        if (((1 << hitLayer) & paintMaskValue) != 0)
                        {
                            if (((1 << hitLayer) & paintMaskValue) > 0 && _hitNormal.y <= 1 + normalLimit &&
                                _hitNormal.y >= 1 - normalLimit)
                            {
                                // Cast a ray downward
                                if (Physics.Raycast(randomPos + Vector3.up * 3f, Vector3.down, out var hit,
                                        float.MaxValue))
                                {
                                    var hitLayer2 = hit.collider.gameObject.layer;

                                    // Check if the hit is in paintMask
                                    if (((1 << hitLayer2) & paintMaskValue) != 0)
                                    {
                                        _hitPos = hit.point;
                                        _hitNormal = hit.normal;
                                        var newData = CreateGrassData(_hitPos, _hitNormal);
                                        grassData.Add(newData);
                                    }
                                }
                            }
                        }
                    }
                }

                // Update the last paint position
                _lastPosition = startPos;
            }

            e.Use();
        }

        private readonly Dictionary<int, float> _cumulativeChanges = new();

        private void EditGrassPainting(Event e)
        {
            if (!Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
                return;

            var hitPos = hit.point;
            var brushSize = toolSettings.BrushSize;
            var brushSizeSqr = brushSize * brushSize;
            var brushFalloffSize = toolSettings.BrushFalloffSize * brushSize;
            var brushFalloffSizeSqr = brushFalloffSize * brushFalloffSize;
            var adjustSize = new Vector2(toolSettings.AdjustWidth, toolSettings.AdjustHeight);
            var adjustColor = toolSettings.AdjustedColor;
            var colorRange = new Vector3(toolSettings.RangeR, toolSettings.RangeG, toolSettings.RangeB);

            var editColor = _selectedEditOption is EditOption.EditColors or EditOption.Both;
            var editSize = _selectedEditOption is EditOption.EditWidthHeight or EditOption.Both;

            for (var j = 0; j < grassData.Count; j++)
            {
                var grassPos = grassData[j].position;
                var distSqr = (hitPos - grassPos).sqrMagnitude;

                if (distSqr <= brushSizeSqr)
                {
                    var currentData = grassData[j];
                    var targetData = currentData; // 목표 상태를 현재 상태로 초기화

                    // 새로운 목표 값 계산
                    if (editColor)
                    {
                        var newCol = new Color(
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

                    // 누적 변화 계산
                    _cumulativeChanges.TryAdd(j, 0f);

                    float changeSpeed;
                    if (distSqr <= brushFalloffSizeSqr)
                    {
                        changeSpeed = 1f; // 내부 영역: 빠른 변화
                    }
                    else
                    {
                        changeSpeed = toolSettings.FalloffOuterSpeed; // 외부 영역: 느린 변화
                    }

                    _cumulativeChanges[j] = Mathf.Clamp01(_cumulativeChanges[j] + changeSpeed * Time.deltaTime);

                    // 변화 적용
                    var newData = new GrassData
                    {
                        position = currentData.position,
                        normal = currentData.normal,
                        color = Vector3.Lerp(currentData.color, targetData.color, _cumulativeChanges[j]),
                        widthHeight = Vector2.Lerp(currentData.widthHeight, targetData.widthHeight, _cumulativeChanges[j])
                    };

                    grassData[j] = newData;
                }
            }

            e.Use();
        }


        private void ReprojectGrassPainting(Event e)
        {
            if(Physics.Raycast(_mousePointRay, out var hit, float.MaxValue, toolSettings.PaintMask.value))
            {
                _hitPos = hit.point;
                _hitNormal = hit.normal;

                for (var j = 0; j < grassData.Count; j++)
                {
                    var pos = grassData[j].position;
                    //  pos += grassObject.transform.position;
                    var dist = Vector3.Distance(hit.point, pos);

                    // if its within the radius of the brush, raycast to a new position
                    if (dist <= toolSettings.BrushSize)
                    {
                        var meshPoint = new Vector3(pos.x, pos.y + toolSettings.ReprojectOffset, pos.z);
                        if (Physics.Raycast(meshPoint, Vector3.down, out var hitInfo, 200f,
                                toolSettings.PaintMask.value))
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
            if (Physics.Raycast(_mousePointRay, out var terrainHit, 100f, toolSettings.PaintMask.value))
            {
                _hitPos = terrainHit.point;
                _hitNormal = terrainHit.normal;

                var pos = point.position;
                // pos += grassObject.transform.position;
                var dist = Vector3.Distance(terrainHit.point, pos);

                // if its within the radius of the brush, remove all info
                return dist <= toolSettings.BrushSize;
            }

            return false;
        }

        private void RebuildMesh()
        {
            _grassAmount = grassData.Count;
            _grassCompute.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildMeshFast()
        {
            _grassAmount = grassData.Count;
            _grassCompute.ResetFaster();
        }

#endif
    }
}
