using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

[ExecuteInEditMode]
public class RenderTerrainMap : MonoBehaviour
{
    public Camera camToDrawWith;

    // layer to render
    [SerializeField] private LayerMask layer;

    // objects to render
    [SerializeField] private Renderer[] renderers;

    // unity terrain to render
    [SerializeField] private Terrain[] terrains;

    // map resolution
    [SerializeField] private int resolution = 512;

    // padding the total size
    [SerializeField] private float adjustScaling = 2.5f;

    [SerializeField] private bool realTimeDiffuse;

    [SerializeField] private float repeatRate = 5f;

    private RenderTexture _tempTex;
    private Bounds _bounds;
    private static readonly int TerrainDiffuse = Shader.PropertyToID("_TerrainDiffuse");
    private static readonly int OrthographicCamSizeTerrain = Shader.PropertyToID("_OrthographicCamSizeTerrain");
    private static readonly int OrthographicCamPosTerrain = Shader.PropertyToID("_OrthographicCamPosTerrain");

    private void OnEnable()
    {
        // reset bounds
        _bounds = new Bounds(transform.position, Vector3.zero);
        _tempTex = new RenderTexture(resolution, resolution, 24);
        GetBounds();
        SetUpCam();
        DrawToMap(TerrainDiffuse);

    }

    private void Start()
    {
        GetBounds();
        SetUpCam();
        // DrawToMap(TerrainDiffuse);
        if (realTimeDiffuse)
        {
            _ = UpdateTex();
        }
    }

    private async UniTask UpdateTex()
    {
        await UniTask.Delay(1000, cancellationToken: destroyCancellationToken);

        while (!destroyCancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(repeatRate), cancellationToken: destroyCancellationToken);
            camToDrawWith.enabled = true;
            camToDrawWith.targetTexture = _tempTex;
            Shader.SetGlobalTexture(TerrainDiffuse, _tempTex);
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
        camToDrawWith.transform.parent = null;
        camToDrawWith.transform.position = _bounds.center + new Vector3(0, _bounds.extents.y + 5f, 0);
        camToDrawWith.transform.parent = gameObject.transform;
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