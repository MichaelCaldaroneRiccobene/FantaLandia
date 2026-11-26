// Cono di luce "volumetrico" finto (lampioni, lampade della veranda): un cono aperto additivo,
// più intenso vicino alla lampada, morbido sui bordi (fresnel) e dove tocca il terreno o gli oggetti (profondità),
// che sparisce quando la camera ci entra dentro o è lontana.
Shader "FantaLandia/LightCone"
{
    Properties
    {
        _Color ("Colore", Color) = (1, 0.8, 0.5, 1)
        _Intensity ("Intensità", Float) = 0.12
        _Softness ("Morbidezza contatto (m)", Float) = 0.6
        _EdgePower ("Bordo sfumato", Float) = 1.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LightCone"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity, _Softness, _EdgePower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float along : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.along = v.uv.y;
                o.eyeDepth = -TransformWorldToView(o.positionWS).z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = saturate(abs(dot(normalize(i.normalWS), view)));
                float edge = pow(facing, _EdgePower);                  // il centro del cono è più denso dei bordi
                float fall = pow(saturate(1.0 - i.along), 1.6);        // più forte vicino alla lampada

                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float soft = saturate((scene - i.eyeDepth) / _Softness); // niente tagli netti contro il terreno
                float nearFade = saturate((i.eyeDepth - 0.4) / 1.5);   // quando la camera ci passa dentro
                float farFade = exp(-i.eyeDepth * 0.025);

                float a = _Intensity * edge * fall * soft * nearFade * farFade;
                return half4(_Color.rgb * a, 1.0);
            }
            ENDHLSL
        }
    }
}
