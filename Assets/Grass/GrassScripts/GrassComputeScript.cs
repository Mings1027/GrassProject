using System;
using System.Collections.Generic;
using System.Linq;
using EventBusSystem.Scripts;
using Grass.GrassScripts;
using Pool;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GrassComputeScript : MonoSingleton<GrassComputeScript>
{
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 4 + (3 + 2) * 3);
    private const int MaxBufferSize = 2500000;

    [SerializeField, HideInInspector]
    private List<GrassData> grassData = new(); // base data lists
    [SerializeField] private GrassSettingSO grassSetting;

    private readonly List<int> _nearbyGrassIds = new();

    [SerializeField, HideInInspector]
    private List<int> grassVisibleIDList = new();
    [SerializeField, HideInInspector]
    private float[] cutIDs;

    private Bounds _bounds; // bounds of the total grass 

    private CullingTree _cullingTree;
    private Camera _mainCamera;
    private List<InteractorData> _interactors = new();

    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeBuffer _visibleIDBuffer; // buffer that contains the ids of all visible instances
    private ComputeBuffer _cutBuffer; // added for cutting

    private ComputeShader _instComputeShader;
    private Material _instantiatedMaterial;

    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass compute shader
    private uint _threadGroupSize; // compute shader thread group size

    // culling tree data ----------------------------------------------------------------------
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];

    private Vector3 _cachedCamPos;
    private Quaternion _cachedCamRot;
    private int _interactorDataID;

    private IGrassColorProvider _colorProvider;

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

    // ======================================= Event Bus ==============================================================================================================================
    private EventBinding<InteractorAddedEvent> _addBinding;
    private EventBinding<InteractorRemovedEvent> _removeBinding;
    //==============================================================================================================================

    public void Reset()
    {
        ReleaseResources();
        MainSetup(true);

        UnregisterEvents();
        RegisterEvents();
    }

    private void ReleaseResources()
    {
        ReleaseBuffer();
        if (!Application.isPlaying)
        {
            DestroyImmediate(_instComputeShader);
            DestroyImmediate(_instantiatedMaterial);
        }
        else
        {
            Destroy(_instComputeShader);
            Destroy(_instantiatedMaterial);
        }
    }
#if UNITY_EDITOR

    private SceneView _view;

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

