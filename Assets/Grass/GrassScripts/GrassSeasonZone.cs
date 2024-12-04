using System.Threading;
using Cysharp.Threading.Tasks;
using Grass.GrassScripts;
using UnityEngine;

[DisallowMultipleComponent]
public class GrassSeasonZone : MonoBehaviour
{
    [SerializeField] private bool showGizmos = true;

    [Header("Season Settings")] [SerializeField]
    private bool overrideGlobalSettings;
    [SerializeField] private float seasonValue;
    [SerializeField] private SeasonRange seasonRange = new();
    [SerializeField] private SeasonSettings winterSettings = new();
    [SerializeField] private SeasonSettings springSettings = new();
    [SerializeField] private SeasonSettings summerSettings = new();
    [SerializeField] private SeasonSettings autumnSettings = new();

    public SeasonType StartSeason => seasonRange.From;
    public SeasonType EndSeason => seasonRange.To;
    public bool OverrideGlobalSettings => overrideGlobalSettings;

    public float MinRange => overrideGlobalSettings ? seasonRange.GetRange().min : 0f;
    public float MaxRange => overrideGlobalSettings ? seasonRange.GetRange().max : 4f;

    private void OnEnable()
    {
        var controller = GrassSeasonManager.Instance;
        if (controller != null)
        {
            controller.RegisterZone(this);
        }
    }

    private void OnDisable()
    {
        var controller = GrassSeasonManager.Instance;
        if (controller != null)
        {
            controller.UnregisterZone(this);
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif

    public void SetSeasonValueOverTime(float targetValue, float duration)
    {
        if (duration <= 0f)
        {
            SetSeasonValue(targetValue);
            return;
        }

        var currentRange = GetMinMaxSeasonRange();
        float clampedTarget = Mathf.Clamp(targetValue, currentRange.min, currentRange.max);

        SetSeasonValueAsync(seasonValue, clampedTarget, duration, destroyCancellationToken).Forget();
    }

    public void SetSeasonValueOverTime(float startValue, float endValue, float duration)
    {
        if (duration <= 0f)
        {
            SetSeasonValue(endValue);
            return;
        }

        var currentRange = GetMinMaxSeasonRange();
        float clampedStart = Mathf.Clamp(startValue, currentRange.min, currentRange.max);
        float clampedEnd = Mathf.Clamp(endValue, currentRange.min, currentRange.max);

        SetSeasonValueAsync(clampedStart, clampedEnd, duration, destroyCancellationToken).Forget();
    }

    private async UniTask SetSeasonValueAsync(float startValue, float endValue, float duration,
                                              CancellationToken cancellationToken)
    {
        var elapsed = 0f;

        while (elapsed < duration)
        {
            if (cancellationToken.IsCancellationRequested) return;

            elapsed += Time.deltaTime;
            var t = elapsed / duration;
            var currentValue = Mathf.Lerp(startValue, endValue, t);
            SetSeasonValue(currentValue);
            await UniTask.Yield(cancellationToken);
        }

        SetSeasonValue(endValue);
    }

    private void UpdateController()
    {
        var controller = GrassSeasonManager.Instance;
        if (controller != null)
        {
            controller.UpdateSingleZone(this);
        }
    }

    public void SetSeasonValue(float value)
    {
        if (overrideGlobalSettings)
        {
            var (min, max) = seasonRange.GetRange();
            value = Mathf.Clamp(value, min, max);
        }

        if (Mathf.Approximately(seasonValue, value)) return;
        seasonValue = value;
        UpdateController();
    }

    public (float min, float max) GetMinMaxSeasonRange()
    {
        if (overrideGlobalSettings)
        {
            return seasonRange.GetRange();
        }

        return (0f, 4f);
    }

    public (Color color, float width, float height) CalculateCurrentSeasonSettings(GrassSettingSO grassSetting)
    {
        if (!overrideGlobalSettings && grassSetting == null)
        {
            return (Color.white, 1f, 1f);
        }

        float normalizedValue = seasonValue % 4f;
        int seasonIndex = Mathf.FloorToInt(normalizedValue);
        float t = normalizedValue - seasonIndex;

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
}