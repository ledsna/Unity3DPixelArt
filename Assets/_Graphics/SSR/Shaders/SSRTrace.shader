Shader "Hidden/Ledsna/SSRTrace"
{
    Properties
    {
        _SSRThickness("Object Thickness", Float) = 1.5
        _SSRStep("Step Size", Float) = 0.2
        _SSRMaxSteps("Max Steps", Float) = 50.0
        _SSREdgeFade("Edge Fade", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SSRTrace"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_SSRDepthTexture);
            SAMPLER(sampler_SSRDepthTexture);
            
            TEXTURE2D(_SceneDepthTexture);
            SAMPLER(sampler_SceneDepthTexture);
            
            TEXTURE2D(_SSRNormalsTexture);
            SAMPLER(sampler_SSRNormalsTexture);
            
            float _SSRThickness;
            float _SSRStep;
            float _SSRStepGrowth;
            float _SSRMaxSteps;
            float _SSREdgeFade;
            float _SSRDistanceFade;
            float _SSRIntensity;

            float3 GetScenePositionVS(float2 uv)
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_SceneDepthTexture, sampler_SceneDepthTexture, uv);
                float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return TransformWorldToView(posWS);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                float4 normalSample = SAMPLE_TEXTURE2D(_SSRNormalsTexture, sampler_SSRNormalsTexture, uv);
                float3 normalWS = normalSample.xyz; 
                
                if(normalSample.a > 1.0) return 0;

                float smoothness = normalSample.a;
                float roughness = 1.0 - smoothness;
                
                if (dot(normalWS, normalWS) < 0.1) return 0;
                normalWS = normalize(normalWS);

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_SSRDepthTexture, sampler_SSRDepthTexture, uv);
                float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                
                float3 viewDirWS = normalize(GetCameraPositionWS() - posWS);

                float3 reflectionVector = reflect(-viewDirWS, normalWS);
                
                float3 viewDirVS = normalize(mul((float3x3)UNITY_MATRIX_V, reflectionVector));
                float3 startPosVS = TransformWorldToView(posWS);
                
                float stepSize = _SSRStep;
                float growth = _SSRStepGrowth; 
                float maxSteps = _SSRMaxSteps;
                float currentThickness = _SSRThickness;
                
                float3 rayPosVS = startPosVS;

                float2 pixelPos = uv * _ScreenParams.xy;
                float dither = frac(52.9829189 * frac(dot(pixelPos, float2(0.06711056, 0.00583715))));
                
                rayPosVS += viewDirVS * stepSize * dither; 

                half3 foundColor = 0;
                float alphaOut = 0;
                
                [loop]
                for(int i = 0; i < maxSteps; i++)
                {
                    rayPosVS += viewDirVS * stepSize;
                    
                    float progress = (float)i / maxSteps;
                    float frameGrowth = lerp(1.0, growth, progress);
                    
                    stepSize *= frameGrowth;
                    currentThickness *= frameGrowth;

                    float rayDist = distance(rayPosVS, startPosVS);
                    float coneRadius = rayDist * roughness * 0.1;
                    
                    float4 posCS = mul(UNITY_MATRIX_P, float4(rayPosVS, 1));
                    float4 screenPos = ComputeScreenPos(posCS);
                    float2 screenUV = screenPos.xy / screenPos.w;
                    
                    if(any(screenUV < 0) || any(screenUV > 1)) break;
                    
                    float3 scenePosVS = GetScenePositionVS(screenUV);
                    
                    if (rayPosVS.z < scenePosVS.z)
                    {
                        float3 coarseRayPosVS = rayPosVS;
                        float currentStepSize = stepSize;
                        
                        rayPosVS -= viewDirVS * stepSize; 
                        stepSize *= 0.5; 
                        
                        [unroll]
                        for(int j = 0; j < 6; j++)
                        {
                            float3 checkPos = rayPosVS + viewDirVS * stepSize;
                            float4 cPosCS = mul(UNITY_MATRIX_P, float4(checkPos, 1));
                            float4 cScreenPos = ComputeScreenPos(cPosCS);
                            float2 cScreenUV = cScreenPos.xy / cScreenPos.w;
                            float3 cScenePosVS = GetScenePositionVS(cScreenUV);
                            
                            if (checkPos.z >= cScenePosVS.z) 
                                rayPosVS = checkPos; 
                                
                            stepSize *= 0.5;
                        }
                        
                        float4 finalPosCS = mul(UNITY_MATRIX_P, float4(rayPosVS, 1));
                        float4 finalScreenPos = ComputeScreenPos(finalPosCS);
                        float2 finalScreenUV = finalScreenPos.xy / finalScreenPos.w;
                        float3 finalScenePosVS = GetScenePositionVS(finalScreenUV);
                        
                        float refinedDist = distance(rayPosVS, startPosVS);
                        float refinedConeRadius = refinedDist * roughness * 0.1;
                        float refinedThickness = currentThickness + refinedConeRadius;
                        
                        float refinedDiff = finalScenePosVS.z - rayPosVS.z;
                        
                        if (abs(refinedDiff) < refinedThickness)
                        {
                            float2 edgeDist = min(finalScreenUV, 1 - finalScreenUV);
                            float edgeFactor = saturate(min(edgeDist.x, edgeDist.y) / _SSREdgeFade);
                            
                            float pixelRadius = (refinedConeRadius * _ScreenParams.y) / abs(rayPosVS.z);
                            float lod = log2(max(1.0, pixelRadius));

                            float2 taps[4] = { float2(0,1), float2(1,0), float2(0,-1), float2(-1,0) };
                            float3 accumColor = 0;
                            float totalWeight = 0.0;

                            float3 centerNormal = SAMPLE_TEXTURE2D_LOD(_SSRNormalsTexture, sampler_SSRNormalsTexture, finalScreenUV, 0).xyz;
                            
                            float angle = dither * 6.28;
                            float c = cos(angle); float s = sin(angle);
                            float2x2 rot = float2x2(c, -s, s, c);
                            
                            [unroll]
                            for(int t=0; t<4; t++) {
                                float2 offset = mul(rot, taps[t]) * pixelRadius * _BlitTexture_TexelSize.xy * 0.5;
                                float2 tapUV = finalScreenUV + offset;

                                float3 tapNormal = SAMPLE_TEXTURE2D_LOD(_SSRNormalsTexture, sampler_SSRNormalsTexture, tapUV, 0).xyz;
                                float3 tapColor = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, tapUV, lod).rgb;

                                float wNormal = (dot(centerNormal, tapNormal) > 0.99) ? 1.0 : 0.0;

                                float3 tapScenePosVS = GetScenePositionVS(tapUV);
                                float tapDepthDiff = abs(tapScenePosVS.z - rayPosVS.z);
                                float wDepth = (tapDepthDiff < (0.001 * abs(rayPosVS.z))) ? 1.0 : 0.0;

                                float w = wNormal * wDepth;

                                accumColor += tapColor * w;
                                totalWeight += w;
                            }
                            
                            foundColor = (totalWeight > 0.001) ? accumColor / totalWeight : SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, finalScreenUV, 0).rgb;
                            
                            foundColor = min(foundColor, half3(8.0, 8.0, 8.0)); 
                            
                            float physicsFactor = _SSRIntensity / (1.0 + refinedDist * _SSRDistanceFade); 
                            foundColor *= physicsFactor * edgeFactor;
                            
                            alphaOut = physicsFactor * edgeFactor; 
                            break;
                        }
                        else
                        {
                            rayPosVS = coarseRayPosVS;
                            stepSize = currentStepSize;
                        }
                    }
                }
                
                return half4(foundColor, alphaOut);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CopyAndUnpackNormals"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUnpack

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragUnpack(Varyings input) : SV_Target
            {
                float3 normal = SampleSceneNormals(input.texcoord);
                
                float4 raw = SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, input.texcoord);
                
                return half4(normal, raw.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CopyDepth"
            ZTest Always 
            ZWrite On 
            ColorMask 0 
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float FragDepth(Varyings input) : SV_Depth
            {
                return SampleSceneDepth(input.texcoord);
            }
            ENDHLSL
        }

    }
}
