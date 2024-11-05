using System;
using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;
using PoolControl;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GrassComputeScript : MonoBehaviour
{
    #region Constants
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 4 + (3 + 2) * 3);
    private const int MaxBufferSize = 2500000;
    #endregion

    #region Serialized Fields
    public GrassSettingSO currentPresets; // grass settings to send to the compute shader
    [SerializeField, HideInInspector] private List<GrassData> grassData = new(); // base data lists
    [SerializeField] private Material instantiatedMaterial;
    #endregion

    #region Private Fields
    private Camera _mainCamera; // main camera
    private List<GrassInteractor> _interactors;
    private readonly List<int> _grassList = new();
    private List<int> _grassVisibleIDList = new(); // list of all visible grass ids, rest are culled
    private bool _initialized; // A state variable to help keep track of whether compute buffers have been set up
    private bool _fastMode;
    private int _shaderID;

    // Compute Shader Related
    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeShader _instComputeShader; // Instantiate the shaders so data belong to their unique compute buffers
    private ComputeBuffer _visibleIDBuffer; // buffer that contains the ids of all visible instances
    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass compute shader
    private uint _threadGroupSize; // compute shader thread group size

    // Cutting Related
    private ComputeBuffer _cutBuffer; // added for cutting
    private float[] _cutIDs;

    // Bounds & Culling Related
    private Bounds _bounds; // bounds of the total grass 
    private CullingTree _cullingTree;
    private readonly List<Bounds> _boundsListVis = new();
    private readonly List<CullingTree> _leaves = new();
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];
    private float _cameraOriginalFarPlane;
    private Vector3 _cachedCamPos;
    private Quaternion _cachedCamRot;

    private readonly uint[] _argsBufferReset =
    {
        0, // Number of vertices to render (Calculated in the compute shader with "InterlockedAdd(_IndirectArgsBuffer[0].numVertices);")
        1, // Number of instances to render (should only be 1 instance since it should produce a single mesh)
        0, // Index of the first vertex to render
        0, // Index of the first instance to render
        0 // Not used
    };
    #endregion

    #region Properties
    public List<GrassData> GrassDataList
    {
        get => grassData;
        set => grassData = value;
    }
    #endregion

    #region Editor Only Fields
#if UNITY_EDITOR
    [HideInInspector] public bool autoUpdate; // very slow, but will update always
    private SceneView _view;
#endif
    #endregion

    #region Unity Event Functions
    private void OnEnable()
    {
        RegisterEvents();
        InitializeInteractors();
        if (_initialized) OnDisable();
        MainSetup(true);
    }

    private void Update()
    {
        if (!ValidateInitialization()) return;

        UpdateGrassRendering();
    }

    private void OnDisable()
    {
        UnregisterEvents();
        CleanupResources();
    }
    #endregion

    #region Initialization Methods
    private void MainSetup(bool full)
    {
        SetupEditorComponents();
        if (!ValidateSetupRequirements()) return;

        InitializeBuffersAndShaders();
        SetupComputeShader();
        InitializeInteractors();

        if (full)
        {
            UpdateBounds();
        }

        SetupQuadTree(full);
    }

    private void SetupEditorComponents()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnScene;
        SceneView.duringSceneGui += OnScene;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        UpdateMainCamera();
