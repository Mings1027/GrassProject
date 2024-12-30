// using UnityEngine;
//
// namespace Grass.GrassScripts
// {
//     public enum SeasonType
//     {
//         Winter = 0,
//         Spring = 1,
//         Summer = 2,
//         Autumn = 3
//     }
//
//     [System.Serializable]
//     public class SeasonRange
//     {
//         [SerializeField] private SeasonType from;
//         [SerializeField] private SeasonType to;
//         [SerializeField] private bool isFullCycle; // 한 바퀴 도는 옵션
//
//         public SeasonType From => from;
//         public SeasonType To => to;
//         public bool IsFullCycle => isFullCycle;
//
//         public (float min, float max) GetRange()
//         {
//             var fromValue = (float)from;
//             var toValue = (float)to;
//
//             if (Mathf.Approximately(fromValue, toValue) && isFullCycle)
//             {
//                 // 한 바퀴 도는 경우
//                 toValue += 4f;
//             }
//             else if (fromValue > toValue)
//             {
//                 // from이 to보다 크면 (예: Summer -> Spring) 한 사이클을 돌아야 함
//                 toValue += 4f;
//             }
//
//             return (fromValue, toValue);
//         }
//
//         // 현재 값이 어떤 계절인지 계산 (에디터 표시용)
//         public string GetCurrentSeasonProgress()
//         {
//             if (isFullCycle && from == to)
//             {
//                 return $"{from} → {from} (Full Cycle)";
//             }
//
//             if (from == to)
//             {
//                 return from.ToString();
//             }
//
//             return $"{from} → {to}";
//         }
//
// #if UNITY_EDITOR
//         public static string GetSeasonRangeInfo(SeasonType from, SeasonType to, bool isFullCycle)
//         {
//             if (from == to && isFullCycle)
//             {
//                 // Full Cycle: 한 바퀴 도는 경우
//                 var seasons = "";
//                 var current = from;
//                 do
//                 {
//                     seasons += current + " → ";
//                     current = (SeasonType)(((int)current + 1) % 4);
//                 } while (current != from);
//
//                 seasons += from.ToString();
//                 return seasons;
//             }
//
//             if (from == to)
//             {
//                 // 같은 계절: 고정
//                 return from.ToString();
//             }
//
//             if (from < to)
//             {
//                 // 정방향 순서
//                 var seasons = "";
//                 for (var season = from; season <= to; season++)
//                 {
//                     seasons += season + " → ";
//                 }
//
//                 return seasons.TrimEnd('→', ' ');
//             }
//             else
//             {
//                 // 역방향 순서 (한 사이클)
//                 var seasons = "";
//                 for (var season = from; season <= SeasonType.Autumn; season++)
//                 {
//                     seasons += season + " → ";
//                 }
//
//                 for (var season = SeasonType.Winter; season <= to; season++)
//                 {
//                     seasons += season + " → ";
//                 }
//
//                 return seasons.TrimEnd('→', ' ');
//             }
//         }
// #endif
//     }
// }