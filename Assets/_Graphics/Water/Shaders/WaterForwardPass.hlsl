#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

#define NUM_TARGETS 12
static const float2 PREDEFINED_TARGET_XY_OFFSETS[NUM_TARGETS] = {
    float2(0.723f, 0.189f), float2(0.099f, 0.874f), float2(0.452f, 0.547f), float2(0.891f, 0.332f),
    float2(0.276f, 0.961f), float2(0.638f, 0.075f), float2(0.112f, 0.423f), float2(0.805f, 0.788f),
    float2(0.587f, 0.219f), float2(0.034f, 0.692f), float2(0.976f, 0.501f), float2(0.314f, 0.157f)
};

TEXTURE2D(_RippleMap);
float4 _RippleMap_TexelSize;

TEXTURE2D(_SSRReflectionTexture);
SAMPLER(sampler_SSRReflectionTexture);

CBUFFER_START(UnityPerMaterial)
    float4 _Tint;
    float4 _ScatterColor;
    float _Smoothness;
    float4 _SpecularColor;
    
    float _Density;
    float _FoamThreshold;
    float _RefractionStrength;
    
    float _SSRStr;
    
    float4 _RippleMapOrigin;
    
    float _NoiseScale;
    float _NoiseSpeed;
    float _NoiseStrength;

    float _WaveScale;
    float _WaveSpeed;
    float _WaveHeight;
    
    float _TessellationFactor;
    float _TessellationMinDistance;
    float _TessellationMaxDistance;
    
    float _HighlightWidth;
    float _HighlightOscillation;
    float _HighlightSpeed;
CBUFFER_END

struct Attributes { 
    float4 positionOS: POSITION; 
    float3 normalOS: NORMAL;
};

struct TessControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
    float3 positionWS : TEXCOORD0;
    float3 normalOS : TEXCOORD1;
};

struct TessFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float3 positionOS : TEXCOORD2;
};

