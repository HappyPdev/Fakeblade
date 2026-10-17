// Pixelado de la escena (O2, GDD 7.6). Pase de pantalla completa (Full Screen Pass Renderer Feature, "FB Pixelate")
// que se pinta después de la arena y las peonzas y antes de las partículas (capa VFX, "FB VFX") y del postprocesado.
// Cada bloque de _FB_PixelSize × _FB_PixelSize píxeles toma el color de su centro. _FB_PixelSize lo pone
// ScreenPixelation antes de pintar cada cámara (1 o menos = sin pixelar). Compatible con OpenGL ES 3 / WebGL 2.
Shader "FakeBlade/Pixelate"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Pixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FB_PixelSize;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_FB_PixelSize > 1.0)
                {
                    float2 block = _FB_PixelSize / _ScreenParams.xy;
                    uv = (floor(uv / block) + 0.5) * block;
                }
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
            }
            ENDHLSL
        }
    }
}
