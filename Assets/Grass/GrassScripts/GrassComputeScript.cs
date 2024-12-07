using System;
using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;
using Pool;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GrassComputeScript : MonoSingleton<GrassComputeScript>
{
    //
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 1 + (3 + 2) * 3);
    private const int MaxBufferSize = 2500000;
    private const int MaxDispatchSize = 65535;

    //
    [SerializeField, HideInInspector] private List<GrassData> grassData = new(); // base data lists
    [SerializeField] private Material instantiatedMaterial;
    [SerializeField] private GrassSettingSO grassSetting;

    private readonly List<int> _nearbyGrassIds = new();
    private SpatialGrid _spatialGrid;

    [SerializeField, HideInInspector] private float[] _cutIDs;

    private Bounds _bounds; // bounds of the total grass 

    private Camera _mainCamera; // main camera
    private List<GrassInteractor> _interactors = new();

    //
    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeBuffer _cutBuffer; // added for cutting

    //
    private ComputeShader _instComputeShader;
    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass compute shader
    private uint _threadGroupSize; // compute shader thread group size

    // speeding up the editor a bit
    private int _interactorDataID;

    private readonly uint[] _argsBufferReset =
    {
        0, // Number of vertices to render (Calculated in the compute shader with "InterlockedAdd(_IndirectArgsBuffer[0].numVertices);")
        1, // Number of instances to render (should only be 1 instance since it should produce a single mesh)
        0, // Index of the first vertex to render
        0, // Index of the first instance to render
        0 // Not used
    };

    public List<GrassData> GrassDataList
    {
        get => grassData;
        set => grassData = value;
    }

    public GrassSettingSO GrassSetting
    {
        get => grassSetting;
        set => grassSetting = value;
    }
#if UNITY_EDITOR

    private SceneView _view;

    public void Reset()
    {
        ReleaseResources();
        MainSetup();

        UnregisterEvents();
        RegisterEvents();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneView.duringSceneGui -= OnScene;
    }

    private void OnScene(SceneView scene)
    {
        _view = scene;
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            if (_view.camera)
            {
                _mainCamera = _view.camera;
            }
        }
        else
        {
            _mainCamera = Camera.main;
        }
    }

    private void OnValidate()
    {
        // Set up components
        if (!Application.isPlaying)
        {
            if (_view)
            {
                _mainCamera = _view.camera;
            }
        }
        else
        {
            _mainCamera = Camera.main;
        }
    }

    public void ResetFaster()
    {
        ReleaseResources();
        MainSetup();
    }

    private void ReleaseResources()
    {
        DestroyImmediate(_instComputeShader);
        DestroyImmediate(instantiatedMaterial);
        ReleaseBuffer();
    }