float2 Unity_GradientNoise_Dir(float2 p)
{
    p = p % 289;
    float x = (34 * p.x + 1) * p.x % 289 + p.y;
    x = (34 * x + 1) * x % 289;
    x = frac(x / 41) * 2 - 1;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

float Unity_GradientNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(Unity_GradientNoise_Dir(ip), fp);
    float d01 = dot(Unity_GradientNoise_Dir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(Unity_GradientNoise_Dir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(Unity_GradientNoise_Dir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * (3 - 2 * fp);
    return lerp(lerp(d00, d10, fp.x), lerp(d01, d11, fp.x), fp.y);
}

float GetWaveHeight(float3 posWS)
{
    float2 p1 = posWS.xz * _WaveScale + float2(_Time.y * _WaveSpeed, _Time.y * _WaveSpeed * 0.8);
    float h1 = Unity_GradientNoise(p1);

    float2 p2 = posWS.xz * _WaveScale * 2.0 - float2(_Time.y * _WaveSpeed * 1.2, 0);
    float h2 = Unity_GradientNoise(p2) * 0.5;
    
    return (h1 + h2) * _WaveHeight; 
}

float3 ComputeRipple(float3 posWS)
{
    float2 uv = (posWS.xz - _RippleMapOrigin.xy) / _RippleMapOrigin.z + 0.5;
    
    float dist = distance(posWS, _WorldSpaceCameraPos);
    float lod = saturate((dist - _TessellationMinDistance) / (_TessellationMaxDistance - _TessellationMinDistance)) * 4.0;
    
    float2 texelSize = _RippleMap_TexelSize.xy;
    float h = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv, lod).r;
    float h_right = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv + float2(texelSize.x, 0), lod).r;
    float h_up = SAMPLE_TEXTURE2D_LOD(_RippleMap, sampler_LinearClamp, uv + float2(0, texelSize.y), lod).r;
    
    float2 distFromEdge = min(uv, 1.0 - uv);
    float mask = saturate(min(distFromEdge.x, distFromEdge.y) * 20.0);
    
    h *= mask;
    h_right *= mask;
    h_up *= mask;

    float dH_du = (h_right - h);
    float dH_dv = (h_up - h);
    
    float worldStep = max(_RippleMapOrigin.z * texelSize.x, 0.0001);
    
    float dHdx = dH_du / worldStep;
    float dHdz = dH_dv / worldStep;
    
    float noiseStep = 0.005;
    float wave_c = GetWaveHeight(posWS);
    float wave_r = GetWaveHeight(posWS + float3(noiseStep, 0, 0));
    float wave_u = GetWaveHeight(posWS + float3(0, 0, noiseStep));
    
    float wave_dHdx = (wave_r - wave_c) / noiseStep;
    float wave_dHdz = (wave_u - wave_c) / noiseStep;
    
    h += wave_c;
    dHdx += wave_dHdx;
    dHdz += wave_dHdz;

    return float3(h, dHdx, dHdz);
}

TessControlPoint Vert(Attributes v)
{
    TessControlPoint o;
    o.positionOS = v.positionOS;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.normalOS = v.normalOS;
    return o;
}

TessFactors PatchConstantFunc(InputPatch<TessControlPoint, 3> patch)
{
    TessFactors factors;
    
    float3 cameraPos = _WorldSpaceCameraPos;
    
    float3 edge0 = patch[2].positionWS - patch[1].positionWS;
    float3 edge1 = patch[0].positionWS - patch[2].positionWS;
    float3 edge2 = patch[1].positionWS - patch[0].positionWS;
    
    float edgeLen0 = length(edge0);
    float edgeLen1 = length(edge1);
    float edgeLen2 = length(edge2);
    
    float3 edge0Mid = 0.5 * (patch[1].positionWS + patch[2].positionWS);
    float3 edge1Mid = 0.5 * (patch[2].positionWS + patch[0].positionWS);
    float3 edge2Mid = 0.5 * (patch[0].positionWS + patch[1].positionWS);
    
    float dist0 = distance(edge0Mid, cameraPos);
    float dist1 = distance(edge1Mid, cameraPos);
    float dist2 = distance(edge2Mid, cameraPos);
    
    float rcpRange = rcp(_TessellationMaxDistance - _TessellationMinDistance);
    float distFade0 = saturate(1.0 - (dist0 - _TessellationMinDistance) * rcpRange);
    float distFade1 = saturate(1.0 - (dist1 - _TessellationMinDistance) * rcpRange);
    float distFade2 = saturate(1.0 - (dist2 - _TessellationMinDistance) * rcpRange);
    
    float tess0 = edgeLen0 * _TessellationFactor * distFade0;
    float tess1 = edgeLen1 * _TessellationFactor * distFade1;
    float tess2 = edgeLen2 * _TessellationFactor * distFade2;
    
    factors.edge[0] = clamp(tess0, 1.0, 64.0);
    factors.edge[1] = clamp(tess1, 1.0, 64.0);
    factors.edge[2] = clamp(tess2, 1.0, 64.0);
    factors.inside = clamp((tess0 + tess1 + tess2) * 0.333333, 1.0, 64.0);
    
    return factors;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[patchconstantfunc("PatchConstantFunc")]
[outputcontrolpoints(3)]
TessControlPoint Hull(InputPatch<TessControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

[domain("tri")]
Varyings Domain(TessFactors factors, OutputPatch<TessControlPoint, 3> patch, float3 baryCoords : SV_DomainLocation)
{
    Varyings o;
    
    float4 posOS = patch[0].positionOS * baryCoords.x + 
                    patch[1].positionOS * baryCoords.y + 
                    patch[2].positionOS * baryCoords.z;

    float3 normalOS = patch[0].normalOS * baryCoords.x + 
                        patch[1].normalOS * baryCoords.y + 
                        patch[2].normalOS * baryCoords.z;
    
    float3 baseWS = TransformObjectToWorld(posOS.xyz);
    
    float3 geomNormalWS = TransformObjectToWorldNormal(normalOS);

    float3 rippleData = ComputeRipple(baseWS);
    
    float verticalDisplacement = rippleData.x;
    float dHdx = rippleData.y;
    float dHdz = rippleData.z;
    
    float3 posWS = baseWS + geomNormalWS * verticalDisplacement;

    float3 tangent = normalize(cross(geomNormalWS, float3(0,0,1) + float3(1,0,0)*0.0001));
    float3 bitangent = cross(geomNormalWS, tangent);
    
    float3 nWS = normalize(tangent * (-dHdx) + geomNormalWS * 1.0 + bitangent * (-dHdz));

    o.positionWS = posWS;
    o.normalWS   = nWS;
    o.positionCS = TransformWorldToHClip(posWS);
    o.positionOS = posOS.xyz;
    return o;
}

float Hash2D(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.13);
    p3 += dot(p3, p3.yzx + 3.333);
    return frac((p3.x + p3.y) * p3.z);
}

float ValueNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = Hash2D(i);
    float b = Hash2D(i + float2(1.0, 0.0));
    float c = Hash2D(i + float2(0.0, 1.0));
    float d = Hash2D(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float FBMNoise(float2 p)
{
    float value = ValueNoise2D(p) * 0.5;
    value += ValueNoise2D(p * 2.0) * 0.25;
    value += ValueNoise2D(p * 4.0) * 0.125;
    return value;
}

float3 ApplyUnderwaterFog(float3 sceneCol, float viewDist, float lightDist)
{
    float3 absorptionCoeff = (1.0 - _Tint.rgb) * _Density;
    
    float3 lightTransmittance = exp(-absorptionCoeff * max(0.0, lightDist));
    float3 objectColor = sceneCol * lightTransmittance;

    float3 viewTransmittance = exp(-absorptionCoeff * max(0.0, viewDist));
    
    return objectColor * viewTransmittance + _ScatterColor.rgb * (1.0 - viewTransmittance);
}

float HitFoamParticle(float3 pos_os)
{
    float3 scale;
    scale.x = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
    scale.y = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
    scale.z = length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z));

    const float2 TILE_DIMENSIONS = float2(30, 14) / scale.xz; 
    float2 tileGridCoordinate = floor(pos_os.xz / TILE_DIMENSIONS);
    float2 tileCornerOS_xz = tileGridCoordinate * TILE_DIMENSIONS;
    
    float4 pos_hcs = TransformObjectToHClip(pos_os);
    
    float2 screen_uv_pos;
    if (unity_OrthoParams.w) screen_uv_pos = pos_hcs.xy * 0.5 + 0.5;
    else screen_uv_pos = pos_hcs.xy / pos_hcs.w * 0.5 + 0.5;
    
    float pos_pixel_y = screen_uv_pos.y * _ScaledScreenParams.y;
    
    float4 origin_hcs = TransformObjectToHClip(float3(0,0,0));
    float4 basis_hcs_x = TransformObjectToHClip(float3(1,0,0)) - origin_hcs;
    float4 basis_hcs_z = TransformObjectToHClip(float3(0,0,1)) - origin_hcs;
    
    float3 basis_vs_x_vec = TransformWorldToViewDir(TransformObjectToWorldDir(float3(1,0,0)));
    float3 basis_vs_z_vec = TransformWorldToViewDir(TransformObjectToWorldDir(float3(0,0,1)));
    float basis_vs_x_val = basis_vs_x_vec.x * scale.x;
    float basis_vs_z_val = basis_vs_z_vec.x * scale.z;

    [loop]
    for (int k = 0; k < NUM_TARGETS; ++k) {
        float k_float = (float)k;
        float2 localTargetNormalizedOffset = PREDEFINED_TARGET_XY_OFFSETS[k];
        float2 actualOffsetFromTileCorner = localTargetNormalizedOffset * TILE_DIMENSIONS;
        
        float2 delta_xz = (tileCornerOS_xz + actualOffsetFromTileCorner) - pos_os.xz;
        float4 target_hcs = pos_hcs + basis_hcs_x * delta_xz.x + basis_hcs_z * delta_xz.y;
        
        float target_uv_y;
        if (unity_OrthoParams.w) target_uv_y = target_hcs.y * 0.5 + 0.5;
        else target_uv_y = target_hcs.y / target_hcs.w * 0.5 + 0.5;
        
        float target_pixel_y = target_uv_y * _ScaledScreenParams.y;
        
        if (abs(target_pixel_y - pos_pixel_y) >= 0.5) continue;

        float dist_x_optimized = abs(basis_vs_x_val * delta_xz.x + basis_vs_z_val * delta_xz.y);

        float variation = frac(k_float * 0.618034);
        float phase = variation * 6.283185;
        float oscillation = sin(_Time.y * _HighlightSpeed + phase);
        float current_width = _HighlightWidth + oscillation * _HighlightOscillation;
        
        if (2 * dist_x_optimized < current_width) {
            return 1;
        }
    }
    return 0;
}

float ComputeFoam(float vDepth)
{
    return saturate(1 - vDepth) > .95;
}

half3 LPB(BRDFData brdfData,
    half3 lightColor, half3 lightDirectionWS, float distanceAttenuation, float shadowAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    bool specularHighlightsOff)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));

    half3 diffuse = lightColor * NdotL * shadowAttenuation * distanceAttenuation;
    half3 brdf;

    half s = 0;
    s += HALF_MIN;
