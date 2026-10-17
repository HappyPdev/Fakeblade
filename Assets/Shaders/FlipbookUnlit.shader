// Malla con una hoja de fotogramas en rejilla (K7, efecto de la punta en el acelerón, GDD 7.6). Pinta las dos caras
// (Cull Off: el ImpactPlane de Wallcoeur tiene las normales hacia dentro), transparente y sin luz. _Frame elige la
// casilla (0 = arriba a la izquierda) y _Color la tiñe; los dos los pone BladeBurstTip con un MaterialPropertyBlock.
// Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/FlipbookUnlit"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Hoja de fotogramas", 2D) = "white" {}
        _TilesX ("Casillas en horizontal", Float) = 3
        _TilesY ("Casillas en vertical", Float) = 3
        _Frame ("Fotograma", Float) = 0
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FlipbookUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _TilesX;
                float _TilesY;
                float _Frame;
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float tilesX = max(1.0, _TilesX);
                float tilesY = max(1.0, _TilesY);
                float frame = fmod(max(0.0, floor(_Frame)), tilesX * tilesY);
                float column = fmod(frame, tilesX);
                float row = floor(frame / tilesX);
                // Sin llegar al borde de la casilla, para no coger una línea de la de al lado
                float2 local = clamp(input.uv, 0.002, 0.998);
                float2 uv = float2((column + local.x) / tilesX, (tilesY - 1.0 - row + local.y) / tilesY);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
            }
            ENDHLSL
        }
    }
}
