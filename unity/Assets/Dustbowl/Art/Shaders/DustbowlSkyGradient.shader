// Dustbowl gradient sky. Reproduces the web build's three-band vertex-coloured
// sky shell (zenith -> mid -> horizon haze) with a soft sun disc so distant
// terrain fades into the same warm haze that the scene fog uses.
Shader "Dustbowl/Sky Gradient"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.153, 0.306, 0.533, 1)
        _MidColor ("Mid Sky", Color) = (0.749, 0.494, 0.455, 1)
        _HorizonColor ("Horizon Haze", Color) = (0.953, 0.698, 0.475, 1)
        _GroundColor ("Below Horizon", Color) = (0.910, 0.675, 0.471, 1)
        _MidHeight ("Mid Band Height", Range(-0.2, 0.5)) = 0.02
        _ZenithBlend ("Zenith Blend", Range(0.05, 1)) = 0.34
        _HorizonBlend ("Horizon Blend", Range(0.01, 0.5)) = 0.14
        _SunDirection ("Sun Direction", Vector) = (-0.521, 0.591, -0.616, 0)
        _SunColor ("Sun Colour", Color) = (1, 0.941, 0.784, 1)
        _SunSize ("Sun Disc Size", Range(0.0002, 0.05)) = 0.0015
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor;
            fixed4 _MidColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            float _MidHeight;
            float _ZenithBlend;
            float _HorizonBlend;
            float4 _SunDirection;
            fixed4 _SunColor;
            float _SunSize;
            float _SunGlow;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float y = dir.y;

                float3 upper = lerp(_MidColor.rgb, _ZenithColor.rgb,
                    smoothstep(_MidHeight, _MidHeight + _ZenithBlend, y));
                float3 lower = lerp(_HorizonColor.rgb, _MidColor.rgb,
                    smoothstep(_MidHeight - _HorizonBlend, _MidHeight, y));
                float3 sky = y > _MidHeight ? upper : lower;
                sky = lerp(_GroundColor.rgb, sky, smoothstep(-0.06, -0.005, y));

                float3 sunDir = normalize(_SunDirection.xyz);
                float cosAngle = dot(dir, sunDir);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, cosAngle);
                float glow = pow(saturate(cosAngle), 48.0) * _SunGlow * 0.35;
                sky += _SunColor.rgb * (disc * 1.5 + glow);

                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
