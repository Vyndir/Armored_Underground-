// Mech armor: baked PBR maps (albedo with weathering, metallic/roughness/AO, normal)
// plus paint masks so each pilot's primary/accent colours tint the armor in-engine.
//   _MaskMap: R = primary paint, G = accent paint, B = edge wear
//   _MRAMap:  R = metallic, G = roughness, B = ambient occlusion
Shader "AU/MechSurface"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _MaskMap ("Paint Mask", 2D) = "black" {}
        _MRAMap ("Metal Rough AO", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _PaintColor ("Primary Paint", Color) = (0.24, 0.27, 0.32, 1)
        _AccentColor ("Accent Paint", Color) = (0.85, 0.45, 0.08, 1)
        _PaintGain ("Paint Gain", Float) = 1.8
        _HitFlash ("Hit Flash", Range(0,1)) = 0
        _DetailNormal ("Detail Normal (tiling)", 2D) = "bump" {}
        _DetailScale ("Detail Tiling", Float) = 5
        _DetailStrength ("Detail Strength", Float) = 0.1
        _ClearCoat ("Paint Clear Coat", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _PaintColor;
            half4 _AccentColor;
            half _PaintGain;
            half _BumpScale;
            half _HitFlash;
            float _DetailScale;
            half _DetailStrength;
            half _ClearCoat;
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
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #define _CLEARCOAT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_MRAMap);  SAMPLER(sampler_MRAMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogCoord : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half4 mra = SAMPLE_TEXTURE2D(_MRAMap, sampler_MRAMap, i.uv);
                albedo = lerp(albedo, albedo * _PaintColor.rgb * _PaintGain, mask.r);
                albedo = lerp(albedo, albedo * _AccentColor.rgb * (_PaintGain * 0.95h), mask.g);

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 nD = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, i.uv * _DetailScale), _DetailStrength);
                nTS = normalize(half3(nTS.xy + nD.xy, nTS.z * nD.z));
                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 nWS = normalize(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = mra.r;
                s.smoothness = 1.0h - mra.g;
                s.occlusion = lerp(1.0h, mra.b, 0.85h);
                s.normalTS = nTS;
                s.alpha = 1;
                s.emission = half3(1.0h, 0.35h, 0.2h) * _HitFlash * 2.0h;
                half painted = saturate(mask.r + mask.g);
                s.clearCoatMask = painted * _ClearCoat;
                s.clearCoatSmoothness = 0.9h;

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

                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, i.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert (A v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, l));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }
            half4 frag () : SV_Target { return 0; }
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

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; half3 n : TEXCOORD0; };
            V vert (A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.n = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag (V i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
}
