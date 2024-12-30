using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
{
    private GrassComputeScript _grassComputeScript;

    [SerializeField] private float globalSeasonValue;
    [SerializeField] private List<GrassSeasonZone> seasonZones = new();

    private void OnEnable()
    {
        _grassComputeScript = FindAnyObjectByType<GrassComputeScript>();
        GrassEventManager.AddEvent(GrassEvent.UpdateShaderData, UpdateShaderData);
        GrassFuncManager.AddEvent<Vector3, (Color, bool)>(GrassEvent.TryGetGrassColor, TryGetGrassColor);
        UpdateSeasonZones();
        Init();
    }

    private void OnDisable()
    {
        GrassEventManager.RemoveEvent(GrassEvent.UpdateShaderData, UpdateShaderData);
        GrassFuncManager.RemoveEvent<Vector3, (Color, bool)>(GrassEvent.TryGetGrassColor, TryGetGrassColor);
        Init();
    }

    private void Update()
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            seasonZones[i].UpdateZone();
        }
    }

    public void Init()
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            seasonZones[i].Init(_grassComputeScript.GrassSetting);
        }
    }

    public void UpdateSeasonZones()
    {
        seasonZones.Clear();
        var foundZones = GetComponentsInChildren<GrassSeasonZone>();
        var seasonZoneCount = Mathf.Min(foundZones.Length, _grassComputeScript.GrassSetting.maxZoneCount);
        for (int i = 0; i < seasonZoneCount; i++)
        {
            if (foundZones[i] != null)
            {
                seasonZones.Add(foundZones[i]);
            }
        }
    }

    public void UpdateAllSeasonZones()
    {
        foreach (var seasonZone in seasonZones)
        {
            seasonZone.UpdateSeasonValue(globalSeasonValue, 0, _grassComputeScript.GrassSetting.seasonSettings.Count);
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

        _grassComputeScript.UpdateSeasonData(positions, scales, colors, widthHeights, seasonZones.Count);
    }

    private (Color, bool) TryGetGrassColor(Vector3 position)
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            var zone = seasonZones[i];
            if (!zone || !zone.gameObject.activeInHierarchy) continue;

            if (zone.ContainsPosition(position))
            {
                return (zone.GetZoneColor(), true);
            }
        }

        return (Color.clear, false);
    }

    private void OnTransformChildrenChanged()
    {
        UpdateSeasonZones();
    }
}