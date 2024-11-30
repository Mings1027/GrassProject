using System.Collections.Generic;
using UnityEngine;

namespace Grass.GrassScripts
{
    [ExecuteInEditMode]
    public class GrassSeasonController : MonoBehaviour
    {
        private const int MAX_VOLUMES = 9;
        
        [SerializeField, HideInInspector] private List<SeasonEffectVolume> seasonVolumes = new();
        private readonly Dictionary<SeasonEffectVolume, Vector3> lastPositions = new();
        private readonly Dictionary<SeasonEffectVolume, Vector3> lastScales = new();
        private readonly VolumeData[] volumeData = new VolumeData[MAX_VOLUMES];
        private bool isDirty;
        private GrassSettingSO grassSetting;

        // public IReadOnlyList<SeasonEffectVolume> Volumes => seasonVolumes;

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

        public void Initialize(GrassSettingSO setting)
        {
            grassSetting = setting;
            UpdateSeasonVolumes();
        }

        public void UpdateSeasonVolumes()
        {
            var updatedVolumes = new List<SeasonEffectVolume>();
            var foundVolumes = GetComponentsInChildren<SeasonEffectVolume>();

            // 최대 MAX_VOLUMES개까지만 처리
            for (int i = 0; i < Mathf.Min(foundVolumes.Length, MAX_VOLUMES); i++)
            {
                if (foundVolumes[i] != null)
                {
                    updatedVolumes.Add(foundVolumes[i]);
                }
            }

            // 이전에 있던 볼륨들 정리
            foreach (var volume in seasonVolumes)
            {
                if (volume != null && !updatedVolumes.Contains(volume))
                {
                    RemoveVolumeData(volume);
                }
            }

            // 새로운 볼륨들 추가
            foreach (var volume in updatedVolumes)
            {
                if (!seasonVolumes.Contains(volume))
                {
                    AddVolumeData(volume);
                }
            }

            seasonVolumes = updatedVolumes;
            isDirty = true;
        }

        private void AddVolumeData(SeasonEffectVolume volume)
        {
            lastPositions[volume] = volume.transform.position;
            lastScales[volume] = volume.transform.localScale;
        }

        private void RemoveVolumeData(SeasonEffectVolume volume)
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

                // 새로 추가된 볼륨이면 딕셔너리에 추가
                if (!lastPositions.ContainsKey(volume) || !lastScales.ContainsKey(volume))
                {
                    AddVolumeData(volume);
                    needsUpdate = true;
                    continue;
                }

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
            // VolumeData 배열 초기화
            for (int i = 0; i < MAX_VOLUMES; i++)
            {
                volumeData[i] = new VolumeData { isActive = false };
            }

            // 활성화된 볼륨 데이터만 업데이트
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
        }

        public void SetGlobalSeasonValue(float globalValue)
        {
            foreach (var volume in seasonVolumes)
            {
                if (!volume) continue;

                if (volume.OverrideGlobalSettings)
                {
                    float normalizedValue = globalValue / 4f;
                    float rangeValue = Mathf.Lerp(volume.SeasonRange.x, volume.SeasonRange.y, normalizedValue);
                    volume.SetSeasonValue(Mathf.Clamp(rangeValue, volume.SeasonRange.x, volume.SeasonRange.y));
                }
                else
                {
                    volume.SetSeasonValue(globalValue);
                }
            }

            UpdateSeasonVolumes();
        }

        public VolumeData[] GetCurrentSeasonData()
        {
            if (isDirty)
            {
                UpdateAllSeasonEffects();
                isDirty = false;
            }

            return volumeData;
        }

        private void OnTransformChildrenChanged()
        {
            UpdateSeasonVolumes();
        }
    }
}