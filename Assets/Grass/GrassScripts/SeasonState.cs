using UnityEngine;

namespace Grass.GrassScripts
{
    public readonly struct SeasonState
    {
        private readonly Vector3 _position;
        private readonly Vector3 _scale;
        public readonly Color color;
        private readonly float _width;
        private readonly float _height;

        public SeasonState(Vector3 position, Vector3 scale, Color color, float width, float height)
        {
            _position = position;
            _scale = scale;
            this.color = color;
            _width = width;
            _height = height;
        }

        public static SeasonState Default(Transform transform) => new(
            transform.position,
            transform.localScale,
            Color.white,
            1f,
            1f
        );

        public ZoneData ToZoneData(bool isActive) => new()
        {
            position = _position,
            scale = _scale,
            color = color,
            width = _width,
            height = _height,
            isActive = isActive
        };
    }
}