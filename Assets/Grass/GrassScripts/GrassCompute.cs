using System;
using System.Collections.Generic;
using EventBusSystem.Scripts;
using Grass.GrassScripts;
using Pool;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GrassCompute : MonoBehaviour
{
    private const int SourceVertStride = sizeof(float) * (3 + 3 + 2 + 3);
    private const int DrawStride = sizeof(float) * (3 + 3 + 4 + (3 + 2) * 3);
    private const int MaxBufferSize = 3000000;

    [SerializeField] private List<GrassData> grassData = new(); // base data lists
    [SerializeField] private GrassSettingSO grassSetting;

    [SerializeField, HideInInspector] private List<int> grassVisibleIDList = new();
    [SerializeField, HideInInspector] private float[] cutIDs;

    private readonly List<int> _nearbyGrassIds = new();
    private Bounds _bounds; // bounds of the total grass 
    private CullingTree _cullingTree;
    private Camera _mainCamera;
    private List<GrassInteractor> _interactors;
    private Vector4[] _interactorData;

    private ComputeBuffer _sourceVertBuffer; // A compute buffer to hold vertex data of the source mesh
    private ComputeBuffer _drawBuffer; // A compute buffer to hold vertex data of the generated mesh
    private GraphicsBuffer _argsBuffer; // A compute buffer to hold indirect draw arguments
    private ComputeBuffer _visibleIDBuffer; // buffer that contains the ids of all visible instances
    private ComputeBuffer _cutBuffer; // added for cutting

    private ComputeShader _grassComputeShader;
    private Material _grassMaterial;

    private int _idGrassKernel; // The id of the kernel in the grass compute shader
    private int _dispatchSize; // The x dispatch size for the grass computes shader
    private uint _threadGroupSize; // compute shader thread group size

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

    private EventBinding<CutGrass> _cutGrassEvent;

    public void Reset()
    {
#if UNITY_EDITOR
        _fastMode = false;
#endif
        ReleaseResources();
        MainSetup(true);
    }

    private void ReleaseResources()
    {
        ReleaseBuffer();
        if (!Application.isPlaying)
        {
            DestroyImmediate(_grassComputeShader);
            DestroyImmediate(_grassMaterial);
        }
        else
        {
            Destroy(_grassComputeShader);
            Destroy(_grassMaterial);
        }
    }
#if UNITY_EDITOR

    private SceneView _view;

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
        _interactors = new List<GrassInteractor>();
        _interactorData = Array.Empty<Vector4>();

        RegisterEvents();
        MainSetup(true);
    }

    private void Start()
    {
#if UNITY_EDITOR
        var interactors = FindObjectsByType<GrassInteractor>(FindObjectsSortMode.None);
        for (int i = 0; i < interactors.Length; i++)
        {
            _interactors.Add(interactors[i]);
        }

        _interactorData = new Vector4[_interactors.Count];
        for (int i = 0; i < _interactors.Count; i++)
        {
            var pos = _interactors[i].transform.position;
            _interactorData[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].radius);
        }
#endif
    }

    private void Update()
    {
        if (grassData.Count <= 0) return;
        GetFrustumData();
        SetGrassDataUpdate();

        _drawBuffer.SetCounterValue(0);
        _argsBuffer.SetData(_argsBufferReset);

        _dispatchSize = (grassVisibleIDList.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log(_threadGroupSize, 2);
        if (grassVisibleIDList.Count > 0)
        {
            _dispatchSize += 1;
        }

        if (_dispatchSize > 0)
        {
            _grassComputeShader.Dispatch(_idGrassKernel, _dispatchSize, 1, 1);
#if UNITY_EDITOR
            UpdateBufferCount();
#endif
            var renderParams = new RenderParams(_grassMaterial)
            {
                worldBounds = _bounds,
                shadowCastingMode = grassSetting.castShadow,
                receiveShadows = true,
            };
            // var args = new uint[5];
            // _argsBuffer.GetData(args);
            // Debug.Log(_argsBufferReset[0]);
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, _argsBuffer);
        }
    }

    private void OnDisable()
    {
        UnregisterEvents();
        ReleaseBuffer();
        _interactors = null;
        _interactorData = null;

        if (!Application.isPlaying)
        {
            DestroyImmediate(_grassComputeShader);
            DestroyImmediate(_grassMaterial);
        }
        else
        {
            Destroy(_grassComputeShader);
            Destroy(_grassMaterial);
        }
    }

