#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LightCookie/LightCookie.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Clustering.hlsl"

#ifndef QUANTIZE_INCLUDED
#define QUANTIZE_INCLUDED

real3 QuantizeDirectionSpherical(real3 dir, real levelsTheta, real levelsPhi)
{
    real theta = acos(clamp(dir.y, -1.0, 1.0));
    real phi   = atan2(dir.z, dir.x);

    theta = floor(theta * levelsTheta / PI) * (PI / levelsTheta);
    phi   = floor((phi + PI) * levelsPhi / (2.0 * PI)) * (2.0 * PI / levelsPhi) - PI;

    float3 result;
    result.x = sin(theta) * cos(phi);
    result.y = cos(theta);
    result.z = sin(theta) * sin(phi);
    return result;
}

real Quantize(real steps, real shade)
{
    if (steps == -1) return shade;
    if (steps == 0) return 0;
    if (steps == 1) return 1;

    shade = round(shade * (steps - .5)) / (steps - 1);
    return shade;
}

real3 Quantize(real steps, real3 shade)
{
    if (steps == -1) return shade;
    if (steps == 0) return 0;
    if (steps == 1) return 1;

    shade = round(shade * (steps - .5)) / (steps - 1);
    return shade;
}
#endif
