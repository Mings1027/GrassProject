using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[ExecuteInEditMode]
public class RenderTerrainMap : MonoBehaviour
{
    private static readonly int TerrainDiffuse = Shader.PropertyToID("_TerrainDiffuse");
    private static readonly int OrthographicCamSizeTerrain = Shader.PropertyToID("_OrthographicCamSizeTerrain");
    private static readonly int OrthographicCamPosTerrain = Shader.PropertyToID("_OrthographicCamPosTerrain");

    public Camera camToDrawWith;

    [SerializeField] private LayerMask layer; // layer to render
    [SerializeField] private Renderer[] renderers; // objects to render
    [SerializeField] private Terrain[] terrains; // unity terrain to render
    [SerializeField] private int resolution = 512; // map resolution
    [SerializeField] private float adjustScaling = 2.5f; // padding the total size
    [SerializeField] private float repeatRate = 5f;
    [SerializeField] private bool realTimeDiffuse;

    private RenderTexture _tempTex;
    private CancellationTokenSource _updaetCts;
    private Bounds _bounds;

    public bool RealTimeDiffuse
    {
        get => realTimeDiffuse;
        set
        {
            if (realTimeDiffuse != value)
            {
                realTimeDiffuse = value;
                if (Application.isPlaying)
                {
                    HandelRealTimeDiffuse();
                }
            }
        }
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        HandelRealTimeDiffuse();
    }
#endif
    private void OnEnable()
    {
        _bounds = new Bounds(transform.position, Vector3.zero);
        _tempTex = new RenderTexture(resolution, resolution, 24);
        GetBounds();
        SetUpCam();
        DrawToMap(TerrainDiffuse);

        if (Application.isPlaying && realTimeDiffuse)
        {
            StartRealTimeDiffuseUpdate();
        }
    }

    private void OnDisable()
    {
        StopRealTimeDiffuseUpdate();
    }

    private void HandelRealTimeDiffuse()
    {
        if (realTimeDiffuse)
        {
            StartRealTimeDiffuseUpdate();
        }
        else
        {
            StopRealTimeDiffuseUpdate();
        }
    }

    private void StartRealTimeDiffuseUpdate()
    {
        _updaetCts?.Cancel();
        _updaetCts?.Dispose();
        _updaetCts = new CancellationTokenSource();
        _ = UpdateTex();
    }

    private void StopRealTimeDiffuseUpdate()
    {
        _updaetCts?.Cancel();
        _updaetCts?.Dispose();
        _updaetCts = null;
    }

    private async UniTask UpdateTex()
    {
        await UniTask.Delay(1000, cancellationToken: _updaetCts.Token);

        while (!_updaetCts.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(repeatRate), cancellationToken: _updaetCts.Token);
            DrawToMap(TerrainDiffuse);
        }
    }

    private void GetBounds()
    {
        if (renderers.Length > 0)
        {
            foreach (var render in renderers)
            {
                if (_bounds.size.magnitude < 0.1f)
                {
                    _bounds = new Bounds(render.transform.position, Vector3.zero);
                }

                _bounds.Encapsulate(render.bounds);
            }
        }

        if (terrains.Length > 0)
        {
            foreach (var terrain in terrains)
            {
                if (_bounds.size.magnitude < 0.1f)
                {
                    _bounds = new Bounds(terrain.transform.position, Vector3.zero);
                }

                var terrainCenter = terrain.GetPosition() + terrain.terrainData.bounds.center;
                var worldBounds = new Bounds(terrainCenter, terrain.terrainData.bounds.size);
                _bounds.Encapsulate(worldBounds);
            }
        }
    }

    private void SetUpCam()
    {
        if (camToDrawWith == null)
        {
            camToDrawWith = GetComponentInChildren<Camera>();
        }

        var size = _bounds.size.magnitude;
        camToDrawWith.cullingMask = layer;
        camToDrawWith.orthographicSize = size / adjustScaling;
        // camToDrawWith.transform.parent = null;
        camToDrawWith.transform.position = _bounds.center + new Vector3(0, _bounds.extents.y + 5f, 0);
        // camToDrawWith.transform.parent = gameObject.transform;
    }

    private void DrawToMap(int target)
    {
        camToDrawWith.enabled = true;
        camToDrawWith.targetTexture = _tempTex;
        camToDrawWith.depthTextureMode = DepthTextureMode.Depth;

        Shader.SetGlobalFloat(OrthographicCamSizeTerrain, camToDrawWith.orthographicSize);
        Shader.SetGlobalVector(OrthographicCamPosTerrain, camToDrawWith.transform.position);
        camToDrawWith.Render();
        Shader.SetGlobalTexture(target, _tempTex);

        camToDrawWith.enabled = false;
    }
}