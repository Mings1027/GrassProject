using UnityEngine;

namespace SharedVariable.VariableScripts
{
    public abstract class BaseVariable : ScriptableObject
    {
#if UNITY_EDITOR
        public string variableName;
#endif
    }
}