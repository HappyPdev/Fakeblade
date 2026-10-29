// Escudos del Escudo (K8, 2026-10-29; elegido por el usuario: panal de hexágonos, GDD 7.6): cada escudo es una franja curva de
// hexágonos dorados translúcidos con el borde blanco y un contorno fino, que brillan a ratos uno a uno. Al aparecer, cada
// hexágono crece desde su centro con un desfase al azar; al romperse, se hace añicos: los hexágonos salen despedidos (hacia
// fuera del escudo, hacia fuera del choque y hacia arriba, con gravedad), giran sobre sí mismos, encogen y se apagan. Todo en
// el shader, sin trabajo de CPU por hexágono. La malla (BladeShieldHex) lleva en uv.x la distancia al centro del hexágono
// (0 en el centro, 1 en el borde), en uv2.x una semilla por hexágono y en uv3 su centro. Los valores los pone BladeShieldHex
// con un MaterialPropertyBlock por escudo. Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/ShieldHex"
{
    Properties
    {
        _FillColor ("Relleno", Color) = (1, 0.78, 0.2, 0.35)
        _EdgeColor ("Borde", Color) = (1, 0.97, 0.85, 1)
        _OutlineColor ("Contorno", Color) = (0.25, 0.15, 0, 0.9)
        _EdgeFrom ("Borde: desde qué distancia al centro (0-1)", Range(0, 1)) = 0.72
        _OutlineFrom ("Contorno: desde qué distancia al centro (0-1)", Range(0, 1)) = 0.92
        _Alpha ("Transparencia", Range(0, 1)) = 1
        _Flash ("Destello", Range(0, 1)) = 0
        _FxTime ("Tiempo (lo pone el script)", Float) = 0
        _Twinkle ("Brillo de los hexágonos", Range(0, 1)) = 0.6
        _TwinkleSpeed ("Brillos por segundo de cada hexágono", Float) = 0.8
        _Appear ("Aparición (0-1)", Range(0, 1)) = 1
        // x: segundos desde que se rompió; y: lo que dura; z: 1 = rompiéndose
        _Shatter ("Añicos", Vector) = (0, 0.6, 0, 0)
        // xyz: hacia fuera del choque (en el espacio del escudo); w: su velocidad
        _ShatterDir ("Añicos: dirección", Vector) = (1, 0, 0, 4)
        // x: velocidad hacia fuera del escudo; y: hacia arriba; z: gravedad; w: giro (rad/s)
        _ShatterParams ("Añicos: velocidades", Vector) = (2.5, 2, 9, 12)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ShieldHex"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                half4 _EdgeColor;
                half4 _OutlineColor;
                float _EdgeFrom;
                float _OutlineFrom;
                float _Alpha;
                float _Flash;
                float _FxTime;
                float _Twinkle;
                float _TwinkleSpeed;
                float _Appear;
                float4 _Shatter;
                float4 _ShatterDir;
                float4 _ShatterParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                // x: 0 en el centro del hexágono, 1 en el borde
                float2 uv : TEXCOORD0;
                // x: semilla del hexágono (0-1)
                float2 uv2 : TEXCOORD1;
                // centro del hexágono
                float3 uv3 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float radial : TEXCOORD0;
                float seed : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float seed = input.uv2.x;
                float3 center = input.uv3;
                float3 offset = input.positionOS.xyz - center;

                // Aparición: cada hexágono crece desde su centro, con un desfase según su semilla
                offset *= saturate((_Appear - seed * 0.5) * 2.0);

                if (_Shatter.z > 0.5)
                {
                    float t = _Shatter.x;
                    float k = saturate(t / max(0.01, _Shatter.y));
                    float3 n = normalize(input.normalOS);
                    // Giro sobre sí mismo (alrededor de su normal), a un lado o al otro según la semilla
                    float angle = _ShatterParams.w * (seed - 0.5) * 2.0 * t;
                    float c = cos(angle);
                    float s = sin(angle);
                    offset = offset * c + cross(n, offset) * s + n * dot(n, offset) * (1.0 - c);
                    offset *= 1.0 - k * k;
                    float seed2 = frac(seed * 7.31);
                    float3 velocity = n * (_ShatterParams.x * (0.5 + seed)) +
                                      _ShatterDir.xyz * (_ShatterDir.w * (0.6 + seed2 * 0.8)) +
                                      float3(0.0, _ShatterParams.y * (0.5 + seed2), 0.0);
                    center += velocity * t + float3(0.0, -0.5 * _ShatterParams.z * t * t, 0.0);
                }

                output.positionCS = TransformObjectToHClip(center + offset);
                output.radial = input.uv.x;
                output.seed = seed;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Brillo: cada hexágono destella un momento una vez por ciclo, cada uno a su hora
                float cycle = frac(_FxTime * _TwinkleSpeed + input.seed);
                float glow = _Twinkle * saturate(1.0 - cycle * 6.0);
                half4 color = _FillColor;
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, glow * 0.7);
                color.a = lerp(color.a, 1.0, glow * 0.5);
                // Borde y contorno con corte duro (cómic)
                color = lerp(color, _EdgeColor, step(_EdgeFrom, input.radial));
                color = lerp(color, _OutlineColor, step(_OutlineFrom, input.radial));

                float flash = _Flash;
                float fade = 1.0;
                if (_Shatter.z > 0.5)
                {
                    float k = saturate(_Shatter.x / max(0.01, _Shatter.y));
                    flash = max(flash, saturate(1.0 - k * 5.0));
                    fade = 1.0 - k * k * k;
                }
                color.rgb = lerp(color.rgb, half3(1.0, 1.0, 1.0), flash);
                color.a *= _Alpha * fade;
                return color;
            }
            ENDHLSL
        }
    }
}
