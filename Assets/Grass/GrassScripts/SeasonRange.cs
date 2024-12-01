using UnityEngine;

namespace Grass.GrassScripts
{
    public enum SeasonType
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3
    }

    [System.Serializable]
    public class SeasonRange
    {
        [SerializeField] private SeasonType from;
        [SerializeField] private SeasonType to;
        [SerializeField] private bool isFullCycle; // 한 바퀴 도는 옵션

        public SeasonType From => from;
        public SeasonType To => to;
        public bool IsFullCycle => isFullCycle;

        public (float min, float max) GetRange()
        {
            float fromValue = (float)from;
            float toValue = (float)to;

            if (Mathf.Approximately(fromValue, toValue) && isFullCycle)
            {
                // 한 바퀴 도는 경우
                toValue += 4f;
            }
            else if (fromValue > toValue)
            {
                // from이 to보다 크면 (예: Summer -> Spring) 한 사이클을 돌아야 함
                toValue += 4f;
            }

            return (fromValue, toValue);
        }

        // 현재 값이 어떤 계절인지 계산 (에디터 표시용)
        public string GetCurrentSeasonProgress(float value)
        {
            var (min, max) = GetRange();
            if (isFullCycle && from == to)
            {
                return $"{from} → {from} (Full Cycle)";
            }
            else if (from == to)
            {
                return from.ToString();
            }

            return $"{from} → {to}";
        }
    }
}