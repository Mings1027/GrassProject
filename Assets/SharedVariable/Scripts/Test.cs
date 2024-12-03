using SharedVariable.VariableScripts;
using UnityEngine;

namespace SharedVariable.Scripts
{
    public class Test : MonoBehaviour
    {
        [SerializeField] private FloatVariable variable;

        [ContextMenu("Add Value")]
        private void AddValue()
        {
            variable.Value += 1;
        }

        [ContextMenu("Subtract Value")]
        private void SubtractValue()
        {
            variable.Value -= 1;
        }
    }
}