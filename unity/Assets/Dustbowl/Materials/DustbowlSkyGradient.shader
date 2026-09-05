Shader "Dustbowl/SkyGradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.114, 0.227, 0.388, 1)
        _MidColor ("Mid", Color) = (0.753, 0.478, 0.447, 1)
        _HazeColor ("Haze", Color) = (0.965, 0.690, 0.467, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _TopColor;
            float4 _MidColor;
            float4 _HazeColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float elevation = normalize(input.direction).y;
                float lowerBlend = smoothstep(-0.18, 0.025, elevation);
                float upperBlend = smoothstep(0.02, 0.38, elevation);
                half3 color = lerp(_HazeColor.rgb, _MidColor.rgb, lowerBlend);
                color = lerp(color, _TopColor.rgb, upperBlend);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
