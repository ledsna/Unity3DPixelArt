Shader "Ledsna/Lit"
{
    Properties
    {
        _DepthThreshold("Depth Threshold", Float) = 2000
        _NormalsThreshold("Normals Threshold", Float) = 0.6
        _ExternalScale("External Scale", Float) = 1
        _InternalScale("Internal Scale", Float) = 1

        [ToggleUI]_DebugOn("Debug", Float) = 0
        [ToggleUI]_External("External", Float) = 0
        [ToggleUI]_Convex("Convex", Float) = 0
        [ToggleUI]_Concave("Concave", Float) = 0
        _OutlineStrength("OutlineStrength", Range(0, 1)) = 0.5
        _OutlineColour("OutlineColour", Color) = (0,0,0,1)

        [Space(20)]
        [ToggleUI]_DiffuseSpecularCelShader("Diffuse-Specular", Float) = 0

        _DiffuseSteps("Diffuse Steps", Float) = -1
        _FresnelSteps("Fresnel Steps", Float) = -1
        _SpecularStep("Specular Step", Range(0, 1)) = 0.5

        [Space(20)]

        _DistanceSteps("Light Distance Steps", Float) = -1
        _ShadowSteps("Shadow Steps", Float) = -1
        _ReflectionSteps("Reflection Steps", Float) = -1

        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)

        _WorkflowMode("WorkflowMode", Float) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Space(40)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}

        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecGlossMap("Specular", 2D) = "white" {}

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        _SubmeshID("Submesh ID", Float) = 0

        _QueueOffset("Queue offset", Float) = 0.0
        
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "TerrainCompatible" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #define _SHADOWS_SOFT

            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Assets/_Graphics/Toon Lit/Quantize.hlsl"
            #pragma multi_compile_fragment _ _WRITE_PIXEL_PERFECT_DETAIL
            #pragma target 4.5 _WRITE_PIXEL_PERFECT_DETAIL
            
            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _WRITE_OBJECT_ID
            #pragma target 4.5 _WRITE_OBJECT_ID

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "../ShaderLibrary/LitInput.hlsl"
            #include "../ShaderLibrary/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "../ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

    }

    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"

    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.CustomShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
