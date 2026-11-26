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
        _MoonSize ("Dimensione della luna", Float) = 0.024
        _StarDensity ("Densità stelle", Float) = 0.03
        _StarScale ("Scala griglia stelle", Float) = 220
        _BandNormal ("Normale Via Lattea", Vector) = (0.35, 0.55, -0.75, 0)
        _BandColor ("Colore Via Lattea", Color) = (0.18, 0.17, 0.22, 1)
        _CloudCoverage ("Copertura nuvole", Range(0, 1)) = 0.5
        _CloudBottom ("Base nuvole (m)", Float) = 900
        _CloudTop ("Cima nuvole (m)", Float) = 2100
        _CloudScale ("Scala nuvole", Float) = 0.0006
        _CloudDensity ("Densità nuvole", Float) = 0.012
        _CloudWind ("Vento nuvole (xz m/s)", Vector) = (18, 0, 7, 0)
        _CloudLight ("Luce lunare sulle nuvole", Color) = (0.55, 0.62, 0.8, 1)
        _CloudAmbient ("Ambiente nuvole", Color) = (0.035, 0.045, 0.075, 1)
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
                float4 _CloudWind, _CloudLight, _CloudAmbient;
                float _MoonSize, _StarDensity, _StarScale;
                float _CloudCoverage, _CloudBottom, _CloudTop, _CloudScale, _CloudDensity;
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

            float Fbm3(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 3; k++) { v += a * Noise(p); p *= 2.07; a *= 0.5; }
                return v;
            }

            // densità delle nuvole in un punto (metri): forma grande a fbm, bordi erosi dal dettaglio, profilo in altezza a cumulo
            float CloudDensity(float3 p, bool detail)
            {
                float h = saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom));
                float profile = smoothstep(0.0, 0.15, h) * smoothstep(1.0, 0.45, h);
                float3 q = (p + _CloudWind.xyz * _Time.y) * _CloudScale;
                float shape = detail ? Fbm(q) : Fbm3(q);
                float d = shape - (1.0 - _CloudCoverage) * 0.75 - (1.0 - profile) * 0.35;
                if (detail && d > -0.1) d -= Fbm3(q * 5.3 + _Time.y * 0.02) * 0.12;
                return saturate(d * 3.0) * profile;
            }

            float HG(float c, float g) { float g2 = g * g; return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * c, 1.5)); }

            // nuvole volumetriche: raymarching nello strato tra base e cima, illuminate dalla luna (con orlo argentato)
            float4 Clouds(float3 d, float3 moon, float2 pix)
            {
                if (d.y < 0.015) return float4(0, 0, 0, 1);
                float t0 = _CloudBottom / d.y, t1 = min(_CloudTop / d.y, t0 + 9000.0);
                const int STEPS = 36;
                float dt = (t1 - t0) / STEPS;
                float t = t0 + dt * Hash(float3(pix, frac(_Time.y)));
                float3 light = 0;
                float trans = 1.0;
                float cosA = dot(d, moon);
                float phase = lerp(HG(cosA, 0.65), HG(cosA, -0.2), 0.35) * 4.0 * PI;
                float moonUp = saturate(moon.y * 4.0);
                [loop] for (int k = 0; k < STEPS; k++)
                {
                    float3 p = d * t;
                    float den = CloudDensity(p, true);
                    if (den > 0.001)
                    {
                        // ombra verso la luna (3 passi)
                        float ld = 0;
                        [unroll] for (int j = 1; j <= 3; j++) ld += CloudDensity(p + moon * (90.0 * j * j), false);
                        float lightT = exp(-ld * _CloudDensity * 120.0);
                        float powder = 1.0 - exp(-den * 4.0);
                        float3 s = _CloudLight.rgb * lightT * phase * powder * moonUp + _CloudAmbient.rgb * (0.6 + 0.4 * saturate((p.y - _CloudBottom) / (_CloudTop - _CloudBottom)));
                        float a = exp(-den * _CloudDensity * dt);
                        light += trans * s * (1.0 - a);
                        trans *= a;
                        if (trans < 0.02) break;
                    }
                    t += dt;
                }
                // in lontananza le nuvole si perdono nella foschia dell'orizzonte
                float fade = smoothstep(0.015, 0.2, d.y);
                light = lerp(_HorizonColor.rgb * (1.0 - trans), light, fade);
                return float4(light, lerp(1.0, trans, fade));
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

                float4 cl = Clouds(d, m, i.positionCS.xy);
                col = col * cl.a + cl.rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
