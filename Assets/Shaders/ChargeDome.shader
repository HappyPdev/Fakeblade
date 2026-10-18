// Cúpula de la carga (Onda de choque, K8, 2026-10-18; GDD 7.6): media esfera translúcida con estilo de cómic (cortes
// duros, sin degradados): relleno claro, borde de la silueta naranja con una línea negra por fuera, una franja en la base,
// una textura (líneas de velocidad o una Zbam) que se desliza alrededor y líneas en remolino dibujadas (blancas, con una
// negra fina entre cada dos). Las dos giran alrededor de un eje inclinado que se bambolea (_Axis, con _AxisT1 y _AxisT2
// perpendiculares a él). Los valores los pone BladeChargeDome con un
// MaterialPropertyBlock. Transparente, sin luz, solo las caras de delante. Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/ChargeDome"
{
    Properties
    {
        _BodyColor ("Relleno", Color) = (1, 0.97, 0.9, 0.18)
        _RimColor ("Borde", Color) = (1, 0.55, 0.15, 0.95)
        _OutlineColor ("Línea del borde", Color) = (0.05, 0.05, 0.05, 0.9)
        _SwirlColor ("Remolino", Color) = (1, 1, 1, 0.8)
        _SwirlColor2 ("Remolino (contraste)", Color) = (0.05, 0.05, 0.05, 0.6)
        _RimWidth ("Ancho del borde", Range(0, 1)) = 0.3
        _OutlineWidth ("Ancho de la línea", Range(0, 1)) = 0.08
        _SwirlLines ("Líneas del remolino", Float) = 5
        _SwirlTwist ("Vueltas de las líneas", Float) = 0.6
        _SwirlPhase ("Giro del remolino", Float) = 0
        _SwirlWidth ("Grosor de las líneas", Range(0, 1)) = 0.18
        _Flash ("Destello", Range(0, 1)) = 0
        _Alpha ("Transparencia", Range(0, 1)) = 1
        [NoScaleOffset] _LinesTex ("Textura de líneas", 2D) = "white" {}
        _Lines ("Líneas: repeticiones (xy), giro (z), enroscado (w; < 0 = sin textura)", Vector) = (3, 2, 0, -1)
        _Axis ("Eje", Vector) = (0, 1, 0, 0)
        _AxisT1 ("Eje: perpendicular 1", Vector) = (1, 0, 0, 0)
        _AxisT2 ("Eje: perpendicular 2", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ChargeDome"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LinesTex);
            SAMPLER(sampler_LinesTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _RimColor;
                half4 _OutlineColor;
                half4 _SwirlColor;
                half4 _SwirlColor2;
                float _RimWidth;
                float _OutlineWidth;
                float _SwirlLines;
                float _SwirlTwist;
                float _SwirlPhase;
                float _SwirlWidth;
                float _Flash;
                float _Alpha;
                float4 _Lines;
                float4 _Axis;
                float4 _AxisT1;
                float4 _AxisT2;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            // Pone encima un color con su alfa (como pintar una capa encima de otra)
            half4 Over(half4 under, half4 over, half amount)
            {
                half a = over.a * amount;
                half outA = a + under.a * (1.0 - a);
                half3 rgb = (over.rgb * a + under.rgb * under.a * (1.0 - a)) / max(outA, 1e-4);
                return half4(rgb, outA);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = 1.0 - saturate(abs(dot(normal, view)));

                // Vuelta alrededor del eje inclinado (0-1) y altura a lo largo de él (0-1)
                float3 p = normalize(input.positionOS);
                float along = dot(p, _Axis.xyz) * 0.5 + 0.5;
                float turn = atan2(dot(p, _AxisT2.xyz), dot(p, _AxisT1.xyz)) * (0.5 / PI) + 0.5;
                // Todo se apaga cerca del extremo del eje, donde se juntan las líneas
                half top = 1.0 - step(0.9, along);

                // Remolino dibujado: líneas que se enroscan y giran (número entero de líneas: sin costura)
                float lines = max(1.0, floor(_SwirlLines + 0.5));
                float s = frac(turn * lines + along * _SwirlTwist + _SwirlPhase);
                half swirl = step(s, _SwirlWidth);
                half swirl2 = step(abs(s - 0.5), _SwirlWidth * 0.25);

                half4 color = _BodyColor;
                color = Over(color, _SwirlColor2, swirl2 * top);
                color = Over(color, _SwirlColor, swirl * top);

                // Textura que se desliza alrededor (repeticiones enteras alrededor: sin costura)
                if (_Lines.w >= 0.0)
                {
                    float repeat = max(1.0, floor(_Lines.x + 0.5));
                    float2 uv = float2((turn + _Lines.z + along * _Lines.w) * repeat, along * _Lines.y);
                    // frac: se repite aunque la textura esté en Clamp (las Zbam del pack)
                    half mask = SAMPLE_TEXTURE2D_LOD(_LinesTex, sampler_LinesTex, frac(uv), 0).a;
                    color = Over(color, _SwirlColor, mask * top);
                }
                // Borde de la silueta y franja en la base, con la línea negra por fuera
                half rim = max(step(1.0 - _RimWidth, fresnel), step(input.uv.y, 0.07));
                color = Over(color, _RimColor, rim);
                half outline = max(step(1.0 - _OutlineWidth, fresnel), step(input.uv.y, 0.025));
                color = Over(color, _OutlineColor, outline);

                color.rgb = lerp(color.rgb, half3(1.0, 1.0, 1.0), _Flash);
                color.a = saturate(color.a + _Flash * 0.5) * _Alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
