// CRT-эффект на весь экран: лёгкий рыбий глаз, сканлайны, виньетка, сдвиг цветов и мерцание.
// Настройки меняются в материале Assets/Settings/CRT.mat
Shader "SpaceInvaders/CRT"
{
    Properties
    {
        _Fisheye("Рыбий глаз", Range(0, 0.4)) = 0.04
        _Scanlines("Сканлайны", Range(0, 1)) = 0.25
        _ScanlineCount("Число сканлайнов", Float) = 232
        _Vignette("Виньетка", Range(0, 1)) = 0.6
        _ColorShift("Сдвиг цветов", Range(0, 1)) = 0.25
        _Flicker("Мерцание", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "CRT"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Fisheye;
            float _Scanlines;
            float _ScanlineCount;
            float _Vignette;
            float _ColorShift;
            float _Flicker;

            float2 Bend(float2 uv)
            {
                float2 c = uv - 0.5;
                float r = dot(c, c);
                return 0.5 + c * (1.0 + _Fisheye * r * 2.0) / (1.0 + _Fisheye * 0.5);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = Bend(input.texcoord);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                float2 shift = (uv - 0.5) * _ColorShift * 0.012;
                float3 color;
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + shift).r;
                color.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - shift).b;

                float line01 = 0.5 + 0.5 * sin(uv.y * _ScanlineCount * 6.2831853);
                color *= 1.0 - _Scanlines * 0.55 * (1.0 - line01);

                float2 q = uv - 0.5;
                color *= 1.0 - _Vignette * smoothstep(0.18, 0.62, length(q * float2(1.0, 1.05)));

                color *= 1.0 - _Flicker * 0.12 * (0.5 + 0.5 * sin(_Time.y * 37.0))
                             - _Flicker * 0.06 * (0.5 + 0.5 * sin(_Time.y * 11.0 + uv.y * 4.0));

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
