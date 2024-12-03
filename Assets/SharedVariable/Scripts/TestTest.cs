using SharedVariable.VariableScripts;
using UnityEngine;

namespace SharedVariable.Scripts
{
    public class TestTest : MonoBehaviour
    {
        [SerializeField] private FloatVariable variable;

        [ContextMenu("Print Value")]
        private void PrintValue() => Debug.Log(variable.Value);
    }
}