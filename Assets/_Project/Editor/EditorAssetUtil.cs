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
