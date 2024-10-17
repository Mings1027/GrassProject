using System;
using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;
using PoolControl;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GrassComputeScript : MonoBehaviour
{
#if UNITY_EDITOR
    [HideInInspector] public bool autoUpdate; // very slow, but will update always
#endif
    private Camera _mainCamera; // main camera
    public GrassSettingSO currentPresets; // grass settings to send to the compute shader
    private List<GrassInteractor> _interactors;
    [SerializeField, HideInInspector] private List<GrassData> grassData = new(); // base data lists
    private readonly List<int> _grassList = new();
    private List<int> _grassVisibleIDList = new(); // list of all visible grass ids, rest are culled
    private bool _initialized; // A state variable to help keep track of whether compute buffers have been set up

    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeShader _instComputeShader; // Instantiate the shaders so data belong to their unique compute buffers
    private ComputeBuffer _visibleIDBuffer; // buffer that contains the ids of all visible instances

    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass compute shader
    private uint _threadGroupSize; // compute shader thread group size

    // The size of one entry in the various compute buffers, size comes from the float3/float2 entrees in the shader
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 4 + (3 + 2) * 3);

    private Bounds _bounds; // bounds of the total grass 
    private ComputeBuffer _cutBuffer; // added for cutting
    private float[] _cutIDs;

    private readonly uint[] _argsBufferReset =
    {
        0, // Number of vertices to render (Calculated in the compute shader with "InterlockedAdd(_IndirectArgsBuffer[0].numVertices);")
        1, // Number of instances to render (should only be 1 instance since it should produce a single mesh)
        0, // Index of the first vertex to render
        0, // Index of the first instance to render
        0 // Not used
    };

    // culling tree data ----------------------------------------------------------------------
    private CullingTreeNode _cullingTree;
    private readonly List<Bounds> _boundsListVis = new();
    private readonly List<CullingTreeNode> _leaves = new();
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];
    private float _cameraOriginalFarPlane;

    // list of -1 to overwrite the grassvisible buffer with
    // private List<int> _empty = new();

    // speeding up the editor a bit
    private Vector3 _cachedCamPos;
    private Quaternion _cachedCamRot;
    private bool _fastMode;
    private int _shaderID;

    // max buffer size can depend on platform and your draw stride, you may have to change it
    private readonly int _maxBufferSize = 2500000;

    [SerializeField] private Material instantiatedMaterial;

    ///-------------------------------------------------------------------------------------

    public List<GrassData> GrassDataList
    {
        get => grassData;
        set => grassData = value;
    }