#endif
    /*=============================================================================================================
     *                                            Unity Event Functions
     =============================================================================================================*/

    private void OnEnable()
    {
        RegisterEvents();
        MainSetup(true);
    }

    // LateUpdate is called after all Update calls
    private void Update()
    {
        if (grassData.Count <= 0) return;
        GetFrustumData();
        SetGrassDataUpdate();

        // Clear the draw and indirect args buffers of last frame's data
        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);

        _dispatchSize = (grassVisibleIDList.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log(_threadGroupSize, 2);
        if (grassVisibleIDList.Count > 0)
        {
            // make sure the compute shader is dispatched even when theres very little grass
            _dispatchSize += 1;
        }

        if (_dispatchSize > 0)
        {
            // Dispatch the grass shader. It will run on the GPU
            _instComputeShader.Dispatch(_idGrassKernel, _dispatchSize, 1, 1);
#if UNITY_EDITOR
            UpdateBufferCount();
#endif
            var renderParams = new RenderParams(_instantiatedMaterial)
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
        if (!Application.isPlaying)
        {
            DestroyImmediate(_instComputeShader);
            DestroyImmediate(_instantiatedMaterial);
        }
        else
        {
            Destroy(_instComputeShader);
            Destroy(_instantiatedMaterial);
        }
    }

#if UNITY_EDITOR
    // draw the bounds gizmos
    private void OnDrawGizmos()
    {
        if (grassSetting)
        {
            if (grassSetting.drawBounds)
            {
                if (_cullingTree == null) return;
                _cullingTree.DrawBounds();
            }

            if (grassSetting.drawAllBounds)
            {
                if (_cullingTree == null) return;
                _cullingTree.DrawAllBounds();
            }
        }
    }

#endif
    /*=============================================================================================================
      *                                            Unity Event Functions
      =============================================================================================================*/

    private void RegisterEvents()
    {
        _addBinding = new EventBinding<InteractorAddedEvent>(AddInteractorEvent);
        EventBus<InteractorAddedEvent>.Register(_addBinding);
        _removeBinding = new EventBinding<InteractorRemovedEvent>(RemoveInteractorEvent);
        EventBus<InteractorRemovedEvent>.Register(_removeBinding);
    }

    private void UnregisterEvents()
    {
        EventBus<InteractorAddedEvent>.Deregister(_addBinding);
        EventBus<InteractorRemovedEvent>.Deregister(_removeBinding);
    }

    private void ReleaseBuffer()
    {
        // Release each buffer
        _sourceVertBuffer?.Release();
        _drawBuffer?.Release();
        _argsBuffer?.Release();
        _visibleIDBuffer?.Release();
        _cutBuffer?.Release();
    }

    private void MainSetup(bool full)
    {
        SetupCamera();
        if (grassData.Count <= 0)
        {
            ClearAllData();
            return;
        }

        if (ValidateSetup()) return;
        InitResources();
        InitBuffers();
        SetupComputeShader();
        SetupQuadTree(full);
        GetFrustumData();
#if UNITY_EDITOR
        var interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
        _interactors = new List<InteractorData>(interactors);
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

    private void InitResources()
    {
        _instComputeShader = Instantiate(grassSetting.shaderToUse);
        _instantiatedMaterial = Instantiate(grassSetting.materialToUse);
        _idGrassKernel = _instComputeShader.FindKernel("Main");
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
    }

    private void InitBuffers()
    {
        _sourceVertBuffer = new ComputeBuffer(grassData.Count, SourceVertStride, ComputeBufferType.Structured);
        _sourceVertBuffer.SetData(grassData);

        _drawBuffer = new ComputeBuffer(MaxBufferSize, DrawStride, ComputeBufferType.Append);
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            _argsBufferReset.Length * sizeof(uint));
        _visibleIDBuffer = new ComputeBuffer(grassData.Count, sizeof(uint), ComputeBufferType.Structured);
        _cutBuffer = new ComputeBuffer(grassData.Count, sizeof(float), ComputeBufferType.Structured);

        // added for cutting
        cutIDs = new float[grassData.Count];
        for (var i = 0; i < cutIDs.Length; i++)
        {
            cutIDs[i] = -1;
        }

        _cutBuffer.SetData(cutIDs);
    }

    private void SetupComputeShader()
    {
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.VisibleIDBuffer, _visibleIDBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.CutBuffer, _cutBuffer);
        _instComputeShader.SetInt(GrassShaderPropertyID.NumSourceVertices, grassData.Count);

        _instantiatedMaterial.SetBuffer(GrassShaderPropertyID.DrawTriangles, _drawBuffer);

        _interactorDataID = Shader.PropertyToID("_InteractorData");
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log((int)_threadGroupSize, 2);

        SetShaderData();
    }

    private void SetupQuadTree(bool full)
    {
        if (full)
        {
            InitCullingTree();
        }

        var visibleArray = new uint[grassData.Count];
        for (int i = 0; i < grassVisibleIDList.Count && i < visibleArray.Length; i++)
        {
            visibleArray[i] = (uint)grassVisibleIDList[i];
        }

        _visibleIDBuffer?.SetData(visibleArray);
    }

    // Get the data from the camera for culling
    private void GetFrustumData()
    {
        _cachedCamPos = Vector3.zero;
        _cachedCamRot = Quaternion.identity;

        if (!_mainCamera) return;

        // Check if the camera's position or rotation has changed
        if (_cachedCamRot == _mainCamera.transform.rotation && _cachedCamPos == _mainCamera.transform.position)
            return; // Camera hasn't moved, no need for frustum culling

        // Cache camera position and rotation for next frame
        _cachedCamPos = _mainCamera.transform.position;
        _cachedCamRot = _mainCamera.transform.rotation;

        // Get frustum data from the main camera without modifying far clip plane
        GeometryUtility.CalculateFrustumPlanes(_mainCamera, _cameraFrustumPlanes);

        UpdateCulling();
    }

    public void UpdateCulling()
    {
        grassVisibleIDList.Clear();
        _cullingTree?.GetVisibleObjectsInFrustum(_cameraFrustumPlanes, grassVisibleIDList);
        _visibleIDBuffer.SetData(grassVisibleIDList);
    }

    // Update the shader with frame specific data
    private void SetGrassDataUpdate()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);

        if (_interactors.Count >= 0)
        {
            UpdateInteractors();
        }

        if (_mainCamera)
        {
            _instComputeShader.SetVector(GrassShaderPropertyID.CameraPositionWs, _mainCamera.transform.position);
        }
    }

    private void UpdateInteractors()
    {
        var positions = new Vector4[_interactors.Count];
        for (var i = _interactors.Count - 1; i >= 0; i--)
        {
            var interactor = _interactors[i];
            var pos = interactor.Position;
            positions[i] = new Vector4(pos.x, pos.y, pos.z, interactor.Radius);
        }

        _instComputeShader.SetVectorArray(_interactorDataID, positions);
        _instComputeShader.SetFloat(GrassShaderPropertyID.InteractorsLength, _interactors.Count);
    }

    public void RemoveGrassInRadius(Vector3 position, float radius)
    {
        if (grassData.Count <= 0) return;
        GetNearbyGrass(position, radius);
        var squaredRadius = radius * radius;
        for (int i = 0; i < _nearbyGrassIds.Count; i++)
        {
            var currentIndex = _nearbyGrassIds[i];
            var grassPosition = grassData[currentIndex].position;

            if (cutIDs[currentIndex] <= 0 && !Mathf.Approximately(cutIDs[currentIndex], -1))
                continue;
            var squaredDistance = (position - grassPosition).sqrMagnitude;
            if (squaredDistance <= squaredRadius)
            {
                cutIDs[currentIndex] = 0;
            }
        }

        _cutBuffer.SetData(cutIDs);
    }

    public void CutGrass(Vector3 hitPoint, float radius)
    {
        if (grassData.Count > 0)
        {
            GetNearbyGrass(hitPoint, radius);

            var squaredRadius = radius * radius;
            var hitPointY = hitPoint.y;

            // 가져온 ID들에 대해서만 잘리는 검사를 수행
            for (var i = 0; i < _nearbyGrassIds.Count; i++)
            {
                var currentIndex = _nearbyGrassIds[i];
                var grassPosition = grassData[currentIndex].position;

                if (cutIDs[currentIndex] <= hitPointY &&
                    !Mathf.Approximately(cutIDs[currentIndex], -1))
                    continue;

                var squaredDistance = (hitPoint - grassPosition).sqrMagnitude;

                if (squaredDistance <= squaredRadius &&
                    (cutIDs[currentIndex] > hitPointY || Mathf.Approximately(cutIDs[currentIndex], -1)))
                {
                    if (cutIDs[currentIndex] - 0.1f > hitPointY || Mathf.Approximately(cutIDs[currentIndex], -1))
                    {
                        var baseColor = new Color(
                            grassData[currentIndex].color.x,
                            grassData[currentIndex].color.y,
                            grassData[currentIndex].color.z);

                        var finalColor = _colorProvider?.GetGrassColor(grassPosition, baseColor) ?? baseColor;

                        // 이벤트 처리 후의 결과 색상으로 파티클을 생성합니다
                        SpawnCuttingParticle(grassPosition, finalColor);
                    }

                    cutIDs[currentIndex] = hitPointY;
                }
            }
        }

        _cutBuffer.SetData(cutIDs);
    }

    public void SetColorProvider(IGrassColorProvider provider)
    {
        _colorProvider = provider;
    }

    private void SpawnCuttingParticle(Vector3 position, Color col)
    {
        var leafParticle = PoolObjectManager.Get<ParticleSystem>(PoolObjectKey.Leaf, position).main;
        leafParticle.startColor = new ParticleSystem.MinMaxGradient(col);
    }

    private void AddInteractorEvent(InteractorAddedEvent evt)
    {
        _interactors.Add(evt.data);
    }

    private void RemoveInteractorEvent(InteractorRemovedEvent evt)
    {
        _interactors.Remove(evt.data);
    }

    private void InitCullingTree()
    {
        _bounds = new Bounds(grassData[0].position, Vector3.zero);
        foreach (var grass in grassData)
        {
            _bounds.Encapsulate(grass.position);
        }

        var extents = _bounds.extents;
        _bounds.extents = extents * 1.1f;

        RecreateCullingTree();
    }

    private void RecreateCullingTree()
    {
        _cullingTree = new CullingTree(_bounds, grassSetting.cullingTreeDepth);
        grassVisibleIDList.Clear();

        for (int i = 0; i < grassData.Count; i++)
        {
            if (_cullingTree.GetClosestNode(grassData[i].position, i))
            {
                grassVisibleIDList.Add(i);
            }
        }
    }

    public void GetNearbyGrass(Vector3 point, float radius)
    {
        _nearbyGrassIds.Clear();
        _cullingTree?.GetObjectsInRadius(_nearbyGrassIds, point, radius);
    }

    /*=======================================================================================
     *                              Setup Shader Data
     =======================================================================================*/
    #region Set Shader Data
    public void SetShaderData()
    {
        // Send things to compute shader that dont need to be set every frame
        _instComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);
        RandomHeightMinMaxSetting();
        InteractorStrengthSetting();
        BladeMinMaxSetting();
        BladeShapeSetting();
        WindSetting();
        TintSetting();
        BlendSetting();
        ShadowSetting();
        AdditionalLightSetting();
        SpecularSetting();
        BladeAmountSetting();
        LODSetting();
    }

    public void RandomHeightMinMaxSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMin, grassSetting.randomHeightMin);
        _instComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMax, grassSetting.randomHeightMax);
    }

    public void InteractorStrengthSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.InteractorStrength, grassSetting.interactorStrength);
    }

    public void BladeMinMaxSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinHeight, grassSetting.minHeight);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinWidth, grassSetting.minWidth);

        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxHeight, grassSetting.maxHeight);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxWidth, grassSetting.maxWidth);
    }

    public void BladeShapeSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeRadius, grassSetting.bladeRadius);
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeForward, grassSetting.bladeForward);
        _instComputeShader.SetFloat(GrassShaderPropertyID.BladeCurve, Mathf.Max(0, grassSetting.bladeCurve));
        _instComputeShader.SetFloat(GrassShaderPropertyID.BottomWidth, grassSetting.bottomWidth);
    }

    public void WindSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.WindSpeed, grassSetting.windSpeed);
        _instComputeShader.SetFloat(GrassShaderPropertyID.WindStrength, grassSetting.windStrength);
        _instComputeShader.SetVector(GrassShaderPropertyID.WindDirection, grassSetting.windDirectionVector);
    }

    public void TintSetting()
    {
        _instantiatedMaterial.SetColor(GrassShaderPropertyID.TopTint, grassSetting.topTint);
        _instantiatedMaterial.SetColor(GrassShaderPropertyID.BottomTint, grassSetting.bottomTint);
    }

    public void BlendSetting()
    {
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.AmbientStrength, grassSetting.ambientStrength);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.BlendMultiply, grassSetting.blendMultiply);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.BlendOffset, grassSetting.blendOffset);

        if (_instantiatedMaterial.HasProperty(GrassShaderPropertyID.AmbientAdjustmentColor))
        {
            _instantiatedMaterial.SetColor(GrassShaderPropertyID.AmbientAdjustmentColor,
                grassSetting.ambientAdjustmentColor);
        }
    }

    public void ShadowSetting()
    {
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.ShadowDistance, grassSetting.shadowDistance);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.ShadowFadeRange, grassSetting.shadowFadeRange);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.MinShadowBrightness, grassSetting.shadowBrightness);
        _instantiatedMaterial.SetColor(GrassShaderPropertyID.ShadowColor, grassSetting.shadowColor);
    }

    public void AdditionalLightSetting()
    {
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.AdditionalLightIntensity,
            grassSetting.additionalLightIntensity);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.AdditionalLightShadowStrength,
            grassSetting.additionalLightShadowStrength);
        _instantiatedMaterial.SetColor(GrassShaderPropertyID.AdditionalShadowColor,
            grassSetting.additionalLightShadowColor);
    }

    public void SpecularSetting()
    {
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.Glossiness, grassSetting.specularFalloff);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.SpecularStrength, grassSetting.specularStrength);
        _instantiatedMaterial.SetFloat(GrassShaderPropertyID.SpecularHeight, grassSetting.specularHeight);
    }

    public void BladeAmountSetting()
    {
        _instComputeShader.SetInt(GrassShaderPropertyID.MaxBladesPerVertex, grassSetting.bladesPerVertex);
        _instComputeShader.SetInt(GrassShaderPropertyID.MaxSegmentsPerBlade, grassSetting.segmentsPerBlade);
    }

    public void LODSetting()
    {
        _instComputeShader.SetFloat(GrassShaderPropertyID.MinFadeDist, grassSetting.minFadeDistance);
        _instComputeShader.SetFloat(GrassShaderPropertyID.MaxFadeDist, grassSetting.maxFadeDistance);
        if (Application.isPlaying)
        {
            _mainCamera.farClipPlane = grassSetting.maxFadeDistance;
        }
    }
    #endregion

    public void UpdateSeasonData(Vector4[] positions, Vector4[] scales, Vector4[] colors, Vector4[] widthHeights,
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
    }

    public void ClearAllData()
    {
        grassData.Clear();
        _nearbyGrassIds.Clear();
        grassVisibleIDList.Clear();
        cutIDs = Array.Empty<float>();
        _cullingTree = null;
        _bounds = new Bounds();
        ReleaseBuffer();

        _dispatchSize = 0;
    }

