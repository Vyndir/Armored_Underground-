// Day-to-night sky driven by AU.DayNight: blue zenith and pale haze by day, warm
// horizon at sunset, stars and a moon at night, with a drifting cloud layer.
Shader "AU/Sky"
{
    Properties
    {
        _CloudCover ("Cloud Cover", Range(0,1)) = 0.45
        _CloudSpeed ("Cloud Speed", Float) = 0.004
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
            half _CloudCover; float _CloudSpeed;
            float4 _AU_SunDir;   // xyz: direction toward the sun (may be below the horizon)
            float4 _AU_MoonDir;
            half _AU_Night;
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert (A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y); }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 5; k++) { s += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; } return s; }

            half4 frag (V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sd = normalize(_AU_SunDir.xyz);
                float sunH = sd.y;
                float day = saturate(sunH * 3.5 + 0.25);
                float twilight = saturate(1.0 - abs(sunH + 0.02) * 6.0);
                float toSun = saturate(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(sd.x, 0, sd.z))) * 0.5 + 0.5);

                float h = saturate(d.y);
                float3 dayZen = float3(0.16, 0.38, 0.82), dayHor = float3(0.66, 0.78, 0.92);
                float3 nightZen = float3(0.006, 0.009, 0.025), nightHor = float3(0.045, 0.05, 0.1);
                float3 setHor = float3(1.0, 0.5, 0.25), setZen = float3(0.25, 0.28, 0.5);
                float3 zen = lerp(nightZen, dayZen, day);
                float3 hor = lerp(nightHor, dayHor, day);
                zen = lerp(zen, setZen, twilight * 0.5);
                hor = lerp(hor, setHor, twilight * pow(toSun, 2.0) * 0.85);
                // city glow on the night horizon
                hor += float3(0.12, 0.06, 0.1) * _AU_Night * (1.0 - h);
                float3 col = lerp(hor, zen, pow(h, 0.42));
                if (d.y < 0) col = hor * lerp(0.85, 0.55, saturate(-d.y * 4));

                // sun: disk + glow (only above the horizon)
                float sdot = dot(d, sd);
                float sunVis = saturate(sunH * 20 + 0.3);
                col += float3(1.0, 0.85, 0.65) * pow(saturate(sdot), 1800.0) * 40.0 * sunVis;
                col += float3(1.0, 0.7, 0.45) * pow(saturate(sdot), 12.0) * (0.25 + twilight * 0.6) * sunVis;

                // night: stars and moon
                if (_AU_Night > 0.01)
                {
                    float2 sp = d.xz / (d.y + 0.25) * 140.0;
                    float2 cell = floor(sp);
                    float st = hash(cell);
                    float2 off = frac(sp) - float2(hash(cell + 3.1), hash(cell + 7.7));
                    float star = step(0.985, st) * saturate(1.0 - length(off) * 6.0) * (0.6 + 0.4 * sin(_Time.y * 2.0 + st * 60.0));
                    col += star * 1.8 * _AU_Night * h;
                    float3 md = normalize(_AU_MoonDir.xyz);
                    float mdot = dot(d, md);
                    col += float3(0.85, 0.9, 1.0) * smoothstep(0.99955, 0.9997, mdot) * 3.0 * _AU_Night;
                    col += float3(0.3, 0.38, 0.6) * pow(saturate(mdot), 40.0) * 0.25 * _AU_Night;
                }

                // clouds on a flattened dome
                if (d.y > 0.0)
                {
                    float2 cp = d.xz / (d.y + 0.12) * 1.6 + _Time.y * _CloudSpeed * float2(1.0, 0.4);
                    float c = fbm(cp);
                    float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.32, c);
                    cover *= smoothstep(0.0, 0.18, d.y);
                    float shade = fbm(cp * 1.7 + 3.0);
                    float3 lit = lerp(float3(0.06, 0.07, 0.11), float3(1.0, 0.98, 0.95), day);
                    lit = lerp(lit, float3(1.0, 0.62, 0.45), twilight * toSun * 0.8);
                    float3 dark = lit * lerp(0.55, 0.75, day);
                    float3 cc = lerp(lit, dark, shade);
                    cc += float3(1.0, 0.8, 0.6) * pow(saturate(sdot), 6.0) * 0.6 * sunVis;
                    col = lerp(col, cc, cover * 0.92);
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
