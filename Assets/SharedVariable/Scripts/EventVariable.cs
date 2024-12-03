using System;
using SharedVariable.Scripts;

public abstract class EventVariable<T> : BaseVariable 
{
    public event Action<T> OnEvent;
    public void AddListener(Action<T> listener) => OnEvent += listener;
    public void RemoveListener(Action<T> listener) => OnEvent -= listener;
    public void Invoke(T param) => OnEvent?.Invoke(param);
}