#if UNITY_EDITOR

    public int MaximumBufferSize => MaxBufferSize;
    public int CurrentBufferCount => _currentBufferCount;
    private int _currentBufferCount;

    private void UpdateBufferCount()
    {
        var counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(_drawBuffer, counterBuffer, 0);
        int[] counterArray = new int[1];
        counterBuffer.GetData(counterArray);
        _currentBufferCount = counterArray[0];
        counterBuffer.Release();
    }

    public void ResetFaster()
    {
        ReleaseResources();
        MainSetup(false);
    }

    public void AddNewGrass(Vector3 position, int index)
    {
        if (!_bounds.Contains(position))
        {
            _bounds.Encapsulate(position);
            var extents = _bounds.extents;
            _bounds.extents = extents * 1.1f;

            _cullingTree = new CullingTree(_bounds, grassSetting.cullingTreeDepth);

            for (int i = 0; i < grassData.Count; i++)
            {
                _cullingTree.GetClosestNode(grassData[i].position, i);
            }
        }

        if (_cullingTree != null && _cullingTree.GetClosestNode(position, index))
        {
            grassVisibleIDList.Add(index);
        }
    }

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

        _dispatchSize = (grassVisibleIDList.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log(_threadGroupSize, 2);
    }

    public float[] GetCutBuffer() => cutIDs;
    public void SetCutBuffer(float[] buffer)
    {
        cutIDs = buffer;
        _cutBuffer.SetData(cutIDs);
    }
#endif
}