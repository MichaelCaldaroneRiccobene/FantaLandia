using System.Collections.Generic;
using FantaLandia.Horror.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Lo stordimento dopo un colpo fortissimo (capitolo 2): lampo bianco, sangue sulla "lente", visuale sfocata e
    /// arrossata con la vignetta, colori slavati, i suoni ovattati (filtro sul microfono) e il fischio nelle orecchie.
    /// Tutto si attenua da solo in 'recovery' secondi. Va sulla camera del giocatore.
    /// </summary>
    public class StunEffect : MonoBehaviour
    {
        public static StunEffect Instance { get; private set; }

        [Range(0f, 1f)] public float stun;      // quanto si è storditi adesso
        public float recovery = 22f;            // secondi per riprendersi del tutto
        [Range(0f, 1f)] public float muffle;    // suoni ovattati (segue lo stordimento, più lento)
        [Tooltip("Angoscia / claustrofobia (0..1): la vista si stringe, i bordi diventano neri, il cuore batte forte")]
        [Range(0f, 1f)] public float dread;

        Volume dreadVolume;
        Vignette dreadVignette;
        LensDistortion dreadLens;
        AudioSource heart;
        Camera cam;
        float baseFov = -1f;

        Volume volume;
        Vignette vignette;
        ColorAdjustments color;
        DepthOfField blur;
        ChromaticAberration aberration;
        AudioLowPassFilter earFilter;
        AudioSource ringing;
        float flash;
        Color flashColor = Color.white;

        class Drop { public Vector2 pos; public float size, rot, alpha, slide; public int tex; }
        readonly List<Drop> drops = new List<Drop>();
        static Texture2D[] dropTex;

        void Awake()
        {
            Instance = this;
            var go = new GameObject("Stordimento (post-processing)");
            go.transform.SetParent(transform, false);
            go.layer = 0;
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.45f, 0f, 0f));
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.8f);
            color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-55f);
            color.colorFilter.Override(new Color(1f, 0.72f, 0.7f));
            color.postExposure.Override(-0.3f);
            blur = profile.Add<DepthOfField>(true);
            blur.mode.Override(DepthOfFieldMode.Gaussian);
            blur.gaussianStart.Override(0.2f);
            blur.gaussianEnd.Override(2.5f);
            blur.gaussianMaxRadius.Override(1.5f);
            aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.9f);

            // l'angoscia: un secondo volume, nero ai bordi, l'immagine che si "stringe" verso il centro
            var dgo = new GameObject("Angoscia (post-processing)");
            dgo.transform.SetParent(transform, false);
            dreadVolume = dgo.AddComponent<Volume>();
            dreadVolume.isGlobal = true;
            dreadVolume.priority = 49f;
            dreadVolume.weight = 0f;
            var dp = ScriptableObject.CreateInstance<VolumeProfile>();
            dreadVolume.sharedProfile = dp;
            dreadVignette = dp.Add<Vignette>(true);
            dreadVignette.color.Override(Color.black);
            dreadVignette.intensity.Override(0.6f);
            dreadVignette.smoothness.Override(1f);
            dreadLens = dp.Add<LensDistortion>(true);
            dreadLens.intensity.Override(-0.35f);
            var dc = dp.Add<ChromaticAberration>(true);
            dc.intensity.Override(0.7f);
            var dcol = dp.Add<ColorAdjustments>(true);
            dcol.saturation.Override(-35f);
            dcol.contrast.Override(18f);
            cam = GetComponent<Camera>();
            heart = gameObject.AddComponent<AudioSource>();
            heart.clip = ProceduralAudio.Heartbeat();
            heart.loop = true;
            heart.spatialBlend = 0f;
            heart.playOnAwake = false;
            heart.volume = 0f;
            heart.bypassListenerEffects = true;

            earFilter = GetComponent<AudioLowPassFilter>();
            if (earFilter == null) earFilter = gameObject.AddComponent<AudioLowPassFilter>();
            earFilter.cutoffFrequency = 22000f;
            ringing = gameObject.AddComponent<AudioSource>();
            ringing.clip = ProceduralAudio.Tinnitus();
            ringing.loop = true;
            ringing.spatialBlend = 0f;
            ringing.playOnAwake = false;
            ringing.volume = 0f;
            ringing.bypassListenerEffects = true; // il fischio è "dentro la testa": non si ovatta
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Un colpo: 'strength' 0..1 (1 = il macigno a due metri). 'blood' = gocce di sangue sulla lente.</summary>
        public void Hit(float strength, int blood)
        {
            stun = Mathf.Max(stun, strength);
            muffle = Mathf.Max(muffle, strength);
            flash = Mathf.Max(flash, strength);
            flashColor = Color.white;
            if (ringing != null && !ringing.isPlaying) ringing.Play();
            AddBlood(blood);
        }

        /// <summary>Spruzzi di sangue sullo schermo (si asciugano e scivolano giù piano).</summary>
        public void AddBlood(int count)
        {
            if (dropTex == null) dropTex = new[] { MakeDrop(1), MakeDrop(2), MakeDrop(3) };
            for (int i = 0; i < count; i++)
            {
                // più fitti ai bordi e in basso, qualcuno grande in mezzo
                Vector2 p = new Vector2(Random.value, Random.value);
                if (i % 3 != 0) p = Vector2.Lerp(p, new Vector2(Random.value < 0.5f ? 0f : 1f, Random.value), 0.5f);
                drops.Add(new Drop
                {
                    pos = p,
                    size = (i % 5 == 0 ? Random.Range(0.25f, 0.45f) : Random.Range(0.05f, 0.18f)),
                    rot = Random.Range(0f, 360f),
                    alpha = Random.Range(0.75f, 1f),
                    slide = Random.Range(0.002f, 0.012f),
                    tex = Random.Range(0, 3),
                });
            }
        }

        /// <summary>Pulisce lo schermo (cambio di scena).</summary>
        public void ClearAll()
        {
            drops.Clear();
            stun = muffle = flash = dread = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            stun = Mathf.MoveTowards(stun, 0f, dt / Mathf.Max(1f, recovery));
            muffle = Mathf.MoveTowards(muffle, 0f, dt / Mathf.Max(1f, recovery * 0.8f));
            flash = Mathf.MoveTowards(flash, 0f, dt * 2.2f);

            float k = stun * stun * (3f - 2f * stun);
            if (volume != null)
            {
                volume.weight = Mathf.Clamp01(k * 1.15f);
                // la vista va e viene a ondate (il battito nelle orecchie)
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
                vignette.intensity.value = 0.35f + 0.3f * pulse * k;
                blur.gaussianMaxRadius.value = 0.6f + 0.9f * k;
            }
            if (earFilter != null) earFilter.cutoffFrequency = Mathf.Lerp(22000f, 380f, Mathf.Pow(muffle, 0.6f));
            if (ringing != null)
            {
                ringing.volume = Mathf.Clamp01(muffle * 1.4f) * 0.12f * FantaLandia.Horror.UI.GameSettings.Effects;
                if (muffle <= 0.001f && ringing.isPlaying) ringing.Stop();
            }
            // angoscia: il cuore accelera, la vista si stringe e "pulsa" a ogni battito
            float dk = dread * dread * (3f - 2f * dread);
            if (dreadVolume != null)
            {
                float beat = heart != null && heart.isPlaying && heart.clip != null ? Mathf.Exp(-(heart.time % heart.clip.length) * 6f) : 0f;
                dreadVolume.weight = dk;
                dreadVignette.intensity.value = 0.45f + 0.2f * dk + 0.08f * beat;
                dreadLens.intensity.value = -0.2f - 0.25f * dk - 0.06f * beat;
            }
            if (cam != null)
            {
                if (baseFov < 0f) baseFov = cam.fieldOfView;
                cam.fieldOfView = baseFov - 16f * dk;
            }
            if (heart != null)
            {
                heart.volume = dk * 0.55f * FantaLandia.Horror.UI.GameSettings.Effects;
                heart.pitch = 1f + 0.35f * dk;
                if (dk > 0.01f && !heart.isPlaying) heart.Play();
                if (dk <= 0.01f && heart.isPlaying) heart.Stop();
            }
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                d.pos.y += d.slide * dt;
                d.alpha -= dt / 40f; // si asciuga in una quarantina di secondi
                if (d.alpha <= 0f) drops.RemoveAt(i);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            Matrix4x4 m = GUI.matrix;
            float big = Mathf.Max(Screen.width, Screen.height);
            foreach (var d in drops)
            {
                float size = d.size * big;
                Vector2 c = new Vector2(d.pos.x * Screen.width, d.pos.y * Screen.height);
                GUIUtility.RotateAroundPivot(d.rot, c);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(d.alpha));
                GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), dropTex[d.tex]);
                GUI.matrix = m;
            }
            if (flash > 0.001f)
            {
                GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Clamp01(flash * 1.3f));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        /// <summary>Una macchia di sangue: goccia centrale irregolare, schizzi intorno, più scura ai bordi.</summary>
        static Texture2D MakeDrop(int seed)
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[S * S];
            var rng = new System.Random(seed * 97);
            var blobs = new List<Vector3>(); // x, y, raggio (0..1)
            blobs.Add(new Vector3(0.5f, 0.5f, 0.22f + (float)rng.NextDouble() * 0.08f));
            int n = 10 + rng.Next(14);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.15f + (float)rng.NextDouble() * 0.32f;
                blobs.Add(new Vector3(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r, 0.015f + (float)rng.NextDouble() * 0.05f));
                if (i % 3 == 0) // una scia verso il centro
                    for (float k = 0.3f; k < 1f; k += 0.12f)
                        blobs.Add(new Vector3(0.5f + Mathf.Cos(a) * r * k, 0.5f + Mathf.Sin(a) * r * k, 0.012f + 0.02f * (1f - k)));
            }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S, best = 0f;
                    foreach (var b in blobs)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y)) / b.z;
                        best = Mathf.Max(best, 1f - d);
                    }
                    float a = Mathf.Clamp01(best * 6f);
                    float edge = Mathf.Clamp01(1f - best * 2.5f);
                    px[y * S + x] = new Color(Mathf.Lerp(0.42f, 0.18f, edge), 0.01f, 0.01f, a * 0.92f);
                }
            t.SetPixels(px);
            t.Apply(false);
            return t;
        }
    }
}
