using System;
using UnityEngine;

namespace SharedVariable.VariableScripts
{
    public abstract class Variable<T> : BaseVariable, IValueSaveable
    {
        [SerializeField] private T initialValue;
        [SerializeField] private bool saveRuntimeChanges;
        [SerializeField] private T runtimeValue;
        private event Action OnValueChanged;

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
                OnValueChanged?.Invoke();
            }
        }

        public void AddListener(Action listener) => OnValueChanged += listener;
        public void RemoveListener(Action listener) => OnValueChanged -= listener;

        public void Save()
        {
            if (saveRuntimeChanges)
            {
                initialValue = runtimeValue;
            }
        }
    }
}