#endif
    }

    private bool ValidateSetupRequirements()
    {
        if (grassData.Count == 0) return false;

        if (!currentPresets.shaderToUse || !currentPresets.materialToUse)
        {
            Debug.LogWarning("Missing Compute Shader/Material in grass Settings", this);
            return false;
        }

        if (!currentPresets.cuttingParticles)
        {
            Debug.LogWarning("Missing Cut Particles in grass Settings", this);
        }

        return true;
    }

    private void InitializeBuffersAndShaders()
    {
        _initialized = true;
        CreateShaderInstances();
        CreateComputeBuffers();
        InitializeCutBuffer();
        SetupComputeShaderKernel();
    }

    private void CreateShaderInstances()
    {
        _instComputeShader = Instantiate(currentPresets.shaderToUse);
        instantiatedMaterial = Instantiate(currentPresets.materialToUse);
    }

    private void CreateComputeBuffers()
    {
        var numSourceVertices = grassData.Count;
        _sourceVertBuffer = new ComputeBuffer(numSourceVertices, SourceVertStride,
            ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        _sourceVertBuffer.SetData(grassData);

        _drawBuffer = new ComputeBuffer(MaxBufferSize, DrawStride, ComputeBufferType.Append);
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            _argsBufferReset.Length * sizeof(uint));
        _visibleIDBuffer = new ComputeBuffer(grassData.Count, sizeof(int), ComputeBufferType.Structured);
    }

    private void InitializeCutBuffer()
    {
        _cutBuffer = new ComputeBuffer(grassData.Count, sizeof(float), ComputeBufferType.Structured);
        _cutIDs = new float[grassData.Count];
        Array.Fill(_cutIDs, -1);
        _cutBuffer.SetData(_cutIDs);
    }

    private void SetupComputeShaderKernel()
    {
        _idGrassKernel = _instComputeShader.FindKernel("Main");
        SetComputeBuffers();
        SetComputeParameters();
        _shaderID = Shader.PropertyToID("_PositionsMoving");
    }

    private void SetComputeBuffers()
    {
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.VisibleIDBuffer, _visibleIDBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.CutBuffer, _cutBuffer);
        instantiatedMaterial.SetBuffer(ShaderProperties.DrawTriangles, _drawBuffer);
    }

    private void SetComputeParameters()
    {
        _instComputeShader.SetInt(ShaderProperties.NumSourceVertices, grassData.Count);
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);
        SetShaderData();
    }
    #endregion

    #region Rendering Methods
    private bool ValidateInitialization()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && autoUpdate && !_fastMode)
        {
            OnDisable();
            OnEnable();
        }
