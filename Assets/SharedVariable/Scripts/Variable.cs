using System;
using UnityEngine;

namespace SharedVariable.Scripts
{
    public class Variable<T> : BaseVariable
    {
        [SerializeField] private T initialValue;
        [SerializeField] private bool saveRuntimeChanges;
        private T runtimeValue;
        private event Action<T> OnValueChanged;

        private void OnEnable()
        {
            runtimeValue = initialValue;
        }

        public T Value
        {
            get => runtimeValue;
            set
            {
                runtimeValue = value;
                if (saveRuntimeChanges)
                {
                    initialValue = value;
                }
                OnValueChanged?.Invoke(value);
            }
        }

        public void ResetToInitial() => Value = initialValue;

        public void AddListener(Action<T> listener) => OnValueChanged += listener;
        public void RemoveListener(Action<T> listener) => OnValueChanged -= listener;
    }
}