// Partícula que se disuelve (humo de Onda de choque, 2026-10-18; copiado del humo de CFXR Explosion 1, de Cartoon FX
// Remaster): el color del vértice tiñe la textura, y su transparencia no transparenta, sino que se usa como umbral contra
// una textura de ruido (_DissolveTex): al bajar, desaparecen primero las zonas oscuras del ruido, así que una nube gorda se
// rompe en varios pedacitos que se van haciendo pequeños. _DissolveSmooth: borde del corte (0 = duro, pixel art). La
// textura de ruido usa la misma UV que la principal (también con hojas de fotogramas). Transparente, sin luz. Compatible
// con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/ParticleDissolve"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        [NoScaleOffset] _DissolveTex ("Ruido de la disolución", 2D) = "white" {}
        _DissolveSmooth ("Borde del corte", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ParticleDissolve"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _DissolveSmooth;
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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half noise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, input.uv).r;
                // Umbral: con transparencia 1 se ve entera; al bajar, se va comiendo desde lo oscuro del ruido
                half threshold = 1.0 - input.color.a;
                half keep = saturate((noise - threshold) / max(_DissolveSmooth, 0.001) + 0.5);
                if (_DissolveSmooth <= 0.001) keep = step(threshold, noise);
                return half4(tex.rgb * input.color.rgb, tex.a * keep);
            }
            ENDHLSL
        }
    }
}
