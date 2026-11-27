using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Applica le impostazioni alla scena: volume generale, luminosità (gamma, senza toccare le zone di post-processing),
    /// qualità grafica (risoluzione interna, distanza delle ombre, ombre delle lampade, polvere sospesa).
    /// Rimette a posto la pipeline quando si esce (nell'editor le modifiche all'asset resterebbero).
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        Volume brightnessVolume;
        LiftGammaGain gamma;
        UniversalRenderPipelineAsset urp;
        float origScale = 1f, origShadowDistance = 60f;
        readonly Dictionary<Light, LightShadows> origShadows = new Dictionary<Light, LightShadows>();

        void Awake()
        {
            GameSettings.Load();
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) { origScale = urp.renderScale; origShadowDistance = urp.shadowDistance; }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            gamma = profile.Add<LiftGammaGain>(true);
            gameObject.layer = 2; // sul layer che la camera usa per i volumi
            brightnessVolume = gameObject.AddComponent<Volume>();
            brightnessVolume.isGlobal = true;
            brightnessVolume.priority = 50;
            brightnessVolume.sharedProfile = profile;
        }

        void OnEnable() => GameSettings.Changed += Apply;

        void OnDisable()
        {
            GameSettings.Changed -= Apply;
            Restore();
        }

        void OnApplicationQuit() => Restore();

        void Start()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type != LightType.Directional) origShadows[l] = l.shadows;
            Apply();
        }

        void Apply()
        {
            AudioListener.volume = GameSettings.Master;
            if (gamma != null) gamma.gamma.Override(new Vector4(1f, 1f, 1f, GameSettings.Brightness * 0.07f));

            int q = GameSettings.Quality;
            if (urp != null)
            {
                urp.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : origScale;
                urp.shadowDistance = q == 0 ? 25f : q == 1 ? 40f : origShadowDistance;
            }
            // con la qualità bassa le lampade non fanno ombra (la torcia sì)
            foreach (var kv in origShadows)
                if (kv.Key != null) kv.Key.shadows = q == 0 && kv.Key.name != "Fascio torcia" ? LightShadows.None : kv.Value;
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ps.name.StartsWith("Polvere")) ps.gameObject.SetActive(q > 0);
        }

        void Restore()
        {
            if (urp == null) return;
            urp.renderScale = origScale;
            urp.shadowDistance = origShadowDistance;
        }
    }
}
