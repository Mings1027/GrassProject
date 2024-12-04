using System.Collections.Generic;
using UnityEngine;

namespace SharedVariable.VariableScripts
{
    public static class RuntimeVariableManager
    {
        private static List<BaseVariable> variables = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.quitting += SaveAllVariables;
        }

        private static void AddVariable(BaseVariable variable)
        {
            variables.Add(variable);
        }

        private static void RemoveVariable(BaseVariable variable)
        {
            variables.Remove(variable);
        }

        private static void SaveAllVariables()
        {
            foreach (var variable in variables)
            {
                if (variable is IValueSaveable variableSaveable)
                {
                    variableSaveable.Save();
                }
            }
        }
    }
}