using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonZone : MonoBehaviour
{
    private readonly struct SeasonState
    {
        private readonly Vector3 _position;
        private readonly Vector3 _scale;
        public readonly Color color;
        private readonly float _width;
        private readonly float _height;

        public SeasonState(Vector3 position, Vector3 scale, Color color, float width, float height)
        {
            _position = position;
            _scale = scale;
            this.color = color;
            _width = width;
            _height = height;
        }

        public static SeasonState Default(Transform transform) => new(
            transform.position,
            transform.localScale,
            Color.white,
            1f,
            1f
        );

        public ZoneData ToZoneData(bool isActive) => new()
        {
            position = _position,
            scale = _scale,
            color = color,
            width = _width,
            height = _height,
            isActive = isActive
        };
    }

    [SerializeField] private bool overrideGlobalSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private List<SeasonSettings> seasonSettings = new();

    private GrassSettingSO _grassSetting;
    private ZoneData _zoneData;
    private Color _zoneColor;
    private Vector3 _lastPosition;
    private Vector3 _lastScale;
    private CancellationTokenSource _transitionCts;

    private const float DefaultTransitionDuration = 2.0f;

    public event Action OnZoneStateChanged;

    public float MinRange => 0;
    public float MaxRange => overrideGlobalSettings ? seasonSettings.Count : _grassSetting?.seasonSettings.Count ?? 0;

    public SeasonType CurrentSeason
    {
        get
        {
            var settings = ActiveSettings;
            var normalizedValue = seasonValue % settings.Count;
            var currentIndex = Mathf.FloorToInt(normalizedValue);
            return settings[currentIndex].seasonType;
        }
    }

    private List<SeasonSettings> ActiveSettings =>
        overrideGlobalSettings ? seasonSettings : _grassSetting?.seasonSettings;

    private bool HasValidSettings =>
        _grassSetting != null && ActiveSettings?.Count > 0;

#if UNITY_EDITOR
    [SerializeField] private bool showGizmos = true;
    public bool OverrideGlobalSettings
    {
        get => overrideGlobalSettings;
        set => overrideGlobalSettings = value;
    }
    public List<SeasonSettings> SeasonSettings => seasonSettings;
    public GrassSettingSO GrassSetting => _grassSetting;
#endif

    private void OnEnable() => UpdateZoneState();

    private void OnDisable()
    {
        StopSeasonTransition();
        UpdateZoneState();
    }

    public void Init(GrassSettingSO settings)
    {
        _grassSetting = settings;
        _lastPosition = Vector3.zero;
        _lastScale = Vector3.zero;
        UpdateZoneState();
    }

    public void UpdateZone()
    {
        if (transform.position != _lastPosition || transform.localScale != _lastScale)
        {
            _lastPosition = transform.position;
            _lastScale = transform.localScale;
            UpdateZoneState();
        }
    }

    public void UpdateSeasonValue(float globalValue, float globalMin, float globalMax)
    {
        seasonValue = overrideGlobalSettings
            ? Mathf.Lerp(MinRange, MaxRange, Mathf.InverseLerp(globalMin, globalMax, globalValue))
            : Mathf.Clamp(globalValue, globalMin, globalMax);

        UpdateZoneState();
    }

    private void UpdateZoneState()
    {
        var state = CalculateZoneState();
        _zoneData = state.ToZoneData(gameObject.activeInHierarchy);
        _zoneColor = gameObject.activeInHierarchy ? state.color : Color.white;
        _zoneColor.a = 1;

        OnZoneStateChanged?.Invoke();
    }

    private SeasonState CalculateZoneState()
    {
        if (!HasValidSettings)
            return SeasonState.Default(transform);

        var settings = ActiveSettings;
        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - currentIndex;
        int nextIndex = (currentIndex + 1) % settings.Count;

        var currentSeason = settings[currentIndex];
        var nextSeason = settings[nextIndex];

        return new SeasonState
        (
            transform.position,
            transform.localScale,
            Color.Lerp(currentSeason.seasonColor, nextSeason.seasonColor, t),
            Mathf.Lerp(currentSeason.width, nextSeason.width, t),
            Mathf.Lerp(currentSeason.height, nextSeason.height, t)
        );
    }

    private async Task TransitionSeason(float startValue, float targetValue, float duration,
                                        CancellationToken cancellationToken)
    {
        if (!HasValidSettings) return;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float newValue = Mathf.Lerp(startValue, targetValue, t);

            await Task.Yield();
            SetSeasonValue(newValue);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            SetSeasonValue(targetValue);
        }
    }

    public async Task PlayFullSeasonCycle(float transitionDuration = DefaultTransitionDuration,
                                          CancellationToken cancellationToken = default)
    {
        if (!HasValidSettings) return;

        float startValue = seasonValue;
        float targetValue = startValue + ActiveSettings.Count;
        await TransitionSeason(startValue, targetValue, transitionDuration * ActiveSettings.Count, cancellationToken);
    }

    public async Task TransitionToNextSeason(float transitionDuration = DefaultTransitionDuration,
                                             CancellationToken cancellationToken = default)
    {
        if (!HasValidSettings) return;

        float startValue = seasonValue;
        float targetValue = Mathf.Floor(startValue) + 1;

        if (targetValue >= ActiveSettings.Count)
        {
            targetValue = 0;
        }

        await TransitionSeason(startValue, targetValue, transitionDuration, cancellationToken);
    }

    public async Task StartSeasonTransition(bool fullCycle = false,
                                            float transitionDuration = DefaultTransitionDuration)
    {
        StopSeasonTransition();
        _transitionCts = new CancellationTokenSource();

        try
        {
            if (fullCycle)
                await PlayFullSeasonCycle(transitionDuration, _transitionCts.Token);
            else
                await TransitionToNextSeason(transitionDuration, _transitionCts.Token);
        }
        catch (TaskCanceledException)
        {
            // 취소된 경우 무시
        }
        finally
        {
            _transitionCts?.Dispose();
            _transitionCts = null;
        }
    }

    public void StopSeasonTransition()
    {
        if (_transitionCts?.IsCancellationRequested == false)
        {
            _transitionCts.Cancel();
        }
    }

    public void SetSeasonValue(float value)
    {
        var settings = ActiveSettings;
        seasonValue = value;
        if (seasonValue >= settings.Count)
        {
            seasonValue %= settings.Count;
        }

        UpdateZoneState();
    }

    public ZoneData GetZoneData() => _zoneData;

    public Color GetZoneColor() => _zoneColor;

    public bool ContainsPosition(Vector3 position)
    {
        var worldBounds = new Bounds(transform.position, Vector3.Scale(Vector3.one, transform.localScale));
        return worldBounds.Contains(position);
    }

#if UNITY_EDITOR
    public (SeasonType currentSeason, float transition) GetCurrentSeasonTransition()
    {
        var settings = ActiveSettings;
        if (settings == null || settings.Count == 0)
            return (SeasonType.Winter, 0f);

        var validSettings = settings.FindAll(s => s != null);
        if (validSettings.Count == 0)
            return (SeasonType.Winter, 0f);

        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float transitionValue = normalizedValue - currentIndex;

        return (settings[currentIndex].seasonType, transitionValue);
    }

    public void UpdateZoneImmediate() => UpdateZoneState();

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        // 반투명 노란색 큐브
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        // 노란색 와이어프레임
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}