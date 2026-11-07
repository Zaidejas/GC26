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

            // Глобальная добавка на заставках (CrtBoost.cs), в остальное время 0
            float _CrtExtraFisheye;
            float _CrtExtraColorShift;
            // Размытие после моргания на Боссе 2 (BlinkEffect.cs): радиус в долях высоты экрана, 0 - выключено
            float _CrtBlur;

            float2 Bend(float2 uv)
            {
                float2 c = uv - 0.5;
                float r = dot(c, c);
                float fisheye = _Fisheye + _CrtExtraFisheye;
                return 0.5 + c * (1.0 + fisheye * r * 2.0) / (1.0 + fisheye * 0.5);
            }

            // Готовая CRT-картинка в точке экрана: изгиб, сдвиг цветов, сканлайны, виньетка, мерцание
            float3 Crt(float2 screenUv)
            {
                float2 uv = Bend(screenUv);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return float3(0.0, 0.0, 0.0);

                float2 shift = (uv - 0.5) * (_ColorShift + _CrtExtraColorShift) * 0.012;
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
                return color;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 color = Crt(input.texcoord);

                // Размытие поверх CRT: среднее готовой CRT-картинки по двум кольцам вокруг точки,
                // поэтому размываются и сканлайны. Веки моргания рисуются позже и остаются чёткими.
                // Считается только пока размытие включено
                if (_CrtBlur > 0.0001)
                {
                    float2 radius = float2(_CrtBlur * _ScreenParams.y / _ScreenParams.x, _CrtBlur);
                    float3 sum = color;
                    [unroll] for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.0471976; // 60 градусов
                        sum += Crt(input.texcoord + float2(cos(a), sin(a)) * radius * 0.5);
                        sum += Crt(input.texcoord + float2(cos(a + 0.5235988), sin(a + 0.5235988)) * radius);
                    }
                    color = sum / 13.0;
                }

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