#endif
    /*=============================================================================================================
     *                                            Unity Event Functions
     =============================================================================================================*/

    private void OnEnable()
    {
        RegisterEvents();
        MainSetup();
    }

    // LateUpdate is called after all Update calls
    private void Update()
    {
        if (grassData.Count <= 0) return;
        SetGrassDataUpdate();

        // Clear the draw and indirect args buffers of last frame's data
        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);

        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);
        _dispatchSize = Math.Min(_dispatchSize, MaxDispatchSize);
        if (_dispatchSize > 0)
        {
            // Dispatch the grass shader. It will run on the GPU
            _instComputeShader.Dispatch(_idGrassKernel, _dispatchSize, 1, 1);

            var renderParams = new RenderParams(instantiatedMaterial)
            {
                worldBounds = _bounds,
                shadowCastingMode = grassSetting.castShadow,
                receiveShadows = true,
            };

            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _argsBuffer);
        }
    }

    private void OnDisable()
    {
        UnregisterEvents();
        ReleaseBuffer();
        _interactors.Clear();
        _spatialGrid?.Clear();
        Destroy(_instComputeShader);
        Destroy(instantiatedMaterial);
    }

    /*=============================================================================================================
      *                                            Unity Event Functions
      =============================================================================================================*/

    private void RegisterEvents()
    {
        GrassEventManager.AddEvent<Vector3, float>(GrassEvent.UpdateCutBuffer, UpdateCutBuffer);
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.AddInteractor, AddInteractor);
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.RemoveInteractor, RemoveInteractor);
        GrassEventManager.AddEvent<Vector4[], Vector4[], Vector4[], Vector4[], int>(GrassEvent.UpdateSeasonData,
            UpdateSeasonData);

        GrassFuncManager.AddEvent(GrassEvent.GetGrassSetting, () => grassSetting);
    }

    private void UnregisterEvents()
    {
        GrassEventManager.RemoveEvent<Vector3, float>(GrassEvent.UpdateCutBuffer, UpdateCutBuffer);
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.AddInteractor, AddInteractor);
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.RemoveInteractor, RemoveInteractor);
        GrassEventManager.RemoveEvent<Vector4[], Vector4[], Vector4[], Vector4[], int>(GrassEvent.UpdateSeasonData,
            UpdateSeasonData);

        GrassFuncManager.RemoveEvent(GrassEvent.GetGrassSetting, () => grassSetting);
    }

    private void ReleaseBuffer()
    {
        // Release each buffer
        _sourceVertBuffer?.Release();
        _drawBuffer?.Release();
        _argsBuffer?.Release();
        _cutBuffer?.Release();
    }

    private void MainSetup()
    {
        SetupCamera();
        if (ValidateSetup()) return;
        InitializeShader();
        InitializeBuffers();
        InitSpatialGrid();
        SetupComputeShader();
        InitializeBounds();
#if UNITY_EDITOR
        _interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
#endif
    }

    private void SetupCamera()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnScene;
        SceneView.duringSceneGui += OnScene;

        if (!Application.isPlaying)
        {
            if (_view && _view.camera)
            {
                _mainCamera = _view.camera;
            }
        }
