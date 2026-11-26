using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// L'incendio dopo l'esplosione, visto da lontano: un edificio che brucia con fiamme sul tetto squarciato
    /// e dalle finestre, scintille che salgono, un grande alone arancione e una colonna di fumo che piega col vento.
    /// Le particelle si creano dal codice quando si chiama Ignite() (prima c'è solo l'edificio buio).
    /// </summary>
    public class SmokeColumn : MonoBehaviour
    {
        public Material smokeMaterial;
        public Material glowMaterial;
        public Vector3 wind = new Vector3(1.6f, 0f, 0.4f);
        [Tooltip("Altezza del tetto che brucia (dal punto di questo oggetto)")]
        public float roofHeight = 9f;
        [Tooltip("Larghezza (x) e profondità (z) della zona in fiamme sul tetto")]
        public Vector2 roofFireSize = new Vector2(12f, 7f);
        [Tooltip("Finestre da cui escono le fiamme (posizioni locali)")]
        public Vector3[] windowFires = new Vector3[0];
        public float fireLightIntensity = 30f;
        public float fireLightRange = 70f;

        public bool Burning { get; private set; }
        Light fire;
        float flash;

        public void Ignite()
        {
            if (Burning) return;
            Burning = true;
            flash = 1f;

            Vector3 roof = Vector3.up * roofHeight;

            // --- palla di fuoco dell'esplosione (una sola volta)
            var ball = Create("Palla di fuoco", glowMaterial, roof, 0f);
            var main = ball.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.5f, 0.9f), new Color(1f, 0.5f, 0.15f, 0.9f));
            var em = ball.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
            var sh = ball.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 3f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ball, new Color(1f, 0.9f, 0.6f), new Color(0.8f, 0.25f, 0.05f), 0.05f);
            var size = ball.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
            ball.Play();

            // --- fiamme sul tetto: lingue che salgono e si stringono, dal giallo-bianco al rosso
            Flames("Fiamme tetto", roof, new Vector3(roofFireSize.x, roofFireSize.y, 0.5f), 90f, new Vector2(1.6f, 3.4f), new Vector2(2.5f, 5f));
            // --- fiamme dalle finestre
            for (int i = 0; i < windowFires.Length; i++)
                Flames("Fiamme finestra " + (i + 1), windowFires[i], new Vector3(1.2f, 0.4f, 0.3f), 14f, new Vector2(0.8f, 1.6f), new Vector2(1.2f, 2.5f));

            // --- alone: grandi sbuffi tenui che illuminano il fumo da sotto
            var halo = Create("Alone", glowMaterial, roof + Vector3.up * 2f, 0f);
            main = halo.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(18f, 28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.12f, 0.16f), new Color(1f, 0.3f, 0.08f, 0.2f));
            em = halo.emission;
            em.rateOverTime = 3f;
            sh = halo.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 3f;
            Fade(halo, Color.white, Color.white, 0.3f);
            halo.Play();

            // --- scintille: puntini luminosi allungati che salgono veloci e ondeggiano
            var sparks = Create("Scintille", glowMaterial, roof, 0f);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f, 1f), new Color(1f, 0.45f, 0.1f, 1f));
            main.gravityModifier = 0.15f;
            main.maxParticles = 400;
            em = sparks.emission;
            em.rateOverTime = 45f;
            sh = sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(roofFireSize.x, roofFireSize.y, 0.5f);
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 1.5f;
            noise.frequency = 0.6f;
            Fade(sparks, Color.white, new Color(1f, 0.3f, 0.1f), 0.02f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.06f;
            sr.lengthScale = 1.5f;
            sparks.Play();

            // --- fumo: dal tetto intero, denso, illuminato d'arancio alla base, piega col vento
            var smoke = Create("Fumo", smokeMaterial, roof + Vector3.up * 2f, 0f);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.14f, 0.13f, 0.13f, 0.9f), new Color(0.22f, 0.2f, 0.19f, 0.9f));
            main.maxParticles = 700;
            em = smoke.emission;
            em.rateOverTime = 16f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            sh = smoke.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(roofFireSize.x * 0.8f, roofFireSize.y * 0.8f, 0.5f);
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = smoke.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(wind.z);
            size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3.4f)));
            var rot = smoke.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.25f), 0f), new GradientColorKey(new Color(0.75f, 0.6f, 0.55f), 0.1f), new GradientColorKey(new Color(0.55f, 0.55f, 0.6f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            smoke.Play();

            fire = new GameObject("Luce incendio").AddComponent<Light>();
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = roof + Vector3.up * 1.5f;
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.5f, 0.2f);
            fire.range = fireLightRange;
            fire.shadows = LightShadows.None;
        }

        void Flames(string name, Vector3 localPos, Vector3 area, float rate, Vector2 life, Vector2 sizeRange)
        {
            var f = Create(name, glowMaterial, localPos, 0f);
            var main = f.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x * 0.5f, life.y * 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 0.85f), new Color(1f, 0.55f, 0.2f, 0.85f));
            main.maxParticles = 600;
            var em = f.emission;
            em.rateOverTime = rate;
            var sh = f.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = area;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var size = f.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            var rot = f.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var noise = f.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.8f;
            Fade(f, new Color(1f, 0.95f, 0.75f), new Color(0.7f, 0.12f, 0.02f), 0.08f);
            f.Play();
        }

        /// <summary>Colore lungo la vita della particella: da 'start' a 'end', comparsa rapida e scomparsa dolce.</summary>
        static void Fade(ParticleSystem ps, Color start, Color end, float fadeIn)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(Color.Lerp(start, end, 0.5f), 0.4f), new GradientColorKey(end, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        ParticleSystem Create(string name, Material mat, Vector3 localPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        void Update()
        {
            if (fire == null) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 0.5f);
            float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 5f, 0.3f);
            fire.intensity = fireLightIntensity * (flicker + flash * 4f);
        }
    }
}
