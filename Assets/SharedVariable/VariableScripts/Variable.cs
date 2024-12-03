using System;
using UnityEngine;

namespace SharedVariable.VariableScripts
{
    public abstract class Variable<T> : BaseVariable
    {
        [SerializeField] private T initialValue;
        [SerializeField] private bool saveRuntimeChanges;
        private T runtimeValue;
        private event Action OnValueChanged;

        private void OnEnable()
        {
            runtimeValue = initialValue;
        }

        private void OnDisable()
        {
            if (saveRuntimeChanges)
            {
                initialValue = runtimeValue;
            }
        }

        public T Value
        {
            get => runtimeValue;
            set
            {
                runtimeValue = value;
                OnValueChanged?.Invoke();
            }
        }

        public void AddListener(Action listener) => OnValueChanged += listener;
        public void RemoveListener(Action listener) => OnValueChanged -= listener;
    }
}