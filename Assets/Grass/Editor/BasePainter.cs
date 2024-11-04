using System.Collections.Generic;

namespace Grass.Editor
{
    public abstract class BasePainter
    {
        protected GrassComputeScript grassCompute;
        protected SpatialGrid spatialGrid;
        protected readonly List<int> sharedIndices = CollectionsPool.GetList<int>();
        protected const int BatchSize = 100;

        protected BasePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            Initialize(grassCompute, spatialGrid);
        }

        public void Initialize(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
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