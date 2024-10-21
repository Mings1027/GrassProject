using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public class ObjectProgress
    {
        public float progress;
        public string progressMessage;
    }

    public class GrassDataStructure
    {
        private readonly List<GrassData> _grassData = new();
        private readonly Dictionary<int, int> _indexMap = new(); // 키: 원래 인덱스, 값: 현재 리스트에서의 인덱스

        public void Add(GrassData data)
        {
            _indexMap[_grassData.Count] = _grassData.Count;
            _grassData.Add(data);
        }

        public void Remove(int index)
        {
            if (_indexMap.TryGetValue(index, out var currentIndex))
            {
                var lastIndex = _grassData.Count - 1;
                if (currentIndex != lastIndex)
                {
                    // 마지막 요소를 제거할 위치로 이동
                    _grassData[currentIndex] = _grassData[lastIndex];
                    _indexMap[lastIndex] = currentIndex;
                }

                _grassData.RemoveAt(lastIndex);
                _indexMap.Remove(index);
            }
        }

        public int Count => _grassData.Count;

        public GrassData this[int index] => _grassData[_indexMap[index]];

        public List<GrassData> GetAllGrassData() => _grassData;
    }
}