// Cielo notturno procedurale per URP: sfumatura verso l'orizzonte (colore della nebbia), stelle che brillano,
// Via Lattea (fascia di polvere e stelle fitte) e luna con crateri e alone. Va su una sfera grande attorno alla camera.
Shader "FantaLandia/NightSky"
{
    Properties
    {
        _ZenithColor ("Colore allo zenit", Color) = (0.01, 0.015, 0.04, 1)
        _HorizonColor ("Colore all'orizzonte", Color) = (0.03, 0.04, 0.07, 1)
        _MoonDir ("Direzione della luna", Vector) = (0.3, 0.45, 0.85, 0)
        _MoonColor ("Colore della luna", Color) = (0.95, 0.95, 0.85, 1)
        _MoonSize ("Dimensione della luna", Float) = 0.035
        _StarDensity ("Densità stelle", Float) = 0.03
        _StarScale ("Scala griglia stelle", Float) = 220
        _BandNormal ("Normale Via Lattea", Vector) = (0.35, 0.55, -0.75, 0)
        _BandColor ("Colore Via Lattea", Color) = (0.18, 0.17, 0.22, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Front
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor, _HorizonColor, _MoonDir, _MoonColor, _BandNormal, _BandColor;
                float _MoonSize, _StarDensity, _StarScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = TransformObjectToWorldDir(v.positionOS.xyz);
                return o;
            }

            float Hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float3 Hash3(float3 p) { return float3(Hash(p), Hash(p + 31.7), Hash(p + 71.3)); }

            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { v += a * Noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            float Stars(float3 d, float scale, float density)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float h = Hash(cell);
                if (h < 1.0 - density) return 0.0;
                float3 c = (Hash3(cell) - 0.5) * 0.6;
                float dist = length(frac(p) - 0.5 - c);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 3.0) + h * 40.0);
                return smoothstep(0.12, 0.0, dist) * twinkle * (0.4 + 0.6 * frac(h * 13.0));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.45));
                float visible = smoothstep(-0.02, 0.18, d.y); // vicino all'orizzonte la nebbia copre tutto

                // Via Lattea: fascia lungo un cerchio massimo, con polvere e più stelle
                float3 n = normalize(_BandNormal.xyz);
                float band = exp(-pow(dot(d, n) / 0.2, 2.0));
                float dust = Fbm(d * 6.0) * Fbm(d * 13.0 + 5.0);
                col += _BandColor.rgb * band * (0.35 + dust * 1.6) * visible;

                // stelle: sparse ovunque, fitte nella Via Lattea
                float s = Stars(d, _StarScale, _StarDensity) + Stars(d, _StarScale * 1.9, _StarDensity * 2.5 * band) * 0.6;
                col += s * visible * float3(0.9, 0.92, 1.0);

                // luna: disco con crateri e alone
                float3 m = normalize(_MoonDir.xyz);
                float md = dot(d, m);
                float edge = cos(_MoonSize);
                float disc = smoothstep(edge - 0.00015, edge + 0.00015, md);
                float craters = 0.75 + 0.25 * Fbm(d * 160.0);
                col = lerp(col, _MoonColor.rgb * craters, disc);
                col += _MoonColor.rgb * (pow(saturate(md), 600.0) * 0.5 + pow(saturate(md), 40.0) * 0.06) * (1.0 - disc);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