#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
        {
            half specComponent = DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
            brdf = brdfData.specular * exp(floor(log(specComponent) / s) * s);
        }
#endif
    return brdf * diffuse;
}

float2 GetRefractedUV(float2 uv, float3 positionWS, float3 normalWS, float strength, float noiseStrength, float noiseScale, float noiseSpeed)
{
    float2 noiseCoord = positionWS.xz * noiseScale + _Time.y * noiseSpeed;
    float2 noiseDistortion = float2(
        FBMNoise(noiseCoord),
        FBMNoise(noiseCoord + float2(17.3, -23.7))
    ) * 2.0 - 1.0;
    noiseDistortion *= noiseStrength;

    float3 viewNormalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
    float2 baseRefraction = viewNormalVS.xy * strength;

    float2 targetUV = uv + baseRefraction + noiseDistortion;

    if (!all(targetUV >= 0.0 && targetUV <= 1.0))
        return uv;
    
    return targetUV;
}

float3 GetBackgroundPosition(float2 uv, float depth)
{
    float4 normalsData = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv);
    
    if (normalsData.a > 1.0)
        return normalsData.xyz;
    
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

half3 CalculateWaterSpecular(float3 positionWS, float3 normalWS, float3 viewDirWS, BRDFData brdfData, Light mainLight, float2 screenUV)
{
    half3 spec = 0;

    spec += LPB(brdfData, mainLight.color, mainLight.direction, mainLight.distanceAttenuation, mainLight.shadowAttenuation, normalWS, viewDirWS, 0);

    #if defined(_ADDITIONAL_LIGHTS)
        uint pixelLightCount = GetAdditionalLightsCount();
        #if USE_FORWARD_PLUS
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
            {
                FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
                spec += LPB(brdfData, light.color, light.direction, light.distanceAttenuation, light.shadowAttenuation, normalWS, viewDirWS, 0);
            }
            {
                uint lightIndex;
                ClusterIterator _urp_internal_clusterIterator = ClusterInit(screenUV, positionWS, 0);
                [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) {
                    lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT;
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
                    spec += LPB(brdfData, light.color, light.direction, light.distanceAttenuation, light.shadowAttenuation, normalWS, viewDirWS, 0);
                }
            }
        #else
            for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex) {
                Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
                spec += LPB(brdfData, light.color, light.direction, light.distanceAttenuation, light.shadowAttenuation, normalWS, viewDirWS, 0);
            }
        #endif
    #endif

    return spec;
}

