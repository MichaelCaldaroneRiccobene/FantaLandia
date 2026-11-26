using UnityEditor;
using UnityEngine;

namespace FantaLandia.EditorTools
{
    /// <summary>Utilità condivise dai generatori: cartelle e materiali URP salvati come asset.</summary>
    public static class EditorAssetUtil
    {
        public const string MaterialFolder = "Assets/_Project/Generated/Materials";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>Vetro: URP/Lit trasparente (si vede attraverso).</summary>
        public static Material Glass(string name, Color color, float alpha = 0.22f)
        {
            Material m = Mat(name, color, 0.9f);
            color.a = alpha;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);           // trasparente
                m.SetFloat("_Blend", 0f);             // alpha
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Materiale che emette luce (finestre accese, lampadine, paralumi).</summary>
        public static Material Emissive(string name, Color color, float intensity = 1.5f)
        {
            Material m = Mat(name, color, 0.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Materiale "al vento" (shader FantaLandia/WindVegetation): chiome, cespugli, erba.
        /// Senza colore dei vertici la piegatura dipende dall'altezza nell'oggetto (bendStart..bendStart+bendHeight).
        /// </summary>
        public static Material WindMat(string name, Color color, float strength, float bendStart, float bendHeight, bool useVertexColor = false)
        {
            Shader sh = Shader.Find("FantaLandia/WindVegetation");
            if (sh == null) return Mat(name, color);
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);
            m.SetFloat("_WindStrength", strength);
            m.SetFloat("_WindSpeed", useVertexColor ? 1.3f : 0.8f);
            m.SetFloat("_WindScale", useVertexColor ? 0.35f : 0.25f);
            m.SetFloat("_BendStart", bendStart);
            m.SetFloat("_BendHeight", bendHeight);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Crea (o aggiorna) un materiale URP/Lit con il colore dato.</summary>
        public static Material Mat(string name, Color color, float smoothness = 0.1f)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
