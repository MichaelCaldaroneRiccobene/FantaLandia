using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Evidenzia un oggetto da trovare: un contorno luminoso che pulsa (copia della mesh un po' più grande con il
    /// materiale FantaLandia/Highlight) e una lucina calda che si vede anche al buio. Si toglie con Remove().
    /// </summary>
    public class Highlighter : MonoBehaviour
    {
        GameObject[] shells;
        Light glow;

        public static Highlighter Attach(GameObject target, Material material)
        {
            if (target == null) return null;
            var h = target.GetComponent<Highlighter>();
            if (h == null) h = target.AddComponent<Highlighter>();
            h.Build(material);
            return h;
        }

        public static void Remove(GameObject target)
        {
            if (target == null) return;
            var h = target.GetComponent<Highlighter>();
            if (h != null) Destroy(h);
        }

        void Build(Material material)
        {
            if (shells != null) return;
            var filters = GetComponentsInChildren<MeshFilter>();
            shells = new GameObject[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null || material == null) continue;
                var go = new GameObject("Evidenziatura");
                go.transform.SetParent(filters[i].transform, false);
                go.transform.localScale = Vector3.one * 1.12f;
                go.transform.localPosition = filters[i].sharedMesh.bounds.center * -0.12f; // ingrandita attorno al centro della mesh
                go.AddComponent<MeshFilter>().sharedMesh = filters[i].sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                shells[i] = go;
            }
            glow = new GameObject("Luce evidenziatura").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.25f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.82f, 0.5f);
            glow.range = 1.4f;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (glow != null) glow.intensity = 0.5f + 0.35f * Mathf.Sin(Time.time * 3f);
        }

        void OnDestroy()
        {
            if (shells != null) foreach (var s in shells) if (s != null) Destroy(s);
            if (glow != null) Destroy(glow.gameObject);
        }
    }
}
