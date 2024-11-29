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
public class GrassComputeScript : MonoBehaviour
{
    //
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 1 + (3 + 2) * 3);
    private const int MaxBufferSize = 2500000;

    //
    [SerializeField, HideInInspector] private List<GrassData> grassData = new(); // base data lists
    [SerializeField] private Material instantiatedMaterial;
    [SerializeField] private GrassSettingSO grassSetting;

    private readonly List<int> _nearbyGrassIds = new();

    // list of all visible grass ids, rest are culled
    [SerializeField, HideInInspector] private List<int> _grassVisibleIDList = new();
    [SerializeField, HideInInspector] private float[] _cutIDs;

    private Bounds _bounds; // bounds of the total grass 
    private readonly List<Bounds> _boundsListVis = new();

    private CullingTree _cullingTree;
    private readonly List<CullingTree> _leaves = new();

    //
    private Camera _mainCamera; // main camera
    private List<GrassInteractor> _interactors = new();

    //
    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeBuffer _visibleIDBuffer; // buffer that contains the ids of all visible instances
    private ComputeBuffer _cutBuffer; // added for cutting

    //
    private ComputeShader _instComputeShader;
    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass compute shader
    private uint _threadGroupSize; // compute shader thread group size

    // culling tree data ----------------------------------------------------------------------
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];

    // speeding up the editor a bit
    private Vector3 _cachedCamPos;
    private Quaternion _cachedCamRot;
    private bool _fastMode;
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
    public Material InstantiatedMaterial
    {
        get => instantiatedMaterial;
        set => instantiatedMaterial = value;
    }
    public GrassSettingSO GrassSetting
    {
        get => grassSetting;
        set => grassSetting = value;
    }

#if UNITY_EDITOR
    private SceneView _view;
#endif

#if UNITY_EDITOR
    public void Reset()
    {
        _fastMode = false;
        OnDisable();
        MainSetup(true);
    }

    private void OnDestroy()
    {
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

        // _dispatchSize = Mathf.CeilToInt((int)(_grassVisibleIDList.Count / _threadGroupSize));
        _dispatchSize = (_grassVisibleIDList.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log(_threadGroupSize, 2);
        if (_grassVisibleIDList.Count > 0)
        {
            // make sure the compute shader is dispatched even when theres very little grass
            _dispatchSize += 1;
        }

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

        _interactors.Clear();

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

        ReleaseBuffer();
    }

#if UNITY_EDITOR
    // draw the bounds gizmos
    private void OnDrawGizmos()
    {
        if (grassSetting)
        {
            if (grassSetting.drawBounds)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                for (var i = 0; i < _boundsListVis.Count; i++)
                {
                    Gizmos.DrawWireCube(_boundsListVis[i].center, _boundsListVis[i].size);
                }

                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawWireCube(_bounds.center, _bounds.size);
            }
        }
    }
