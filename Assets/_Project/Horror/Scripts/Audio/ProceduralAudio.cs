using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.Audio
{
    public enum Surface { Wood, Grass, Concrete, Carpet }

    /// <summary>
    /// Suoni generati dal codice (niente file audio): passi, porte, colpi, grilli, TV...
    /// Ogni clip viene sintetizzata una volta e poi tenuta in cache.
    /// Sono segnaposto di buona qualità: in futuro si possono sostituire con registrazioni.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        static AudioClip Build(string name, float seconds, Action<float[], System.Random> fill, int seed = 1)
        {
            if (cache.TryGetValue(name, out AudioClip c) && c != null) return c;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            fill(data, new System.Random(seed));
            Normalize(data, 0.9f);
            c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            cache[name] = c;
            return c;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            foreach (float v in d) m = Mathf.Max(m, Mathf.Abs(v));
            if (m < 1e-5f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Filtro passa-basso a un polo, applicato sul posto.</summary>
        static void LowPass(float[] d, float cutoffHz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / Rate);
            float y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        // ------------------------------------------------------------------ passi e oggetti

        public static AudioClip Footstep(Surface surface, int variant)
        {
            return Build($"passo_{surface}_{variant}", 0.18f, (d, r) =>
            {
                float cutoff = surface == Surface.Grass ? 2500f : surface == Surface.Wood ? 900f : surface == Surface.Carpet ? 380f : 1800f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * (surface == Surface.Grass ? 28f : surface == Surface.Carpet ? 60f : 45f)) * (surface == Surface.Carpet ? 0.5f : 1f);
                    d[i] = Noise(r) * env;
                    if (surface == Surface.Grass) d[i] *= 0.6f + 0.4f * Mathf.Sin(t * 900f + variant); // fruscio
                }
                LowPass(d, cutoff);
                if (surface == Surface.Wood)
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] += Mathf.Sin(2f * Mathf.PI * (85f + variant * 7f) * t) * Mathf.Exp(-t * 35f) * 0.8f; // rimbombo del legno
                    }
            }, 100 + variant * 13 + (int)surface * 7);
        }

        public static AudioClip Knock()
        {
            return Build("bussare", 0.75f, (d, r) =>
            {
                float[] times = { 0f, 0.2f, 0.42f };
                foreach (float t0 in times)
                {
                    int s0 = (int)(t0 * Rate);
                    for (int i = s0; i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        if (t > 0.15f) break;
                        d[i] += Mathf.Sin(2f * Mathf.PI * 105f * t) * Mathf.Exp(-t * 28f)
                              + Noise(r) * Mathf.Exp(-t * 90f) * 0.5f;
                    }
                }
                LowPass(d, 1400f);
            }, 7);
        }

        /// <summary>Un singolo colpo di nocche su una porta di legno (per bussare a mano, colpo per colpo).</summary>
        public static AudioClip KnockOnce(int variant)
        {
            return Build($"colpo_{variant}", 0.18f, (d, r) =>
            {
                float f = 100f + variant * 9f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 30f) + Noise(r) * Mathf.Exp(-t * 95f) * 0.55f;
                }
                LowPass(d, 1500f);
            }, 8 + variant);
        }

        public static AudioClip DoorCreak(int variant)
        {
            return Build($"cigolio_{variant}", 0.9f, (d, r) =>
            {
                float phase = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float f = 330f + variant * 40f + 110f * Mathf.Sin(2f * Mathf.PI * 2.7f * t) + 50f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                    phase += f / Rate;
                    float saw = (phase % 1f) * 2f - 1f;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f));
                    float grain = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 31f * t);
                    d[i] = (saw * 0.6f + Noise(r) * 0.15f) * env * grain;
                }
                LowPass(d, 2200f);
            }, 21 + variant);
        }

        public static AudioClip DoorShut()
        {
            return Build("porta_chiusa", 0.35f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 14f) + Noise(r) * Mathf.Exp(-t * 60f) * 0.6f;
                }
                LowPass(d, 1100f);
            }, 33);
        }

        public static AudioClip DoorLocked()
        {
            return Build("porta_bloccata", 0.4f, (d, r) =>
            {
                for (int k = 0; k < 3; k++)
                {
                    int s0 = (int)(k * 0.11f * Rate);
                    for (int i = s0; i < d.Length && i < s0 + Rate / 12; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += (Noise(r) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.3f) * Mathf.Exp(-t * 60f);
                    }
                }
                LowPass(d, 3000f);
            }, 34);
        }

        public static AudioClip Click()
        {
            return Build("click", 0.05f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = (Noise(r) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2400f * t)) * Mathf.Exp(-t * 180f);
                }
            }, 41);
        }

        public static AudioClip PickUp()
        {
            return Build("raccogli", 0.25f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Noise(r) * Mathf.Exp(-t * 18f) * (0.5f + 0.5f * Mathf.Sin(t * 400f));
                }
                LowPass(d, 2500f);
            }, 42);
        }

        /// <summary>Lattine di birra posate su un bancone: tintinnio metallico.</summary>
        public static AudioClip CansClink()
        {
            return Build("lattine", 0.5f, (d, r) =>
            {
                float[] freqs = { 2350f, 3120f, 2780f, 4010f };
                for (int k = 0; k < 4; k++)
                {
                    int s0 = (int)((k * 0.035f + (float)r.NextDouble() * 0.02f) * Rate);
                    for (int i = s0; i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * Mathf.Exp(-t * 22f) * 0.4f;
                    }
                }
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] += Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 30f) * 0.8f; // appoggio sul legno
                }
            }, 43);
        }

        // ------------------------------------------------------------------ ambiente

        /// <summary>Notte di periferia: grilli e un filo di vento. In loop.</summary>
        public static AudioClip NightAmbience()
        {
            return Build("notte", 8f, (d, r) =>
            {
                // vento: rumore molto filtrato
                var wind = new float[d.Length];
                for (int i = 0; i < wind.Length; i++) wind[i] = Noise(r);
                LowPass(wind, 180f);
                LowPass(wind, 180f);
                for (int i = 0; i < d.Length; i++) d[i] = wind[i] * 3f;

                // grilli: gruppi di 3-4 impulsi intorno ai 4.3 kHz
                float t0 = 0.1f;
                while (t0 < 7.6f)
                {
                    float f = 4100f + (float)r.NextDouble() * 500f;
                    int pulses = 3 + r.Next(2);
                    float vol = 0.08f + (float)r.NextDouble() * 0.1f;
                    for (int p = 0; p < pulses; p++)
                    {
                        int s0 = (int)((t0 + p * 0.045f) * Rate);
                        for (int i = s0; i < s0 + Rate / 60 && i < d.Length; i++)
                        {
                            float t = (i - s0) / (float)Rate;
                            d[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Sin(Mathf.PI * t * 60f) * vol;
                        }
                    }
                    t0 += 0.35f + (float)r.NextDouble() * 0.6f;
                }
            }, 51);
        }

        public static AudioClip TvStatic()
        {
            return Build("tv_statica", 2f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++) d[i] = Noise(r) * 0.6f;
                LowPass(d, 6000f);
            }, 61);
        }

        /// <summary>Musichetta da cartone animato: onda quadra allegra con basso. In loop.</summary>
        public static AudioClip CartoonMusic()
        {
            return Build("tv_cartoni", 6.4f, (d, r) =>
            {
                // do maggiore, 150 bpm, crome
                int[] melody = { 72, 76, 79, 76, 77, 74, 71, 74, 72, 76, 79, 84, 83, 79, 77, 74,
                                 72, 76, 79, 76, 77, 81, 79, 77, 76, 74, 72, 74, 76, 72, 67, 0 };
                int[] bass = { 48, 48, 55, 55, 53, 53, 55, 55 };
                float eighth = 60f / 150f / 2f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    int step = (int)(t / eighth) % melody.Length;
                    float local = t - Mathf.Floor(t / eighth) * eighth;
                    float v = 0f;
                    if (melody[step] > 0)
                    {
                        float f = Midi(melody[step]);
                        v += ((t * f) % 1f < 0.5f ? 1f : -1f) * 0.25f * Mathf.Exp(-local * 6f);
                    }
                    int bstep = (int)(t / (eighth * 4f)) % bass.Length;
                    float bf = Midi(bass[bstep]);
                    float tri = 2f * Mathf.Abs(2f * ((t * bf) % 1f) - 1f) - 1f;
                    v += tri * 0.3f;
                    d[i] = v;
                }
            }, 62);
        }


        // ------------------------------------------------------------------ storia: esplosione, sirene, paura

        /// <summary>Esplosione lontana: colpo sordo e un lungo brontolio che si spegne (con qualche eco).</summary>
        public static AudioClip Explosion()
        {
            return Build("esplosione", 5f, (d, r) =>
            {
                var rumble = new float[d.Length];
                for (int i = 0; i < d.Length; i++) rumble[i] = Noise(r);
                LowPass(rumble, 120f);
                LowPass(rumble, 120f);
                foreach (float echo in new[] { 0f, 0.42f, 0.95f })
                {
                    int s0 = (int)(echo * Rate);
                    float g = echo == 0f ? 1f : 0.35f / (1f + echo);
                    for (int i = s0; i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 0.9f);
                        float thump = Mathf.Sin(2f * Mathf.PI * (38f + 30f * Mathf.Exp(-t * 6f)) * t) * Mathf.Exp(-t * 3f);
                        d[i] += g * (rumble[i - s0] * 14f * env + thump * 0.9f);
                    }
                }
                // crepitio di detriti
                for (int k = 0; k < 40; k++)
                {
                    int s0 = (int)((0.1f + (float)r.NextDouble() * 2.5f) * Rate);
                    float a = 0.05f + (float)r.NextDouble() * 0.12f;
                    for (int i = s0; i < s0 + Rate / 40 && i < d.Length; i++) d[i] += Noise(r) * a * Mathf.Exp(-(i - s0) / (float)Rate * 120f);
                }
                LowPass(d, 900f);
            }, 301);
        }

        /// <summary>Sirene lontane (polizia e pompieri): due ululati che salgono e scendono, sfasati. In loop.</summary>
        public static AudioClip Siren()
        {
            return Build("sirene", 12f, (d, r) =>
            {
                float p1 = 0f, p2 = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    // l'ululato: un ciclo ogni 4 s (loop perfetto su 12 s)
                    float w1 = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 4f);
                    float w2 = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (t / 3f + 0.3f));
                    p1 += (620f + 560f * w1) / Rate;
                    p2 += (720f + 380f * w2) / Rate;
                    float a = Mathf.Sin(2f * Mathf.PI * p1) + 0.3f * Mathf.Sin(4f * Mathf.PI * p1);
                    float b = Mathf.Sin(2f * Mathf.PI * p2) + 0.25f * Mathf.Sin(6f * Mathf.PI * p2);
                    d[i] = a * 0.5f + b * 0.35f;
                }
                LowPass(d, 1800f); // la distanza smussa gli acuti
            }, 302);
        }

        /// <summary>Colpo da spavento: accordo dissonante di archi e un tonfo grave.</summary>
        public static AudioClip Sting()
        {
            return Build("spavento", 2.2f, (d, r) =>
            {
                float[] f = { 233f, 247f, 349f, 370f, 466f, 494f, 740f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 1.6f);
                    float v = 0f;
                    for (int k = 0; k < f.Length; k++)
                    {
                        float saw = ((t * f[k] * (1f + 0.004f * Mathf.Sin(t * 30f + k))) % 1f) * 2f - 1f;
                        v += saw * 0.18f;
                    }
                    v += Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-t * 4f) * 1.2f + Noise(r) * Mathf.Exp(-t * 25f) * 0.5f;
                    d[i] = v * env;
                }
                LowPass(d, 4000f);
            }, 303);
        }

        /// <summary>Blackout: il ronzio della corrente che cala e muore, con uno schiocco.</summary>
        public static AudioClip PowerDown()
        {
            return Build("blackout", 1.8f, (d, r) =>
            {
                float ph = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float f = 120f * Mathf.Exp(-t * 1.6f) + 20f;
                    ph += f / Rate;
                    float hum = Mathf.Sin(2f * Mathf.PI * ph) + 0.5f * Mathf.Sin(6f * Mathf.PI * ph);
                    d[i] = hum * Mathf.Exp(-t * 1.8f) * 0.6f + (t < 0.03f ? Noise(r) * (1f - t / 0.03f) : 0f);
                }
            }, 304);
        }

        /// <summary>Strappo della corda d'avviamento.</summary>
        public static AudioClip CordPull()
        {
            return Build("corda", 0.5f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f));
                    d[i] = Noise(r) * env * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * (60f + t * 200f)));
                }
                LowPass(d, 3000f);
            }, 305);
        }

        /// <summary>Il motore tossisce e non parte (variant: 0 corto, 1 più lungo).</summary>
        public static AudioClip Sputter(int variant)
        {
            return Build($"tossisce_{variant}", 0.6f + variant * 0.6f, (d, r) =>
            {
                int pops = 3 + variant * 5;
                float t0 = 0.05f;
                for (int k = 0; k < pops; k++)
                {
                    int s0 = (int)(t0 * Rate);
                    float a = 1f - k / (float)(pops + 1);
                    for (int i = s0; i < d.Length && i < s0 + Rate / 10; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += (Mathf.Sin(2f * Mathf.PI * 55f * t) + Noise(r) * 0.6f) * Mathf.Exp(-t * 30f) * a;
                    }
                    t0 += 0.07f + (float)r.NextDouble() * 0.06f + k * 0.012f;
                }
                LowPass(d, 1200f);
            }, 306 + variant);
        }

        /// <summary>Motore del generatore acceso. In loop.</summary>
        public static AudioClip EngineLoop()
        {
            return Build("generatore", 2f, (d, r) =>
            {
                // 30 colpi al secondo (loop esatto su 2 s) con rumore meccanico
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float firing = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 30f * t), 4f);
                    d[i] = firing * (Mathf.Sin(2f * Mathf.PI * 60f * t) + Noise(r) * 0.15f) + Mathf.Sin(2f * Mathf.PI * 30f * t) * 0.3f;
                }
                LowPass(d, 450f); // un borbottio cupo e morbido, non un trapano
            }, 308);
        }

        /// <summary>Qualcosa di pesante che cade (una scatola, in cantina).</summary>
        public static AudioClip Thud()
        {
            return Build("tonfo", 0.9f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 9f) + Noise(r) * Mathf.Exp(-t * 35f) * 0.7f;
                    float t2 = t - 0.18f; // rimbalzo
                    if (t2 > 0f) d[i] += (Mathf.Sin(2f * Mathf.PI * 90f * t2) * 0.4f + Noise(r) * 0.3f) * Mathf.Exp(-t2 * 25f);
                }
                LowPass(d, 1000f);
            }, 309);
        }

        /// <summary>Graffi dentro il muro: unghiate irregolari.</summary>
        public static AudioClip Scratch()
        {
            return Build("graffi", 2.4f, (d, r) =>
            {
                float t0 = 0.05f;
                while (t0 < 2.2f)
                {
                    float len = 0.12f + (float)r.NextDouble() * 0.25f;
                    int s0 = (int)(t0 * Rate), n = (int)(len * Rate);
                    float rate = 70f + (float)r.NextDouble() * 60f;
                    for (int i = 0; i < n && s0 + i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float grain = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * rate * t)), 8f);
                        d[s0 + i] += Noise(r) * grain * Mathf.Sin(Mathf.PI * t / len);
                    }
                    t0 += len + 0.05f + (float)r.NextDouble() * 0.3f;
                }
                LowPass(d, 2500f);
            }, 310);
        }

        /// <summary>Un sussurro: rumore soffiato con formanti di voce che cambiano, incomprensibile.</summary>
        public static AudioClip Whisper()
        {
            return Build("sussurro", 2.6f, (d, r) =>
            {
                var n = new float[d.Length];
                for (int i = 0; i < n.Length; i++) n[i] = Noise(r);
                float y1 = 0f, y2 = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    // due "risonatori" che si spostano come vocali sussurrate
                    float f1 = 500f + 300f * Mathf.Sin(t * 7.3f), f2 = 1700f + 600f * Mathf.Sin(t * 5.1f + 1f);
                    float a1 = 1f - Mathf.Exp(-2f * Mathf.PI * f1 / Rate), a2 = 1f - Mathf.Exp(-2f * Mathf.PI * f2 / Rate);
                    y1 += a1 * (n[i] - y1);
                    y2 += a2 * (n[i] - y2);
                    float syll = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t * 3.4f)), 2f);
                    d[i] = (n[i] - y2 * 0.7f + y1 * 0.5f) * syll * Mathf.Sin(Mathf.PI * t / 2.6f);
                }
                LowPass(d, 5000f);
            }, 311);
        }

        /// <summary>Bordone cupo per la cantina: note gravi battenti e un fruscio. In loop.</summary>
        public static AudioClip Drone()
        {
            return Build("bordone", 10f, (d, r) =>
            {
                var air = new float[d.Length];
                for (int i = 0; i < air.Length; i++) air[i] = Noise(r);
                LowPass(air, 300f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    // 41.0 / 41.1 / 61.6 Hz: battimenti lenti (multipli di 0.1 Hz: loop esatto su 10 s)
                    d[i] = Mathf.Sin(2f * Mathf.PI * 41f * t) + Mathf.Sin(2f * Mathf.PI * 41.1f * t) * 0.8f
                         + Mathf.Sin(2f * Mathf.PI * 61.6f * t) * 0.4f + air[i] * 2f;
                }
            }, 312);
        }

        // ------------------------------------------------------------------ casa

        /// <summary>Ronzio del frigorifero (compressore a 50 Hz con armoniche e un filo di vibrazione). In loop.</summary>
        public static AudioClip FridgeHum()
        {
            return Build("frigo", 4f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.3f
                         + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.12f + Noise(r) * 0.04f;
                    d[i] *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                }
                LowPass(d, 600f);
            }, 401);
        }

        /// <summary>Tic-tac dell'orologio da parete (un secondo per colpo). In loop.</summary>
        public static AudioClip ClockTick()
        {
            return Build("orologio", 2f, (d, r) =>
            {
                for (int k = 0; k < 2; k++)
                {
                    int s0 = k * Rate;
                    float f = k == 0 ? 3200f : 2600f;
                    for (int i = s0; i < s0 + Rate / 40 && i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + Noise(r) * 0.6f) * Mathf.Exp(-t * 300f);
                    }
                }
            }, 402);
        }

        /// <summary>Uno scricchiolio del legno della casa che si assesta (variant 0..3).</summary>
        public static AudioClip WoodCreak(int variant)
        {
            return Build($"scricchiolio_{variant}", 0.5f + variant * 0.15f, (d, r) =>
            {
                float ph = 0f, len = d.Length / (float)Rate;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float f = 140f + variant * 30f + 60f * Mathf.Sin(2f * Mathf.PI * (1.5f + variant) * t);
                    ph += f / Rate;
                    float saw = (ph % 1f) * 2f - 1f;
                    float grain = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * t * (25f + variant * 7f))), 6f);
                    d[i] = saw * grain * Mathf.Sin(Mathf.PI * t / len) + Noise(r) * 0.05f;
                }
                LowPass(d, 1400f);
            }, 410 + variant);
        }

        // ------------------------------------------------------------------ capitolo 2: la creatura e i macigni

        /// <summary>Ruggito della creatura gigante: un ringhio bassissimo che sale, armoniche ruvide, eco lunga (variant 0..2).</summary>
        public static AudioClip Roar(int variant)
        {
            return Build($"ruggito_{variant}", 4.5f, (d, r) =>
            {
                float ph = 0f, len = d.Length / (float)Rate;
                var rough = new float[d.Length];
                for (int i = 0; i < rough.Length; i++) rough[i] = Noise(r);
                LowPass(rough, 400f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((len - t) / 1.6f);
                    float f = 38f + variant * 6f + 30f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2.6f)) + 3f * Mathf.Sin(t * 23f);
                    ph += f / Rate;
                    float saw = (ph % 1f) * 2f - 1f;
                    float growl = saw * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 31f * t)) + Mathf.Sin(2f * Mathf.PI * ph * 2f) * 0.4f;
                    d[i] = (growl + rough[i] * 6f) * env;
                }
                // eco lontana
                int delay = (int)(0.35f * Rate);
                for (int i = d.Length - 1; i >= delay; i--) d[i] += d[i - delay] * 0.35f;
                LowPass(d, 700f);
            }, 501 + variant);
        }

        /// <summary>Il fischio di un macigno che arriva dall'alto (sale di volume e di tono).</summary>
        public static AudioClip Whistle()
        {
            return Build("fischio_macigno", 2.4f, (d, r) =>
            {
                float ph = 0f, len = d.Length / (float)Rate;
                var air = new float[d.Length];
                for (int i = 0; i < air.Length; i++) air[i] = Noise(r);
                LowPass(air, 1500f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate, k = t / len;
                    ph += (250f + 500f * k * k) / Rate;
                    d[i] = (Mathf.Sin(2f * Mathf.PI * ph) * 0.3f + air[i] * 3f) * k * k;
                }
            }, 510);
        }

        /// <summary>Impatto enorme: tonfo profondo, schianto di legno e pietre, detriti che ricadono.</summary>
        public static AudioClip HugeImpact()
        {
            return Build("impatto_macigno", 3.5f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * (30f + 40f * Mathf.Exp(-t * 8f)) * t) * Mathf.Exp(-t * 2.2f) * 1.4f
                         + Noise(r) * Mathf.Exp(-t * 6f) * 0.9f;
                }
                for (int k = 0; k < 70; k++)   // detriti
                {
                    int s0 = (int)((0.05f + (float)r.NextDouble() * 2.6f) * Rate);
                    float a = 0.1f + (float)r.NextDouble() * 0.3f, f = 200f + (float)r.NextDouble() * 900f;
                    for (int i = s0; i < s0 + Rate / 25 && i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += (Noise(r) * 0.7f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f) * a * Mathf.Exp(-t * 60f);
                    }
                }
                LowPass(d, 2200f);
            }, 511);
        }

        /// <summary>Fischio nelle orecchie dopo lo stordimento (acuto e sottile). In loop.</summary>
        public static AudioClip Tinnitus()
        {
            return Build("fischio_orecchie", 2f, (d, r) =>
            {
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Mathf.Sin(2f * Mathf.PI * 5600f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 5650f * t) * 0.3f;
                }
            }, 512);
        }

        /// <summary>Il ronzio del segnale che salta ("Bzzzz"): onda quadra a 120 Hz che gracchia.</summary>
        public static AudioClip Buzz()
        {
            return Build("ronzio_segnale", 1.6f, (d, r) =>
            {
                float len = d.Length / (float)Rate;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 241f * t)) * 0.2f;
                    d[i] = (sq + Noise(r) * 0.35f) * Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((len - t) / 0.3f) * 0.7f;
                }
            }, 514);
        }

        /// <summary>Il boato sordo di una scossa che arriva dall'alto: rombo basso, vetri e barattoli che tintinnano.</summary>
        public static AudioClip Rumble()
        {
            return Build("scossa", 3.2f, (d, r) =>
            {
                float len = d.Length / (float)Rate;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[i] = Noise(r) * Mathf.Clamp01(t / 0.15f) * Mathf.Exp(-t * 0.9f) * 2.5f;
                }
                LowPass(d, 90f);
                for (int k = 0; k < 40; k++)   // tintinnii
                {
                    int s0 = (int)((0.1f + (float)r.NextDouble() * 2.2f) * Rate);
                    float f = 1800f + (float)r.NextDouble() * 2500f;
                    for (int i = s0; i < s0 + Rate / 12 && i < d.Length; i++)
                    {
                        float t = (i - s0) / (float)Rate;
                        d[i] += Mathf.Sin(2f * Mathf.PI * f * t) * 0.06f * Mathf.Exp(-t * 40f);
                    }
                }
            }, 515);
        }

        /// <summary>Urla lontane di gente (più voci, filtrate dalla distanza).</summary>
        public static AudioClip DistantScreams()
        {
            return Build("urla_lontane", 4f, (d, r) =>
            {
                for (int v = 0; v < 4; v++)
                {
                    float start = (float)r.NextDouble() * 1.5f, dur = 1.2f + (float)r.NextDouble() * 1.5f, f0 = 380f + (float)r.NextDouble() * 300f;
                    float ph = 0f;
                    for (int i = (int)(start * Rate); i < d.Length && i < (start + dur) * Rate; i++)
                    {
                        float t = i / (float)Rate - start;
                        float f = f0 * (1f + 0.15f * Mathf.Sin(t * 7f + v) - 0.2f * t / dur);
                        ph += f / Rate;
                        float tone = Mathf.Sin(2f * Mathf.PI * ph) + 0.5f * Mathf.Sin(4f * Mathf.PI * ph) + 0.3f * Mathf.Sin(6f * Mathf.PI * ph);
                        d[i] += (tone + Noise(r) * 0.4f) * Mathf.Sin(Mathf.PI * t / dur) * 0.3f;
                    }
                }
                LowPass(d, 1400f);
            }, 513);
        }

        public static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        // ------------------------------------------------------------------ voci "alla Animal Crossing"

        static readonly Vector2[] Vowels =
        {
            new Vector2(800f, 1200f),  // a
            new Vector2(450f, 1900f),  // e
            new Vector2(300f, 2300f),  // i
            new Vector2(480f, 850f),   // o
            new Vector2(330f, 800f),   // u
        };

        /// <summary>
        /// Sillaba borbottata: armoniche di una fondamentale con due formanti di una vocale,
        /// più un piccolo attacco consonantico. Il tono del personaggio si ottiene con AudioSource.pitch.
        /// </summary>
        public static AudioClip Syllable(int index)
        {
            return Build($"sillaba_{index}", 0.09f + (index % 3) * 0.012f, (d, r) =>
            {
                Vector2 formant = Vowels[index % Vowels.Length];
                float f0 = 210f + (index * 37 % 60);
                float len = d.Length / (float)Rate;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float vib = 1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                    float v = 0f;
                    for (int h = 1; h * f0 < 4500f; h++)
                    {
                        float hf = h * f0;
                        float a = Mathf.Exp(-Mathf.Pow((hf - formant.x) / 160f, 2f))
                                + 0.6f * Mathf.Exp(-Mathf.Pow((hf - formant.y) / 260f, 2f)) + 0.04f;
                        v += Mathf.Sin(2f * Mathf.PI * hf * vib * t) * a;
                    }
                    float env = Mathf.Clamp01(t / 0.012f) * Mathf.Clamp01((len - t) / 0.035f);
                    float consonant = index % 2 == 0 ? Noise(r) * Mathf.Exp(-t * 200f) * 0.8f : 0f;
                    d[i] = v * env + consonant;
                }
            }, 70 + index);
        }

        public const int SyllableCount = 15;
    }
}
