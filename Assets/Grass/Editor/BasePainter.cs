using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public abstract class BasePainter
    {
        protected readonly GrassComputeScript grassCompute;
        protected readonly SpatialGrid spatialGrid;
        protected readonly List<int> sharedIndices = CollectionsPool.GetList<int>();
        private const int BatchSize = 100;

        protected BasePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            this.grassCompute = grassCompute;
            this.spatialGrid = spatialGrid;
        }

        // 배치 처리를 위한 공통 메서드
        protected void ProcessInBatches<T>(List<T> items, System.Action<int, int> batchProcessor)
        {
            int totalItems = items.Count;
            int batchCount = (totalItems + BatchSize - 1) / BatchSize;

            for (int batch = 0; batch < batchCount; batch++)
            {
                int start = batch * BatchSize;
                int end = Mathf.Min(start + BatchSize, totalItems);
                batchProcessor(start, end);
            }
        }

        public virtual void Clear()
        {
            CollectionsPool.ReturnList(sharedIndices);
        }
    }
}