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