#endif
        return _initialized;
    }

    private void UpdateGrassRendering()
    {
        GetFrustumData();
        SetGrassDataUpdate();
        PrepareBuffersForRendering();
        RenderGrass();
    }

    private void PrepareBuffersForRendering()
    {
        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);
        UpdateDispatchSize();
    }

    private void UpdateDispatchSize()
    {
        _dispatchSize = (_grassVisibleIDList.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);
        if (_grassVisibleIDList.Count > 0)
        {
            _dispatchSize += 1;
        }
    }

    private void RenderGrass()
    {
        if (_dispatchSize <= 0) return;

        _instComputeShader.Dispatch(_idGrassKernel, _dispatchSize, 1, 1);

        var renderParams = new RenderParams(instantiatedMaterial)
        {
            worldBounds = _bounds,
            shadowCastingMode = currentPresets.castShadow,
            receiveShadows = true,
        };

        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _argsBuffer);
    }
    #endregion

    #region Culling Methods
    private void GetFrustumData()
    {
        if (!_mainCamera) return;
        if (!HasCameraChanged()) return;

        UpdateCameraCache();
        UpdateFrustumCulling();
    }

    private bool HasCameraChanged()
    {
        return !(_cachedCamRot == _mainCamera.transform.rotation &&
                 _cachedCamPos == _mainCamera.transform.position &&
                 Application.isPlaying);
    }

    private void UpdateCameraCache()
    {
        _cachedCamPos = _mainCamera.transform.position;
        _cachedCamRot = _mainCamera.transform.rotation;
        GeometryUtility.CalculateFrustumPlanes(_mainCamera, _cameraFrustumPlanes);
    }

    private void UpdateFrustumCulling()
    {
        if (_fastMode) return;

        _cameraOriginalFarPlane = _mainCamera.farClipPlane;
        _mainCamera.farClipPlane = currentPresets.maxFadeDistance;

        _boundsListVis.Clear();
        _grassVisibleIDList.Clear();
        _cullingTree.RetrieveLeaves(_cameraFrustumPlanes, _boundsListVis, _grassVisibleIDList);
        _visibleIDBuffer.SetData(_grassVisibleIDList);

        _mainCamera.farClipPlane = _cameraOriginalFarPlane;
    }
    #endregion

    #region Grass Management Methods
    private void UpdateBounds()
    {
        _bounds = new Bounds();
        foreach (var grass in grassData)
        {
            _bounds.Encapsulate(grass.position);
        }
    }

    private void SetupQuadTree(bool full)
    {
        if (full)
        {
            SetupFullQuadTree();
        }
        else
        {
            SetupSimpleQuadTree();
        }
    }

    private void SetupFullQuadTree()
    {
        _cullingTree = new CullingTree(_bounds, currentPresets.cullingTreeDepth);
        _leaves.Clear();
        _cullingTree.RetrieveAllLeaves(_leaves);

        for (var i = 0; i < grassData.Count; i++)
        {
            _cullingTree.FindLeaf(grassData[i].position, i);
        }

        _cullingTree.ClearEmpty();
    }

    private void SetupSimpleQuadTree()
    {
        _grassVisibleIDList = new List<int>(grassData.Count);
        for (var i = 0; i < grassData.Count; i++)
        {
            _grassVisibleIDList.Add(i);
        }

        _visibleIDBuffer.SetData(_grassVisibleIDList);
    }

    public void UpdateCutBuffer(Vector3 hitPoint, float radius)
    {
        if (grassData.Count == 0) return;

        UpdateGrassCutting(hitPoint, radius);
    }

    private void UpdateGrassCutting(Vector3 hitPoint, float radius)
    {
        _grassList.Clear();
        _cullingTree.ReturnLeafList(hitPoint, _grassList, radius);

        var squaredRadius = radius * radius;
        var hitPointY = hitPoint.y;

        foreach (var grassIndex in _grassList)
        {
            ProcessGrassCut(grassIndex, hitPoint, hitPointY, squaredRadius);
        }

        _cutBuffer.SetData(_cutIDs);
    }

    private void ProcessGrassCut(int grassIndex, Vector3 hitPoint, float hitPointY, float squaredRadius)
    {
        var grassPosition = grassData[grassIndex].position;

        if (_cutIDs[grassIndex] <= hitPointY && !Mathf.Approximately(_cutIDs[grassIndex], -1))
            return;

        var squaredDistance = (hitPoint - grassPosition).sqrMagnitude;

        if (squaredDistance <= squaredRadius &&
            (_cutIDs[grassIndex] > hitPointY || Mathf.Approximately(_cutIDs[grassIndex], -1)))
        {
            if (_cutIDs[grassIndex] - 0.1f > hitPointY || Mathf.Approximately(_cutIDs[grassIndex], -1))
            {
                SpawnCuttingParticle(grassPosition, grassData[grassIndex].color);
            }

            _cutIDs[grassIndex] = hitPointY;
        }
    }

    private void SpawnCuttingParticle(Vector3 position, Vector3 colorVector)
    {
        var color = new Color(colorVector.x, colorVector.y, colorVector.z);
        var leafParticle = PoolObjectManager.Get<ParticleSystem>(PoolObjectKey.Leaf, position).main;
        leafParticle.startColor = new ParticleSystem.MinMaxGradient(color);
    }
    #endregion

    #region Event Methods
    private void RegisterEvents()
    {
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.InteractorAdded, AddInteractor);
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.InteractorRemoved, RemoveInteractor);
        GrassFuncManager.AddEvent(GrassEvent.TotalGrassCount, () => grassData.Count);
        GrassFuncManager.AddEvent(GrassEvent.VisibleGrassCount, () => _grassVisibleIDList.Count);
    }

    private void UnregisterEvents()
    {
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.InteractorAdded, AddInteractor);
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.InteractorRemoved, RemoveInteractor);
        GrassFuncManager.RemoveEvent(GrassEvent.TotalGrassCount, () => grassData.Count);
        GrassFuncManager.RemoveEvent(GrassEvent.VisibleGrassCount, () => _grassVisibleIDList.Count);
        _interactors.Clear();
    }

    private void InitializeInteractors()
    {
        _interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
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
    #endregion

    #region Shader Data Management
    private void SetGrassDataUpdate()
    {
        UpdateTimeInShader();
        UpdateInteractorsInShader();
        UpdateCameraPositionInShader();
    }

    private void UpdateTimeInShader()
    {
        _instComputeShader.SetFloat(ShaderProperties.Time, UnityEngine.Time.time);
    }

    private void UpdateInteractorsInShader()
    {
        if (_interactors.Count <= 0) return;

        var interactorCount = _interactors.Count;
        var positions = new Vector4[interactorCount];

        for (var i = interactorCount - 1; i >= 0; i--)
        {
            var pos = _interactors[i].transform.position;
            positions[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].radius);
        }

        _instComputeShader.SetVectorArray(_shaderID, positions);
        _instComputeShader.SetFloat(ShaderProperties.InteractorsLength, interactorCount);
    }

    private void UpdateCameraPositionInShader()
    {
        if (_mainCamera)
        {
            _instComputeShader.SetVector(ShaderProperties.CameraPositionWs, _mainCamera.transform.position);
            return;
        }

#if UNITY_EDITOR
        if (_view?.camera)
        {
            _instComputeShader.SetVector(ShaderProperties.CameraPositionWs, _view.camera.transform.position);
        }
#endif
    }

    private void SetShaderData()
    {
        // Basic settings
        _instComputeShader.SetFloat(ShaderProperties.Time, UnityEngine.Time.time);
        _instComputeShader.SetFloat(ShaderProperties.GrassRandomHeightMin, currentPresets.randomHeightMin);
        _instComputeShader.SetFloat(ShaderProperties.GrassRandomHeightMax, currentPresets.randomHeightMax);

        // Wind settings
        _instComputeShader.SetFloat(ShaderProperties.WindSpeed, currentPresets.windSpeed);
        _instComputeShader.SetFloat(ShaderProperties.WindStrength, currentPresets.windStrength);

        // Blade settings
        _instComputeShader.SetFloat(ShaderProperties.InteractorStrength, currentPresets.interactorStrength);
        _instComputeShader.SetFloat(ShaderProperties.BladeRadius, currentPresets.bladeRadius);
        _instComputeShader.SetFloat(ShaderProperties.BladeForward, currentPresets.bladeForward);
        _instComputeShader.SetFloat(ShaderProperties.BladeCurve, Mathf.Max(0, currentPresets.bladeCurve));
        _instComputeShader.SetFloat(ShaderProperties.BottomWidth, currentPresets.bottomWidth);

        // Count settings
        _instComputeShader.SetInt(ShaderProperties.MaxBladesPerVertex, currentPresets.bladesPerVertex);
        _instComputeShader.SetInt(ShaderProperties.MaxSegmentsPerBlade, currentPresets.segmentsPerBlade);

        // Size settings
        SetSizeSettings();

        // Material settings
        SetMaterialSettings();

        // Distance settings
        SetDistanceSettings();
    }

    private void SetSizeSettings()
    {
        _instComputeShader.SetFloat(ShaderProperties.MinHeight, currentPresets.minHeight);
        _instComputeShader.SetFloat(ShaderProperties.MinWidth, currentPresets.minWidth);
        _instComputeShader.SetFloat(ShaderProperties.MaxHeight, currentPresets.maxHeight);
        _instComputeShader.SetFloat(ShaderProperties.MaxWidth, currentPresets.maxWidth);
    }

    private void SetMaterialSettings()
    {
        instantiatedMaterial.SetColor(ShaderProperties.TopTint, currentPresets.topTint);
        instantiatedMaterial.SetColor(ShaderProperties.BottomTint, currentPresets.bottomTint);
    }

    private void SetDistanceSettings()
    {
        _instComputeShader.SetFloat(ShaderProperties.MinFadeDist, currentPresets.minFadeDistance);
        _instComputeShader.SetFloat(ShaderProperties.MaxFadeDist, currentPresets.maxFadeDistance);
    }
    #endregion

    #region Resource Cleanup
    private void CleanupResources()
    {
        if (!_initialized) return;

        DestroyShaderInstances();
        ReleaseBuffers();
        _initialized = false;
    }

    private void DestroyShaderInstances()
    {
        if (Application.isPlaying)
        {
            Destroy(_instComputeShader);
            Destroy(instantiatedMaterial);
        }
        else
        {
            DestroyImmediate(_instComputeShader);
            DestroyImmediate(instantiatedMaterial);
        }
    }

    private void ReleaseBuffers()
    {
        _sourceVertBuffer?.Release();
        _drawBuffer?.Release();
        _argsBuffer?.Release();
        _visibleIDBuffer?.Release();
        _cutBuffer?.Release();
    }
    #endregion

    #region Editor Only
