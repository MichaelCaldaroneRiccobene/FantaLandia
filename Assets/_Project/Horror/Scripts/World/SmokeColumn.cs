using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// L'esplosione nel quartiere vista da lontano: palla di fuoco iniziale, bagliore arancione che tremola alla base
    /// e una colonna di fumo che sale e piega col vento. Le particelle si creano dal codice quando si chiama Ignite().
    /// </summary>
    public class SmokeColumn : MonoBehaviour
    {
        public Material smokeMaterial;
        public Material glowMaterial;
        public Vector3 wind = new Vector3(1.6f, 0f, 0.4f);
        public float fireLightIntensity = 40f;
        public float fireLightRange = 60f;

        public bool Burning { get; private set; }
        Light fire;
        float flash;

        public void Ignite()
        {
            if (Burning) return;
            Burning = true;
            flash = 1f;

            var smoke = Create("Fumo", smokeMaterial);
            var main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.15f, 0.85f), new Color(0.24f, 0.22f, 0.21f, 0.85f));
            main.maxParticles = 500;
            var em = smoke.emission;
            em.rateOverTime = 10f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var sh = smoke.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 2.5f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = smoke.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(wind.z);
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3.2f)));
            var rot = smoke.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            // in basso illuminato dal fuoco, poi grigio scuro che svanisce in alto
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f), new GradientColorKey(new Color(0.7f, 0.65f, 0.65f), 0.12f), new GradientColorKey(new Color(0.55f, 0.55f, 0.6f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            smoke.Play();

            var embers = Create("Bagliore", glowMaterial);
            main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 0.6f), new Color(1f, 0.7f, 0.25f, 0.6f));
            main.maxParticles = 200;
            em = embers.emission;
            em.rateOverTime = 25f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) }); // palla di fuoco
            sh = embers.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 2f;
            col = embers.colorOverLifetime;
            col.enabled = true;
            var g2 = new Gradient();
            g2.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g2;
            embers.Play();

            fire = new GameObject("Luce incendio").AddComponent<Light>();
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = Vector3.up * 3f;
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.5f, 0.2f);
            fire.range = fireLightRange;
            fire.shadows = LightShadows.None;
        }

        ParticleSystem Create(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
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
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 4f, 0.3f);
            fire.intensity = fireLightIntensity * (flicker + flash * 4f);
        }
    }
}
