// Conos de la cúpula de Pinchos (K8, 2026-10-28; idea del usuario, GDD 7.6): pinchos macizos pegados a la cúpula por la base,
// con sombreado plano de cómic (dos tonos con corte duro y una luz fija, sin la de la escena), la punta más clara y un contorno
// negro: la malla lleva primero una copia inflada con las caras al revés (uv2.y = 1, casco invertido), que solo asoma por los
// bordes. Los valores los pone BladeChargeDome con un MaterialPropertyBlock. Escribe profundidad y se pinta justo antes que la
// cúpula (transparente, por encima). Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/SpikeCone"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.8, 0.4, 1, 1)
        _TipColor ("Punta", Color) = (0.95, 0.88, 1, 1)
        _ShadeColor ("Sombra", Color) = (0.42, 0.18, 0.62, 1)
        _OutlineColor ("Contorno", Color) = (0.05, 0.05, 0.05, 1)
        _TipFrom ("Punta: desde qué parte del largo (1 = sin punta)", Range(0, 1)) = 0.7
        _LightDir ("Luz fija: dirección hacia ella (mundo)", Vector) = (-0.4, 0.8, -0.45, 0)
        _ShadeThreshold ("Corte de la sombra (-1 a 1)", Range(-1, 1)) = 0.15
        _Flash ("Destello", Range(0, 1)) = 0
        _Alpha ("Transparencia", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Back

        Pass
        {
            Name "SpikeCone"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _ShadeColor;
                half4 _OutlineColor;
                float _TipFrom;
                float4 _LightDir;
                float _ShadeThreshold;
                float _Flash;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                // y = 0 en la base, 1 en la punta
                float2 uv : TEXCOORD0;
                // y = 1 en el casco del contorno
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float along : TEXCOORD1;
                float outline : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.along = input.uv.y;
                output.outline = input.uv2.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (input.outline > 0.5) return half4(_OutlineColor.rgb, _OutlineColor.a * _Alpha);
                // Dos tonos con corte duro (cómic) y la punta de otro color
                float lit = dot(normalize(input.normalWS), normalize(_LightDir.xyz));
                half4 color = lerp(_ShadeColor, _BaseColor, step(_ShadeThreshold, lit));
                color = lerp(color, _TipColor, step(_TipFrom, input.along));
                color.rgb = lerp(color.rgb, half3(1.0, 1.0, 1.0), _Flash);
                color.a *= _Alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
