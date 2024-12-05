using System.Collections.Generic;
using System.Linq;
using Grass.GrassScripts;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
{
    private const int MAX_ZONES = 9;
    private GrassComputeScript grassComputeScript;
    [SerializeField] private List<GrassSeasonZone> seasonZones = new();
    [SerializeField] private float globalSeasonValue;

    private void OnEnable()
    {
        grassComputeScript = FindAnyObjectByType<GrassComputeScript>();

        GrassEventManager.AddEvent(GrassEvent.UpdateShaderData, UpdateShaderData);
        GrassFuncManager.AddEvent<Vector3, Color>(GrassEvent.TryGetGrassColor, TryGetGrassColor);
        UpdateSeasonZones();
        SetGlobalSeasonValue(globalSeasonValue);
    }

    private void OnDisable()
    {
        GrassEventManager.RemoveEvent(GrassEvent.UpdateShaderData, UpdateShaderData);
        GrassFuncManager.RemoveEvent<Vector3, Color>(GrassEvent.TryGetGrassColor, TryGetGrassColor);
    }

    public float GlobalMinRange()
    {
        var grassSetting = grassComputeScript.GrassSetting;
        return grassSetting != null ? grassSetting.seasonRange.GetRange().min : 0f;
    }

    public float GlobalMaxRange()
    {
        var grassSetting = grassComputeScript.GrassSetting;
        return grassSetting != null ? grassSetting.seasonRange.GetRange().max : 4f;
    }

    public void UpdateSeasonZones()
    {
        seasonZones.Clear();
        var foundZones = GetComponentsInChildren<GrassSeasonZone>();
        for (int i = 0; i < Mathf.Min(foundZones.Length, MAX_ZONES); i++)
        {
            if (foundZones[i] != null)
            {
                seasonZones.Add(foundZones[i]);
            }
        }

        UpdateAllZones();
    }

    private void UpdateAllZones()
    {
        var grassSettings = GrassFuncManager.TriggerEvent<GrassSettingSO>(GrassEvent.GetGrassSetting);
        var (min, max) = grassSettings.seasonRange.GetRange();

        foreach (var zone in seasonZones)
        {
            if (zone != null)
            {
                zone.UpdateSeasonValue(globalSeasonValue, min, max);
            }
        }
    }

    public void UpdateShaderData()
    {
        var positions = new Vector4[seasonZones.Count];
        var scales = new Vector4[seasonZones.Count];
        var colors = new Vector4[seasonZones.Count];
        var widthHeights = new Vector4[seasonZones.Count];

        for (int i = 0; i < seasonZones.Count; i++)
        {
            var data = seasonZones[i].GetZoneData();
            positions[i] = data.position;
            positions[i].w = data.isActive ? 1.0f : 0.0f;
            scales[i] = data.scale;
            colors[i] = data.color;
            widthHeights[i] = new Vector4(data.width, data.height, 0, 0);
        }

        GrassEventManager.TriggerEvent(GrassEvent.UpdateSeasonData, positions, scales, colors, widthHeights,
            seasonZones.Count);
    }

    private Color TryGetGrassColor(Vector3 position)
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            var zone = seasonZones[i];
            if (!zone || !zone.gameObject.activeInHierarchy) continue;

            if (zone.ContainsPosition(position))
            {
                return zone.GetZoneColor();
            }
        }

        return Color.white;
    }

    public void SetGlobalSeasonValue(float value)
    {
        globalSeasonValue = value;
        UpdateAllZones();
    }

    private void OnTransformChildrenChanged()
    {
        UpdateSeasonZones();
    }
}