// Open bay water: two scrolling wave normal maps, a sun glint, the planar
// reflection (AU.PlanarReflection) or the sky probe, and a depth tint that goes
// from turquoise near the eye to deep navy toward the horizon.
Shader "AU/Water"
{
    Properties
    {
        _WaveMap ("Wave Normal", 2D) = "bump" {}
        _WaveScale ("Wave Tiling", Float) = 0.018
        _WaveStrength ("Wave Strength", Range(0,2)) = 0.7
        _WaveSpeed ("Wave Speed", Float) = 0.03
        _ShallowColor ("Shallow Color", Color) = (0.05,0.28,0.33,1)
        _DeepColor ("Deep Color", Color) = (0.02,0.08,0.16,1)
        _ReflectionStrength ("Reflection Strength", Range(0,2)) = 1
        _Distortion ("Reflection Distortion", Range(0,0.2)) = 0.05
        _SunGlint ("Sun Glint", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WaveMap_ST;
                float _WaveScale;
                half _WaveStrength;
                float _WaveSpeed;
                half4 _ShallowColor;
                half4 _DeepColor;
                half _ReflectionStrength;
                half _Distortion;
                half _SunGlint;
            CBUFFER_END

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            TEXTURE2D(_PlanarReflectionTex);
            float _PlanarReflectionFlip;
            half _AU_Night;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogCoord : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Waves(float2 xz, float scale, float t)
            {
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, xz * scale + t * float2(0.9, 0.35)), 1);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, xz * scale * 1.71 + float2(0.37, 0.61) - t * float2(0.4, 0.8)), 1);
                return half3(a.xy + b.xy, a.z * b.z);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float dist = distance(i.positionWS, GetCameraPositionWS());
                // big + small waves, flattened with distance so the horizon doesn't shimmer
                half3 w = Waves(i.positionWS.xz, _WaveScale, t) + Waves(i.positionWS.xz, _WaveScale * 6.3, t * 2.1) * 0.5;
                half flat = saturate(dist / 1800.0);
                half3 nTS = normalize(half3(w.xy * _WaveStrength * lerp(1.0, 0.25, flat), 1.0));
                half3 N = normalize(half3(nTS.x, nTS.z, nTS.y));

                half ndv = saturate(dot(N, V));
                half fresnel = 0.02h + 0.98h * pow(1.0h - ndv, 5.0h);

                // body color: see down into the water near the eye, deep blue far away
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(1.0 - V.y * 1.6 + flat * 0.4));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sunUp = saturate(sun.direction.y * 3.0);
                half3 ambient = SampleSH(half3(0, 1, 0));
                body *= ambient * 1.4 + sun.color * saturate(dot(N, sun.direction)) * 0.35;

                // reflections: planar for nearby structures, sky probe beyond
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                if (_PlanarReflectionFlip > 0.5) suv.y = 1.0 - suv.y;
                suv += N.xz * _Distortion;
                half3 planar = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv).rgb;
                half3 R = reflect(-V, N);
                R.y = abs(R.y);
                half3 sky = GlossyEnvironmentReflection(R, 0.05h, 1.0h);
                half pl = saturate(dot(planar, 1) * 4.0);
                half3 refl = lerp(sky, planar, pl * 0.85);

                half3 col = lerp(body, refl * _ReflectionStrength, saturate(fresnel * 1.15 + 0.08));

                // sun glint
                half3 H = normalize(sun.direction + V);
                half spec = pow(saturate(dot(N, H)), 420.0) * _SunGlint + pow(saturate(dot(N, H)), 60.0) * 0.25;
                col += sun.color * spec * sun.shadowAttenuation * lerp(0.15, 1.0, sunUp);

                col = MixFog(col, i.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert (float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float4 positionCS : SV_POSITION; };
            V vert (float4 positionOS : POSITION) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); return o; }
            half4 frag (V i) : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
