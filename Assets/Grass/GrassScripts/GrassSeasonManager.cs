using System.Collections.Generic;
using System.Threading.Tasks;
using EventBusSystem.Scripts;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class GrassSeasonManager : MonoBehaviour
{
    private GrassCompute _grassCompute;
    private EventBinding<GrassColorRequest> _colorRequestBinding;

    [SerializeField] private float globalSeasonValue;
    [SerializeField] private float transitionDuration;
    [SerializeField] private List<GrassSeasonZone> seasonZones = new();

    private void OnEnable()
    {
        _grassCompute = FindAnyObjectByType<GrassCompute>();
        foreach (var zone in seasonZones)
        {
            SubscribeToZone(zone);
        }

        UpdateSeasonZones();
        Init();

        _colorRequestBinding = new EventBinding<GrassColorRequest>(HandleColorRequest);
        EventBus<GrassColorRequest>.Register(_colorRequestBinding);
    }

    private void OnDisable()
    {
        foreach (var zone in seasonZones)
        {
            UnsubscribeFromZone(zone);
        }

        Init();

        EventBus<GrassColorRequest>.Deregister(_colorRequestBinding);
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

    private void HandleColorRequest(GrassColorRequest evt)
    {
        foreach (var zone in seasonZones)
        {
            if (zone.gameObject.activeInHierarchy && zone.ContainsPosition(evt.position))
            {
                EventBusExtensions.Response(new GrassColorResponse
                {
                    resultColor = zone.GetZoneColor()
                });
                return;
            }
        }

        EventBusExtensions.Response(new GrassColorResponse
        {
            resultColor = evt.defaultColor
        });
    }

    private void Init()
    {
        for (int i = 0; i < seasonZones.Count; i++)
        {
            if (seasonZones[i] == null) continue;
            seasonZones[i].Init(_grassCompute.GrassSetting);
        }
    }

    public void UpdateSeasonZones()
    {
        seasonZones.Clear();
        var foundZones =
            GetComponentsInChildren<GrassSeasonZone>(true); // 파라미터인 includeInactive를 true로 설정하여 비활성화된 zone도 가져옴
        var maxCount = _grassCompute.GrassSetting.maxZoneCount;

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

        _grassCompute.UpdateSeasonData(ref positions, ref scales, ref colors, ref widthHeights,
            seasonZones.Count);
    }

    private const float DefaultTransitionDuration = 2.0f;
    private bool _isTransitioning;

    private async Task PlayCycleTask(float duration)
    {
        _isTransitioning = true;

        var tasks = new List<Task>();
        foreach (var zone in seasonZones)
        {
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                tasks.Add(zone.PlayCycleAsync(duration));
            }
        }

        await Task.WhenAll(tasks);

        _isTransitioning = false;
    }

    public void PlayCycles(float duration = DefaultTransitionDuration)
    {
        if (_isTransitioning) return;
        _ = PlayCycleTask(duration);
    }

    private async Task PlayNextSeasonTask(float duration)
    {
        _isTransitioning = true;
        var tasks = new List<Task>();
        foreach (var zone in seasonZones)
        {
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                tasks.Add(zone.PlayNextSeasonAsync(duration));
            }
        }

        await Task.WhenAll(tasks);
        _isTransitioning = false;
    }

    public void PlayNextSeasons(float duration = DefaultTransitionDuration)
    {
        if (_isTransitioning) return;
        _ = PlayNextSeasonTask(duration);
    }

    public void ResumeTransitions()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        foreach (var zone in seasonZones)
        {
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                zone.ResumeTransition();
            }
        }
    }

    public void PauseTransitions(float duration = DefaultTransitionDuration)
    {
        if (!_isTransitioning) return;

        foreach (var zone in seasonZones)
        {
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                zone.PauseTransition();
            }
        }

        _isTransitioning = false;
    }

#if UNITY_EDITOR

    public void UpdateAllSeasonZones()
    {
        foreach (var seasonZone in seasonZones)
        {
            seasonZone.UpdateSeasonValue(globalSeasonValue, 0, _grassCompute.GrassSetting.seasonSettings.Count);
        }
    }

    public void CreateSeasonZone()
    {
        var zoneObject = new GameObject("Grass Season Zone");
        zoneObject.transform.SetParent(transform);
        var seasonZone = zoneObject.AddComponent<GrassSeasonZone>();
        zoneObject.transform.localPosition = Vector3.zero;
        zoneObject.transform.localScale = new Vector3(10f, 10f, 10f);
        seasonZone.Init(_grassCompute.GrassSetting);

        SubscribeToZone(seasonZone);
        Init();
        UpdateSeasonZones();
        UpdateShaderData();
        Undo.RegisterCreatedObjectUndo(zoneObject, "Create Season Zone");
    }
#endif
}