using System.Collections.Generic;

namespace Grass.Editor
{
    public abstract class BasePainter
    {
        protected readonly GrassComputeScript grassCompute;
        protected readonly SpatialGrid spatialGrid;
        protected readonly List<int> sharedIndices = new();

        protected BasePainter(GrassComputeScript grassCompute, SpatialGrid spatialGrid)
        {
            this.grassCompute = grassCompute;
            this.spatialGrid = spatialGrid;
        }

        public virtual void Clear()
        {
            sharedIndices.Clear();
        }
    }
}