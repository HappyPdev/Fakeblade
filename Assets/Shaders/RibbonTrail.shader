// Rastro tipo cinta de las peonzas (K7, GDD 7.6). Lo pinta un TrailRenderer (BladeTrail) con una tira de fotogramas en
// horizontal (_MainTex, _Frames casillas) estirada a lo largo de toda la cinta. _Frame elige el fotograma (lo avanza
// BladeTrail según la velocidad) y _Flip da la vuelta al dibujo para que la cabeza quede pegada a la peonza.
// El color del vértice (color del jugador y transparencia) tiñe el dibujo blanco. Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/RibbonTrail"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Tira de fotogramas", 2D) = "white" {}
        _Frames ("Fotogramas", Float) = 8
        _Frame ("Fotograma", Float) = 0
        [Toggle] _Flip ("Al revés", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RibbonTrail"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Frames;
                float _Frame;
                float _Flip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float frames = max(1.0, _Frames);
                float frame = fmod(max(0.0, floor(_Frame)), frames);
                float u = saturate(input.uv.x);
                if (_Flip > 0.5) u = 1.0 - u;
                // Sin llegar al borde de la casilla, para no coger una columna del fotograma de al lado
                u = clamp(u, 0.002, 0.998);
                float2 uv = float2((frame + u) / frames, input.uv.y);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
            }
            ENDHLSL
        }
    }
}
