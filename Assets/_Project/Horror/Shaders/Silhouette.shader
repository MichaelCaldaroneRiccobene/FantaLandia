// La sagoma lontana della creatura gigante (capitolo 2): colore pieno, senza luci e senza nebbia,
// così a 150 metri, nella nebbia fitta, resta un'ombra nera contro il cielo. In basso un riverbero arancione
// (gli incendi della città la illuminano da sotto), in alto si confonde con il buio.
Shader "FantaLandia/Silhouette"
{
    Properties
    {
        _Color ("Colore", Color) = (0.03, 0.025, 0.03, 1)
        _GlowColor ("Riverbero da sotto", Color) = (0.35, 0.12, 0.04, 1)
        _GlowBottom ("Altezza del riverbero (inizio)", Float) = 0
        _GlowTop ("Altezza del riverbero (fine)", Float) = 25
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _GlowColor;
                float _GlowBottom, _GlowTop;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float worldY : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.worldY = w.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float k = 1.0 - saturate((i.worldY - _GlowBottom) / max(0.01, _GlowTop - _GlowBottom));
                return half4(lerp(_Color.rgb, _GlowColor.rgb, k * k), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
