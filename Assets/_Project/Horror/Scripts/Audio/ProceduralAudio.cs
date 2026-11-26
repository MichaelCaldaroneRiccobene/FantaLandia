using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.Audio
{
    public enum Surface { Wood, Grass, Concrete }

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
                float cutoff = surface == Surface.Grass ? 2500f : surface == Surface.Wood ? 900f : 1800f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * (surface == Surface.Grass ? 28f : 45f));
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
