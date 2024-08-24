using UnityEngine;

public class GPUGraph : MonoBehaviour
{
    private const int MaxResolution = 1000;

    private static readonly int
        PositionsId = Shader.PropertyToID("_Positions"),
        ResolutionId = Shader.PropertyToID("_Resolution"),
        StepId = Shader.PropertyToID("_Step"),
        TimeId = Shader.PropertyToID("_Time"),
        TransitionProgressId = Shader.PropertyToID("_TransitionProgress");

    private MaterialPropertyBlock _matProps;

    [SerializeField] private ComputeShader computeShader;

    [SerializeField] private Material material;

    [SerializeField] private Mesh mesh;

    [SerializeField, Range(1, MaxResolution)]
    private int resolution = 10;

    [SerializeField] private FunctionLibrary.FunctionName function;

    private enum TransitionMode
    {
        Cycle,
        Random
    }

    [SerializeField] private TransitionMode transitionMode;

    [SerializeField, Min(0f)] private float functionDuration = 1f, transitionDuration = 1f;

    private float _duration;

    private bool _transitioning;

    private FunctionLibrary.FunctionName _transitionFunction;

    private ComputeBuffer _positionsBuffer;

    private void OnEnable()
    {
        _positionsBuffer = new ComputeBuffer(MaxResolution * MaxResolution, 3 * 4);
        _matProps = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        _positionsBuffer.Release();
        _positionsBuffer = null;
    }

    private void Update()
    {
        _duration += Time.deltaTime;
        if (_transitioning)
        {
            if (_duration >= transitionDuration)
            {
                _duration -= transitionDuration;
                _transitioning = false;
            }
        }
        else if (_duration >= functionDuration)
        {
            _duration -= functionDuration;
            _transitioning = true;
            _transitionFunction = function;
            PickNextFunction();
        }

        UpdateFunctionOnGPU();
    }

    private void PickNextFunction()
    {
        function = transitionMode == TransitionMode.Cycle
            ? FunctionLibrary.GetNextFunctionName(function)
            : FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }

    private void UpdateFunctionOnGPU()
    {
        float step = 2f / resolution;
        computeShader.SetInt(ResolutionId, resolution);
        computeShader.SetFloat(StepId, step);
        computeShader.SetFloat(TimeId, Time.time);
        if (_transitioning)
        {
            computeShader.SetFloat(
                TransitionProgressId,
                Mathf.SmoothStep(0f, 1f, _duration / transitionDuration)
            );
        }

        var kernelIndex =
            (int)function +
            (int)(_transitioning ? _transitionFunction : function) *
            FunctionLibrary.FunctionCount;
        computeShader.SetBuffer(kernelIndex, PositionsId, _positionsBuffer);

        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(kernelIndex, groups, groups, 1);

        // Use the new RenderMeshPrimitives method
        var rparams = new RenderParams(material);
        rparams.matProps = _matProps;
        rparams.matProps.SetBuffer(PositionsId, _positionsBuffer);
        rparams.matProps.SetFloat(StepId, step);
        Graphics.RenderMeshPrimitives(rparams, mesh, 0, resolution * resolution);

        // Use the old DrawMeshInstanced method
        // var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        // Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);
    }
}