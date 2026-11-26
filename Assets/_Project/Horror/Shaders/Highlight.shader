// Contorno luminoso che pulsa (oggetti da trovare, es. la torcia): additivo, più forte sui bordi (fresnel).
// Va su una copia leggermente ingrandita della mesh dell'oggetto.
Shader "FantaLandia/Highlight"
{
    Properties
    {
        _Color ("Colore", Color) = (1, 0.85, 0.45, 1)
        _Intensity ("Intensità", Float) = 1.2
        _Speed ("Velocità pulsazione", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Highlight"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float rim = 1.0 - saturate(abs(dot(normalize(i.normalWS), view)));
                float pulse = 0.55 + 0.45 * sin(_Time.y * _Speed);
                float a = (0.25 + rim * rim * 1.5) * pulse * _Intensity;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
