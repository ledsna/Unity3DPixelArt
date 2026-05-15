#ifndef GRASS_SHADER_INCLUDED
#define GRASS_SHADER_INCLUDED

#include "BillboardGpuInstance.hlsl"

TEXTURE2D_ARRAY(_TextureArray);
SAMPLER(sampler_TextureArray);

float _WindSpeed;
float _WindStrength;
float4 _WindDirection;
float _WindFrequency;
float _WindGustStrength;

float _WildNormalStrength;

float _FlowerCameraNudge;

half4 SampleTextureArray(float2 uv, int precomputedTexIndex)
{
    float width, height, elements;
    _TextureArray.GetDimensions(width, height, elements);

    if (elements == 0)
        return half4(1, 1, 1, 1);

    int texIndex = clamp(precomputedTexIndex, 0, (int)elements - 1);
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_TextureArray, sampler_TextureArray, uv, texIndex, dx, dy);
}

float Hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float3 RandomDirection(float seed)
{
    float theta = Hash(float3(seed, seed * 2.0, seed * 3.0)) * 6.28318530718;
    float phi = Hash(float3(seed * 4.0, seed * 5.0, seed * 6.0)) * 3.14159265359;
    return float3(sin(phi) * cos(theta), cos(phi), sin(phi) * sin(theta));
}

float3 ApplyWildGrassNormal(float3 originalNormal, float3 worldPos)
{
    float hash = Hash(worldPos);
    
    if (hash < _WildGrassChance)
    {
        float3 randomDir = RandomDirection(hash * 1000.0);
        float3 perturbedNormal = lerp(originalNormal, randomDir, _WildNormalStrength);
        return normalize(perturbedNormal);
    }
    
    return originalNormal;
}

float3 PerturbGrassNormal(float3 originalNormal, float3 worldPos)
{
    float h = Hash(worldPos);
    float3 randomDir = RandomDirection(h * 7.0);
    return normalize(lerp(originalNormal, randomDir, 0.08));
}

#ifndef UNIVERSAL_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_PASS_INCLUDED

#define IS_BILLBOARD 1

#include "../ShaderLibrary/Lighting.hlsl"

#if defined(_NORMALMAP)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

float3 CalculateWindDisplacement(float3 worldPos, float vertexHeight)
{
    float time = _Time.y * _WindSpeed;
    float3 windDir = normalize(_WindDirection.xyz);
    
    float windWave = sin(time + (worldPos.x + worldPos.z) * _WindFrequency);
    
    float windVariation = sin(time * 0.7 + worldPos.x * 0.5) * cos(time * 0.9 + worldPos.z * 0.5);
    
    float windGust = sin(time * 2.5 + worldPos.x * 2.0 + worldPos.z * 1.5) * _WindGustStrength;
    
    float windAmount = (windWave * _WindStrength + windVariation * _WindStrength * 0.3 + windGust) * vertexHeight;
    
    return windDir * windAmount;
}

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;
#endif

    nointerpolation int textureIndex : TEXCOORD4;

    half  fogFactor                 : TEXCOORD5;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
#if defined(_NORMALMAP)
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactor);

#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(positionWS),
        normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.positionCS = input.positionCS;
}

Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 viewDirToCamera = GetWorldSpaceNormalizeViewDir(positionWS);
    float terrainFacing = dot(normalWS, viewDirToCamera);
    if (terrainFacing < -0.2)
    {
        output.positionCS = float4(0, 0, 0, 0);
        return output;
    }

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    float3 windDisplacement = CalculateWindDisplacement(vertexInput.positionWS, input.texcoord.y);
    vertexInput.positionWS += windDisplacement;

    #if defined(_USE_TEXTURE_COLOR)
        bool shouldNudge = true;
        float nudgeAmount = _FlowerCameraNudge;
    #else
        bool shouldNudge = isWildGrass;
        float nudgeAmount = 0.02;
    #endif

    if (shouldNudge)
    {
        vertexInput.positionWS -= normalize(viewDirToCamera) * nudgeAmount;
    }

    vertexInput.positionCS = TransformWorldToHClip(vertexInput.positionWS);
    
    float3 perturbedNormal = isWildGrass ? ApplyWildGrassNormal(normalWS, positionWS) : PerturbGrassNormal(normalWS, positionWS);
    
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = input.texcoord;
    output.textureIndex = textureIndex;

    output.normalWS = normalize(perturbedNormal);
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

#if defined(LIGHTMAP_ON)
    output.staticLightmapUV = lightmapUV;
#endif
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
    OUTPUT_SH(normalWS, output.vertexSH);
    output.fogFactor = fogFactor;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    
    output.positionCS = vertexInput.positionCS;
    return output;
}

void LitPassFragment(
    Varyings input, out half4 outColor : SV_Target0
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
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 textureColor = SampleTextureArray(input.uv, input.textureIndex);

    clip(textureColor.a - _Cutoff);

    SurfaceData surfaceData;

    #if defined(_USE_TEXTURE_COLOR)
        surfaceData.alpha = Alpha(textureColor.a, half4(1,1,1,1), _Cutoff);
        surfaceData.albedo = textureColor.rgb;

        half4 specGloss = SampleMetallicSpecGloss(input.uv);
        surfaceData.metallic = specGloss.r;
        surfaceData.specular = half3(0.0, 0.0, 0.0);
        surfaceData.smoothness = specGloss.a;
        surfaceData.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
        surfaceData.occlusion = SampleOcclusion(input.uv);
        surfaceData.emission = SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
        surfaceData.clearCoatMask = half(0.0);
        surfaceData.clearCoatSmoothness = half(0.0);
    #else
        InitializeStandardLitSurfaceData(input.uv, surfaceData);
    #endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    inputData.normalWS = normalWS;
    inputData.positionWS = positionWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv);

    half isPixelPerfectDetail;
    half4 color = UniversalFragmentPBR(inputData, surfaceData, isPixelPerfectDetail);

    half3 colour = color.rgb;
 
    colour = MixFog(colour, inputData.fogCoord);

    outColor.rgb = colour;
    outColor.a = 1.0;

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif

#ifdef _WRITE_PIXEL_PERFECT_DETAIL
    outPixelPerfectDetail = half4(isPixelPerfectDetail, 0, 0, 0);
#endif
}

#endif

#endif
