using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// La corrente di tutto il quartiere: lampioni, finestre accese e luci delle verande dei vicini,
    /// lampade della casa di Tom. Nel blackout le luci si spengono e i materiali luminosi diventano scuri.
    /// </summary>
    public class NeighborhoodPower : MonoBehaviour
    {
        public Light[] lights;
        [Tooltip("Renderer con almeno un materiale luminoso")]
        public Renderer[] glowRenderers;
        [Tooltip("Materiale che prende il posto di quelli luminosi senza corrente")]
        public Material darkMaterial;
        public bool powered = true;

        Material[][] original;

        public void SetPower(bool on)
        {
            if (powered == on) return;
            powered = on;
            foreach (var l in lights) if (l != null) l.enabled = on;
            if (original == null)
            {
                original = new Material[glowRenderers.Length][];
                for (int i = 0; i < glowRenderers.Length; i++)
                    if (glowRenderers[i] != null) original[i] = glowRenderers[i].sharedMaterials;
            }
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                Renderer r = glowRenderers[i];
                if (r == null || original[i] == null) continue;
                if (on) { r.sharedMaterials = original[i]; continue; }
                var mats = (Material[])original[i].Clone();
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && mats[m].IsKeywordEnabled("_EMISSION")) mats[m] = darkMaterial;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Sfarfallio di qualche istante (l'onda d'urto dell'esplosione), poi torna com'era.</summary>
        public System.Collections.IEnumerator Flicker(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                bool on = Random.value > 0.45f;
                foreach (var l in lights) if (l != null) l.enabled = on && powered;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
            }
            foreach (var l in lights) if (l != null) l.enabled = powered;
        }
    }
}
