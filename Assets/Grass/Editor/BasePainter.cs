using System.Collections.Generic;
using UnityEngine;

namespace Grass.Editor
{
    public abstract class BasePainter
    {
        protected readonly GrassComputeScript grassCompute;
        protected readonly SpatialGrid spatialGrid;
        protected readonly List<int> sharedIndices = CollectionsPool.GetList<int>();

        protected BasePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            this.grassCompute = grassCompute;
            this.spatialGrid = spatialGrid;
        }

        public virtual void Clear()
        {
            CollectionsPool.ReturnList(sharedIndices);
        }
    }
}