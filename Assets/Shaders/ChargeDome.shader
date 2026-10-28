// Cúpula de la carga (Onda de choque, K8, 2026-10-18; GDD 7.6): media esfera translúcida con estilo de cómic (cortes
// duros, sin degradados): relleno claro, borde de la silueta naranja con una línea negra por fuera, una franja en la base,
// una textura (líneas de velocidad o una Zbam) que se desliza alrededor y líneas en remolino dibujadas (blancas, con una
// negra fina entre cada dos). Las dos giran alrededor de un eje inclinado que se bambolea (_Axis, con _AxisT1 y _AxisT2
// perpendiculares a él). Los valores los pone BladeChargeDome con un
// MaterialPropertyBlock. Transparente, sin luz, solo las caras de delante. Compatible con OpenGL ES 3 / WebGL 2.
// Tornado (K8, 2026-10-18): la misma malla se puede dibujar como un embudo que se abre hacia arriba (_Funnel, de 0 a 1 pasa
// de cúpula a embudo) y con la parte de arriba cortada en picos (_TopCut): un aro de picos abierto, como en el boceto.
// Con interior (malla con las caras de dentro, uv2.x = 1, y _InsideAlpha): se ve por dentro a través del corte de arriba.
// Rebote (K8, 2026-10-21): la malla puede ser una esfera entera (burbuja) y en cada rebote se abre en ella una Zbam desde el
// punto del golpe (_HitTex, _Hit, _HitRot, _HitColor, _HitShadowColor), como el anillo de la peonza golpeada (BladeOverlay).
// Máscara e inestabilidad (Defensa, K8, 2026-10-24; pedido del usuario): huecos con corte duro y una línea en su borde, de
// una textura o de ruido, que se deslizan y se comen más arriba (el borde de arriba no parece un cilindro); la superficie se
// ondula (vértices) y franjas horizontales se desplazan de lado (glitch). El latido de los huecos y el parpadeo los mueve
// BladeChargeDome con _Mask.x y _Alpha.
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
        _LinesRise ("Líneas: cuánto han subido (repeticiones)", Float) = 0
        _Axis ("Eje", Vector) = (0, 1, 0, 0)
        _AxisT1 ("Eje: perpendicular 1", Vector) = (1, 0, 0, 0)
        _AxisT2 ("Eje: perpendicular 2", Vector) = (0, 0, 1, 0)
        _Funnel ("Embudo (0 = cúpula, 1 = embudo)", Range(0, 1)) = 0
        _FunnelShape ("Embudo: radio abajo (x), radio arriba (y), alto (z)", Vector) = (0.25, 1, 2.5, 0)
        _TopCut ("Corte de arriba en picos: alto (x, 0 = cerrada), picos (y), giro (z)", Vector) = (0, 10, 0, 0)
        _InsideAlpha ("Interior: transparencia (caras de dentro)", Range(0, 1)) = 1
        [NoScaleOffset] _HitTex ("Zbam del golpe", 2D) = "white" {}
        _Hit ("Golpe: dirección desde el centro (xyz) y radio (w; 0 = sin Zbam)", Vector) = (1, 0, 0, 0)
        _HitRot ("Golpe: giro (cos x, sen y) y tamaño de la sombra (z)", Vector) = (1, 0, 1.12, 0)
        _HitColor ("Golpe: color", Color) = (1, 1, 1, 0)
        _HitShadowColor ("Golpe: sombra", Color) = (0, 0, 0, 0)
        [NoScaleOffset] _MaskTex ("Máscara (gris o alfa)", 2D) = "white" {}
        _Mask ("Máscara: parte oculta (x; < 0 = sin máscara), repeticiones alrededor (y) y de alto (z), borde (w)", Vector) = (-1, 3, 1.5, 0.06)
        _MaskScroll ("Máscara: desliz alrededor (x) y hacia arriba (y), con textura (z: 1) o ruido (0)", Vector) = (0, 0, 0, 0)
        _MaskTop ("Máscara: cuánto más se oculta arriba (x) y desde qué altura (y)", Vector) = (0, 0.5, 0, 0)
        _MaskEdgeColor ("Máscara: línea del borde de los huecos", Color) = (1, 1, 1, 0.9)
        _Wobble ("Ondulación: fuerza (x), ondas alrededor (y) y de alto (z), fase (w)", Vector) = (0, 5, 2, 0)
        _Glitch ("Bandas: cuántas (x), desplazamiento (y), fuerza ahora (z), semilla (w)", Vector) = (8, 0.05, 0, 0)
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
            TEXTURE2D(_HitTex);
            SAMPLER(sampler_HitTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

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
                float _LinesRise;
                float4 _Axis;
                float4 _AxisT1;
                float4 _AxisT2;
                float _Funnel;
                float4 _FunnelShape;
                float4 _TopCut;
                float _InsideAlpha;
                float4 _Hit;
                float4 _HitRot;
                half4 _HitColor;
                half4 _HitShadowColor;
                float4 _Mask;
                float4 _MaskScroll;
                float4 _MaskTop;
                half4 _MaskEdgeColor;
                float4 _Wobble;
                float4 _Glitch;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // x = 1 en las caras de dentro (la malla con interior las lleva primero: se pintan antes que las de fuera)
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                // Punto de la media esfera (aunque se dibuje como embudo): el remolino y la textura se miden sobre él
                float3 domeOS : TEXCOORD3;
                float inside : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Embudo (Tornado): anillos a la misma altura de la malla, del radio de abajo al de arriba (abriéndose más
                // hacia arriba) y del suelo al alto del embudo. _Funnel pasa de la media esfera a él
                float v = input.uv.y;
                float theta = input.uv.x * 6.2831853;
                float3 around = float3(cos(theta), 0.0, sin(theta));
                float r = lerp(_FunnelShape.x, _FunnelShape.y, v * v);
                float3 funnelPos = around * r + float3(0.0, v * _FunnelShape.z, 0.0);
                float3 funnelNormal = normalize(around * _FunnelShape.z - float3(0.0, (_FunnelShape.y - _FunnelShape.x) * 2.0 * v, 0.0));
                float3 positionOS = lerp(input.positionOS.xyz, funnelPos, _Funnel);
                float3 normalOS = normalize(lerp(input.normalOS, funnelNormal, _Funnel));
                // Ondulación (2026-10-24): ondas que recorren la superficie hacia fuera y hacia dentro; la base, quieta en el suelo
                if (_Wobble.x > 0.0)
                {
                    float wave = sin(theta * _Wobble.y + _Wobble.w) * 0.6 + sin(v * _Wobble.z * 6.2831853 - _Wobble.w * 1.7 + theta * 2.0) * 0.4;
                    positionOS += normalOS * _Wobble.x * wave * smoothstep(0.0, 0.15, v);
                }
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = input.uv;
                output.domeOS = input.positionOS.xyz;
                output.inside = input.uv2.x;
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

            // Zbam del golpe (Rebote, 2026-10-21): la textura proyectada en el plano tangente a la cúpula en el punto del golpe
            // (_Hit.xyz, desde el centro), de radio scale (en partes del radio), girada; solo en la mitad que mira al golpe
            half HitMask(float3 p, float scale)
            {
                float3 d = _Hit.xyz;
                if (dot(p, d) <= 0.0) return 0.0;
                float3 t1 = normalize(cross(d, abs(d.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0)));
                float3 t2 = cross(d, t1);
                float2 q = float2(dot(p, t1), dot(p, t2));
                q = float2(q.x * _HitRot.x - q.y * _HitRot.y, q.x * _HitRot.y + q.y * _HitRot.x);
                float2 uv = q / (2.0 * scale) + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
                return SAMPLE_TEXTURE2D_LOD(_HitTex, sampler_HitTex, uv, 0).a;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Ruido de valor que se repite cada period casillas alrededor (sin costura)
            float ValueNoise(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float x0 = i.x - period * floor(i.x / period);
                float x1 = x0 + 1.0 - period * floor((x0 + 1.0) / period);
                float a = Hash21(float2(x0, i.y));
                float b = Hash21(float2(x1, i.y));
                float c = Hash21(float2(x0, i.y + 1.0));
                float d = Hash21(float2(x1, i.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Máscara (0-1) en un punto de la superficie (uv: vuelta y altura): la textura o dos capas de ruido
            float MaskValue(float2 uv)
            {
                float repeat = max(1.0, floor(_Mask.y + 0.5));
                float2 q = float2((uv.x + _MaskScroll.x) * repeat, uv.y * _Mask.z - _MaskScroll.y);
                if (_MaskScroll.z > 0.5)
                {
                    half4 m = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, frac(q), 0);
                    return min(m.r, m.a);
                }
                return ValueNoise(q, repeat) * 0.65 + ValueNoise(q * 2.0 + 17.0, repeat * 2.0) * 0.35;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Bandas desplazadas (2026-10-24): durante un glitch, algunas franjas horizontales se corren de lado
                float shift = 0.0;
                if (_Glitch.z > 0.0)
                {
                    float band = floor(input.uv.y * max(1.0, _Glitch.x));
                    if (Hash21(float2(band, _Glitch.w)) < 0.5)
                        shift = (Hash21(float2(band + 7.0, _Glitch.w)) - 0.5) * 2.0 * _Glitch.y * _Glitch.z;
                }
                float2 uvShifted = float2(input.uv.x + shift, input.uv.y);

                // Corte de arriba en picos (Tornado: aro de picos abierto arriba). Cada pico baja una altura distinta
                if (_TopCut.x > 0.0)
                {
                    float spikes = max(1.0, floor(_TopCut.y + 0.5));
                    float k = uvShifted.x * spikes + _TopCut.z;
                    float tooth = 1.0 - abs(frac(k) - 0.5) * 2.0;
                    float jitter = frac(sin(floor(k) * 12.9898) * 43758.5453);
                    if (input.uv.y > 1.0 - _TopCut.x * (1.0 - tooth * (0.55 + jitter * 0.45))) discard;
                }
                // Huecos de la máscara, con corte duro; arriba se oculta más (el borde de arriba se deshace)
                half maskEdge = 0.0;
                if (_Mask.x >= 0.0)
                {
                    float threshold = _Mask.x + _MaskTop.x * saturate((input.uv.y - _MaskTop.y) / max(0.001, 1.0 - _MaskTop.y));
                    float m = MaskValue(uvShifted);
                    if (m < threshold) discard;
                    maskEdge = step(m, threshold + _Mask.w) * step(0.001, threshold);
                }
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = 1.0 - saturate(abs(dot(normal, view)));

                // Vuelta alrededor del eje inclinado (0-1) y altura a lo largo de él (0-1)
                float3 p = normalize(input.domeOS);
                float along = dot(p, _Axis.xyz) * 0.5 + 0.5;
                float turn = atan2(dot(p, _AxisT2.xyz), dot(p, _AxisT1.xyz)) * (0.5 / PI) + 0.5 + shift;
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

                // Textura que se desliza alrededor (repeticiones enteras alrededor: sin costura) y sube (Pinchos, 2026-10-28)
                if (_Lines.w >= 0.0)
                {
                    float repeat = max(1.0, floor(_Lines.x + 0.5));
                    float2 uv = float2((turn + _Lines.z + along * _Lines.w) * repeat, along * _Lines.y - _LinesRise);
                    // frac: se repite aunque la textura esté en Clamp (las Zbam del pack)
                    half mask = SAMPLE_TEXTURE2D_LOD(_LinesTex, sampler_LinesTex, frac(uv), 0).a;
                    color = Over(color, _SwirlColor, mask * top);
                }
                // Borde de la silueta y franja en la base, con la línea negra por fuera
                half rim = max(step(1.0 - _RimWidth, fresnel), step(input.uv.y, 0.07));
                color = Over(color, _RimColor, rim);
                half outline = max(step(1.0 - _OutlineWidth, fresnel), step(input.uv.y, 0.025));
                color = Over(color, _OutlineColor, outline);
                color = Over(color, _MaskEdgeColor, maskEdge);

                // Zbam del golpe, con su sombra más grande por debajo
                if (_Hit.w > 0.0)
                {
                    color = Over(color, _HitShadowColor, HitMask(p, _Hit.w * _HitRot.z));
                    color = Over(color, _HitColor, HitMask(p, _Hit.w));
                }

                color.rgb = lerp(color.rgb, half3(1.0, 1.0, 1.0), _Flash);
                color.a = saturate(color.a + _Flash * 0.5) * _Alpha * lerp(1.0, _InsideAlpha, input.inside);
                return color;
            }
            ENDHLSL
        }
    }
}