#endif
    /*=============================================================================================================
      *                                            Unity Event Functions
      =============================================================================================================*/

    private void RegisterEvents()
    {
        GrassEventManager.AddEvent<Vector3, float>(GrassEvent.UpdateCutBuffer, UpdateCutBuffer);
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.AddInteractor, AddInteractor);
        GrassEventManager.AddEvent<GrassInteractor>(GrassEvent.RemoveInteractor, RemoveInteractor);

        GrassFuncManager.AddEvent(GrassEvent.TotalGrassCount, () => grassData.Count);
        GrassFuncManager.AddEvent(GrassEvent.VisibleGrassCount, () => _grassVisibleIDList.Count);
    }

    private void UnregisterEvents()
    {
        GrassEventManager.RemoveEvent<Vector3, float>(GrassEvent.UpdateCutBuffer, UpdateCutBuffer);
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.AddInteractor, AddInteractor);
        GrassEventManager.RemoveEvent<GrassInteractor>(GrassEvent.RemoveInteractor, RemoveInteractor);

        GrassFuncManager.RemoveEvent(GrassEvent.TotalGrassCount, () => grassData.Count);
        GrassFuncManager.RemoveEvent(GrassEvent.VisibleGrassCount, () => _grassVisibleIDList.Count);
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

        // Don't do anything if resources are not found,
        // or no vertex is put on the mesh.
        if (grassData.Count == 0)
        {
            _boundsListVis.Clear();
            return;
        }

        if (!grassSetting.shaderToUse || !grassSetting.materialToUse)
        {
            Debug.LogWarning("Missing Compute Shader/Material in grass Settings", this);
            return;
        }

        if (!grassSetting.cuttingParticles)
        {
            Debug.LogWarning("Missing Cut Particles in grass Settings", this);
        }

        // Instantiate the shaders so they can point to their own buffers
        _instComputeShader = Instantiate(grassSetting.shaderToUse);
        instantiatedMaterial = Instantiate(grassSetting.materialToUse);

        var numSourceVertices = grassData.Count;

        // Create compute buffers
        // The stride is the size, in bytes, each object in the buffer takes up
        _sourceVertBuffer = new ComputeBuffer(numSourceVertices, SourceVertStride, ComputeBufferType.Structured);
        _sourceVertBuffer.SetData(grassData);

        _drawBuffer = new ComputeBuffer(MaxBufferSize, DrawStride, ComputeBufferType.Append);

        // _argsBuffer = new ComputeBuffer(1, _argsBufferReset.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            _argsBufferReset.Length * sizeof(uint));

        //uint only, per visible grass
        _visibleIDBuffer = new ComputeBuffer(grassData.Count, sizeof(uint), ComputeBufferType.Structured);

        // added for cutting
        //uint only, per visible grass
        _cutBuffer = new ComputeBuffer(grassData.Count, sizeof(float), ComputeBufferType.Structured);

        // added for cutting
        InitializeCutData();

        _cutBuffer.SetData(_cutIDs);

        // Cache the kernel IDs we will be dispatching
        _idGrassKernel = _instComputeShader.FindKernel("Main");

        // Set buffer data
        _instComputeShader.SetBuffer(_idGrassKernel, SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, VisibleIDBuffer, _visibleIDBuffer);
        instantiatedMaterial.SetBuffer(DrawTriangles, _drawBuffer);
        // added for cutting
        _instComputeShader.SetBuffer(_idGrassKernel, CutBuffer, _cutBuffer);
        // Set vertex data
        _instComputeShader.SetInt(NumSourceVertices, numSourceVertices);
        // cache shader property to int id for interactivity;
        _interactorDataID = Shader.PropertyToID("_InteractorData");

        // Calculate the number of threads to use. Get the thread size from the kernel
        // Then, divide the number of triangles by that size
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
        //set once only
        // _dispatchSize = Mathf.CeilToInt((int)(grassData.Count / _threadGroupSize));
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log((int)_threadGroupSize, 2);

        SetShaderData();
#if UNITY_EDITOR
        _interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
