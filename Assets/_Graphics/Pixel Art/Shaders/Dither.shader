Shader "Ledsna/Dither"
{
    Properties
    {
        _Colour("Colour", Color) = (0,0,0,1)
        _Density("Density", Float) = 1
    }
    SubShader
    {
        Tags {
            "Queue" = "AlphaTest"
            "RenderType"="Opaque"
        }
        LOD 100
        
        Blend SrcAlpha OneMinusDstColor

        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Density;
            float3 _Colour;

            float2 _PixelResolution;

            float Dither(float In, float2 ScreenPosition)
            {
                float2 pixelPos = ScreenPosition * _PixelResolution;
                
                uint    x       = (pixelPos.x % 4 + 4) % 4;
                uint    y       = (pixelPos.y % 4 + 4) % 4;
                uint    index     = x * 4 + y;

                float DITHER_THRESHOLDS[16] =
                {
                    1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                    13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                    4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                    16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
                };
                return In - DITHER_THRESHOLDS[index];
            }

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
            };

            v2f vert (appdata input)
            {
                v2f output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag (v2f input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float dither = Dither(_Density, uv);
                clip(dither);

                return half4(_Colour * dither, 1);
            }
            ENDHLSL
        }
    }
}