#endif
        if (Application.isPlaying)
        {
            _mainCamera = Camera.main;
        }
    }

    private bool ValidateSetup()
    {
        // Don't do anything if resources are not found,
        // or no vertex is put on the mesh.
        if (grassData.Count == 0)
        {
            return true;
        }

        if (!grassSetting.shaderToUse || !grassSetting.materialToUse)
        {
            Debug.LogWarning("Missing Compute Shader/Material in grass Settings", this);
            return true;
        }

        if (!grassSetting.cuttingParticles)
        {
            Debug.LogWarning("Missing Cut Particles in grass Settings", this);
            return true;
        }

        return false;
    }

    private void InitializeShader()
    {
        _instComputeShader = Instantiate(grassSetting.shaderToUse);
        instantiatedMaterial = Instantiate(grassSetting.materialToUse);
        _idGrassKernel = _instComputeShader.FindKernel("Main");
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
    }

    private void InitializeBuffers()
    {
        _sourceVertBuffer = new ComputeBuffer(grassData.Count, SourceVertStride, ComputeBufferType.Structured);
        _sourceVertBuffer.SetData(grassData);

        _drawBuffer = new ComputeBuffer(MaxBufferSize, DrawStride, ComputeBufferType.Append);
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            _argsBufferReset.Length * sizeof(uint));
        _cutBuffer = new ComputeBuffer(grassData.Count, sizeof(float), ComputeBufferType.Structured);

        // added for cutting
        InitializeCutData();

        _cutBuffer.SetData(_cutIDs);
    }

    private void SetupComputeShader()
    {
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.CutBuffer, _cutBuffer);
        _instComputeShader.SetInt(GrassShaderPropertyID.NumSourceVertices, grassData.Count);

        instantiatedMaterial.SetBuffer(GrassShaderPropertyID.DrawTriangles, _drawBuffer);

        _interactorDataID = Shader.PropertyToID("_InteractorData");
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log((int)_threadGroupSize, 2);

        SetShaderData();
    }

    // Update the shader with frame specific data
    private void SetGrassDataUpdate()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);

        if (_interactors.Count > 0)
        {
            UpdateInteractors();
        }

        if (_mainCamera)
        {
            _instComputeShader.SetVector(GrassShaderPropertyID.CameraPositionWs, _mainCamera.transform.position);
            _instComputeShader.SetVector(GrassShaderPropertyID.CameraForward, _mainCamera.transform.forward);
            _instComputeShader.SetFloat(GrassShaderPropertyID.CameraFOV, _mainCamera.fieldOfView * Mathf.Deg2Rad);
        }
    }

    private void UpdateInteractors()
    {
        var positions = new Vector4[_interactors.Count];
        for (var i = _interactors.Count - 1; i >= 0; i--)
        {
            var pos = _interactors[i].transform.position;
            positions[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].radius);
        }

        _instComputeShader.SetVectorArray(_interactorDataID, positions);
        _instComputeShader.SetFloat(GrassShaderPropertyID.InteractorsLength, _interactors.Count);
    }

    private void UpdateCutBuffer(Vector3 hitPoint, float radius)
    {
        _nearbyGrassIds.Clear();
        _spatialGrid.GetObjectsInRadius(hitPoint, radius, _nearbyGrassIds);
        if (_nearbyGrassIds.Count == 0) return;

        var radiusSqr = radius * radius;
        foreach (var index in _nearbyGrassIds)
        {
            if (index >= 0 && index < grassData.Count)
            {
                var grassPos = grassData[index].position;
                if (Vector3.SqrMagnitude(grassPos - hitPoint) <= radiusSqr)
                {
                    var previousCutHeight = _cutIDs[index];
                    if (Mathf.Approximately(previousCutHeight, -1) || hitPoint.y < previousCutHeight)
                    {
                        _cutIDs[index] = hitPoint.y;
                        var zoneColor =
                            GrassFuncManager.TriggerEvent<Vector3, Color>(GrassEvent.TryGetGrassColor,
                                grassPos);
                        var particleColor = zoneColor == Color.white
                            ? new Color(
                                grassData[index].color.x,
                                grassData[index].color.y,
                                grassData[index].color.z
                            )
                            : zoneColor;

                        SpawnCuttingParticle(grassPos, particleColor);
                    }
                }
            }
        }

        _cutBuffer.SetData(_cutIDs);
    }

    private void SpawnCuttingParticle(Vector3 position, Color col)
    {
        var leafParticle = PoolObjectManager.Get<ParticleSystem>(PoolObjectKey.Leaf, position).main;
        leafParticle.startColor = new ParticleSystem.MinMaxGradient(col);
    }

    private void AddInteractor(GrassInteractor interactor)
    {
        if (!_interactors.Contains(interactor))
            _interactors.Add(interactor);
    }

    private void RemoveInteractor(GrassInteractor interactor)
    {
        _interactors.Remove(interactor);
    }

    private void InitializeBounds()
    {
        _bounds = new Bounds(grassData[0].position, Vector3.zero);
        foreach (var grass in grassData)
        {
            _bounds.Encapsulate(grass.position);
        }

        var extents = _bounds.extents;
        _bounds.extents = extents * 1.1f;
    }

    public void InitializeCutData()
    {
        _cutIDs = new float[grassData.Count];
        for (var i = 0; i < _cutIDs.Length; i++)
        {
            _cutIDs[i] = -1;
        }
    }

    private void InitSpatialGrid()
    {
        // 초기화
        if (grassData == null || grassData.Count == 0) return;

        // 전체 잔디의 경계를 사용 (이미 _bounds가 있으므로 이를 활용)
        _spatialGrid = new SpatialGrid(_bounds, grassSetting.cullingCellSize);

        // 모든 잔디를 그리드에 추가
        for (int i = 0; i < grassData.Count; i++)
        {
            _spatialGrid.AddObject(grassData[i].position, i);
        }
    }
    /*=======================================================================================
     *                              Setup Shader Data
     =======================================================================================*/

    public void SetShaderData()
    {
        // Send things to compute shader that dont need to be set every frame
        _instComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);
        SetRandomHeightMinMax();
        SetWindSetting();
        SetInteractorStrength();
        SetBladeShape();
        SetBladeAmount();
        SetBladeMinMax();
        SetLODSetting();
        SetTint();
    }

    public void SetRandomHeightMinMax()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMin, grassSetting.randomHeightMin);
        _instComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMax, grassSetting.randomHeightMax);
    }

    public void SetInteractorStrength()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.InteractorStrength, grassSetting.interactorStrength);
    }

    public void SetBladeMinMax()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinHeight, grassSetting.minHeight);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinWidth, grassSetting.minWidth);

        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxHeight, grassSetting.maxHeight);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxWidth, grassSetting.maxWidth);
    }

    public void SetBladeShape()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeRadius, grassSetting.bladeRadius);
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeForward, grassSetting.bladeForward);
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeCurve, Mathf.Max(0, grassSetting.bladeCurve));
        _instComputeShader.SetFloat(GrassShaderPropertyID.BottomWidth, grassSetting.bottomWidth);
    }

    public void SetTint()
    {
        instantiatedMaterial.SetColor(GrassShaderPropertyID.TopTint, grassSetting.topTint);
        instantiatedMaterial.SetColor(GrassShaderPropertyID.BottomTint, grassSetting.bottomTint);
    }

    public void SetBladeAmount()
    {
        _instComputeShader.SetInt(GrassShaderPropertyID.MaxBladesPerVertex, grassSetting.bladesPerVertex);
        _instComputeShader.SetInt(GrassShaderPropertyID.MaxSegmentsPerBlade, grassSetting.segmentsPerBlade);
    }

    public void SetWindSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.WindSpeed, grassSetting.windSpeed);
        _instComputeShader.SetFloat(GrassShaderPropertyID.WindStrength, grassSetting.windStrength);
        _instComputeShader.SetVector(GrassShaderPropertyID.WindDirection, grassSetting.windDirectionVector);
    }

    public void SetLODSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinFadeDist, grassSetting.minFadeDistance);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxFadeDist, grassSetting.maxFadeDistance);
        _instComputeShader.SetFloat(GrassShaderPropertyID.LowQualityDistance, grassSetting.lowQualityDistance);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MediumQualityDistance, grassSetting.mediumQualityDistance);

        instantiatedMaterial.SetFloat(GrassShaderPropertyID.LowQualityDistance, grassSetting.lowQualityDistance);
        instantiatedMaterial.SetFloat(GrassShaderPropertyID.MediumQualityDistance, grassSetting.mediumQualityDistance);
    }

    private void UpdateSeasonData(Vector4[] positions, Vector4[] scales, Vector4[] colors, Vector4[] widthHeights,
                                  int zoneCount)
    {
        if (_instComputeShader != null)
        {
            _instComputeShader.SetVectorArray(GrassShaderPropertyID.ZonePositions, positions);
            _instComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneScales, scales);
            _instComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneColors, colors);
            _instComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneWidthHeights, widthHeights);
            _instComputeShader.SetInt(GrassShaderPropertyID.ZoneCount, zoneCount);
        }

        if (instantiatedMaterial != null)
        {
            instantiatedMaterial.SetVectorArray(GrassShaderPropertyID.ZonePositions, positions);
            instantiatedMaterial.SetVectorArray(GrassShaderPropertyID.ZoneScales, scales);
            instantiatedMaterial.SetVectorArray(GrassShaderPropertyID.ZoneColors, colors);
            instantiatedMaterial.SetVectorArray(GrassShaderPropertyID.ZoneWidthHeights, widthHeights);
            instantiatedMaterial.SetInt(GrassShaderPropertyID.ZoneCount, zoneCount);
        }
    }

#if UNITY_EDITOR

    public void UpdateGrassDataFaster(int startIndex = 0, int count = -1)
    {
        if (count < 0)
        {
            count = grassData.Count;
        }

        count = Math.Min(count, grassData.Count - startIndex);

        if (count <= 0 || startIndex < 0 || startIndex >= grassData.Count)
            return;

        _sourceVertBuffer.SetData(grassData, startIndex, startIndex, count);

        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);

        // _dispatchSize = Mathf.CeilToInt((int)(grassData.Count / _threadGroupSize));

        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log(_threadGroupSize, 2);
    }

    public void ClearAllData()
    {
        AllClear();
        _cutBuffer?.SetData(_cutIDs);

        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);

        _dispatchSize = 0;
    }

    public void AllClear()
    {
        grassData.Clear();
        _nearbyGrassIds.Clear();
        _cutIDs = Array.Empty<float>();
        _bounds = new Bounds();
    }
#endif
}