#if UNITY_EDITOR
    public void ResetFaster()
    {
        _fastMode = true;
        OnDisable();
        MainSetup(false);
    }

    public void UpdateGrassDataFaster(int startIndex = 0, int count = -1)
    {
        if (!ValidateGrassDataUpdate(startIndex, ref count)) return;

        UpdateGrassBuffers(startIndex, count);
    }

    private bool ValidateGrassDataUpdate(int startIndex, ref int count)
    {
        if (count < 0)
        {
            count = grassData.Count;
        }

        count = Math.Min(count, grassData.Count - startIndex);

        return count > 0 && startIndex >= 0 && startIndex < grassData.Count;
    }

    private void UpdateGrassBuffers(int startIndex, int count)
    {
        _sourceVertBuffer.SetData(grassData, startIndex, startIndex, count);
        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);
        _dispatchSize = (_grassVisibleIDList.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);
    }

    public void ClearAllGrassData()
    {
        grassData.Clear();
        _grassVisibleIDList?.Clear();
        _visibleIDBuffer?.Release();
        _cutIDs = null;
        _cutBuffer?.Release();
        Reset();
    }

    private void SetupComputeShader()
    {
        _idGrassKernel = _instComputeShader.FindKernel("Main");

        // Set buffer data
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.VisibleIDBuffer, _visibleIDBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, ShaderProperties.CutBuffer, _cutBuffer);
        instantiatedMaterial.SetBuffer(ShaderProperties.DrawTriangles, _drawBuffer);

        // Set vertex data
        _instComputeShader.SetInt(ShaderProperties.NumSourceVertices, grassData.Count);
        _shaderID = Shader.PropertyToID("_PositionsMoving");

        // Calculate thread group sizes
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);

        SetShaderData();
    }

    public void Reset()
    {
        _fastMode = false;
        OnDisable();
        MainSetup(true);
    }

    private void OnScene(SceneView scene)
    {
        _view = scene;
        UpdateMainCamera();
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    private void UpdateMainCamera()
    {
        if (!Application.isPlaying)
        {
            if (_view?.camera)
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
    #endregion

    #region Shader Property IDs
    private static class ShaderProperties
    {
        public static readonly int SourceVertices = Shader.PropertyToID("_SourceVertices");
        public static readonly int DrawTriangles = Shader.PropertyToID("_DrawTriangles");
        public static readonly int IndirectArgsBuffer = Shader.PropertyToID("_IndirectArgsBuffer");
        public static readonly int NumSourceVertices = Shader.PropertyToID("_NumSourceVertices");
        public static readonly int VisibleIDBuffer = Shader.PropertyToID("_VisibleIDBuffer");
        public static readonly int CutBuffer = Shader.PropertyToID("_CutBuffer");
        public static readonly int Time = Shader.PropertyToID("_Time");
        public static readonly int GrassRandomHeightMin = Shader.PropertyToID("_GrassRandomHeightMin");
        public static readonly int GrassRandomHeightMax = Shader.PropertyToID("_GrassRandomHeightMax");
        public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        public static readonly int WindStrength = Shader.PropertyToID("_WindStrength");
        public static readonly int MinFadeDist = Shader.PropertyToID("_MinFadeDist");
        public static readonly int MaxFadeDist = Shader.PropertyToID("_MaxFadeDist");
        public static readonly int InteractorStrength = Shader.PropertyToID("_InteractorStrength");
        public static readonly int InteractorsLength = Shader.PropertyToID("_InteractorsLength");
        public static readonly int BladeRadius = Shader.PropertyToID("_BladeRadius");
        public static readonly int BladeForward = Shader.PropertyToID("_BladeForward");
        public static readonly int BladeCurve = Shader.PropertyToID("_BladeCurve");
        public static readonly int BottomWidth = Shader.PropertyToID("_BottomWidth");
        public static readonly int MaxBladesPerVertex = Shader.PropertyToID("_MaxBladesPerVertex");
        public static readonly int MaxSegmentsPerBlade = Shader.PropertyToID("_MaxSegmentsPerBlade");
        public static readonly int MinHeight = Shader.PropertyToID("_MinHeight");
        public static readonly int MinWidth = Shader.PropertyToID("_MinWidth");
        public static readonly int MaxHeight = Shader.PropertyToID("_MaxHeight");
        public static readonly int MaxWidth = Shader.PropertyToID("_MaxWidth");
        public static readonly int CameraPositionWs = Shader.PropertyToID("_CameraPositionWS");
        public static readonly int TopTint = Shader.PropertyToID("_TopTint");
        public static readonly int BottomTint = Shader.PropertyToID("_BottomTint");
    }
    #endregion
}

[Serializable]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct GrassData
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 widthHeight;
    public Vector3 color;
}