half3 CalculateReflectionColor(float2 uv, float3 normalWS, float3 viewDirWS, float3 positionWS, float smoothness, float ssrStrength)
{
    half4 ssrRaw = SAMPLE_TEXTURE2D_LOD(_SSRReflectionTexture, sampler_SSRReflectionTexture, uv, 0);
    
    float3 reflectVector = reflect(-viewDirWS, normalWS);
    float roughness = 1.0 - smoothness;
    float3 fallbackReflection = GlossyEnvironmentReflection(reflectVector, positionWS, roughness, 1.0);
    
    float blendAlpha = saturate(ssrRaw.a * ssrStrength);
    
    return fallbackReflection * (1.0 - blendAlpha) + ssrRaw.rgb * ssrStrength;
}

void Frag(Varyings i
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
    #ifdef _WRITE_PIXEL_PERFECT_DETAIL
    , out half4 outPixelPerfectDetail : SV_Target2
    #endif
#else
    #ifdef _WRITE_PIXEL_PERFECT_DETAIL
    , out half4 outPixelPerfectDetail : SV_Target1
    #endif
#endif
)
{
    float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
    float3 normalWS = normalize(i.normalWS);

    float wStep = 0.005;
    float w_c = GetWaveHeight(i.positionWS);
    float w_r = GetWaveHeight(i.positionWS + float3(wStep, 0, 0));
    float w_u = GetWaveHeight(i.positionWS + float3(0, 0, wStep));
    float w_dHdx = (w_r - w_c) / wStep;
    float w_dHdz = (w_u - w_c) / wStep;

    normalWS = normalize(normalWS + float3(-w_dHdx, 0, -w_dHdz));

    float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

    float2 refUV = GetRefractedUV(uv, i.positionWS, normalWS, _RefractionStrength, _NoiseStrength, _NoiseScale, _NoiseSpeed);

    #if UNITY_REVERSED_Z
        real depthBg = SampleSceneDepth(uv);
        real depthRef = SampleSceneDepth(refUV);
        bool refractedFurther = depthRef < depthBg;
    #else
        real depthBg = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, SampleSceneDepth(uv));
        real depthRef = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, SampleSceneDepth(refUV));
        bool refractedFurther = depthRef > depthBg;
    #endif

    float3 posBg = GetBackgroundPosition(uv, depthBg);
    float3 posRef = GetBackgroundPosition(refUV, depthRef);

    bool refBelowSurface = posRef.y < i.positionWS.y;
    bool useRefraction = refBelowSurface && refractedFurther;

    float3 targetPosWS = useRefraction ? posRef : posBg;
    float2 targetUV = useRefraction ? refUV : uv;
    
    float3 sceneColor = SampleSceneColor(targetUV);
    float backScatterDist = distance(i.positionWS, targetPosWS);
    
    Light mainLight;
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
         float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
         mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1,1,1,1));
    #else
        mainLight = GetMainLight();
    #endif
    
    float depthDiff = max(0.0, i.positionWS.y - targetPosWS.y);
    float lightPathDist = depthDiff / max(0.01, mainLight.direction.y);
    
    float3 foggedColor = ApplyUnderwaterFog(sceneColor, backScatterDist, lightPathDist);

    float NdotV = saturate(dot(normalWS, viewDirWS));
    
    float roughness = 1.0 - _Smoothness;
    float f0 = 0.02;
    float f90 = saturate(1.0 - roughness); 
    float fresnel = f0 + (f90 - f0) * pow(saturate(1.0 - NdotV), 5.0);

    BRDFData brdfData;
    half alpha = 1;
    half reflectivity = ReflectivitySpecular(_SpecularColor.rgb);
    half oneMinusReflectivity = 1.0 - reflectivity;
    InitializeBRDFDataDirect(_Tint, 0.5, _SpecularColor, reflectivity, oneMinusReflectivity, _Smoothness, alpha, brdfData);

    half3 specularColor = CalculateWaterSpecular(i.positionWS, normalWS, viewDirWS, brdfData, mainLight, GetNormalizedScreenSpaceUV(i.positionCS));
    half3 reflectionColor = CalculateReflectionColor(uv, normalWS, viewDirWS, i.positionWS, _Smoothness, _SSRStr);
    
    half3 combinedReflection = reflectionColor + (specularColor * mainLight.shadowAttenuation);

    float3 waterSurfaceColor = lerp(foggedColor, combinedReflection, fresnel);

    float foamDepthDiff = max(0.0f, i.positionWS.y - posBg.y);
    float foamMask = ComputeFoam(foamDepthDiff) + HitFoamParticle(i.positionOS);
    
    float3 finalColor = saturate(waterSurfaceColor + combinedReflection * foamMask * 10 / max(10, i.positionCS.w));

    outColor = half4(finalColor, 1.0);

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif

    half isPixelPerfectDetail = foamMask > 0 ? 1.0 : 0.0;
#ifdef _WRITE_PIXEL_PERFECT_DETAIL
    outPixelPerfectDetail = half4(isPixelPerfectDetail, 0, 0, 0);
#endif
}
