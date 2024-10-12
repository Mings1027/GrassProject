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
        // main tabs
        private readonly string[] _mainTabBarStrings =
            { "Paint/Edit", "Modify", "Generate", "General Settings", "Material Settings" };

        private int _mainTabCurrent;
        private Vector2 _scrollPos;

        private bool _paintModeActive;
        private bool _enableGrass;

        private readonly string[] _toolbarStrings = { "Add", "Remove", "Edit", "Reproject" };

        private readonly string[] _toolbarStringsEdit = { "Edit Colors", "Edit Length/Width", "Both" };

        private Vector3 _hitPos;
        private Vector3 _hitNormal;

        [SerializeField] private GrassToolSettingSO toolSettings;

        // options
        private int _toolbarInt;
        private int _toolbarIntEdit;

        public List<GrassData> grassData = new();

        private int _grassAmount;

        private Ray _mousePointRay;

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
                (GrassToolSettingSO)AssetDatabase.LoadAssetAtPath("Assets/Grass/Settings/Grass Tool Settings.asset",
                    typeof(GrassToolSettingSO));
            if (mToolSettings == null)
            {
                Debug.Log("creating new one");
                mToolSettings = CreateInstance<GrassToolSettingSO>();

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
                _grassComputeScript.GrassDataList = grassData;
                _grassComputeScript.Reset();
            }

            toolSettings = (GrassToolSettingSO)EditorGUILayout.ObjectField("Grass Tool Settings", toolSettings,
                typeof(GrassToolSettingSO), false);

            grassObject =
                (GameObject)EditorGUILayout.ObjectField("Grass Compute Object", grassObject, typeof(GameObject), true);

            if (grassObject == null)
            {
                grassObject = FindFirstObjectByType<GrassComputeScript>()?.gameObject;
            }

            if (grassObject != null)
            {
                _grassComputeScript = grassObject.GetComponent<GrassComputeScript>();
                _grassComputeScript.currentPresets = (GrassSettingSO)EditorGUILayout.ObjectField(
                    "Grass Settings Object",
                    _grassComputeScript.currentPresets, typeof(GrassSettingSO), false);

                if (_grassComputeScript.GrassDataList.Count > 0)
                {
                    grassData = _grassComputeScript.GrassDataList;
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

            _grassComputeScript.currentPresets.materialToUse = (Material)EditorGUILayout.ObjectField("Grass Material",
                _grassComputeScript.currentPresets.materialToUse, typeof(Material), false);

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable Grass:", EditorStyles.boldLabel);
            _enableGrass = EditorGUILayout.Toggle(_enableGrass);
            if (_enableGrass && !_grassComputeScript.enabled)
            {
                _grassComputeScript.enabled = true;
            }
            else if (!_enableGrass && _grassComputeScript.enabled)
            {
                _grassComputeScript.enabled = false;
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
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel);
            _paintModeActive = EditorGUILayout.Toggle(_paintModeActive);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Update", EditorStyles.boldLabel);
            _grassComputeScript.autoUpdate = EditorGUILayout.Toggle(_grassComputeScript.autoUpdate);
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
                        for (int i = 0; i < selectedObjects.Length; i++)
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
            toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
            toolSettings.sizeHeight = EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
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
                (int)EditorGUILayout.Slider("Grass Place Max Amount", toolSettings.grassAmountToGenerate, 0, 100000);
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
                (GrassToolSettingSO.VertexColorSetting)EditorGUILayout.EnumPopup("Block On vertex Colors",
                    toolSettings.vertexColorSettings);
            toolSettings.vertexFade =
                (GrassToolSettingSO.VertexColorSetting)EditorGUILayout.EnumPopup("Fade on Vertex Colors",
                    toolSettings.vertexFade);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
            toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth,
                toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
            toolSettings.sizeHeight = EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight,
                toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);
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
            _toolbarInt = GUILayout.Toolbar(_toolbarInt, _toolbarStrings, GUILayout.Height(25));

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush Size");
            toolSettings.brushSize = EditorGUILayout.Slider(toolSettings.brushSize,
                toolSettings.MinBrushSize, toolSettings.MaxBrushSize);
            EditorGUILayout.EndHorizontal();

            if (_toolbarInt == 0)
            {
                toolSettings.normalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.normalLimit, 0f, 1f);
                toolSettings.density = EditorGUILayout.IntSlider("Density", toolSettings.density, 1, 100);
            }

            if (_toolbarInt == 2)
            {
                _toolbarIntEdit = GUILayout.Toolbar(_toolbarIntEdit, _toolbarStringsEdit);
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Soft Falloff Settings", EditorStyles.boldLabel);
                toolSettings.brushFalloffSize =
                    EditorGUILayout.Slider("Brush Falloff Size", toolSettings.brushFalloffSize, 0.01f, 1f);
                toolSettings.flow = EditorGUILayout.Slider("Brush Flow", toolSettings.flow, 0.1f, 10f);
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Adjust Width and Height Gradually", EditorStyles.boldLabel);
                toolSettings.AdjustWidth =
                    EditorGUILayout.Slider("Grass Width Adjustment", toolSettings.AdjustWidth, toolSettings.MinAdjust,
                        toolSettings.MaxAdjust);
                toolSettings.AdjustHeight =
                    EditorGUILayout.Slider("Grass Height Adjustment", toolSettings.AdjustHeight, -1f, 1f);

                var labelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true
                };

                EditorGUILayout.LabelField("Grass Width Adjustment Max Clamp", labelStyle);
                toolSettings.adjustWidthMax = EditorGUILayout.Slider(toolSettings.adjustWidthMax, 0.01f, 3f);

                EditorGUILayout.LabelField("Grass Height Adjustment Max Clamp", labelStyle);
                toolSettings.adjustHeightMax = EditorGUILayout.Slider(toolSettings.adjustHeightMax, 0.01f, 3f);

                EditorGUILayout.Separator();
            }

            if (_toolbarInt is 0 or 2)
            {
                EditorGUILayout.Separator();

                if (_toolbarInt == 0)
                {
                    EditorGUILayout.LabelField("Width and Height ", EditorStyles.boldLabel);
                    toolSettings.sizeWidth = EditorGUILayout.Slider("Grass Width", toolSettings.sizeWidth,
                        toolSettings.MinSizeWidth, toolSettings.MaxSizeWidth);
                    toolSettings.sizeHeight = EditorGUILayout.Slider("Grass Height", toolSettings.sizeHeight,
                        toolSettings.MinSizeHeight, toolSettings.MaxSizeHeight);

                    var curPresets = _grassComputeScript.currentPresets;

                    if (toolSettings.sizeHeight > curPresets.maxHeight)
                    {
                        EditorGUILayout.HelpBox(
                            "Grass Height must be less than Blade Height Max in the General Settings",
                            MessageType.Warning, true);
                    }
                }

                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                toolSettings.adjustedColor = EditorGUILayout.ColorField("Brush Color", toolSettings.adjustedColor);
                EditorGUILayout.LabelField("Random Color Variation", EditorStyles.boldLabel);
                toolSettings.rangeR = EditorGUILayout.Slider("Red", toolSettings.rangeR, 0f, 1f);
                toolSettings.rangeG = EditorGUILayout.Slider("Green", toolSettings.rangeG, 0f, 1f);
                toolSettings.rangeB = EditorGUILayout.Slider("Blue", toolSettings.rangeB, 0f, 1f);
            }

            if (_toolbarInt == 3)
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
            var curPresets = _grassComputeScript.currentPresets;

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
            if (_grassComputeScript == null || _grassComputeScript.currentPresets == null)
            {
                EditorGUILayout.HelpBox("Grass Compute Script or Current Presets is not set.", MessageType.Warning);
                return;
            }

            if (_grassComputeScript.currentPresets.materialToUse != _currentMaterial || _materialEditor == null)
            {
                ResetMaterialEditor();
                _currentMaterial = _grassComputeScript.currentPresets.materialToUse;
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

                for (int i = 0; i < _materialProperties.Length; i++)
                {
                    MaterialProperty prop = _materialProperties[i];
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
            _grassComputeScript = grassObject.AddComponent<GrassComputeScript>();

            // setup object
            grassData = new List<GrassData>();
            _grassComputeScript.GrassDataList = grassData;
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
            var hits = Physics.RaycastNonAlloc(_mousePointRay, _results, 200f, toolSettings.paintMask.value);
            for (var i = 0; i < hits; i++)
            {
                _hitPos = _results[i].point;
                _hitNormal = _results[i].normal;
            }

            //base
            var discColor = Color.green;
            Color discColor2 = new(0, 0.5f, 0, 0.4f);
            switch (_toolbarInt)
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
                    Handles.DrawSolidDisc(_hitPos, _hitNormal, toolSettings.brushFalloffSize * toolSettings.brushSize);

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
            ResetMaterialEditor();
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.duringSceneGui += OnScene;
            Undo.undoRedoPerformed += HandleUndo;
            _terrainHit = new RaycastHit[10];
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
            _grassComputeScript.GrassDataList = grassData;
            _grassComputeScript.Reset();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private readonly Collider[] _generateColliders = new Collider[1];

        private void GeneratePositions(GameObject selection)
        {
            var selectionLayer = selection.layer;
            var paintMask = toolSettings.paintMask.value;
            var paintBlockMask = toolSettings.paintBlockMask.value;

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
            }

            else if (selection.TryGetComponent(out Terrain terrain))
            {
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

            public GrassToolSettingSO.VertexColorSetting vertexColorSettings;
            public GrassToolSettingSO.VertexColorSetting vertexFade;

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
                    case GrassToolSettingSO.VertexColorSetting.Red:
                        if (meshColors[meshTriangles[triIndex * 3]].r > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case GrassToolSettingSO.VertexColorSetting.Green:
                        if (meshColors[meshTriangles[triIndex * 3]].g > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                    case GrassToolSettingSO.VertexColorSetting.Blue:
                        if (meshColors[meshTriangles[triIndex * 3]].b > 0.5f)
                        {
                            point[0] = Vector3.zero;
                            return;
                        }

                        break;
                }

                switch (vertexFade)
                {
                    case GrassToolSettingSO.VertexColorSetting.Red:
                        var red = meshColors[meshTriangles[triIndex * 3]].r;
                        var red2 = meshColors[meshTriangles[triIndex * 3 + 1]].r;
                        var red3 = meshColors[meshTriangles[triIndex * 3 + 2]].r;

                        lengthWidth[0] = 1.0f - (red + red2 + red3) * 0.3f;
                        break;
                    case GrassToolSettingSO.VertexColorSetting.Green:
                        var green = meshColors[meshTriangles[triIndex * 3]].g;
                        var green2 = meshColors[meshTriangles[triIndex * 3 + 1]].g;
                        var green3 = meshColors[meshTriangles[triIndex * 3 + 2]].g;

                        lengthWidth[0] = 1.0f - (green + green2 + green3) * 0.3f;
                        break;
                    case GrassToolSettingSO.VertexColorSetting.Blue:
                        var blue = meshColors[meshTriangles[triIndex * 3]].b;
                        var blue2 = meshColors[meshTriangles[triIndex * 3 + 1]].b;
                        var blue3 = meshColors[meshTriangles[triIndex * 3 + 2]].b;

                        lengthWidth[0] = 1.0f - (blue + blue2 + blue3) * 0.3f;
                        break;
                    case GrassToolSettingSO.VertexColorSetting.None:
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
                _mousePointRay = scene.camera.ScreenPointToRay(_mousePos);

                if (e.type == EventType.ScrollWheel && e.control && e.alt)
                {
                    toolSettings.brushSize += e.delta.y;
                    toolSettings.brushSize = Mathf.Clamp(toolSettings.brushSize, toolSettings.MinBrushSize,
                        toolSettings.MaxBrushSize);
                    e.Use();
                    return;
                }

                // undo system
                if (e.type == EventType.MouseDown && e.button == 0 && e.control && e.alt)
                {
                    switch (_toolbarInt)
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
                    switch (_toolbarInt)
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
            var hits = Physics.RaycastNonAlloc(_mousePointRay, terrainHit, 100f, toolSettings.paintMask.value);
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
            var paintMaskValue = toolSettings.paintMask.value;
            var brushSize = toolSettings.brushSize;
            var density = toolSettings.density;
            var normalLimit = toolSettings.normalLimit;

            // Position to shoot rays from (you can modify this as needed)
            Vector3 startPos = _hitPos + Vector3.up * 3f; // Starting a bit above the hit position

            var distanceMoved = Vector3.Distance(_lastPosition, startPos);
            if (distanceMoved < brushSize * 0.5f) return;
            for (int i = 0; i < density; i++)
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
                            if (Physics.Raycast(randomPos + Vector3.up * 3f, Vector3.down, out var hit, float.MaxValue))
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

                _lastPosition = _hitPos;
            }

            e.Use();
        }

        private void AddGrassTest(Event e)
        {
            var paintMaskValue = toolSettings.paintMask.value;
            var brushSize = toolSettings.brushSize;
            var density = toolSettings.density;
            var normalLimit = toolSettings.normalLimit;

            // Position to shoot rays from (you can modify this as needed)
            var startPos = _hitPos + Vector3.up * 3f; // Starting a bit above the hit position

            Physics.Raycast(_mousePointRay, out var curHit, 200f);
            var distanceMoved = Vector3.Distance(_lastPosition, curHit.point);

            if (distanceMoved < brushSize * 0.5f) return;

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
                            if (Physics.Raycast(randomPos + Vector3.up * 3f, Vector3.down, out var hit, float.MaxValue))
                            {
                                var hitLayer2 = hit.collider.gameObject.layer;

                                // Check if the hit is in paintMask
                                if (((1 << hitLayer2) & paintMaskValue) != 0)
                                {
                                    _hitPos = hit.point;
                                    _hitNormal = hit.normal;
                                    var newData = CreateGrassData(_hitPos, _hitNormal);
                                    grassData.Add(newData);
                                    _lastPosition = _hitPos;
                                }
                            }
                        }
                    }
                }
            }

            e.Use();
        }
        // private void AddGrass(Event e)
        // {
        //     var paintBlockMaskValue = toolSettings.paintBlockMask.value;
        //     var paintMaskValue = toolSettings.paintMask.value;
        //     var brushSize = toolSettings.brushSize;
        //     var density = toolSettings.density;
        //     var normalLimit = toolSettings.normalLimit;
        //
        //     if (Physics.Raycast(_ray, out _, 200f))
        //     {
        //         if (Physics.CheckSphere(_hitPos, toolSettings.brushSize, paintBlockMaskValue))
        //         {
        //             return;
        //         }
        //     }
        //
        //     if (Physics.Raycast(_ray, out var hit2, 200f))
        //     {
        //         var hitLayer = hit2.transform.gameObject.layer;
        //         if (((1 << hitLayer) & paintMaskValue) != 0)
        //         {
        //             _hitPos = hit2.point;
        //             _hitNormal = hit2.normal;
        //
        //             if (((1 << hitLayer) & paintMaskValue) > 0 && _hitNormal.y <= 1 + normalLimit &&
        //                 _hitNormal.y >= 1 - normalLimit)
        //             {
        //                 if (Vector3.Distance(_hitPos, _lastPosition) > brushSize / density)
        //                 {
        //                     for (int i = 0; i < density; i++)
        //                     {
        //                         var randomPoint = Random.insideUnitCircle * brushSize;
        //                         var randomPos = new Vector3(_hitPos.x + randomPoint.x, _hitPos.y,
        //                             _hitPos.z + randomPoint.y);
        //                         var castDir = -_hitNormal;
        //                         if (Physics.Raycast(randomPos + _hitNormal, castDir, out var groundHit, 1f))
        //                         {
        //                             var groundHitLayer = groundHit.collider.gameObject.layer;
        //                             if (((1 << groundHitLayer) & paintMaskValue) != 0)
        //                             {
        //                                 var newData = CreateGrassData(groundHit.point, groundHit.normal);
        //                                 grassData.Add(newData);
        //                             }
        //                         }
        //                     }
        //                 }
        //
        //                 _lastPosition = _hitPos;
        //             }
        //         }
        //     }
        //
        //
        //     e.Use();
        // }

        private void EditGrassPainting(RaycastHit[] terrainHit, Event e)
        {
            var hits = Physics.RaycastNonAlloc(_mousePointRay, terrainHit, 200f, toolSettings.paintMask.value);
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
                        var newLength = new Vector2(toolSettings.AdjustWidth, toolSettings.AdjustHeight);

                        _flowTimer++;
                        if (_flowTimer > toolSettings.flow)
                        {
                            // edit colors
                            if (_toolbarIntEdit is 0 or 2)
                            {
                                var newData = grassData[j];
                                newData.color = Vector3.Lerp(newCol, origColor, falloff);
                                grassData[j] = newData;
                            }

                            // edit grass length
                            if (_toolbarIntEdit is 1 or 2)
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
            var hits = Physics.RaycastNonAlloc(_mousePointRay, terrainHit, 200f, toolSettings.paintMask.value);
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
            if (Physics.Raycast(_mousePointRay, out var terrainHit, 100f, toolSettings.paintMask.value))
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