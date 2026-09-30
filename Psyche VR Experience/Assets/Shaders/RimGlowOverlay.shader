// Job Simulator style hover tint: a translucent colour over the whole surface, slightly
// stronger at the silhouette, drawn as an extra pass on top of the prop's own materials.
// GrabGlow sets _RimColor (colour, alpha = strength) through a MaterialPropertyBlock.
Shader "Psyche/RimGlowOverlay"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0)
        _RimPower ("Rim Power", Float) = 3
        _Fill ("Fill (share of the strength on faces seen head-on)", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RimGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _RimPower;
                float _Fill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                return half4(_RimColor.rgb, _RimColor.a * lerp(_Fill, 1.0, rim));
            }
            ENDHLSL
        }
    }
}
