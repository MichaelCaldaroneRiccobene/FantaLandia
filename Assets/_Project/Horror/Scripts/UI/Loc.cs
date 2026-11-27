using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Le lingue del gioco. I testi nel codice sono in italiano; per l'inglese c'è una tabella di traduzioni
    /// (Resources/Lingue/en.txt: una riga per frase, "italiano<TAB>inglese"). T(testo) restituisce il testo nella lingua
    /// scelta (se una frase non è tradotta resta in italiano). Anche le voci registrate sono per lingua (Voci / Voci_en).
    /// </summary>
    public static class Loc
    {
        static Dictionary<string, string> en;

        public static bool English { get { GameSettings.Load(); return GameSettings.Language == "en"; } }

        /// <summary>Cartella delle voci registrate nella lingua scelta.</summary>
        public static string VoiceFolder => English ? "Voci_en/" : "Voci/";

        public static string T(string italian)
        {
            if (string.IsNullOrEmpty(italian) || !English) return italian;
            if (en == null) Load();
            return en.TryGetValue(italian, out string t) ? t : italian;
        }

        static void Load()
        {
            en = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("Lingue/en");
            if (asset == null) return;
            foreach (var raw in asset.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string it = line.Substring(0, tab).Replace("\\n", "\n"), tr = line.Substring(tab + 1).Replace("\\n", "\n");
                en[it] = tr;
            }
        }
    }
}
