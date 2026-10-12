// Night sky: deep blue-violet zenith, warm sodium/magenta light pollution at the horizon.
Shader "AU/NightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.008,0.01,0.03,1)
        _Horizon ("Horizon Glow", Color) = (0.22,0.09,0.2,1)
        _Glow2 ("Low Glow", Color) = (0.45,0.2,0.1,1)
        _Clouds ("Cloud Strength", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _Zenith, _Horizon, _Glow2; half _Clouds;
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert (A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p) { float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(hash(i), hash(i+float2(1,0)), f.x), lerp(hash(i+float2(0,1)), hash(i+float2(1,1)), f.x), f.y); }
            half4 frag (V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y);
                half3 c = lerp(_Horizon.rgb, _Zenith.rgb, pow(h, 0.45));
                c += _Glow2.rgb * pow(saturate(1.0 - h * 6.0), 3.0);
                // low drifting cloud deck lit from below by the city
                float2 cuv = d.xz / max(d.y, 0.05) * 0.6 + _Time.y * 0.004;
                float n = noise(cuv) * 0.6 + noise(cuv * 2.7) * 0.3 + noise(cuv * 6.1) * 0.1;
                c += _Horizon.rgb * 1.6 * smoothstep(0.45, 0.9, n) * _Clouds * saturate(1.0 - h * 1.5);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
