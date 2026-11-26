// Particelle di fumo e bagliori per URP: sprite tondi e sfrangiati calcolati dalle UV (niente texture),
// colore dalla particella. Senza nebbia, così la colonna di fumo lontana si vede sopra i tetti.
// _Additive = 1 per i bagliori del fuoco (si sommano), 0 per il fumo (si mescola).
Shader "FantaLandia/Smoke"
{
    Properties
    {
        _Softness ("Morbidezza bordo", Range(0.05, 1)) = 0.6
        _Breakup ("Sfrangiatura", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Smoke"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Softness, _Breakup, _SrcBlend, _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv.xy - 0.5;
                // bordo sfrangiato (le particelle hanno rotazioni diverse, quindi ognuna sembra diversa)
                float n = Noise(c * 5.0 + _Time.y * 0.15) * 0.6 + Noise(c * 11.0 + 3.7) * 0.4;
                float r = length(c) * 2.0 + (n - 0.5) * _Breakup;
                float a = saturate((1.0 - r) / _Softness);
                a *= a;
                return half4(i.color.rgb * (_SrcBlend < 1.5 ? a * i.color.a : 1.0), a * i.color.a);
            }
            ENDHLSL
        }
    }
}
