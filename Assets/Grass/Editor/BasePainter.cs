using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Grass.Editor
{
    public abstract class BasePainter
    {
        protected GrassComputeScript _grassCompute;
        protected SpatialGrid _spatialGrid;
        protected List<int> sharedIndices = PainterUtils.GetList();
        protected const int BatchSize = 100;

        public virtual void Initialize(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            _grassCompute = grassCompute;
            _spatialGrid = spatialGrid;
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
            PainterUtils.ReturnList(sharedIndices);
        }
    }
}