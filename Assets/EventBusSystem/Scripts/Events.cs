using UnityEngine;

namespace EventBusSystem.Scripts
{
    public interface IEvent { }

    public struct CutGrass : IEvent
    {
        public Vector3 position;
        public float radius;
    }

    public interface IRequest : IEvent { }

    public interface IResponse : IEvent { }

    public struct GrassColorRequest : IRequest
    {
        public Vector3 position;
        public Color defaultColor;
    }

    public struct GrassColorResponse : IResponse
    {
        public Color resultColor;
    }
}