using UnityEngine;

namespace Grass.GrassScripts
{
    [ExecuteInEditMode]
    public class GrassSeasonController : MonoBehaviour
    {
        private GrassComputeScript _grassCompute;
        private SeasonEffectVolume _seasonEffectVolume;
        [SerializeField] private float currentSeasonValue;

        private Vector3 _lastZonePosition;
        private Vector3 _lastZoneScale;

#if UNITY_EDITOR
        public GrassComputeScript GrassCompute => _grassCompute;
#endif
        private void OnEnable()
        {
            _grassCompute = FindAnyObjectByType<GrassComputeScript>();
            _seasonEffectVolume = GetComponentInChildren<SeasonEffectVolume>();
            _lastZonePosition = Vector3.zero;
            _lastZoneScale = Vector3.zero;

            UpdateSeasonEffects(currentSeasonValue);
        }

        private void Update()
        {
            CheckZoneTransform();
        }

        private void CheckZoneTransform()
        {
            if (!_seasonEffectVolume) return;

            var currentPosition = _seasonEffectVolume.transform.position;
            var currentScale = _seasonEffectVolume.transform.localScale;

            if (currentPosition != _lastZonePosition || currentScale != _lastZoneScale)
            {
                UpdateSeasonEffects(currentSeasonValue);
                _lastZonePosition = currentPosition;
                _lastZoneScale = currentScale;
            }
        }

        public void UpdateSeasonEffects(float sliderValue)
        {
            if (!_grassCompute || !_grassCompute.InstantiatedMaterial) return;

            currentSeasonValue = sliderValue;
            var settings = _grassCompute.GrassSetting;

            var normalizedValue = NormalizeSeasonValue(sliderValue, settings);
            var (seasonColor, seasonWidth, seasonHeight) = CalculateSeasonSettings(normalizedValue, settings);

            _grassCompute.UpdateSeasonData(
                _seasonEffectVolume.transform.position,
                _seasonEffectVolume.transform.localScale,
                seasonColor,
                seasonWidth,
                seasonHeight
            );
        }

        private static float NormalizeSeasonValue(float sliderValue, GrassSettingSO settings)
        {
            var normalizedT = Mathf.InverseLerp(settings.seasonRangeMin, settings.seasonRangeMax, sliderValue);
            var seasonRange = settings.seasonRangeMax - settings.seasonRangeMin;
            return settings.seasonRangeMin + (normalizedT * seasonRange);
        }

        private static (Color color, float width, float height) CalculateSeasonSettings(
            float seasonValue, GrassSettingSO settings)
        {
            SeasonSettings from, to;
            float t;

            if (seasonValue < 1f)
            {
                from = settings.winterSettings;
                to = settings.springSettings;
                t = seasonValue;
            }
            else if (seasonValue < 2f)
            {
                from = settings.springSettings;
                to = settings.summerSettings;
                t = seasonValue - 1f;
            }
            else if (seasonValue < 3f)
            {
                from = settings.summerSettings;
                to = settings.autumnSettings;
                t = seasonValue - 2f;
            }
            else
            {
                from = settings.autumnSettings;
                to = settings.winterSettings;
                t = seasonValue - 3f;
            }

            return (
                Color.Lerp(from.seasonColor, to.seasonColor, t),
                Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.height, to.height, t)
            );
        }
    }
}