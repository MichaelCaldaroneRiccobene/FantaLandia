// Superficie con texture "in coordinate del mondo" (proiezione dai tre assi): la texture non si stira sui blocchi
// scalati (muri, pavimenti, tetti, prato) e si ripete sempre alla stessa scala in metri.
// La texture moltiplica il colore base (valore medio 0.5 = colore invariato). Illuminazione URP completa
// (luna, lampade, torcia con cookie, ombre, SSAO); ombre, profondità e normali dai passaggi di URP/Lit.
Shader "FantaLandia/WorldTextured"
{
    Properties
    {
        _BaseColor ("Colore", Color) = (1, 1, 1, 1)
        _DetailMap ("Texture (0.5 = neutro)", 2D) = "grey" {}
        _TileSize ("Dimensione texture (m)", Float) = 1
        _Smoothness ("Lucidità", Range(0, 1)) = 0.1
        _BigNoise ("Variazione a grande scala", Range(0, 0.5)) = 0.12
        // usate dai passaggi presi da URP/Lit
        _BaseMap ("Base Map", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DetailMap_ST;
                float4 _BaseMap_ST;
                float _TileSize, _Smoothness, _BigNoise, _Cutoff;
            CBUFFER_END
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
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
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z);
                float3 p = i.positionWS / max(0.01, _TileSize);
                // facce verticali: righe orizzontali (assi, perline, mattoni) restano orizzontali
                half3 tx = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.zy).rgb;
                half3 ty = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.xz).rgb;
                half3 tz = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, p.xy).rgb;
                half3 detail = tx * w.x + ty * w.y + tz * w.z;
                // macchie grandi (sporco, sole, usura) per non vedere la ripetizione
                float big = Noise(i.positionWS.xz * 0.35 + i.positionWS.y * 0.2) * 0.6 + Noise(i.positionWS.xz * 1.3) * 0.4;
                half3 albedo = _BaseColor.rgb * detail * 2.0 * (1.0 + (big - 0.5) * 2.0 * _BigNoise);

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
                s.albedo = saturate(albedo);
                s.alpha = 1;
                s.occlusion = 1;
                s.metallic = 0;
                s.smoothness = _Smoothness * (0.7 + detail.g * 0.6);
                s.specular = half3(0, 0, 0);

                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, inputData.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        // ombre, profondità e normali (per SSAO) come un materiale URP/Lit opaco
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
