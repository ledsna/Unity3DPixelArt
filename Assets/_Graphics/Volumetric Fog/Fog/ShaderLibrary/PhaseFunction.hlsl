#pragma warning (disable: 3571)

float hg(float a, float g)
{
    float g2 = g * g;
    return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
}
            
float phase(float a, float4 phaseParams)
{
    float blend = .5;
    float hgBlend = hg(a, phaseParams.x) * (1 - blend) + hg(a, -phaseParams.y) * blend;
    return phaseParams.z + hgBlend * phaseParams.w;
}
