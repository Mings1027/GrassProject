using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class GrassShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.name != "_AmbientAdjustmentColor" && property.name != "_BaseMap")
                {
                    materialEditor.ShaderProperty(property, property.displayName);
                }

                // After drawing _BlendOff, check if BLEND is enabled and draw _AmbientAdjustmentColor
                if (property.name == "_BlendOff")
                {
                    Material material = materialEditor.target as Material;
                    if (material.IsKeywordEnabled("BLEND"))
                    {
                        var ambientAdjustmentColorProperty = FindProperty("_AmbientAdjustmentColor", properties);
                        materialEditor.ShaderProperty(ambientAdjustmentColorProperty,
                            ambientAdjustmentColorProperty.displayName);
                    }
                }
            }
        }
    }
}