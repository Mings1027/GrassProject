using Grass.GrassScripts;
using UnityEngine;

public class GrassSeasonZone : MonoBehaviour
{
    [SerializeField] private bool overrideGlobalSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private SeasonRange seasonRange = new();
    [SerializeField] private SeasonSettings winterSettings = new();
    [SerializeField] private SeasonSettings springSettings = new();
    [SerializeField] private SeasonSettings summerSettings = new();
    [SerializeField] private SeasonSettings autumnSettings = new();

    private static GrassSettingSO _grassSetting;
    private ZoneData _zoneData;
    private Color _zoneColor;
    private Vector3 _lastPosition;
    private Vector3 _lastScale;

    public float MinRange => overrideGlobalSettings ? seasonRange.GetRange().min : 0f;
    public float MaxRange => overrideGlobalSettings ? seasonRange.GetRange().max : 4f;

#if UNITY_EDITOR
    [SerializeField] private bool showGizmos = true;
    public bool OverrideGlobalSettings => overrideGlobalSettings;
#endif
    
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
            seasonValue = Mathf.Lerp(seasonRange.GetRange().min, seasonRange.GetRange().max, t);
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
        if (!overrideGlobalSettings && _grassSetting == null)
            return (transform.position, transform.localScale, Color.white, 1f, 1f);

        float normalizedValue = seasonValue % 4f;
        int seasonIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - seasonIndex;

        var settings = overrideGlobalSettings
            ? new[] { winterSettings, springSettings, summerSettings, autumnSettings, winterSettings }
            : new[]
            {
                _grassSetting.winterSettings, _grassSetting.springSettings,
                _grassSetting.summerSettings, _grassSetting.autumnSettings,
                _grassSetting.winterSettings
            };

        var from = settings[seasonIndex];
        var to = settings[seasonIndex + 1];

        return (
            transform.position,
            transform.localScale,
            Color.Lerp(from.seasonColor, to.seasonColor, t),
            Mathf.Lerp(from.width, to.width, t),
            Mathf.Lerp(from.height, to.height, t)
        );
    }

    public ZoneData GetZoneData() => _zoneData;
    public Color GetZoneColor() => _zoneColor;

    public bool ContainsPosition(Vector3 position)
    {
        var worldBounds = new Bounds(transform.position, Vector3.Scale(Vector3.one, transform.localScale));
        return worldBounds.Contains(position);
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