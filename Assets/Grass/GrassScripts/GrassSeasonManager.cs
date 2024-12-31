using System.Collections.Generic;
using Grass.GrassScripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
{
    private GrassComputeScript _grassComputeScript;

    [SerializeField] private float globalSeasonValue;
    [SerializeField] private List<GrassSeasonZone> seasonZones = new();

    public GrassComputeScript GrassComputeScript => _grassComputeScript;

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
        var foundZones = GetComponentsInChildren<GrassSeasonZone>(true); // includeInactive를 true로 설정하여 비활성화된 zone도 가져옴
        var maxCount = _grassComputeScript.GrassSetting.maxZoneCount;

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

#if UNITY_EDITOR
    public void CreateSeasonZone()
    {
        var zoneObject = new GameObject("Grass Season Zone");
        zoneObject.transform.SetParent(transform);
        zoneObject.AddComponent<GrassSeasonZone>();
        zoneObject.transform.localPosition = Vector3.zero;
        zoneObject.transform.localScale = new Vector3(10f, 10f, 10f);
        zoneObject.GetComponent<GrassSeasonZone>().Init(_grassComputeScript.GrassSetting);

        Init();
        UpdateSeasonZones();
        GrassEventManager.TriggerEvent(GrassEvent.UpdateShaderData);
        Undo.RegisterCreatedObjectUndo(zoneObject, "Create Season Zone");
    }
#endif
}