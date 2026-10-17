// Superposición sobre las piezas de la peonza (efectos sobre la peonza, K9, GDD 7.6): se pinta encima de cada malla
// como un material más del renderer (BladeOverlay lo añade). Capas, de abajo arriba:
// - Destello: tiñe toda la pieza de _FlashColor en la proporción _FlashAmount.
// - Dos anillos con textura (Zbam: anillo de pinchos) proyectados desde arriba en el mundo: A (carga y ataque rápido,
//   crece desde el núcleo hacia fuera) y B (impacto, se abre desde el punto del choque y recorre la peonza). Cada uno:
//   _RingX = (centro x, centro z, radio, giro en radianes), _RingXColor (su alfa es la transparencia). Detrás de cada
//   anillo, una copia más grande (_RingShadowScale) y oscura (_RingShadowColor) para que se lea mejor.
// - Sprite animado del especial activo (K8/K9): una hoja de fotogramas en rejilla (_FxTex) proyectada desde arriba,
//   _Fx = (centro x, centro z, radio, giro), _FxSheet = (casillas en horizontal, en vertical, fotograma), teñida con
//   _FxColor (su alfa es la transparencia).
// - Borde de corte duro (tipo cómic): la silueta (fresnel) por encima de 1 - _RimWidth, con _RimSoftness de suavizado.
// Pase aparte (SRPDefaultUnlit, que URP pinta también): contorno grueso, la malla hinchada _OutlineWidth metros a lo
// largo de las normales y con las caras de delante quitadas (casco invertido), del color _OutlineColor.
// Con todo a 0 no se ve nada. Los valores los pone BladeOverlay con un MaterialPropertyBlock. Sin luz.
// Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/BladeOverlay"
{
    Properties
    {
        _RimColor ("Color del borde", Color) = (1, 1, 1, 1)
        _RimIntensity ("Intensidad del borde", Float) = 0
        _RimWidth ("Ancho del borde", Range(0, 1)) = 0.45
        _RimSoftness ("Suavizado del borde", Range(0.001, 0.5)) = 0.04
        _FlashColor ("Color del destello", Color) = (1, 1, 1, 1)
        _FlashAmount ("Destello", Range(0, 1)) = 0
        [NoScaleOffset] _RingTex ("Textura de los anillos", 2D) = "black" {}
        _RingA ("Anillo A (x, z, radio, giro)", Vector) = (0, 0, 0, 0)
        _RingAColor ("Color del anillo A", Color) = (1, 1, 1, 0)
        _RingB ("Anillo B (x, z, radio, giro)", Vector) = (0, 0, 0, 0)
        _RingBColor ("Color del anillo B", Color) = (1, 1, 1, 0)
        _RingShadowColor ("Sombra de los anillos (color y transparencia)", Color) = (0, 0, 0, 0.7)
        _RingShadowScale ("Tamaño de la sombra respecto al anillo", Float) = 1.12
        [NoScaleOffset] _FxTex ("Sprite del especial (hoja de fotogramas)", 2D) = "black" {}
        _Fx ("Sprite del especial (x, z, radio, giro)", Vector) = (0, 0, 0, 0)
        _FxSheet ("Hoja (casillas x, casillas y, fotograma)", Vector) = (1, 1, 0, 0)
        _FxColor ("Color del sprite del especial", Color) = (1, 1, 1, 0)
        _OutlineColor ("Color del contorno", Color) = (1, 1, 1, 0)
        _OutlineWidth ("Grosor del contorno (m)", Float) = 0.04
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_RingTex);
    SAMPLER(sampler_RingTex);
    TEXTURE2D(_FxTex);
    SAMPLER(sampler_FxTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _RimColor;
        half _RimIntensity;
        half _RimWidth;
        half _RimSoftness;
        half4 _FlashColor;
        half _FlashAmount;
        float4 _RingA;
        half4 _RingAColor;
        float4 _RingB;
        half4 _RingBColor;
        half4 _RingShadowColor;
        float _RingShadowScale;
        float4 _Fx;
        float4 _FxSheet;
        half4 _FxColor;
        half4 _OutlineColor;
        float _OutlineWidth;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        // Contorno grueso (casco invertido): se ve alrededor de la silueta, por detrás de la propia peonza
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                // Sin contorno, la malla se queda donde está (y con alfa 0 no se ve)
                positionWS += normalWS * (_OutlineColor.a > 0.001 ? _OutlineWidth : 0.0);
                return TransformWorldToHClip(positionWS);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "BladeOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.positionWS = positionWS;
                return output;
            }

            // Anillo proyectado desde arriba: la textura cubre un cuadrado de 2 × radio alrededor del centro, girada
            half Ring(float3 positionWS, float4 ring)
            {
                if (ring.z <= 0.0001) return 0;
                float2 d = (positionWS.xz - ring.xy) / (2.0 * ring.z);
                float s = sin(ring.w), c = cos(ring.w);
                float2 uv = float2(d.x * c - d.y * s, d.x * s + d.y * c) + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0)) return 0;
                return SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, uv).a;
            }

            // Sprite del especial proyectado desde arriba: como un anillo, pero con la casilla del fotograma de la hoja
            half4 Fx(float3 positionWS)
            {
                if (_FxColor.a <= 0.001 || _Fx.z <= 0.0001) return 0;
                float2 d = (positionWS.xz - _Fx.xy) / (2.0 * _Fx.z);
                float s = sin(_Fx.w), c = cos(_Fx.w);
                float2 local = float2(d.x * c - d.y * s, d.x * s + d.y * c) + 0.5;
                if (any(local < 0.0) || any(local > 1.0)) return 0;
                float tilesX = max(1.0, _FxSheet.x);
                float tilesY = max(1.0, _FxSheet.y);
                float frame = fmod(max(0.0, floor(_FxSheet.z)), tilesX * tilesY);
                float column = fmod(frame, tilesX);
                float row = floor(frame / tilesX);
                // Sin llegar al borde de la casilla, para no coger una línea de la de al lado
                local = clamp(local, 0.002, 0.998);
                float2 uv = float2((column + local.x) / tilesX, (tilesY - 1.0 - row + local.y) / tilesY);
                return SAMPLE_TEXTURE2D_LOD(_FxTex, sampler_FxTex, uv, 0) * _FxColor;
            }

            // Una capa encima de otra (color premultiplicado)
            void Over(inout half3 color, inout half alpha, half3 layerColor, half layerAlpha)
            {
                layerAlpha = saturate(layerAlpha);
                color = color * (1.0 - layerAlpha) + layerColor * layerAlpha;
                alpha = alpha * (1.0 - layerAlpha) + layerAlpha;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = 0;
                half alpha = 0;
                Over(color, alpha, _FlashColor.rgb, _FlashAmount);
                // Cada anillo con una copia más grande y oscura detrás, para que se lea sobre cualquier color
                float4 shadowA = float4(_RingA.xy, _RingA.z * _RingShadowScale, _RingA.w);
                float4 shadowB = float4(_RingB.xy, _RingB.z * _RingShadowScale, _RingB.w);
                Over(color, alpha, _RingShadowColor.rgb, Ring(input.positionWS, shadowA) * _RingShadowColor.a * _RingAColor.a);
                Over(color, alpha, _RingAColor.rgb, Ring(input.positionWS, _RingA) * _RingAColor.a);
                Over(color, alpha, _RingShadowColor.rgb, Ring(input.positionWS, shadowB) * _RingShadowColor.a * _RingBColor.a);
                Over(color, alpha, _RingBColor.rgb, Ring(input.positionWS, _RingB) * _RingBColor.a);
                half4 fx = Fx(input.positionWS);
                Over(color, alpha, fx.rgb, fx.a);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewWS);
                half fresnel = 1.0 - saturate(dot(n, v));
                half edge = 1.0 - _RimWidth;
                half rim = smoothstep(edge - _RimSoftness, edge + _RimSoftness, fresnel) * _RimIntensity;
                Over(color, alpha, _RimColor.rgb * max(1.0, rim), rim);

                return half4(alpha > 0.0001 ? color / alpha : color, alpha);
            }
            ENDHLSL
        }
    }
}
