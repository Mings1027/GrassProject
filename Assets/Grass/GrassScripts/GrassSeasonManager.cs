using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Grass.GrassScripts
{
    [ExecuteInEditMode]
    public class GrassSeasonManager : MonoSingleton<GrassSeasonManager>
    {
        private const int MAX_VOLUMES = 9;

        [SerializeField, HideInInspector] private List<GrassSeasonZone> seasonVolumes = new();
        private readonly Dictionary<GrassSeasonZone, Vector3> lastPositions = new();
        private readonly Dictionary<GrassSeasonZone, Vector3> lastScales = new();
        private readonly VolumeData[] volumeData = new VolumeData[MAX_VOLUMES];
        private bool isDirty;
        private GrassSettingSO grassSetting;

        private GrassComputeScript grassCompute;
        private CancellationTokenSource _globalSeasonUpdateCts;

        [SerializeField] private float globalSeasonValue = 0f;
        public float GlobalSeasonValue => globalSeasonValue;
        public float GlobalMaxRange => 4f;
        public float GlobalMinRange => 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                UpdateSeasonVolumes();
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

                grassSetting = grassCompute.GrassSetting;
                if (grassSetting == null)
                {
                    Debug.LogWarning("GrassSetting not assigned in GrassComputeScript", this);
                    return;
                }
            }

            UpdateSeasonVolumes();
        }

        private void Update()
        {
            if (isDirty || CheckVolumeTransforms())
            {
                UpdateAllSeasonEffects();
                isDirty = false;
            }
        }

        public void RegisterVolume(GrassSeasonZone volume)
        {
            if (volume == null || seasonVolumes.Contains(volume) || seasonVolumes.Count >= MAX_VOLUMES)
                return;

            seasonVolumes.Add(volume);
            lastPositions[volume] = volume.transform.position;
            lastScales[volume] = volume.transform.localScale;
            isDirty = true;
        }

        public void UnregisterVolume(GrassSeasonZone volume)
        {
            if (volume == null || !seasonVolumes.Contains(volume))
                return;

            seasonVolumes.Remove(volume);
            lastPositions.Remove(volume);
            lastScales.Remove(volume);
            isDirty = true;
        }

        public void Initialize(GrassSettingSO setting)
        {
            grassSetting = setting;
            isDirty = true;
        }

        public void UpdateSeasonVolumes()
        {
            var foundVolumes = GetComponentsInChildren<GrassSeasonZone>();
            var updatedVolumes = new List<GrassSeasonZone>();
            var currentVolumes = new HashSet<GrassSeasonZone>(seasonVolumes);

            for (int i = 0; i < Mathf.Min(foundVolumes.Length, MAX_VOLUMES); i++)
            {
                if (foundVolumes[i] != null)
                {
                    updatedVolumes.Add(foundVolumes[i]);
                    currentVolumes.Remove(foundVolumes[i]);
                }
            }

            // 남은 볼륨들은 제거된 것들
            foreach (var removedVolume in currentVolumes)
            {
                if (removedVolume != null)
                {
                    RemoveVolumeData(removedVolume);
                }
            }

            seasonVolumes = updatedVolumes;

            foreach (var volume in seasonVolumes)
            {
                if (!volume) continue;
                if (!lastPositions.ContainsKey(volume))
                {
                    AddVolumeData(volume);
                }
            }

            UpdateAllSeasonEffects();
        }

        private void UpdateShaderData()
        {
            var positions = new Vector4[MAX_VOLUMES];
            var scales = new Vector4[MAX_VOLUMES];
            var colors = new Vector4[MAX_VOLUMES];
            var widthHeights = new Vector4[MAX_VOLUMES];

            for (int i = 0; i < MAX_VOLUMES; i++)
            {
                if (volumeData[i].isActive)
                {
                    positions[i] = volumeData[i].position;
                    positions[i].w = 1.0f;
                    scales[i] = volumeData[i].scale;
                    colors[i] = volumeData[i].color;
                    widthHeights[i] = new Vector4(volumeData[i].width, volumeData[i].height, 0, 0);
                }
                else
                {
                    positions[i].w = 0.0f;
                }
            }

            if (grassCompute != null)
            {
                grassCompute.UpdateSeasonData(positions, scales, colors, widthHeights, MAX_VOLUMES);
            }
        }

        public void UpdateSingleVolume(GrassSeasonZone volume)
        {
            int index = seasonVolumes.IndexOf(volume);
            if (index == -1) return;

            var (color, width, height) = volume.CalculateCurrentSeasonSettings(grassSetting);

            volumeData[index] = new VolumeData
            {
                position = volume.transform.position,
                scale = volume.transform.localScale,
                color = color,
                width = width,
                height = height,
                isActive = true
            };
            UpdateShaderData();
        }

        private void AddVolumeData(GrassSeasonZone volume)
        {
            lastPositions[volume] = volume.transform.position;
            lastScales[volume] = volume.transform.localScale;
        }

        private void RemoveVolumeData(GrassSeasonZone volume)
        {
            lastPositions.Remove(volume);
            lastScales.Remove(volume);
        }

        private bool CheckVolumeTransforms()
        {
            bool needsUpdate = false;

            foreach (var volume in seasonVolumes)
            {
                if (!volume) continue;

                var currentPosition = volume.transform.position;
                var currentScale = volume.transform.localScale;

                if (currentPosition != lastPositions[volume] || currentScale != lastScales[volume])
                {
                    lastPositions[volume] = currentPosition;
                    lastScales[volume] = currentScale;
                    needsUpdate = true;
                }
            }

            return needsUpdate;
        }

        private void UpdateAllSeasonEffects()
        {
            // 기존의 볼륨 데이터 초기화
            for (int i = 0; i < MAX_VOLUMES; i++)
            {
                volumeData[i] = new VolumeData { isActive = false };
            }

            // 각 볼륨의 데이터 업데이트
            for (int i = 0; i < seasonVolumes.Count; i++)
            {
                var volume = seasonVolumes[i];
                if (!volume) continue;

                var (color, width, height) = volume.CalculateCurrentSeasonSettings(grassSetting);

                volumeData[i] = new VolumeData
                {
                    position = volume.transform.position,
                    scale = volume.transform.localScale,
                    color = color,
                    width = width,
                    height = height,
                    isActive = true
                };
            }

            UpdateShaderData();
        }

        public void SetGlobalSeasonValue(float value)
        {
            globalSeasonValue = Mathf.Clamp(value, 0f, 4f);

            foreach (var volume in seasonVolumes)
            {
                if (!volume) continue;

                if (volume.OverrideGlobalSettings)
                {
                    float normalizedValue = globalSeasonValue / 4f;
                    float rangeValue = Mathf.Lerp(volume.SeasonRange.x, volume.SeasonRange.y, normalizedValue);
                    volume.SetSeasonValue(Mathf.Clamp(rangeValue, volume.SeasonRange.x, volume.SeasonRange.y));
                }
                else
                {
                    volume.SetSeasonValue(globalSeasonValue);
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

        public (float min, float max) GetSeasonValueRange()
        {
            return (0f, 4f);
        }

        private void OnTransformChildrenChanged()
        {
            UpdateSeasonVolumes();
        }
    }
}