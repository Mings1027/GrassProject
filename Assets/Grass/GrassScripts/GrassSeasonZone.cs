using System;
using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonZone : MonoBehaviour
{
    [SerializeField] private bool overrideGlobalSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private List<SeasonSettings> seasonSettings = new();

    private static GrassSettingSO _grassSetting;
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
#endif
    private void OnEnable()
    {
        UpdateZoneState();
    }

    private void OnDisable()
    {
        UpdateZoneState();
    }

    public void Init(GrassSettingSO grassSetting)
    {
        _grassSetting = grassSetting;
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

    private (Vector3 position, Vector3 scale, Color color, float width, float height) CalculateZoneState()
    {
        if (!overrideGlobalSettings || seasonSettings.Count == 0)
        {
            if (_grassSetting == null)
                return (transform.position, transform.localScale, Color.white, 1f, 1f);

            return CalculateGlobalSeasonState();
        }

        float normalizedValue = seasonValue % seasonSettings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - currentIndex;
        int nextIndex = (currentIndex + 1) % seasonSettings.Count;

        var currentSeason = seasonSettings[currentIndex];
        var nextSeason = seasonSettings[nextIndex];

        return (
            transform.position,
            transform.localScale,
            Color.Lerp(currentSeason.seasonColor, nextSeason.seasonColor, t),
            Mathf.Lerp(currentSeason.width, nextSeason.width, t),
            Mathf.Lerp(currentSeason.height, nextSeason.height, t)
        );
    }

    private (Vector3 position, Vector3 scale, Color color, float width, float height) CalculateGlobalSeasonState()
    {
        var settings = _grassSetting.seasonSettings;
        if (settings.Count == 0)
            return (transform.position, transform.localScale, Color.white, 1f, 1f);

        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - currentIndex;
        int nextIndex = (currentIndex + 1) % settings.Count;

        var currentSeason = settings[currentIndex];
        var nextSeason = settings[nextIndex];

        return (
            transform.position,
            transform.localScale,
            Color.Lerp(currentSeason.seasonColor, nextSeason.seasonColor, t),
            Mathf.Lerp(currentSeason.width, nextSeason.width, t),
            Mathf.Lerp(currentSeason.height, nextSeason.height, t)
        );
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

    public (SeasonType currentSeason, SeasonType nextSeason, float transition) GetCurrentSeasonTransition()
    {
        var settings = overrideGlobalSettings ? seasonSettings : _grassSetting?.seasonSettings;
        if (settings == null || settings.Count == 0)
        {
            return (SeasonType.Winter, SeasonType.Winter, 0f);
        }

        // 순환하는 값 계산
        float normalizedValue = seasonValue % settings.Count;
        int currentIndex = Mathf.FloorToInt(normalizedValue);
        float transitionValue = normalizedValue - currentIndex;
        int nextIndex = (currentIndex + 1) % settings.Count;

        return (settings[currentIndex].seasonType, settings[nextIndex].seasonType, transitionValue);
    }
#if UNITY_EDITOR
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