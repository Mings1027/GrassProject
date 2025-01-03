using System.Collections.Generic;
using EventBusSystem.Scripts;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
{
    private GrassComputeScript _grassComputeScript;
    private EventBinding<GrassColorEvent> _colorBinding;

    [SerializeField] private float globalSeasonValue;
    [SerializeField] private List<GrassSeasonZone> seasonZones = new();

    public GrassComputeScript GrassComputeScript => _grassComputeScript;

    private void OnEnable()
    {
        _grassComputeScript = FindAnyObjectByType<GrassComputeScript>();
        RegisterEvents();
        foreach (var zone in seasonZones)
        {
            SubscribeToZone(zone);
        }

        UpdateSeasonZones();
        Init();
    }

    private void OnDisable()
    {
        UnregisterEvents();

        foreach (var zone in seasonZones)
        {
            UnsubscribeFromZone(zone);
        }

        Init();
    }

    private void Update()
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            seasonZones[i].UpdateZone();
        }
    }

    private void OnTransformChildrenChanged()
    {
        UpdateSeasonZones();
    }

    private void SubscribeToZone(GrassSeasonZone zone)
    {
        if (zone != null)
        {
            zone.OnZoneStateChanged += UpdateShaderData;
        }
    }

    private void UnsubscribeFromZone(GrassSeasonZone zone)
    {
        if (zone != null)
        {
            zone.OnZoneStateChanged -= UpdateShaderData;
        }
    }

    private void RegisterEvents()
    {
        _colorBinding = new EventBinding<GrassColorEvent>(GetSeasonGrassColor);
        EventBus<GrassColorEvent>.Register(_colorBinding);
    }

    private void GetSeasonGrassColor(ref GrassColorEvent evt)
    {
        foreach (var zone in seasonZones)
        {
            if (zone.gameObject.activeInHierarchy && zone.ContainsPosition(evt.position))
            {
                evt.grassColor = zone.GetZoneColor();
                break;
            }
        }
    }

    private void UnregisterEvents()
    {
        EventBus<GrassColorEvent>.Deregister(_colorBinding);
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
        var foundZones = GetComponentsInChildren<GrassSeasonZone>(true); // includeInactive를 true로 설정하여 비활성화된 zone도 가져옴
        var maxCount = _grassComputeScript.GrassSetting.maxZoneCount;

        foreach (var zone in foundZones)
        {
            UnsubscribeFromZone(zone);
        }

        // maxZoneCount에 따라 활성/비활성 상태 업데이트
        for (int i = 0; i < foundZones.Length; i++)
        {
            if (foundZones[i] != null)
            {
                // maxZoneCount 이내의 zone은 활성화
                if (i < maxCount)
                {
                    foundZones[i].gameObject.SetActive(true);
                    seasonZones.Add(foundZones[i]);
                    SubscribeToZone(foundZones[i]);
                }
                // maxZoneCount를 초과하는 zone은 비활성화
                else
                {
                    foundZones[i].gameObject.SetActive(false);
                }
            }
        }

        // Shader 데이터 업데이트
        UpdateShaderData();
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

#if UNITY_EDITOR
    public void CreateSeasonZone()
    {
        var zoneObject = new GameObject("Grass Season Zone");
        zoneObject.transform.SetParent(transform);
        var seasonZone = zoneObject.AddComponent<GrassSeasonZone>();
        zoneObject.transform.localPosition = Vector3.zero;
        zoneObject.transform.localScale = new Vector3(10f, 10f, 10f);
        seasonZone.Init(_grassComputeScript.GrassSetting);

        SubscribeToZone(seasonZone);
        Init();
        UpdateSeasonZones();
        UpdateShaderData();
        Undo.RegisterCreatedObjectUndo(zoneObject, "Create Season Zone");
    }
#endif
}