#if UNITY_EDITOR
    private SceneView _view;

    public void Reset()
    {
        _fastMode = false;
        OnDisable();
        MainSetup(true);
    }

    private void OnDestroy()
    {
        // When the window is destroyed, remove the delegate
        // so that it will no longer do any drawing.
        SceneView.duringSceneGui -= OnScene;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    private void OnScene(SceneView scene)
    {
        _view = scene;
        if (!Application.isPlaying)
        {
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
        GrassEventManager<GrassInteractor>.AddListener(GrassEvent.InteractorAdded, AddInteractor);
        GrassEventManager<GrassInteractor>.AddListener(GrassEvent.InteractorRemoved, RemoveInteractor);

        _interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
        // If initialized, call on disable to clean things up
        if (_initialized)
        {
            OnDisable();
        }

        MainSetup(true);
    }

    // LateUpdate is called after all Update calls
    private void Update()
    {
        // If in edit mode, we need to update the shaders each Update to make sure settings changes are applied
        // Don't worry, in edit mode, Update isn't called each frame
#if UNITY_EDITOR
        if (!Application.isPlaying && autoUpdate && !_fastMode)
        {
            OnDisable();
            OnEnable();
        }

        // If not initialized, do nothing (creating zero-length buffer will crash)
        if (!_initialized)
        {
            // Initialization is not done, please check if there are null components
            // or just because there is not vertex being painted.
            return;
        }
#endif
        // get the data from the camera for culling
        GetFrustumData();
        // Update the shader with frame specific data
        SetGrassDataUpdate();
        // Clear the draw and indirect args buffers of last frame's data
        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);
        // _dispatchSize = Mathf.CeilToInt((int)(_grassVisibleIDList.Count / _threadGroupSize));
        _dispatchSize = (_grassVisibleIDList.Count + (int)_threadGroupSize - 1) >> (int)Math.Log(_threadGroupSize, 2);
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
                shadowCastingMode = currentPresets.castShadow,
                receiveShadows = true,
            };

            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _argsBuffer);
        }
    }

    private void OnDisable()
    {
        GrassEventManager<GrassInteractor>.RemoveListener(GrassEvent.InteractorAdded, AddInteractor);
        GrassEventManager<GrassInteractor>.RemoveListener(GrassEvent.InteractorRemoved, RemoveInteractor);

        _interactors.Clear();
        // Dispose of buffers and copied shaders here
        if (_initialized)
        {
            // If the application is not in play mode, we have to call DestroyImmediate
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

            // Release each buffer
            _sourceVertBuffer?.Release();
            _drawBuffer?.Release();
            _argsBuffer?.Release();
            _visibleIDBuffer?.Release();
            // added for cutting
            _cutBuffer?.Release();
        }

        _initialized = false;
    }

