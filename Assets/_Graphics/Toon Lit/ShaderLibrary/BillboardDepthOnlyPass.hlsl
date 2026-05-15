#ifndef UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED
#define UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED

#include "BillboardGpuInstance.hlsl"

TEXTURE2D_ARRAY(_TextureArray);
SAMPLER(sampler_TextureArray);

float _WindSpeed;
float _WindStrength;
float4 _WindDirection;
float _WindFrequency;
float _WindGustStrength;

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv           : TEXCOORD0;
    nointerpolation int textureIndex : TEXCOORD1;
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
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

    output.uv = input.texcoord;
    output.textureIndex = textureIndex;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);

    float3 windDisplacement = CalculateWindDisplacement(vertexInput.positionWS, input.texcoord.y);
    vertexInput.positionWS += windDisplacement;

    output.positionCS = TransformWorldToHClip(vertexInput.positionWS);

    return output;
}

half DepthOnlyFragment(Varyings input) : SV_Depth
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 clipSample = SampleTextureArray(input.uv, input.textureIndex);
    clip(clipSample.a - _Cutoff);
    
    return input.positionCS.z;
}
#endif
