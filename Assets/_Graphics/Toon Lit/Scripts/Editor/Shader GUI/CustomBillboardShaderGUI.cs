using UnityEditor;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    public class CustomBillboardShaderGUI : CustomLitShader
    {
        bool showOutlineThresholds = false;
        bool showOutlineHeader = true;
        bool showCelShadingHeader = true;
        bool showBillboardSettings = true;
        bool showTextureArray = true;
        bool showWindSettings = true;
        bool showFlowerSettings = true;

        private MaterialProperty[] properties;

        private MaterialProperty _Scale;
        private MaterialProperty _TextureArray;

        private MaterialProperty _WindSpeed;
        private MaterialProperty _WindStrength;
        private MaterialProperty _WindDirection;
        private MaterialProperty _WindFrequency;
        private MaterialProperty _WindGustStrength;

        private MaterialProperty _WildGrassChance;
        private MaterialProperty _WildNormalStrength;

        private MaterialProperty _FlowerSizeMultiplier;
        private MaterialProperty _FlowerSizeVariation;
        private MaterialProperty _FlowerCameraNudge;

        private MaterialProperty _DepthThreshold;
        private MaterialProperty _NormalsThreshold;

        private MaterialProperty _OutlineStrength;
        private MaterialProperty _DebugOn;
        private MaterialProperty _External;
        private MaterialProperty _Convex;
        private MaterialProperty _Concave;

        private MaterialProperty _DiffuseSpecularCelShader;
        private MaterialProperty _DiffuseSteps;
        private MaterialProperty _FresnelSteps;
        private MaterialProperty _SpecularStep;
        private MaterialProperty _DistanceSteps;
        private MaterialProperty _ShadowSteps;
        private MaterialProperty _ReflectionSteps;

        private MaterialProperty _SubmeshID;

        public override void ValidateMaterial(Material material)
        {
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 1f);

            base.ValidateMaterial(material);
            material.EnableKeyword("_ALPHATEST_ON");
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;

            _Scale = FindProperty("_Scale", properties);
            _TextureArray = FindProperty("_TextureArray", properties);

            _WindSpeed = FindProperty("_WindSpeed", properties);
            _WindStrength = FindProperty("_WindStrength", properties);
            _WindDirection = FindProperty("_WindDirection", properties);
            _WindFrequency = FindProperty("_WindFrequency", properties);
            _WindGustStrength = FindProperty("_WindGustStrength", properties);

            _WildGrassChance = FindProperty("_WildGrassChance", properties);
            _WildNormalStrength = FindProperty("_WildNormalStrength", properties);

            _FlowerSizeMultiplier = FindProperty("_FlowerSizeMultiplier", properties, false);
            _FlowerSizeVariation = FindProperty("_FlowerSizeVariation", properties, false);
            _FlowerCameraNudge = FindProperty("_FlowerCameraNudge", properties, false);

            _DepthThreshold = FindProperty("_DepthThreshold", properties);
            _NormalsThreshold = FindProperty("_NormalsThreshold", properties);

            _OutlineStrength = FindProperty("_OutlineStrength", properties);
            _DebugOn = FindProperty("_DebugOn", properties);
            _External = FindProperty("_External", properties);
            _Convex = FindProperty("_Convex", properties);
            _Concave = FindProperty("_Concave", properties);

            _DiffuseSpecularCelShader = FindProperty("_DiffuseSpecularCelShader", properties);
            _DiffuseSteps = FindProperty("_DiffuseSteps", properties);
            _FresnelSteps = FindProperty("_FresnelSteps", properties);
            _SpecularStep = FindProperty("_SpecularStep", properties);
            _DistanceSteps = FindProperty("_DistanceSteps", properties);
            _ShadowSteps = FindProperty("_ShadowSteps", properties);
            _ReflectionSteps = FindProperty("_ReflectionSteps", properties);
            
            _SubmeshID = FindProperty("_SubmeshID", properties);

            DrawCustomProperties();
            DrawDefaultProperties();
        }

        private void DrawDefaultProperties()
        {
            base.OnGUI(materialEditor, properties);
        }

        private void DrawCustomProperties()
        {
            showBillboardSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBillboardSettings, "Billboard Settings");
            if (showBillboardSettings)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_Scale, "Scale");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showTextureArray = EditorGUILayout.BeginFoldoutHeaderGroup(showTextureArray, "Texture Array");
            if (showTextureArray)
            {
                EditorGUILayout.Space();
                materialEditor.TextureProperty(_TextureArray, "Texture Array");

                if (_TextureArray.textureValue is Texture2DArray texArray)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Array contains {texArray.depth} textures", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Size: {texArray.width}x{texArray.height}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showWindSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showWindSettings, "Wind Settings");
            if (showWindSettings)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_WindSpeed, "Wind Speed");
                materialEditor.ShaderProperty(_WindStrength, "Wind Strength");
                materialEditor.ShaderProperty(_WindDirection, "Wind Direction");
                materialEditor.ShaderProperty(_WindFrequency, "Wind Frequency");
                materialEditor.ShaderProperty(_WindGustStrength, "Wind Gust Strength");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Wild Grass", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_WildGrassChance, "Wild Grass Chance");
                materialEditor.ShaderProperty(_WildNormalStrength, "Wild Normal Strength");

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showFlowerSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showFlowerSettings, "Flower Settings");
            if (showFlowerSettings)
            {
                EditorGUILayout.Space();
                if (_FlowerSizeMultiplier != null)
                    materialEditor.ShaderProperty(_FlowerSizeMultiplier, "Flower Size Multiplier");
                if (_FlowerSizeVariation != null)
                    materialEditor.ShaderProperty(_FlowerSizeVariation, "Flower Size Variation");
                if (_FlowerCameraNudge != null)
                    materialEditor.ShaderProperty(_FlowerCameraNudge, "Flower Camera Nudge");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showOutlineThresholds = EditorGUILayout.BeginFoldoutHeaderGroup(showOutlineThresholds, "Outline Thresholds");
            if (showOutlineThresholds)
            {
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(_DepthThreshold, "Depth Threshold");
                materialEditor.ShaderProperty(_NormalsThreshold, "Normals Threshold");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showOutlineHeader = EditorGUILayout.BeginFoldoutHeaderGroup(showOutlineHeader, "Outline Settings");
            if (showOutlineHeader)
            {
                materialEditor.ShaderProperty(_SubmeshID, "Submesh Object ID");
                materialEditor.ShaderProperty(_OutlineStrength, "Intensity");
                materialEditor.ShaderProperty(_DebugOn, "Debug View");
                materialEditor.ShaderProperty(_External, "External");
                materialEditor.ShaderProperty(_Convex, "Internal Convex");
                materialEditor.ShaderProperty(_Concave, "Internal Concave");
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            showCelShadingHeader = EditorGUILayout.BeginFoldoutHeaderGroup(showCelShadingHeader, "Cel Shading Settings");
            if (showCelShadingHeader)
            {
                materialEditor.ShaderProperty(_DiffuseSpecularCelShader, "Diffuse-Specular Cel Shader");
                materialEditor.ShaderProperty(_DiffuseSteps, "Diffuse Lighting Steps");
                materialEditor.ShaderProperty(_FresnelSteps, "Fresnel Steps");
                materialEditor.ShaderProperty(_SpecularStep, "Specular Step Size");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Attenuation Steps", EditorStyles.boldLabel);
                materialEditor.ShaderProperty(_DistanceSteps, "Light Distance Steps");
                materialEditor.ShaderProperty(_ShadowSteps, "Shadow Steps");
                materialEditor.ShaderProperty(_ReflectionSteps, "Reflection Steps");

                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();
        }
    }
}
