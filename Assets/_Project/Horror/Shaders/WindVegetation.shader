// Vegetazione al vento (erba, chiome, cespugli) per URP.
// - Vento: onde lente che spostano i vertici in proporzione alla loro altezza.
// - Spinta: l'erba si piega lontano dai personaggi vicini (_GrassPushers, impostato da GrassPusher).
// L'altezza di piegatura viene dal colore dei vertici (alfa: 0 = base, 1 = punta) oppure
// dall'altezza nell'oggetto (_BendStart.._BendStart+_BendHeight) se _UseVertexColor = 0.
Shader "FantaLandia/WindVegetation"
{
    Properties
    {
        _BaseColor ("Colore", Color) = (0.25, 0.4, 0.18, 1)
        _UseVertexColor ("Usa il colore dei vertici (erba)", Float) = 1
        _WindStrength ("Forza del vento (m)", Float) = 0.08
        _WindSpeed ("Velocità del vento", Float) = 1.2
        _WindScale ("Grandezza delle onde", Float) = 0.35
        _BendStart ("Inizio piegatura (m, oggetto)", Float) = 0
        _BendHeight ("Altezza piegatura (m, oggetto)", Float) = 1
        _PushRadius ("Raggio della spinta (m)", Float) = 0.75
        _PushStrength ("Forza della spinta (m)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _UseVertexColor;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float _BendStart;
            float _BendHeight;
            float _PushRadius;
            float _PushStrength;
        CBUFFER_END

        // posizioni (xyz) dei personaggi che spingono l'erba; w > 0 = attivo
        float4 _GrassPushers[8];

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float BendFactor(Attributes v)
        {
            if (_UseVertexColor > 0.5) return v.color.a;
            return saturate((v.positionOS.y - _BendStart) / max(0.001, _BendHeight));
        }

        float3 ApplyWind(float3 posWS, float bend)
        {
            float t = _Time.y * _WindSpeed;
            float phase = posWS.x * _WindScale + posWS.z * _WindScale * 0.7;
            float wave = sin(t + phase) * 0.7 + sin(t * 1.9 + phase * 2.3) * 0.3;
            float gust = 0.6 + 0.4 * sin(t * 0.23 + posWS.x * 0.05);
            float2 dir = normalize(float2(1.0, 0.45));
            float2 offset = dir * wave * gust * _WindStrength * bend;

            // spinta dei personaggi vicini
            [unroll]
            for (int i = 0; i < 8; i++)
            {
                float4 p = _GrassPushers[i];
                if (p.w <= 0) continue;
                float2 d = posWS.xz - p.xz;
                float dist = length(d);
                float k = saturate(1.0 - dist / _PushRadius) * step(abs(posWS.y - p.y), 1.5);
                offset += (dist > 0.001 ? d / dist : float2(0, 0)) * k * _PushStrength * bend;
                posWS.y -= k * bend * 0.12;
            }
            posWS.xz += offset;
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), BendFactor(v));
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = _BaseColor.rgb * (_UseVertexColor > 0.5 ? i.color.rgb : half3(1, 1, 1));
                s.alpha = 1;
                s.occlusion = 1;
                s.smoothness = 0.1;
                s.specular = half3(0, 0, 0);

                half4 c = UniversalFragmentBlinnPhong(inputData, s);
                c.rgb = MixFog(c.rgb, inputData.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vertShadow(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = ApplyWind(TransformObjectToWorld(v.positionOS.xyz), BendFactor(v));
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }

            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            float4 vertDepth(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(ApplyWind(TransformObjectToWorld(v.positionOS.xyz), BendFactor(v)));
            }

            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
