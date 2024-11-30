using UnityEngine;

namespace Grass.GrassScripts
{
    public class SeasonEffectVolume : MonoBehaviour
    {
        [SerializeField] private bool showGizmos = true;

        [Header("Season Settings")] [SerializeField]
        private bool overrideGlobalSettings;
        [SerializeField] private float seasonValue;
        [SerializeField] private SeasonSettings winterSettings = new();
        [SerializeField] private SeasonSettings springSettings = new();
        [SerializeField] private SeasonSettings summerSettings = new();
        [SerializeField] private SeasonSettings autumnSettings = new();
        [SerializeField] private Vector2 seasonRange = new(0f, 4f);

        public bool OverrideGlobalSettings => overrideGlobalSettings;
        public float SeasonValue => seasonValue;
        public Vector2 SeasonRange => seasonRange;

        public void SetSeasonValue(float value)
        {
            seasonValue = value;
        }

        public void SetShowGizmos(bool show)
        {
            showGizmos = show;
        }

        public (Color color, float width, float height) CalculateCurrentSeasonSettings(GrassSettingSO grassSetting)
        {
            if (!overrideGlobalSettings && grassSetting == null)
            {
                return (Color.white, 1f, 1f);
            }

            seasonValue = overrideGlobalSettings ? Mathf.Clamp(seasonValue, seasonRange.x, seasonRange.y) : seasonValue;

            float seasonSection = seasonValue % 4;
            int seasonIndex = Mathf.FloorToInt(seasonSection);
            float t = seasonSection - seasonIndex;

            SeasonSettings from, to;
            if (overrideGlobalSettings)
            {
                var settings = new[] { winterSettings, springSettings, summerSettings, autumnSettings, winterSettings };
                from = settings[seasonIndex];
                to = settings[seasonIndex + 1];
            }
            else
            {
                var settings = new[]
                {
                    grassSetting.winterSettings, grassSetting.springSettings,
                    grassSetting.summerSettings, grassSetting.autumnSettings,
                    grassSetting.winterSettings
                };
                from = settings[seasonIndex];
                to = settings[seasonIndex + 1];
            }

            return (
                Color.Lerp(from.seasonColor, to.seasonColor, t),
                Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.height, to.height, t)
            );
        }

        private void OnValidate()
        {
            if (overrideGlobalSettings)
            {
                seasonValue = Mathf.Clamp(seasonValue, seasonRange.x, seasonRange.y);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}