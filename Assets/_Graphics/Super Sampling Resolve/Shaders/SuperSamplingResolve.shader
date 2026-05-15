Shader "Ledsna/SuperSamplingResolve"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Super Sampling Resolve"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_PixelPerfectDetailTexture);
            TEXTURE2D(_CameraObjectIDTexture);
            int _SuperSamplingScale;

            bool SameID(float2 a, float2 b)
            {
                return distance(a, b) < 0.1;
            }

            static const float DEPTH_DETAIL_CUTOFF = 50.0;

            static const float DETAIL_TIER_EPSILON = 1.0 / 255.0;

            bool SameDetail(float a, float b)
            {
                return abs(a - b) < DETAIL_TIER_EPSILON;
            }

            struct ResolveResult
            {
                float3 color;
                float  rawDepth;
                float  linearDepth;
                float4 normal;
                float2 objID;
                float  detail;
            };

            struct FragmentOutput
            {
                half4  color    : SV_Target0;
                float4 depth    : SV_Target1;
                float4 normal   : SV_Target2;
                float4 objID    : SV_Target3;
                float4 detail   : SV_Target4;
            };

            float RawDepthToLinearEyeDepth(float rawDepth)
            {
                return (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
            }

            float4 NormalizeResolvedNormal(float4 normalValue)
            {
                float3 n = normalValue.xyz;
                float lenSq = dot(n, n);
                if (lenSq > 1e-6)
                    n *= rsqrt(lenSq);
                return float4(n, normalValue.a);
            }

            ResolveResult ResolveBlock2x2(int2 base)
            {
                float3 color[4];
                float  rawDepth[4];
                float  linDepth[4];
                float4 normal[4];
                float2 objID[4];
                float  detail[4];

                int i = 0;
                [unroll] for (int y = 0; y < 2; y++)
                [unroll] for (int x = 0; x < 2; x++, i++)
                {
                    int2 pos       = base + int2(x, y);
                    color[i]       = LOAD_TEXTURE2D(_BlitTexture,                   pos).rgb;
                    rawDepth[i]    = LOAD_TEXTURE2D_X(_CameraDepthTexture,          pos).r;
                    linDepth[i]    = RawDepthToLinearEyeDepth(rawDepth[i]);
                    normal[i]      = LOAD_TEXTURE2D_X(_CameraNormalsTexture,        pos);
                    objID[i]       = LOAD_TEXTURE2D(_CameraObjectIDTexture,         pos).rg;
                    detail[i]      = LOAD_TEXTURE2D(_PixelPerfectDetailTexture,     pos).r;
                }

                int closestIndex = 0;
                float closestLin = linDepth[0];
                [unroll] for (int i = 1; i < 4; i++)
                {
                    if (linDepth[i] < closestLin)
                    {
                        closestLin = linDepth[i];
                        closestIndex = i;
                    }
                }

                ResolveResult result;

                int detailCount = 0;
                [unroll] for (int i = 0; i < 4; i++)
                    if (detail[i] > 0.0) detailCount++;

                if (closestLin > DEPTH_DETAIL_CUTOFF || detailCount <= 0)
                {
                    result.color = (color[0] + color[1] + color[2] + color[3]) * 0.25;
                    result.rawDepth = rawDepth[closestIndex];
                    result.linearDepth = linDepth[closestIndex];
                    result.normal = NormalizeResolvedNormal((normal[0] + normal[1] + normal[2] + normal[3]) * 0.25);
                    result.objID = objID[closestIndex];
                    result.detail = max(max(detail[0], detail[1]), max(detail[2], detail[3]));
                    return result;
                }

                if (detailCount < 2)
                {
                    result.color = color[closestIndex];
                    result.rawDepth = rawDepth[closestIndex];
                    result.linearDepth = linDepth[closestIndex];
                    result.normal = normal[closestIndex];
                    result.objID = objID[closestIndex];
                    result.detail = detail[closestIndex];
                    return result;
                }

                int votes[4] = { 0, 0, 0, 0 };
                [unroll] for (int a = 0; a < 4; a++)
                {
                    if (detail[a] <= 0.0) continue;
                    votes[a] = 1;
                    [unroll] for (int b = a + 1; b < 4; b++)
                    {
                        if (detail[b] <= 0.0) continue;
                        if (SameID(objID[a], objID[b]))
                            { votes[a]++; votes[b]++; }
                    }
                }

                int maxVotes = 0;
                [unroll] for (int i = 0; i < 4; i++)
                    maxVotes = max(maxVotes, votes[i]);

                int    topIDCount = 0;
                float2 topIDs[4];
                [unroll] for (int i = 0; i < 4; i++)
                {
                    if (votes[i] != maxVotes) continue;
                    bool already = false;
                    for (int j = 0; j < topIDCount; j++)
                        if (SameID(objID[i], topIDs[j])) already = true;
                    if (!already)
                        topIDs[topIDCount++] = objID[i];
                }

                float2 winnerID = topIDs[0];
                if (topIDCount > 1)
                {
                    float closestTie = 1e30;
                    [unroll] for (int j = 0; j < topIDCount; j++)
                    {
                        [unroll] for (int i = 0; i < 4; i++)
                        {
                            if (detail[i] > 0.0 && SameID(objID[i], topIDs[j]) && linDepth[i] < closestTie)
                            {
                                closestTie = linDepth[i];
                                winnerID  = topIDs[j];
                            }
                        }
                    }
                }

                float maxTier = -1.0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    if (detail[i] > 0.0 && SameID(objID[i], winnerID) && detail[i] > maxTier)
                        maxTier = detail[i];
                }

                float3 winnerColorSum = 0;
                float4 winnerNormalSum = 0;
                int    winnerCount = 0;
                int    winnerClosestIndex = closestIndex;
                float  winnerClosestLin = 1e30;

                [unroll] for (int i = 0; i < 4; i++)
                {
                    if (detail[i] > 0.0
                        && SameID(objID[i], winnerID)
                        && SameDetail(detail[i], maxTier))
                    {
                        winnerColorSum += color[i];
                        winnerNormalSum += normal[i];
                        winnerCount++;

                        if (linDepth[i] < winnerClosestLin)
                        {
                            winnerClosestLin = linDepth[i];
                            winnerClosestIndex = i;
                        }
                    }
                }

                if (winnerCount <= 0)
                {
                    result.color = color[closestIndex];
                    result.rawDepth = rawDepth[closestIndex];
                    result.linearDepth = linDepth[closestIndex];
                    result.normal = normal[closestIndex];
                    result.objID = objID[closestIndex];
                    result.detail = detail[closestIndex];
                    return result;
                }

                float invWinnerCount = rcp((float)winnerCount);
                result.color = winnerColorSum * invWinnerCount;
                result.rawDepth = rawDepth[winnerClosestIndex];
                result.linearDepth = linDepth[winnerClosestIndex];
                result.normal = NormalizeResolvedNormal(winnerNormalSum * invWinnerCount);
                result.objID = winnerID;
                result.detail = maxTier;
                return result;
            }

            FragmentOutput frag(Varyings input)
            {
                int2 outputPos = int2(input.positionCS.xy);
                int2 basePos   = outputPos * _SuperSamplingScale;

                ResolveResult resolved;

                if (_SuperSamplingScale == 2)
                    resolved = ResolveBlock2x2(basePos);

                FragmentOutput output;
                output.color = half4(resolved.color, 1.0);
                output.depth = float4(resolved.rawDepth, 0.0, 0.0, 1.0);
                output.normal = resolved.normal;
                output.objID = float4(resolved.objID, 0.0, 0.0);
                output.detail = float4(resolved.detail, 0.0, 0.0, 1.0);
                return output;
            }
            ENDHLSL
        }
    }
}
