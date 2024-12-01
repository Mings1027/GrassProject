using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.GrassScripts
{
    [ExecuteInEditMode]
    public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
    {
        private const int MAX_ZONES = 9;

        [SerializeField, HideInInspector] private List<GrassSeasonZone> seasonZones = new();
        private readonly Dictionary<GrassSeasonZone, Vector3> lastPositions = new();
        private readonly Dictionary<GrassSeasonZone, Vector3> lastScales = new();
        private readonly ZoneData[] zoneData = new ZoneData[MAX_ZONES];
        private bool isDirty;
        private GrassSettingSO grassSetting;

        private GrassComputeScript grassCompute;

        [SerializeField] private float globalSeasonValue;

        public float GlobalMinRange => grassSetting != null ? grassSetting.seasonRange.GetRange().min : 0f;
        public float GlobalMaxRange => grassSetting != null ? grassSetting.seasonRange.GetRange().max : 4f;

#if UNITY_EDITOR
        public float GlobalSeasonValue => globalSeasonValue;

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (grassSetting != null)
                {
                    var (min, max) = grassSetting.seasonRange.GetRange();
                    globalSeasonValue = Mathf.Clamp(globalSeasonValue, min, max);
                }

                UpdateSeasonZones();
            }
        }
#endif

        private void OnEnable()
        {
            if (grassCompute == null)
            {
                grassCompute = FindAnyObjectByType<GrassComputeScript>();
                if (grassCompute == null)
                {
                    Debug.LogWarning("GrassComputeScript not found in scene", this);
                    return;
                }
            }

            if (grassSetting == null)
            {
                grassSetting = grassCompute.GrassSetting;
                if (grassSetting == null)
                {
                    Debug.LogWarning("GrassSetting not assigned in GrassComputeScript", this);
                    return;
                }

                var (min, max) = grassSetting.seasonRange.GetRange();
                globalSeasonValue = Mathf.Clamp(globalSeasonValue, min, max);
            }

            UpdateSeasonZones();
        }

        private void Update()
        {
            if (isDirty || CheckZoneTransforms())
            {
                UpdateAllSeasonEffects();
                isDirty = false;
            }
        }

        public void RegisterZone(GrassSeasonZone zone)
        {
            if (zone == null || seasonZones.Contains(zone) || seasonZones.Count >= MAX_ZONES)
                return;

            seasonZones.Add(zone);
            lastPositions[zone] = zone.transform.position;
            lastScales[zone] = zone.transform.localScale;
            isDirty = true;
        }

        public void UnregisterZone(GrassSeasonZone zone)
        {
            if (zone == null || !seasonZones.Contains(zone))
                return;

            seasonZones.Remove(zone);
            lastPositions.Remove(zone);
            lastScales.Remove(zone);
            isDirty = true;
        }

        public void Initialize(GrassSettingSO setting)
        {
            grassSetting = setting;
            isDirty = true;
        }

        public void UpdateSeasonZones()
        {
            var foundZones = GetComponentsInChildren<GrassSeasonZone>();
            var updatedZones = new List<GrassSeasonZone>();
            var currentZones = new HashSet<GrassSeasonZone>(seasonZones);

            for (int i = 0; i < Mathf.Min(foundZones.Length, MAX_ZONES); i++)
            {
                if (foundZones[i] != null)
                {
                    updatedZones.Add(foundZones[i]);
                    currentZones.Remove(foundZones[i]);
                }
            }

            // 남은 볼륨들은 제거된 것들
            foreach (var removedZone in currentZones)
            {
                if (removedZone != null)
                {
                    RemoveZoneData(removedZone);
                }
            }

            seasonZones = updatedZones;

            foreach (var zone in seasonZones)
            {
                if (!zone) continue;
                if (!lastPositions.ContainsKey(zone))
                {
                    AddZoneData(zone);
                }
            }

            UpdateAllSeasonEffects();
        }

        private void UpdateShaderData()
        {
            var zoneCount=seasonZones.Count;
            var positions = new Vector4[zoneCount];
            var scales = new Vector4[zoneCount];
            var colors = new Vector4[zoneCount];
            var widthHeights = new Vector4[zoneCount];

            for (int i = 0; i < zoneCount; i++)
            {
                if (zoneData[i].isActive)
                {
                    positions[i] = zoneData[i].position;
                    positions[i].w = 1.0f;
                    scales[i] = zoneData[i].scale;
                    colors[i] = zoneData[i].color;
                    widthHeights[i] = new Vector4(zoneData[i].width, zoneData[i].height, 0, 0);
                }
                else
                {
                    positions[i].w = 0.0f;
                }
            }

            if (grassCompute != null)
            {
                grassCompute.UpdateSeasonData(positions, scales, colors, widthHeights, zoneCount);
            }
        }

        public void UpdateSingleZone(GrassSeasonZone zone)
        {
            int index = seasonZones.IndexOf(zone);
            if (index == -1) return;

            var (color, width, height) = zone.CalculateCurrentSeasonSettings(grassSetting);

            zoneData[index] = new ZoneData
            {
                position = zone.transform.position,
                scale = zone.transform.localScale,
                color = color,
                width = width,
                height = height,
                isActive = zone.gameObject.activeInHierarchy
            };
            UpdateShaderData();
        }

        private void AddZoneData(GrassSeasonZone zone)
        {
            lastPositions[zone] = zone.transform.position;
            lastScales[zone] = zone.transform.localScale;
        }

        private void RemoveZoneData(GrassSeasonZone zone)
        {
            lastPositions.Remove(zone);
            lastScales.Remove(zone);
        }

        private bool CheckZoneTransforms()
        {
            bool needsUpdate = false;

            foreach (var zone in seasonZones)
            {
                if (!zone) continue;

                var currentPosition = zone.transform.position;
                var currentScale = zone.transform.localScale;

                if (currentPosition != lastPositions[zone] || currentScale != lastScales[zone])
                {
                    lastPositions[zone] = currentPosition;
                    lastScales[zone] = currentScale;
                    needsUpdate = true;
                }
            }

            return needsUpdate;
        }

        private void UpdateAllSeasonEffects()
        {
            // 기존의 볼륨 데이터 초기화
            for (int i = 0; i < seasonZones.Count; i++)
            {
                zoneData[i] = new ZoneData { isActive = false };
            }

            // 각 볼륨의 데이터 업데이트
            for (int i = 0; i < seasonZones.Count; i++)
            {
                var zone = seasonZones[i];
                if (!zone) continue;

                var (color, width, height) = zone.CalculateCurrentSeasonSettings(grassSetting);

                zoneData[i] = new ZoneData
                {
                    position = zone.transform.position,
                    scale = zone.transform.localScale,
                    color = color,
                    width = width,
                    height = height,
                    isActive = zone.gameObject.activeInHierarchy
                };
            }

            UpdateShaderData();
        }

        public void SetGlobalSeasonValue(float value)
        {
            if (grassSetting == null) return;

            var (min, max) = grassSetting.seasonRange.GetRange();
            globalSeasonValue = value; // 전역 시즌 값은 전체 범위에서 움직일 수 있음

            foreach (var zone in seasonZones)
            {
                if (!zone) continue;

                if (zone.OverrideGlobalSettings)
                {
                    // Override된 zone은 전역 range와 무관하게 자신의 전체 range를 사용
                    // 전역값을 현재 min-max 범위에서의 비율로 변환 후 zone의 전체 범위에 적용
                    float t = Mathf.InverseLerp(min, max, value);
                    zone.SetSeasonValue(Mathf.Lerp(zone.MinRange, zone.MaxRange, t));
                }
                else
                {
                    // Override되지 않은 zone은 글로벌 설정의 범위를 따름
                    float clampedValue = Mathf.Clamp(value, min, max);
                    zone.SetSeasonValue(clampedValue);
                }
            }

            isDirty = true;
        }

        public void SetGlobalSeasonValueOverTime(float targetValue, float duration)
        {
            if (duration <= 0f)
            {
                SetGlobalSeasonValue(targetValue);
                return;
            }

            float clampedTarget = Mathf.Clamp(targetValue, GlobalMinRange, GlobalMaxRange);

            SetGlobalSeasonValueAsync(globalSeasonValue, clampedTarget, duration, destroyCancellationToken).Forget();
        }

        public void SetGlobalSeasonValueOverTime(float startValue, float endValue, float duration)
        {
            if (duration <= 0f)
            {
                SetGlobalSeasonValue(endValue);
                return;
            }

            float clampedStart = Mathf.Clamp(startValue, GlobalMinRange, GlobalMaxRange);
            float clampedEnd = Mathf.Clamp(endValue, GlobalMinRange, GlobalMaxRange);

            SetGlobalSeasonValueAsync(clampedStart, clampedEnd, duration, destroyCancellationToken).Forget();
        }

        private async UniTask SetGlobalSeasonValueAsync(float startValue, float endValue, float duration,
                                                        CancellationToken token)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var currentValue = Mathf.Lerp(startValue, endValue, t);
                SetGlobalSeasonValue(currentValue);
                await UniTask.Yield(token);
            }

            SetGlobalSeasonValue(endValue);
        }

        private void OnTransformChildrenChanged()
        {
            UpdateSeasonZones();
        }
    }
}