#if UNITY_EDITOR
    // draw the bounds gizmos
    private void OnDrawGizmos()
    {
        if (currentPresets)
        {
            if (currentPresets.drawBounds)
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

    private void MainSetup(bool full)
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= OnScene;
        SceneView.duringSceneGui += OnScene;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
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
        if (grassData.Count == 0) return;

        if (!currentPresets.shaderToUse || !currentPresets.materialToUse)
        {
            Debug.LogWarning("Missing Compute Shader/Material in grass Settings", this);
            return;
        }

        if (!currentPresets.cuttingParticles)
        {
            Debug.LogWarning("Missing Cut Particles in grass Settings", this);
        }

        _initialized = true;

        // Instantiate the shaders so they can point to their own buffers
        _instComputeShader = Instantiate(currentPresets.shaderToUse);
        instantiatedMaterial = Instantiate(currentPresets.materialToUse);

        var numSourceVertices = grassData.Count;

        // Create compute buffers
        // The stride is the size, in bytes, each object in the buffer takes up
        _sourceVertBuffer = new ComputeBuffer(numSourceVertices, SourceVertStride, ComputeBufferType.Structured,
            ComputeBufferMode.Immutable);
        _sourceVertBuffer.SetData(grassData);

        _drawBuffer = new ComputeBuffer(_maxBufferSize, DrawStride, ComputeBufferType.Append);

        // _argsBuffer = new ComputeBuffer(1, _argsBufferReset.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            _argsBufferReset.Length * sizeof(uint));

        //uint only, per visible grass
        _visibleIDBuffer = new ComputeBuffer(grassData.Count, sizeof(int), ComputeBufferType.Structured);

        // added for cutting
        //uint only, per visible grass
        _cutBuffer = new ComputeBuffer(grassData.Count, sizeof(float), ComputeBufferType.Structured);

        // added for cutting
        _cutIDs = new float[grassData.Count];

        for (var i = 0; i < _cutIDs.Length; i++)
        {
            _cutIDs[i] = -1;
        }

        // Cache the kernel IDs we will be dispatching
        _idGrassKernel = _instComputeShader.FindKernel("Main");

        // Set buffer data
        _instComputeShader.SetBuffer(_idGrassKernel, SourceVertices, _sourceVertBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, DrawTriangles, _drawBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, IndirectArgsBuffer, _argsBuffer);
        _instComputeShader.SetBuffer(_idGrassKernel, VisibleIDBuffer, _visibleIDBuffer);
        // added for cutting
        _instComputeShader.SetBuffer(_idGrassKernel, CutBuffer, _cutBuffer);
        instantiatedMaterial.SetBuffer(DrawTriangles, _drawBuffer);
        // Set vertex data
        _instComputeShader.SetInt(NumSourceVertices, numSourceVertices);
        // cache shader property to int id for interactivity;
        _shaderID = Shader.PropertyToID("_PositionsMoving");

        // Calculate the number of threads to use. Get the thread size from the kernel
        // Then, divide the number of triangles by that size
        _instComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
        //set once only
        // _dispatchSize = Mathf.CeilToInt((int)(grassData.Count / _threadGroupSize));
        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >> (int)Math.Log((int)_threadGroupSize, 2);

        SetGrassDataBase(full);

        if (full)
        {
            UpdateBounds();
        }

        SetupQuadTree(full);
    }

    private void UpdateBounds()
    {
        // Get the bounds of all the grass points and then expand
        _bounds = new Bounds(grassData[0].position, Vector3.one);

        for (var i = 0; i < grassData.Count; i++)
        {
            var target = grassData[i].position;
            _bounds.Encapsulate(target);
        }
    }

    private void SetupQuadTree(bool full)
    {
        if (full)
        {
            _cullingTree = new CullingTreeNode(_bounds, currentPresets.cullingTreeDepth);
            _leaves.Clear();
            _cullingTree.RetrieveAllLeaves(_leaves);
            //add the id of each grass point into the right cullingtree
            for (var i = 0; i < grassData.Count; i++)
            {
                _cullingTree.FindLeaf(grassData[i].position, i);
            }

            _cullingTree.ClearEmpty();
        }
        else
        {
            // just make everything visible while editing grass
            _grassVisibleIDList = Enumerable.Range(0, grassData.Count).ToArray().ToList();
            _visibleIDBuffer.SetData(_grassVisibleIDList);
        }
    }

    private void GetFrustumData()
    {
        if (!_mainCamera) return;

        // Check if the camera's position or rotation has changed
        if (_cachedCamRot == _mainCamera.transform.rotation && _cachedCamPos == _mainCamera.transform.position &&
            Application.isPlaying) return; // Camera hasn't moved, no need for frustum culling

        // Cache camera position and rotation for next frame
        _cachedCamPos = _mainCamera.transform.position;
        _cachedCamRot = _mainCamera.transform.rotation;

        // Get frustum data from the main camera without modifying far clip plane
        GeometryUtility.CalculateFrustumPlanes(_mainCamera, _cameraFrustumPlanes);

        if (!_fastMode)
        {
            // Perform full frustum culling
            _cameraOriginalFarPlane = _mainCamera.farClipPlane;
            _mainCamera.farClipPlane = currentPresets.maxFadeDistance;
            _boundsListVis.Clear();
            _grassVisibleIDList.Clear();
            _cullingTree.RetrieveLeaves(_cameraFrustumPlanes, _boundsListVis, _grassVisibleIDList);
            _visibleIDBuffer.SetData(_grassVisibleIDList);
            _mainCamera.farClipPlane = _cameraOriginalFarPlane;
        }
    }

    private void SetGrassDataBase(bool full)
    {
        SetShaderData();

        if (full)
        {
            _instComputeShader.SetFloat(MinFadeDist, currentPresets.minFadeDistance);
            _instComputeShader.SetFloat(MaxFadeDist, currentPresets.maxFadeDistance);
            _interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None).ToList();
        }
        else
        {
            _instComputeShader.SetFloat(MinFadeDist, currentPresets.minFadeDistance);
            _instComputeShader.SetFloat(MaxFadeDist, currentPresets.maxFadeDistance);
        }

        _cutBuffer.SetData(_cutIDs);
    }

    public void ResetFaster()
    {
        _fastMode = true;
        OnDisable();
        MainSetup(false);
    }

    private void SetGrassDataUpdate()
    {
        // Variables sent to the shader every frame
        _instComputeShader.SetFloat(Time, UnityEngine.Time.time);

        // Update interactors data if interactors exist
        if (_interactors.Count > 0)
        {
            var interectors = _interactors.Count;
            var positions = new Vector4[interectors];

            for (var i = interectors - 1; i >= 0; i--)
            {
                var pos = _interactors[i].transform.position;
                positions[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].radius);
            }

            _instComputeShader.SetVectorArray(_shaderID, positions);
            _instComputeShader.SetFloat(InteractorsLength, interectors);
        }

        // Update camera position
        if (_mainCamera)
        {
            _instComputeShader.SetVector(CameraPositionWs, _mainCamera.transform.position);
        }

