using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFog.Core
{
    [VolumeRequiresRendererFeatures(typeof(VolumetricFogFeature))]
    [Serializable, VolumeComponentMenu("Custom/Volumetric Fog")]
    [CreateAssetMenu(fileName = "VolumetricFogSettings", menuName = "Volumetric Fog/Volumetric Fog Settings")]
    public class VolumetricFogVolumeComponent : VolumeComponent
    {
        [BoxGroup("Shape")] public MinFloatParameter shapeScale = new(10f, 0f);
        [HideInInspector]
        public Vector3Parameter shapeOffset = new(Vector3.zero);

        [BoxGroup("Detail")] public MinFloatParameter detailScale = new(10f, 0f);
        [HideInInspector]
        public Vector3Parameter detailOffset = new(Vector3.zero);

        [Header("Density / marching")]
        public FloatParameter densityOffset = new(-3.36f);

        public MinFloatParameter densityMultiplier = new(15.221f, 0f);

        public MinFloatParameter detailMultiplier = new(4.16f, 0f);

        [BoxGroup("Shape")] public Vector4Parameter shapeWeights = new(new Vector4(1f, 0.48f, 0.15f, 0f));

        [BoxGroup("Detail")] public Vector4Parameter detailWeights = new(new Vector4(1f, 0.5f, 0.25f, 0.1f));

        [Header("Phase Parameters")]
        [Foldout("Lighting")] public ClampedFloatParameter forwardScattering = new(0.827f, 0f, 1f);

        [Foldout("Lighting")] public ClampedFloatParameter backScattering = new(0.007f, 0f, 1f);

        [Foldout("Lighting")] public ClampedFloatParameter baseBrightness = new(0.657f, 0f, 1f);

        [Foldout("Lighting")] public ClampedFloatParameter phaseFactor = new(0.506f, 0f, 1f);

        [Header("Absorption and Threshold")]
        [Foldout("Lighting")] public FloatParameter lightAbsorptionThroughCloud = new(1f);

        [Foldout("Lighting")] public FloatParameter lightAbsorptionTowardSun = new(1f);

        [Foldout("Lighting")] public ClampedFloatParameter darknessThreshold = new(0.253f, 0f, 1f);

        [Foldout("Wind")] public MinFloatParameter shapeSpeed = new(0f, 0f);

        [Foldout("Wind")] public MinFloatParameter detailSpeed = new(0f, 0f);

        [Foldout("Wind")] public Vector3Parameter windDirection = new(new Vector3(1f, 0f, 0f));

        [Header("Point Lights")]
        [Foldout("Point Lights")] public BoolParameter enablePointLights = new(false);

        [Foldout("Point Lights")] public ClampedIntParameter maxPointLights = new(2, 0, 8);

        [Foldout("Point Lights")] public ClampedIntParameter pointLightExtraSamples = new(0, 0, 3);

        [Foldout("Point Lights")] public MinFloatParameter pointLightExtraThreshold = new(0.05f, 0f);

        [Header("Quality")]
        [Foldout("Quality")] public MinFloatParameter maxStepSize = new(2f, 0f);

        [Header("Edge Fade")]
        [Foldout("Edge Fade")] public MinFloatParameter edgeFadeDistance = new(10f, 0f);

        [Foldout("Edge Fade")] public ClampedFloatParameter topFadeStrength = new(1f, 0f, 1f);

        [Foldout("Edge Fade")] public MinFloatParameter verticalFadeMultiplier = new(2f, 0.1f);

        [Header("Appearance")]
        [Foldout("Appearance")] public ColorParameter fogColor = new(new Color(0.7f, 0.7f, 0.7f));
    }
}
