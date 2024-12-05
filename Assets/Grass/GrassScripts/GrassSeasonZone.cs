using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonZone : MonoBehaviour
{
    [SerializeField] private bool showGizmos = true;

    [Header("Season Settings")] [SerializeField]
    private bool overrideGlobalSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private SeasonRange seasonRange = new();
    [SerializeField] private SeasonSettings winterSettings = new();
    [SerializeField] private SeasonSettings springSettings = new();
    [SerializeField] private SeasonSettings summerSettings = new();
    [SerializeField] private SeasonSettings autumnSettings = new();

    private ZoneData zoneData;
    private Color zoneColor;
    private Vector3 lastPosition;
    private Vector3 lastScale;

    public bool OverrideGlobalSettings => overrideGlobalSettings;
    public float MinRange => overrideGlobalSettings ? seasonRange.GetRange().min : 0f;
    public float MaxRange => overrideGlobalSettings ? seasonRange.GetRange().max : 4f;

    private void OnEnable()
    {
        lastPosition = transform.position;
        lastScale = transform.localScale;
        UpdateZoneState();
    }

    private void Update()
    {
        if (transform.position != lastPosition || transform.localScale != lastScale)
        {
            lastPosition = transform.position;
            lastScale = transform.localScale;
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
        zoneData = new ZoneData
        {
            position = state.position,
            scale = state.scale,
            color = state.color,
            width = state.width,
            height = state.height,
            isActive = gameObject.activeInHierarchy
        };
        zoneColor = gameObject.activeInHierarchy ? state.color : Color.white;
        zoneColor.a = 1;

        GrassEventManager.TriggerEvent(GrassEvent.UpdateShaderData);
    }

    private (Vector3 position, Vector3 scale, Color color, float width, float height) CalculateZoneState()
    {
        var grassSetting = GrassFuncManager.TriggerEvent<GrassSettingSO>(GrassEvent.GetGrassSetting);

        if (!overrideGlobalSettings && grassSetting == null)
            return (transform.position, transform.localScale, Color.white, 1f, 1f);

        float normalizedValue = seasonValue % 4f;
        int seasonIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - seasonIndex;

        var settings = overrideGlobalSettings
            ? new[] { winterSettings, springSettings, summerSettings, autumnSettings, winterSettings }
            : new[]
            {
                grassSetting.winterSettings, grassSetting.springSettings,
                grassSetting.summerSettings, grassSetting.autumnSettings,
                grassSetting.winterSettings
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

    public ZoneData GetZoneData() => zoneData;
    public Color GetZoneColor() => zoneColor;

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