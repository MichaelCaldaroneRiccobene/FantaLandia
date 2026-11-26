// Nuvole voxel nel cielo notturno: colore pieno, illuminate dall'alto dalla luna (facce in alto più chiare),
// sfumano nel colore dell'orizzonte con la distanza (non usano la nebbia della scena, che le coprirebbe).
Shader "FantaLandia/SkyCloud"
{
    Properties
    {
        _BaseColor ("Colore", Color) = (0.12, 0.13, 0.17, 1)
        _TopColor ("Colore lato illuminato", Color) = (0.28, 0.3, 0.38, 1)
        _HorizonColor ("Colore all'orizzonte", Color) = (0.03, 0.04, 0.07, 1)
        _FadeDistance ("Distanza di dissolvenza", Float) = 260
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Cloud"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _TopColor, _HorizonColor;
                float _FadeDistance;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float lit = saturate(normalize(i.normalWS).y * 0.5 + 0.5);
                float3 col = lerp(_BaseColor.rgb, _TopColor.rgb, lit * lit);
                float dist = length(i.positionWS - GetCameraPositionWS());
                col = lerp(col, _HorizonColor.rgb, saturate(dist / _FadeDistance));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