#if UNITY_EDITOR
    // draw the bound gizmos
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
        EventBus.Register<GrassInteractor>(new InteractorAddEvent(), AddInteractor);
        EventBus.Register<GrassInteractor>(new InteractorRemoveEvent(), RemoveInteractor);

        _cutGrassEvent = new EventBinding<CutGrass>(CutGrass);
        EventBus<CutGrass>.Register(_cutGrassEvent);
    }

    private void UnregisterEvents()
    {
        EventBus.Deregister<GrassInteractor>(new InteractorAddEvent(), AddInteractor);
        EventBus.Deregister<GrassInteractor>(new InteractorRemoveEvent(), RemoveInteractor);

        EventBus<CutGrass>.Deregister(_cutGrassEvent);
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
        SetCamera();
        if (grassData.Count <= 0)
        {
            ClearAllData();
            return;
        }

        if (ValidateSetup()) return;
        InitResources();
        InitBuffers();
        InitComputeShader();
        SetShaderData();
        InitQuadTree(full);
    }

    private void SetCamera()
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
        _grassComputeShader = Instantiate(grassSetting.shaderToUse);
        _grassMaterial = Instantiate(grassSetting.materialToUse);
        _idGrassKernel = _grassComputeShader.FindKernel("Main");
        _grassComputeShader.GetKernelThreadGroupSizes(_idGrassKernel, out _threadGroupSize, out _, out _);
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

    private void InitComputeShader()
    {
        _grassComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.SourceVertices, _sourceVertBuffer);
        _grassComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.DrawTriangles, _drawBuffer);
        _grassComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.IndirectArgsBuffer, _argsBuffer);
        _grassComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.VisibleIDBuffer, _visibleIDBuffer);
        _grassComputeShader.SetBuffer(_idGrassKernel, GrassShaderPropertyID.CutBuffer, _cutBuffer);
        _grassComputeShader.SetInt(GrassShaderPropertyID.NumSourceVertices, grassData.Count);

        _grassMaterial.SetBuffer(GrassShaderPropertyID.DrawTriangles, _drawBuffer);

        _dispatchSize = (grassData.Count + (int)_threadGroupSize - 1) >>
                        (int)Math.Log((int)_threadGroupSize, 2);
    }

    private void InitQuadTree(bool full)
    {
        if (full)
        {
            _bounds = new Bounds(grassData[0].position, Vector3.zero);
            foreach (var grass in grassData)
            {
                _bounds.Encapsulate(grass.position);
            }

            var extents = _bounds.extents;
            _bounds.extents = extents * 1.1f;

            _cullingTree = new CullingTree(_bounds, grassSetting.cullingTreeDepth);
            _cullingTree.InsertGrassData(grassData, grassVisibleIDList);
        }
        else
        {
            grassVisibleIDList = new List<int>(grassData.Count);
            for (int i = 0; i < grassData.Count; i++)
            {
                grassVisibleIDList.Add(i);
            }

            _visibleIDBuffer?.SetData(grassVisibleIDList);
        }
    }

    private void GetFrustumData()
    {
        _cachedCamPos = Vector3.zero;
        _cachedCamRot = Quaternion.identity;

        if (!_mainCamera) return;

        if (_cachedCamRot == _mainCamera.transform.rotation && _cachedCamPos == _mainCamera.transform.position)
            return;

        _cachedCamPos = _mainCamera.transform.position;
        _cachedCamRot = _mainCamera.transform.rotation;

#if UNITY_EDITOR
        if (_fastMode) return;
#endif
        grassVisibleIDList.Clear();
        _cullingTree?.FrustumCullTest(_mainCamera, grassVisibleIDList);
        _visibleIDBuffer?.SetData(grassVisibleIDList);
    }

    // Update the shader with frame-specific data
    private void SetGrassDataUpdate()
    {
        _grassComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);

        UpdateInteractors();

        if (_mainCamera)
        {
            _grassComputeShader.SetVector(GrassShaderPropertyID.CameraPositionWs, _mainCamera.transform.position);
        }
    }

    private void UpdateInteractors()
    {
        var interactorCount = _interactors.Count;

        if (interactorCount <= 0)
        {
            _grassComputeShader.SetFloat(GrassShaderPropertyID.InteractorsLength, 0);
            return;
        }

        for (int i = 0; i < interactorCount; i++)
        {
            var pos = _interactors[i].transform.position;
            _interactorData[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].radius);
        }

        _grassComputeShader.SetVectorArray(GrassShaderPropertyID.InteractorData, _interactorData);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.InteractorsLength, interactorCount);
    }

    public void AddInteractor(GrassInteractor interactor)
    {
        if (_interactors.Contains(interactor)) return;
        _interactors.Add(interactor);
        ResizeInteractorData();
    }

    public void RemoveInteractor(GrassInteractor interactor)
    {
        if (_interactors == null || !_interactors.Contains(interactor)) return;
        _interactors.Remove(interactor);
        ResizeInteractorData();
    }

    private void ResizeInteractorData()
    {
        var interactorsLength = _interactors.Count;

        if (_interactorData == null || _interactorData.Length != interactorsLength)
        {
            _interactorData = new Vector4[interactorsLength];
        }
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

    private void CutGrass(CutGrass cutGrass)
    {
        if (grassData.Count <= 0) return;

        GetNearbyGrass(cutGrass.position, cutGrass.radius);

        var squaredRadius = cutGrass.radius * cutGrass.radius;
        var hitPointY = cutGrass.position.y;

        // 가져온 ID들에 대해서만 잘리는 검사를 수행
        foreach (var currentIndex in _nearbyGrassIds)
        {
            if (!ShouldCutGrass(currentIndex, hitPointY, cutGrass.position, squaredRadius)) continue;
            if (IsFirstCutAtHeight(currentIndex, hitPointY))
            {
                var grassPosition = grassData[currentIndex].position;
                var grassColor = GetGrassColor(currentIndex, grassPosition);
                SpawnCuttingParticle(grassPosition, grassColor);
            }

            cutIDs[currentIndex] = hitPointY;
        }

        _cutBuffer.SetData(cutIDs);
    }

    private bool ShouldCutGrass(int index, float hitPointY, Vector3 hitPoint, float squaredRadius)
    {
        if (cutIDs[index] <= hitPointY && !Mathf.Approximately(cutIDs[index], -1)) return false;
        var grassPosition = grassData[index].position;
        var squaredDistance = (hitPoint - grassPosition).sqrMagnitude;
        return squaredDistance <= squaredRadius;
    }

    private bool IsFirstCutAtHeight(int index, float hitPointY)
    {
        return cutIDs[index] - 0.1f > hitPointY || Mathf.Approximately(cutIDs[index], -1);
    }

    private Color GetGrassColor(int index, Vector3 position)
    {
        var baseColor = new Color(grassData[index].color.x, grassData[index].color.y, grassData[index].color.z);
        var colorRequest = new GrassColorRequest { position = position, defaultColor = baseColor };

        return EventBusExtensions.TryRequest<GrassColorRequest, GrassColorResponse>(colorRequest, out var response)
            ? response.resultColor
            : baseColor;
    }

    private void SpawnCuttingParticle(Vector3 position, Color col)
    {
        var leafParticle = PoolObjectManager.Get<ParticleSystem>(PoolObjectKey.Leaf, position).main;
        leafParticle.startColor = new ParticleSystem.MinMaxGradient(col);
    }

    public void GetNearbyGrass(Vector3 point, float radius)
    {
        _nearbyGrassIds.Clear();
        _cullingTree.GetObjectsInRadiusTest(_nearbyGrassIds, point, radius);
    }

    /*=======================================================================================
     *                              Setup Shader Data
     =======================================================================================*/

    #region Set Shader Data

    public void SetShaderData()
    {
        // Send things to compute shader that don't need to be set every frame
        _grassComputeShader.SetFloat(GrassShaderPropertyID.Time, Time.time);
        SetInteractorStrength();
        SetBladeMinMax();
        SetBladeShape();
        SetWind();
        SetTint();
        SetBlend();
        SetShadow();
        SetAdditionalLight();
        SetSpecular();
        SetGrassAppearance();
        SetFade();
    }

    public void SetInteractorStrength()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetFloat(GrassShaderPropertyID.InteractorStrength, grassSetting.interactorStrength);
    }

    public void SetBladeMinMax()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetFloat(GrassShaderPropertyID.MinWidth, grassSetting.minWidth);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.MaxWidth, grassSetting.maxWidth);

        _grassComputeShader.SetFloat(GrassShaderPropertyID.MinHeight, grassSetting.minHeight);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.MaxHeight, grassSetting.maxHeight);

        _grassComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMin, grassSetting.randomHeightMin);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.GrassRandomHeightMax, grassSetting.randomHeightMax);
    }

    public void SetBladeShape()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetFloat(GrassShaderPropertyID.BladeRadius, grassSetting.bladeRadius);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.BladeForward, grassSetting.bladeForward);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.BladeCurve, Mathf.Max(0, grassSetting.bladeCurve));
        _grassComputeShader.SetFloat(GrassShaderPropertyID.BottomWidth, grassSetting.bottomWidth);
    }

    public void SetWind()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetFloat(GrassShaderPropertyID.WindSpeed, grassSetting.windSpeed);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.WindStrength, grassSetting.windStrength);
        _grassComputeShader.SetVector(GrassShaderPropertyID.WindDirection, grassSetting.windDirectionVector);
    }

    public void SetTint()
    {
        if (_grassMaterial == null) return;
        _grassMaterial.SetColor(GrassShaderPropertyID.TopTint, grassSetting.topTint);
        _grassMaterial.SetColor(GrassShaderPropertyID.BottomTint, grassSetting.bottomTint);
    }

    public void SetBlend()
    {
        if (_grassMaterial == null) return;
        _grassMaterial.SetFloat(GrassShaderPropertyID.AmbientStrength, grassSetting.ambientStrength);
        _grassMaterial.SetFloat(GrassShaderPropertyID.BlendMultiply, grassSetting.blendMultiply);
        _grassMaterial.SetFloat(GrassShaderPropertyID.BlendOffset, grassSetting.blendOffset);

        if (_grassMaterial.HasProperty(GrassShaderPropertyID.AmbientAdjustmentColor))
        {
            _grassMaterial.SetColor(GrassShaderPropertyID.AmbientAdjustmentColor,
                grassSetting.ambientAdjustmentColor);
        }
    }

    public void SetShadow()
    {
        if (_grassMaterial == null) return;
        _grassMaterial.SetFloat(GrassShaderPropertyID.ShadowDistance, grassSetting.shadowDistance);
        _grassMaterial.SetFloat(GrassShaderPropertyID.ShadowFadeRange, grassSetting.shadowFadeRange);
        _grassMaterial.SetFloat(GrassShaderPropertyID.MinShadowBrightness, grassSetting.shadowBrightness);
        _grassMaterial.SetColor(GrassShaderPropertyID.ShadowColor, grassSetting.shadowColor);
    }

    public void SetAdditionalLight()
    {
        if (_grassMaterial == null) return;
        _grassMaterial.SetFloat(GrassShaderPropertyID.AdditionalLightIntensity,
            grassSetting.additionalLightIntensity);
        _grassMaterial.SetFloat(GrassShaderPropertyID.AdditionalLightShadowStrength,
            grassSetting.additionalLightShadowStrength);
        _grassMaterial.SetColor(GrassShaderPropertyID.AdditionalShadowColor,
            grassSetting.additionalLightShadowColor);
    }

    public void SetSpecular()
    {
        if (_grassMaterial == null) return;
        _grassMaterial.SetFloat(GrassShaderPropertyID.SpecularFalloff, grassSetting.specularFalloff);
        _grassMaterial.SetFloat(GrassShaderPropertyID.SpecularStrength, grassSetting.specularStrength);
        _grassMaterial.SetFloat(GrassShaderPropertyID.SpecularHeight, grassSetting.specularHeight);
    }

    public void SetGrassAppearance()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetInt(GrassShaderPropertyID.GrassAmount, grassSetting.grassAmount);
        _grassComputeShader.SetInt(GrassShaderPropertyID.GrassQuality, grassSetting.grassQuality);
    }

    public void SetFade()
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetFloat(GrassShaderPropertyID.MinFadeDist, grassSetting.minFadeDistance);
        _grassComputeShader.SetFloat(GrassShaderPropertyID.MaxFadeDist, grassSetting.maxFadeDistance);
        if (Application.isPlaying)
        {
            _mainCamera.farClipPlane = grassSetting.maxFadeDistance;
        }
    }

    #endregion

    public void UpdateSeasonData(ref Vector4[] positions, ref Vector4[] scales, ref Vector4[] colors,
        ref Vector4[] widthHeights,
        int zoneCount)
    {
        if (_grassComputeShader == null) return;
        _grassComputeShader.SetVectorArray(GrassShaderPropertyID.ZonePositions, positions);
        _grassComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneScales, scales);
        _grassComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneColors, colors);
        _grassComputeShader.SetVectorArray(GrassShaderPropertyID.ZoneWidthHeights, widthHeights);
        _grassComputeShader.SetInt(GrassShaderPropertyID.ZoneCount, zoneCount);
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
    private bool _fastMode;

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
        _fastMode = true;
        ReleaseResources();
        MainSetup(false);
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