namespace EventBusSystem.Scripts
{
    public interface IEventMethod { }

    public struct InteractorAddEvent : IEventMethod { }

    public struct InteractorRemoveEvent : IEventMethod { }
}