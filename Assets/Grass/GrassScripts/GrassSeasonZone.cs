using System;
using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonZone : MonoBehaviour
{
    private struct SeasonState
    {
        public Vector3 position;
        public Vector3 scale;
        public Color color;
        public float width;
        public float height;

        public static SeasonState Default(Transform transform) =>
            new()
            {
                position = transform.position,
                scale = transform.localScale,
                color = Color.white,
                width = 1f,
                height = 1f
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

    public float MinRange => 0;
    public float MaxRange => overrideGlobalSettings ? seasonSettings.Count : _grassSetting.seasonSettings.Count;

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
    private void OnEnable()
    {
        UpdateZoneState();
    }

    private void OnDisable()
    {
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
        if (overrideGlobalSettings)
        {
            float t = Mathf.InverseLerp(globalMin, globalMax, globalValue);
            seasonValue = Mathf.Lerp(MinRange, MaxRange, t);
        }
        else
        {
            seasonValue = Mathf.Clamp(globalValue, globalMin, globalMax);
        }

        UpdateZoneState();
    }

    private void UpdateZoneState()
    {
        var state = CalculateZoneState();
        _zoneData = new ZoneData
        {
            position = state.position,
            scale = state.scale,
            color = state.color,
            width = state.width,
            height = state.height,
            isActive = gameObject.activeInHierarchy
        };
        _zoneColor = gameObject.activeInHierarchy ? state.color : Color.white;
        _zoneColor.a = 1;

        GrassEventManager.TriggerEvent(GrassEvent.UpdateShaderData);
    }

    private SeasonState CalculateZoneState()
    {
        // 기본값 반환 조건 체크
        if (!overrideGlobalSettings && _grassSetting == null)
            return SeasonState.Default(transform);

        // 사용할 시즌 설정 결정
        var settings = overrideGlobalSettings ? seasonSettings : _grassSetting.seasonSettings;
        if (settings == null || settings.Count == 0)
            return SeasonState.Default(transform);

        // 현재 시즌과 다음 시즌 인덱스 계산
        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - currentIndex;
        int nextIndex = (currentIndex + 1) % settings.Count;

        var currentSeason = settings[currentIndex];
        var nextSeason = settings[nextIndex];

        // 보간된 상태 반환
        return new SeasonState
        {
            position = transform.position,
            scale = transform.localScale,
            color = Color.Lerp(currentSeason.seasonColor, nextSeason.seasonColor, t),
            width = Mathf.Lerp(currentSeason.width, nextSeason.width, t),
            height = Mathf.Lerp(currentSeason.height, nextSeason.height, t)
        };
    }

    public ZoneData GetZoneData() => _zoneData;
    public Color GetZoneColor() => _zoneColor;

    public bool ContainsPosition(Vector3 position)
    {
        var worldBounds = new Bounds(transform.position, Vector3.Scale(Vector3.one, transform.localScale));
        return worldBounds.Contains(position);
    }

    public void SetSeasonValue(float value)
    {
        seasonValue = value;
        UpdateZoneState();
    }

#if UNITY_EDITOR

    public (SeasonType currentSeason, float transition) GetCurrentSeasonTransition()
    {
        var settings = overrideGlobalSettings ? seasonSettings : _grassSetting?.seasonSettings;
        if (settings == null || settings.Count == 0)
        {
            return (SeasonType.Winter, 0f);
        }

        // settings[currentIndex]가 null인지 확인
        var validSettings = new List<SeasonSettings>();
        foreach (var s in settings)
        {
            if (s != null) validSettings.Add(s);
        }

        if (validSettings.Count == 0)
        {
            return (SeasonType.Winter, 0f);
        }

        // 순환하는 값 계산
        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float transitionValue = normalizedValue - currentIndex;

        return (settings[currentIndex].seasonType, transitionValue);
    }

    public void UpdateZoneImmediate()
    {
        UpdateZoneState();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}