#endif
        SetupQuadTree(full);
        GetFrustumData();
    }

    private void SetupQuadTree(bool full)
    {
        if (grassData.Count <= 0) return;

        if (full)
        {
            InitCullingTree(grassSetting.cullingTreeDepth);
            for (int i = 0; i < grassData.Count; i++)
            {
                if (FindLeafForGrass(grassData[i].position, i))
                {
                    _grassVisibleIDList.Add(i);
                }
            }
        }
#if UNITY_EDITOR
        else
        {
            SetupForEditorMode();
        }
#endif
        var visibleArray = new uint[grassData.Count];
        for (int i = 0; i < _grassVisibleIDList.Count && i < visibleArray.Length; i++)
        {
            visibleArray[i] = (uint)_grassVisibleIDList[i];
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

        if (!_fastMode)
        {
            _mainCamera.farClipPlane = grassSetting.maxFadeDistance;
            UpdateCulling(_cameraFrustumPlanes);
            _visibleIDBuffer.SetData(_grassVisibleIDList);
        }
    }

    public void ResetFaster()
    {
        _fastMode = true;
        OnDisable();
        MainSetup(false);
    }

    // Update the shader with frame specific data
    private void SetGrassDataUpdate()
    {
        _instComputeShader.SetFloat(Time, UnityEngine.Time.time);

        if (_interactors.Count > 0)
        {
            UpdateInteractors();
        }

        if (_mainCamera)
        {
            _instComputeShader.SetVector(CameraPositionWs, _mainCamera.transform.position);
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
        _instComputeShader.SetFloat(InteractorsLength, _interactors.Count);
    }

    private void UpdateCutBuffer(Vector3 hitPoint, float radius)
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

                if (_cutIDs[currentIndex] <= hitPointY &&
                    !Mathf.Approximately(_cutIDs[currentIndex], -1))
                    continue;

                var squaredDistance = (hitPoint - grassPosition).sqrMagnitude;

                if (squaredDistance <= squaredRadius &&
                    (_cutIDs[currentIndex] > hitPointY ||
                     Mathf.Approximately(_cutIDs[currentIndex], -1)))
                {
                    if (_cutIDs[currentIndex] - 0.1f > hitPointY ||
                        Mathf.Approximately(_cutIDs[currentIndex], -1))
                    {
                        SpawnCuttingParticle(grassPosition, new Color(grassData[currentIndex].color.x,
                            grassData[currentIndex].color.y, grassData[currentIndex].color.z));
                    }

                    _cutIDs[currentIndex] = hitPointY;
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

    public void InitCullingTree(int cullingTreeDepth)
    {
        if (grassData.Count == 0)
        {
            ResetBounds();
            return;
        }

        InitializeBounds();
        _cullingTree = new CullingTree(_bounds, cullingTreeDepth);
        _leaves.Clear();
        _cullingTree.RetrieveAllLeaves(_leaves);
    }

    private void ResetBounds()
    {
        _bounds = new Bounds();
        _cullingTree = null;
        _boundsListVis.Clear();
        _grassVisibleIDList = new List<int>();
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

    public void UpdateCulling(Plane[] cameraFrustumPlanes)
    {
        _boundsListVis.Clear();
        _grassVisibleIDList.Clear();
        _cullingTree?.RetrieveLeaves(cameraFrustumPlanes, _boundsListVis, _grassVisibleIDList);
    }

    public bool FindLeafForGrass(Vector3 position, int index)
    {
        return _cullingTree != null && _cullingTree.FindLeaf(position, index);
    }

    public void GetNearbyGrass(Vector3 point, float radius)
    {
        _nearbyGrassIds.Clear();
        _cullingTree?.ReturnLeafList(point, _nearbyGrassIds, radius);
    }

    public void InitializeCutData()
    {
        _cutIDs = new float[grassData.Count];
        for (var i = 0; i < _cutIDs.Length; i++)
        {
            _cutIDs[i] = -1;
        }
    }

    public void AllClear()
    {
        grassData.Clear();
        _nearbyGrassIds.Clear();
        _grassVisibleIDList.Clear();
        _boundsListVis.Clear();
        _leaves.Clear();
        _cutIDs = Array.Empty<float>();
        _cullingTree = null;
        _bounds = new Bounds();
    }

    /*=======================================================================================
     *                              Setup Shader Data
     =======================================================================================*/

    private void SetShaderData()
    {
        // Send things to compute shader that dont need to be set every frame
        _instComputeShader.SetFloat(Time, UnityEngine.Time.time);
        _instComputeShader.SetFloat(GrassRandomHeightMin, grassSetting.randomHeightMin);
        _instComputeShader.SetFloat(GrassRandomHeightMax, grassSetting.randomHeightMax);
        _instComputeShader.SetFloat(WindSpeed, grassSetting.windSpeed);
        _instComputeShader.SetFloat(WindStrength, grassSetting.windStrength);

        _instComputeShader.SetFloat(InteractorStrength, grassSetting.interactorStrength);
        _instComputeShader.SetFloat(BladeRadius, grassSetting.bladeRadius);
        _instComputeShader.SetFloat(BladeForward, grassSetting.bladeForward);
        _instComputeShader.SetFloat(BladeCurve, Mathf.Max(0, grassSetting.bladeCurve));
        _instComputeShader.SetFloat(BottomWidth, grassSetting.bottomWidth);

        _instComputeShader.SetInt(MaxBladesPerVertex, grassSetting.bladesPerVertex);
        _instComputeShader.SetInt(MaxSegmentsPerBlade, grassSetting.segmentsPerBlade);

        _instComputeShader.SetFloat(MinHeight, grassSetting.minHeight);
        _instComputeShader.SetFloat(MinWidth, grassSetting.minWidth);

        _instComputeShader.SetFloat(MaxHeight, grassSetting.maxHeight);
        _instComputeShader.SetFloat(MaxWidth, grassSetting.maxWidth);
        instantiatedMaterial.SetColor(TopTint, grassSetting.topTint);
        instantiatedMaterial.SetColor(BottomTint, grassSetting.bottomTint);

        _instComputeShader.SetFloat(MinFadeDist, grassSetting.minFadeDistance);
        _instComputeShader.SetFloat(MaxFadeDist, grassSetting.maxFadeDistance);

        _instComputeShader.SetVector(ZonePosData, Vector3.zero);
        _instComputeShader.SetVector(ZoneScaleData, Vector3.one);
        _instComputeShader.SetFloat(SeasonWidth, 1.0f);
        _instComputeShader.SetFloat(SeasonHeight, 1.0f);

        if (instantiatedMaterial != null)
        {
            instantiatedMaterial.SetVector(ZonePosData, Vector3.zero);
            instantiatedMaterial.SetVector(ZoneScaleData, Vector3.one);
            instantiatedMaterial.SetFloat(SeasonWidth, 1.0f);
            instantiatedMaterial.SetFloat(SeasonHeight, 1.0f);
        }
    }

    public void UpdateSeasonData(Vector3 zonePosition, Vector3 zoneScale, Color seasonColor, float width, float height)
    {
        if (_instComputeShader != null)
        {
            _instComputeShader.SetVector(ZonePosData, zonePosition);
            _instComputeShader.SetVector(ZoneScaleData, zoneScale);
            _instComputeShader.SetFloat(SeasonWidth, width);
            _instComputeShader.SetFloat(SeasonHeight, height);
        }

        if (instantiatedMaterial != null)
        {
            instantiatedMaterial.SetVector(ZonePosData, zonePosition);
            instantiatedMaterial.SetVector(ZoneScaleData, zoneScale);
            instantiatedMaterial.SetFloat(SeasonWidth, width);
            instantiatedMaterial.SetFloat(SeasonHeight, height);
            instantiatedMaterial.SetColor(SeasonTint, seasonColor);
        }
    }

    private static readonly int SourceVertices = Shader.PropertyToID("_SourceVertices");
    private static readonly int DrawTriangles = Shader.PropertyToID("_DrawTriangles");
    private static readonly int IndirectArgsBuffer = Shader.PropertyToID("_IndirectArgsBuffer");
    private static readonly int NumSourceVertices = Shader.PropertyToID("_NumSourceVertices");

    // For culling
    private static readonly int VisibleIDBuffer = Shader.PropertyToID("_VisibleIDBuffer");

    private static readonly int CutBuffer = Shader.PropertyToID("_CutBuffer");
    private static readonly int Time = Shader.PropertyToID("_Time");
    private static readonly int GrassRandomHeightMin = Shader.PropertyToID("_GrassRandomHeightMin");
    private static readonly int GrassRandomHeightMax = Shader.PropertyToID("_GrassRandomHeightMax");
    private static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
    private static readonly int WindStrength = Shader.PropertyToID("_WindStrength");
    private static readonly int MinFadeDist = Shader.PropertyToID("_MinFadeDist");
    private static readonly int MaxFadeDist = Shader.PropertyToID("_MaxFadeDist");
    private static readonly int InteractorStrength = Shader.PropertyToID("_InteractorStrength");
    private static readonly int InteractorsLength = Shader.PropertyToID("_InteractorsLength");
    private static readonly int BladeRadius = Shader.PropertyToID("_BladeRadius");
    private static readonly int BladeForward = Shader.PropertyToID("_BladeForward");
    private static readonly int BladeCurve = Shader.PropertyToID("_BladeCurve");
    private static readonly int BottomWidth = Shader.PropertyToID("_BottomWidth");
    private static readonly int MaxBladesPerVertex = Shader.PropertyToID("_MaxBladesPerVertex");
    private static readonly int MaxSegmentsPerBlade = Shader.PropertyToID("_MaxSegmentsPerBlade");
    private static readonly int MinHeight = Shader.PropertyToID("_MinHeight");
    private static readonly int MinWidth = Shader.PropertyToID("_MinWidth");
    private static readonly int MaxHeight = Shader.PropertyToID("_MaxHeight");
    private static readonly int MaxWidth = Shader.PropertyToID("_MaxWidth");

    private static readonly int CameraPositionWs = Shader.PropertyToID("_CameraPositionWS");

    private static readonly int TopTint = Shader.PropertyToID("_TopTint");
    private static readonly int BottomTint = Shader.PropertyToID("_BottomTint");

    private static readonly int SeasonWidth = Shader.PropertyToID("_SeasonWidth");
    private static readonly int SeasonHeight = Shader.PropertyToID("_SeasonHeight");
    private static readonly int ZonePosData = Shader.PropertyToID("_ZonePosData");
    private static readonly int ZoneScaleData = Shader.PropertyToID("_ZoneScaleData");
    private static readonly int SeasonTint = Shader.PropertyToID("_SeasonTint");

#if UNITY_EDITOR

    public void SetupForEditorMode()
    {
        if (grassData.Count == 0) return;

        _grassVisibleIDList = new List<int>(grassData.Count);
        for (int i = 0; i < grassData.Count; i++)
        {
            _grassVisibleIDList.Add(i);
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

        _dispatchSize = (_grassVisibleIDList.Count + (int)_threadGroupSize - 1) >>
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
#endif
}