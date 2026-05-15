#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir)
{
    float3 t0 = (boundsMin - rayOrigin) * invRaydir;
    float3 t1 = (boundsMax - rayOrigin) * invRaydir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);

    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(tmax.x, min(tmax.y, tmax.z));

    float dstToBox = max(0, dstA);
    float dstInsideBox = max(0, dstB - dstToBox);
    return float2(dstToBox, dstInsideBox);
}

float InterleavedGradientNoise(float2 pixelCoord)
{
    float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
    return frac(magic.z * frac(dot(pixelCoord, magic.xy)));
}

float GetCorrectDepth(float2 uv)
{
    #if UNITY_REVERSED_Z
    real depth = SampleSceneDepth(uv);
    #else
    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
    #endif
    return depth;
}