#if UNITY_EDITOR
        else if (_view && _view.camera)
        {
            _instComputeShader.SetVector(CameraPositionWs, _view.camera.transform.position);
        }

#endif
    }

    public void UpdateCutBuffer(Vector3 hitPoint, float radius)
    {
        if (grassData.Count > 0)
        {
            _grassList.Clear();
            _cullingTree.ReturnLeafList(hitPoint, _grassList, radius);

            var squaredRadius = radius * radius;
            var hitPointY = hitPoint.y;

            for (var i = 0; i < _grassList.Count; i++)
            {
                var currentIndex = _grassList[i];
                var grassPosition = grassData[currentIndex].position;

                if (_cutIDs[currentIndex] <= hitPointY && !Mathf.Approximately(_cutIDs[currentIndex], -1))
                    continue;

                var squaredDistance = (hitPoint - grassPosition).sqrMagnitude;

                if (squaredDistance <= squaredRadius &&
                    (_cutIDs[currentIndex] > hitPointY || Mathf.Approximately(_cutIDs[currentIndex], -1)))
                {
                    if (_cutIDs[currentIndex] - 0.1f > hitPointY || Mathf.Approximately(_cutIDs[currentIndex], -1))
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
    /*=======================================================================================
     *                              Setup Shader Data
     =======================================================================================*/

    private void SetShaderData()
    {
        // Send things to compute shader that dont need to be set every frame
        _instComputeShader.SetFloat(Time, UnityEngine.Time.time);
        _instComputeShader.SetFloat(GrassRandomHeightMin, currentPresets.randomHeightMin);
        _instComputeShader.SetFloat(GrassRandomHeightMax, currentPresets.randomHeightMax);
        _instComputeShader.SetFloat(WindSpeed, currentPresets.windSpeed);
        _instComputeShader.SetFloat(WindStrength, currentPresets.windStrength);

        _instComputeShader.SetFloat(InteractorStrength, currentPresets.interactorStrength);
        _instComputeShader.SetFloat(BladeRadius, currentPresets.bladeRadius);
        _instComputeShader.SetFloat(BladeForward, currentPresets.bladeForward);
        _instComputeShader.SetFloat(BladeCurve, Mathf.Max(0, currentPresets.bladeCurve));
        _instComputeShader.SetFloat(BottomWidth, currentPresets.bottomWidth);

        _instComputeShader.SetInt(MaxBladesPerVertex, currentPresets.bladesPerVertex);
        _instComputeShader.SetInt(MaxSegmentsPerBlade, currentPresets.segmentsPerBlade);

        _instComputeShader.SetFloat(MinHeight, currentPresets.minHeight);
        _instComputeShader.SetFloat(MinWidth, currentPresets.minWidth);

        _instComputeShader.SetFloat(MaxHeight, currentPresets.maxHeight);
        _instComputeShader.SetFloat(MaxWidth, currentPresets.maxWidth);
        instantiatedMaterial.SetColor(TopTint, currentPresets.topTint);
        instantiatedMaterial.SetColor(BottomTint, currentPresets.bottomTint);
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