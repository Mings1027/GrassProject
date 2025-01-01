namespace Grass.Editor
{
    public static class LayerMaskExtensions
    {
        public static bool Matches(this int layer, int layerMask) => ((1 << layer) & layerMask) != 0;
        public static bool NotMatches(this int layer, int layerMask) => !Matches(layer, layerMask);
    }
}