using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class GrassSeasonZone : MonoBehaviour
{
    private const float DefaultTransitionDuration = 2.0f;

    [SerializeField] private bool useLocalSeasonSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private List<SeasonSettings> seasonSettings = new();
    [SerializeField] private GrassSettingSO grassSetting;

    private ZoneData _zoneData;
    private Color _zoneColor;
    private Vector3 _lastPosition;
    private Vector3 _lastScale;
    private CancellationTokenSource _transitionCts;

    private TransitionType _lastTransitionType;
    private float _lastStartValue;
    private float _lastTargetValue;
    private float _lastDuration;
    private float _elapsedTime;
    private bool _wasTransitioning;

    public event Action OnZoneStateChanged;

    public float MinRange => 0;
    public float MaxRange => useLocalSeasonSettings ? seasonSettings.Count : grassSetting?.seasonSettings.Count ?? 0;

    public SeasonType CurrentSeason => GetCurrentSeasonType();

    private List<SeasonSettings> SeasonSettingsList =>
        useLocalSeasonSettings ? seasonSettings : grassSetting?.seasonSettings;

    private bool HasValidSettings =>
        grassSetting != null && SeasonSettingsList?.Count > 0;

    public Color GetZoneColor() => _zoneColor;

    #region Unity Lifecycle
    private void OnEnable() => UpdateZoneState();

    private void OnDisable()
    {
        PauseTransition();
        UpdateZoneState();
    }
    #endregion

    #region Public Methods
    public void Init(GrassSettingSO settings)
    {
        grassSetting = settings;
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
        seasonValue = useLocalSeasonSettings
            ? Mathf.Lerp(MinRange, MaxRange, Mathf.InverseLerp(globalMin, globalMax, globalValue))
            : Mathf.Clamp(globalValue, globalMin, globalMax);

        UpdateZoneState();
    }

    public void PlayCycle(float transitionDuration = DefaultTransitionDuration) =>
        _ = PlayTransition(TransitionType.FullCycle, transitionDuration);

    public void PlayNextSeason(float transitionDuration = DefaultTransitionDuration) =>
        _ = PlayTransition(TransitionType.NextSeason, transitionDuration);

    public Task PlayCycleAsync(float transitionDuration = DefaultTransitionDuration) =>
        PlayTransition(TransitionType.FullCycle, transitionDuration);

    public Task PlayNextSeasonAsync(float transitionDuration = DefaultTransitionDuration) =>
        PlayTransition(TransitionType.NextSeason, transitionDuration);

    public void ResumeTransition()
    {
        if (!_wasTransitioning) return;
        var remainingTime = _lastDuration - _elapsedTime;
        if (remainingTime <= 0) return;
        switch (_lastTransitionType)
        {
            case TransitionType.FullCycle:
                _ = PlayTransition(TransitionType.FullCycle, remainingTime);
                break;
            case TransitionType.NextSeason:
                _ = PlayTransition(TransitionType.NextSeason, remainingTime);
                break;
        }
    }

    public void PauseTransition()
    {
        if (_transitionCts?.IsCancellationRequested == false)
        {
            _transitionCts.Cancel();
        }
    }

    public void SetSeasonValue(float value)
    {
        var settings = SeasonSettingsList;
        if (settings != null && value >= settings.Count)
            seasonValue = value % settings.Count;
        else
            seasonValue = value;

        UpdateZoneState();
    }

    public ZoneData GetZoneData() => _zoneData;

    public bool ContainsPosition(Vector3 position)
    {
        var worldBounds = new Bounds(transform.position, Vector3.Scale(Vector3.one, transform.localScale));
        return worldBounds.Contains(position);
    }
    #endregion

    #region Private Methods
    private SeasonType GetCurrentSeasonType()
    {
        var settings = SeasonSettingsList;
        if (settings == null || settings.Count == 0) return default;

        var normalizedValue = seasonValue % settings.Count;
        var currentIndex = Mathf.FloorToInt(normalizedValue);
        return settings[currentIndex].seasonType;
    }

    private void UpdateZoneState()
    {
        if (!HasValidSettings)
        {
            _zoneData.position = transform.position;
            _zoneData.scale = transform.localScale;
            _zoneData.color = Color.white;
            _zoneData.width = 1f;
            _zoneData.height = 1f;
            _zoneData.isActive = false;
        }
        else
        {
            var settings = SeasonSettingsList;
            var normalizedValue = seasonValue % settings.Count;
            var currentIndex = Mathf.FloorToInt(normalizedValue);
            var nextIndex = (currentIndex + 1) % settings.Count;

            var currentSeason = settings[currentIndex];
            var nextSeason = settings[nextIndex];
            var transitionProgress = normalizedValue - currentIndex;

            _zoneData.position = transform.position;
            _zoneData.scale = transform.localScale;
            _zoneData.color = Color.Lerp(currentSeason.seasonColor, nextSeason.seasonColor, transitionProgress);
            _zoneData.width = Mathf.Lerp(currentSeason.width, nextSeason.width, transitionProgress);
            _zoneData.height = Mathf.Lerp(currentSeason.height, nextSeason.height, transitionProgress);
            _zoneData.isActive = gameObject.activeInHierarchy;
        }

        _zoneColor = gameObject.activeInHierarchy ? _zoneData.color : Color.white;
        _zoneColor.a = 1;

        OnZoneStateChanged?.Invoke();
    }

    private async Task PlayTransition(TransitionType transitionType,
                                      float transitionDuration = DefaultTransitionDuration)
    {
        PauseTransition();
        _transitionCts = new CancellationTokenSource();
        _lastTransitionType = transitionType;
        _wasTransitioning = true;

        try
        {
            switch (transitionType)
            {
                case TransitionType.FullCycle:
                    await TransitionFullCycle(transitionDuration, _transitionCts.Token);
                    break;
                case TransitionType.NextSeason:
                    await TransitionToNext(transitionDuration, _transitionCts.Token);
                    break;
            }

            if (!_transitionCts.IsCancellationRequested)
            {
                _wasTransitioning = false;
                _elapsedTime = 0;
            }
        }
        finally
        {
            _transitionCts?.Dispose();
            _transitionCts = null;
        }
    }

    private async Task TransitionFullCycle(float transitionDuration = DefaultTransitionDuration,
                                           CancellationToken cancellationToken = default)
    {
        if (!HasValidSettings) return;

        var startValue = seasonValue;
        var targetValue = startValue + SeasonSettingsList.Count;
        await TransitionToValue(startValue, targetValue, transitionDuration * SeasonSettingsList.Count,
            cancellationToken);
    }

    private async Task TransitionToNext(float transitionDuration = DefaultTransitionDuration,
                                        CancellationToken cancellationToken = default)
    {
        if (!HasValidSettings) return;

        var startValue = seasonValue;
        var targetValue = Mathf.Floor(startValue) + 1;

        if (targetValue >= SeasonSettingsList.Count)
        {
            targetValue = 0;
        }

        await TransitionToValue(startValue, targetValue, transitionDuration, cancellationToken);
    }

    private async Task TransitionToValue(float startValue, float targetValue, float duration,
                                         CancellationToken cancellationToken)
    {
        if (!HasValidSettings) return;

        _lastStartValue = startValue;
        _lastTargetValue = targetValue;
        _lastDuration = duration;
        _elapsedTime = 0;

        while (_elapsedTime < duration && !cancellationToken.IsCancellationRequested)
        {
            _elapsedTime += Time.deltaTime;
            var t = _elapsedTime / duration;
            var newValue = Mathf.Lerp(startValue, targetValue, t);

            await Task.Yield();
            SetSeasonValue(newValue);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            SetSeasonValue(targetValue);
        }
    }
    #endregion

#if UNITY_EDITOR
    [SerializeField] private bool showGizmos = true;

    public (SeasonType currentSeason, float transition) GetCurrentSeasonTransition()
    {
        var settings = SeasonSettingsList;
        if (settings == null || settings.Count == 0)
            return (SeasonType.Winter, 0f);

        var validSettings = settings.FindAll(s => s != null);
        if (validSettings.Count == 0)
            return (SeasonType.Winter, 0f);

        var normalizedValue = seasonValue % settings.Count;
        var currentIndex = Mathf.FloorToInt(normalizedValue);
        var transitionValue = normalizedValue - currentIndex;

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

    private enum TransitionType
    {
        FullCycle,
        NextSeason,
    }
}