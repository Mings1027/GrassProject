using UnityEngine;

namespace Grass.GrassScripts
{
    public class QuadTreeNode
    {
        private Bounds _bounds;
        private QuadTreeNode[] _children;
        private int _maxDepth = 2; // Maximum depth for subdivision
        private int _currentDepth;

        public Bounds Bounds => _bounds;
        public QuadTreeNode[] Children => _children;

        public QuadTreeNode(Bounds bounds, int currentDepth = 0)
        {
            _bounds = bounds;
            _currentDepth = currentDepth;
        }

        public void CreateTree(Bounds bounds)
        {
            _bounds = bounds;
            CreateChildren(bounds);
        }

        private void CreateChildren(Bounds bounds)
        {
            _children = new QuadTreeNode[4];
            var halfX = bounds.size.x * 0.5f;
            var halfZ = bounds.size.z * 0.5f;
            var halfSize = new Vector3(halfX, bounds.size.y, halfZ);

            var nwCenter = bounds.center + new Vector3(-halfX, 0, halfZ);
            CreateChild(0, nwCenter, halfSize);
            var neCenter = bounds.center + new Vector3(halfX, 0, halfZ);
            CreateChild(1, neCenter, halfSize);
            var swCenter = bounds.center + new Vector3(-halfX, 0, -halfZ);
            CreateChild(2, swCenter, halfSize);
            var seCenter = bounds.center + new Vector3(halfX, 0, -halfZ);
            CreateChild(3, seCenter, halfSize);
        }

        private void CreateChild(int index, Vector3 center, Vector3 size)
        {
            var childNode = new QuadTreeNode(new Bounds(center, size), _currentDepth + 1);
            _children[index] = childNode;

            // Further subdivide even-indexed nodes if not at max depth
            if (index % 2 == 0 && _currentDepth < _maxDepth)
            {
                childNode.CreateChildren(childNode.Bounds);
            }
        }
    }

}