using UnityEngine;

namespace Grass.GrassScripts
{
    public static class GrassShaderPropertyID
    {
        public static readonly int SourceVertices = Shader.PropertyToID("_SourceVertices");
        public static readonly int DrawTriangles = Shader.PropertyToID("_DrawTriangles");
        public static readonly int IndirectArgsBuffer = Shader.PropertyToID("_IndirectArgsBuffer");
        public static readonly int NumSourceVertices = Shader.PropertyToID("_NumSourceVertices");

        // For culling
        public static readonly int VisibleIDBuffer = Shader.PropertyToID("_VisibleIDBuffer");

        public static readonly int CutBuffer = Shader.PropertyToID("_CutBuffer");
        public static readonly int Time = Shader.PropertyToID("_Time");
        public static readonly int GrassRandomHeightMin = Shader.PropertyToID("_GrassRandomHeightMin");
        public static readonly int GrassRandomHeightMax = Shader.PropertyToID("_GrassRandomHeightMax");
        public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        public static readonly int WindStrength = Shader.PropertyToID("_WindStrength");
        public static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
        
        public static readonly int MinFadeDist = Shader.PropertyToID("_MinFadeDist");
        public static readonly int MaxFadeDist = Shader.PropertyToID("_MaxFadeDist");
        
        public static readonly int InteractorData = Shader.PropertyToID("_InteractorData");
        public static readonly int InteractorStrength = Shader.PropertyToID("_InteractorStrength");
        public static readonly int InteractorsLength = Shader.PropertyToID("_InteractorsLength");
        
        public static readonly int BladeRadius = Shader.PropertyToID("_BladeRadius");
        public static readonly int BladeForward = Shader.PropertyToID("_BladeForward");
        public static readonly int BladeCurve = Shader.PropertyToID("_BladeCurve");
        public static readonly int BottomWidth = Shader.PropertyToID("_BottomWidth");
        public static readonly int MaxBladesPerVertex = Shader.PropertyToID("_MaxBladesPerVertex");
        public static readonly int MaxSegmentsPerBlade = Shader.PropertyToID("_MaxSegmentsPerBlade");
        public static readonly int MinHeight = Shader.PropertyToID("_MinHeight");
        public static readonly int MinWidth = Shader.PropertyToID("_MinWidth");
        public static readonly int MaxHeight = Shader.PropertyToID("_MaxHeight");
        public static readonly int MaxWidth = Shader.PropertyToID("_MaxWidth");

        public static readonly int CameraPositionWs = Shader.PropertyToID("_CameraPositionWS");

        public static readonly int TopTint = Shader.PropertyToID("_TopTint");
        public static readonly int BottomTint = Shader.PropertyToID("_BottomTint");
        
        public static readonly int AmbientStrength = Shader.PropertyToID("_AmbientStrength");
        public static readonly int BlendMultiply = Shader.PropertyToID("_BlendMultiply");
        public static readonly int BlendOffset = Shader.PropertyToID("_BlendOffset");
        public static readonly int AmbientAdjustmentColor = Shader.PropertyToID("_AmbientAdjustmentColor");
        
        public static readonly int ShadowDistance = Shader.PropertyToID("_ShadowDistance");
        public static readonly int ShadowFadeRange = Shader.PropertyToID("_ShadowFadeRange");
        public static readonly int MinShadowBrightness = Shader.PropertyToID("_MinShadowBrightness");
        public static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        
        public static readonly int AdditionalLightIntensity = Shader.PropertyToID("_AdditionalLightIntensity");
        public static readonly int AdditionalLightShadowStrength = Shader.PropertyToID("_AdditionalLightShadowStrength");
        public static readonly int AdditionalShadowColor = Shader.PropertyToID("_AdditionalShadowColor");
        
        public static readonly int SpecularFalloff = Shader.PropertyToID("_SpecularFalloff");
        public static readonly int SpecularStrength = Shader.PropertyToID("_SpecularStrength");
        public static readonly int SpecularHeight = Shader.PropertyToID("_SpecularHeight");
        
        public static readonly int ZonePositions = Shader.PropertyToID("_ZonePositions");
        public static readonly int ZoneScales = Shader.PropertyToID("_ZoneScales");
        public static readonly int ZoneColors = Shader.PropertyToID("_ZoneColors");
        public static readonly int ZoneWidthHeights = Shader.PropertyToID("_ZoneWidthHeights");
        public static readonly int ZoneCount = Shader.PropertyToID("_ZoneCount");
    }
}