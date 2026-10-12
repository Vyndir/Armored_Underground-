// Additive soft glow for light shafts under streetlights, thruster cones, halos.
// Fades at silhouette edges (fresnel) and near the camera.
Shader "AU/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.7,0.4,1)
        _EdgePower ("Edge Softness", Range(0.5,8)) = 2
        _HeightFade ("Fade Toward Base (0=none)", Range(0,1)) = 0.6
        _MainTex ("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _EdgePower; half _HeightFade; float4 _MainTex_ST;
            CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 nWS : TEXCOORD0; float3 vWS : TEXCOORD1; float2 uv : TEXCOORD2; float camDist : TEXCOORD3; half4 color : COLOR; };
            V vert (A v)
            {
                V o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.nWS = TransformObjectToWorldNormal(v.normalOS);
                o.vWS = GetWorldSpaceNormalizeViewDir(p);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.camDist = distance(p, _WorldSpaceCameraPos);
                o.color = v.color;
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                half edge = pow(abs(dot(normalize(i.nWS), normalize(i.vWS))), _EdgePower);
                half hf = lerp(1.0h, saturate(i.uv.y), _HeightFade);
                half near = saturate((i.camDist - 2.0) / 10.0);
                half m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                return half4(_Color.rgb * i.color.rgb * edge * hf * near * m, 1);
            }
            ENDHLSL
        }
    }
}
