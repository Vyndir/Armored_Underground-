// Rain-soaked asphalt: world-space UVs, puddle mask, animated rain ripples and a
// planar reflection of the street (rendered by AU.PlanarReflection) blended in
// by wetness and fresnel. This is the core of the night-street look.
Shader "AU/WetAsphalt"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _MaskMap ("Mask (R puddles, G grime)", 2D) = "black" {}
        _RippleMap ("Rain Ripple Normal", 2D) = "bump" {}
        _Tiling ("World Tiling", Float) = 0.12
        _PuddleTiling ("Puddle Tiling", Float) = 0.02
        _DrySmoothness ("Dry Smoothness", Range(0,1)) = 0.45
        _WetSmoothness ("Wet Smoothness", Range(0,1)) = 0.96
        _Wetness ("Wetness", Range(0,1)) = 0.75
        _RainAmount ("Rain Ripples", Range(0,1)) = 1
        _ReflectionStrength ("Reflection Strength", Range(0,2)) = 1
        _Distortion ("Reflection Distortion", Range(0,0.2)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            float _Tiling;
            float _PuddleTiling;
            half _DrySmoothness;
            half _WetSmoothness;
            half _Wetness;
            half _RainAmount;
            half _ReflectionStrength;
            half _Distortion;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
            TEXTURE2D(_RippleMap);  SAMPLER(sampler_RippleMap);
            TEXTURE2D(_PlanarReflectionTex);
            float _PlanarReflectionFlip;

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

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz * _Tiling;
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.positionWS.xz * _PuddleTiling);
                half wet = saturate((mask.r - (1.0h - _Wetness)) * 3.0h);
                wet = max(wet, _Wetness * 0.35h); // everything is at least damp

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale * (1.0h - wet * 0.9h));
                float2 ruv = i.positionWS.xz * 0.45;
                half3 r1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, ruv + _Time.y * float2(0.031, 0.047)), 1);
                half3 r2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, ruv * 1.37 + float2(0.5, 0.2) - _Time.y * float2(0.043, 0.021)), 1);
                half3 ripple = normalize(half3(r1.xy + r2.xy, 1.0h));
                nTS = normalize(lerp(nTS, ripple, wet * _RainAmount * 0.6h));
                // flat ground: tangent +X, bitangent +Z, normal +Y
                half3 nWS = normalize(half3(nTS.x, nTS.z, nTS.y));

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb * lerp(1.0h, 0.45h, wet);
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = lerp(_DrySmoothness * (0.6h + 0.4h * mask.g), _WetSmoothness, wet);
                s.normalTS = nTS;
                s.occlusion = 1;
                s.alpha = 1;

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = nWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogCoord;
                d.bakedGI = SampleSH(nWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(d, s);

                // Planar reflection
                float2 suv = d.normalizedScreenSpaceUV;
                if (_PlanarReflectionFlip > 0.5) suv.y = 1.0 - suv.y;
                suv += nWS.xz * _Distortion * (0.4 + wet);
                // vertical streaking: lights smear toward the viewer like on real wet roads
                half rough = 1.0h - s.smoothness;
                float spread = 0.004 + rough * 0.06;
                half3 refl = 0;
                refl += SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv).rgb * 0.30h;
                refl += SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv + float2(0, spread * 0.5)).rgb * 0.20h;
                refl += SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv - float2(0, spread * 0.5)).rgb * 0.20h;
                refl += SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv + float2(0, spread)).rgb * 0.15h;
                refl += SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, suv - float2(0, spread)).rgb * 0.15h;
                half ndv = saturate(dot(nWS, d.viewDirectionWS));
                half fresnel = 0.04h + 0.96h * pow(1.0h - ndv, 5.0h);
                half w = saturate(lerp(0.06h, 0.9h, wet * wet) * lerp(0.3h, 1.0h, fresnel)) * _ReflectionStrength;
                color.rgb = color.rgb * (1.0h - w * 0.6h) + refl * w;

                color.rgb = MixFog(color.rgb, i.fogCoord);
                return half4(color.rgb, 1);
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
            struct V { float4 positionCS : SV_POSITION; };
            V vert (float4 positionOS : POSITION) { V o; o.positionCS = TransformObjectToHClip(positionOS.xyz); return o; }
            half4 frag (V i) : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
