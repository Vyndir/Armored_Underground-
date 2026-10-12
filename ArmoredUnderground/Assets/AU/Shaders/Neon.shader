// HDR emissive surface for neon tubes, signs and light strips. Values above 1
// feed the bloom pass. Optional texture (rgb * a) and flicker.
Shader "AU/Neon"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0,4,6,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Flicker ("Flicker", Range(0,1)) = 0
        _FlickerSeed ("Flicker Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _MainTex_ST;
            half _Flicker;
            float _FlickerSeed;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vert (A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.fog = ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag (V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float n = frac(sin(floor(_Time.y * 12.0 + _FlickerSeed) * 91.7 + _FlickerSeed) * 43758.5);
                half f = 1.0h - _Flicker * step(0.86, n) * 0.85h;
                half3 c = _Color.rgb * t.rgb * t.a * f;
                // fog dims neon less than surfaces so it reads through haze
                c = lerp(unity_FogColor.rgb, c, saturate(i.fog * 0.6 + 0.4));
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert (float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
