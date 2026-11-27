using System;
using UnityEngine;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Le impostazioni del giocatore (volumi, mouse, luminosità, qualità, sottotitoli) e l'ultimo punto di salvataggio
    /// della storia. Si salvano da sole (PlayerPrefs) e avvisano chi le usa quando cambiano.
    /// </summary>
    public static class GameSettings
    {
        public static float Master = 1f, Voices = 1f, Music = 1f, Effects = 1f;
        public static float Sensitivity = 1f;      // moltiplica la sensibilità del mouse (0.3 .. 2)
        public static int Brightness;              // -5 .. +5
        public static int Quality = 2;             // 0 bassa, 1 media, 2 alta
        public static bool Subtitles = true;
        public static int TextSize = 1;            // 0 piccolo, 1 medio, 2 grande
        public static string Language = "en";      // "en" inglese (predefinita), "it" italiano

        public static event Action Changed;

        public static float TextScale => TextSize == 0 ? 0.85f : TextSize == 2 ? 1.25f : 1f;

        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Master = PlayerPrefs.GetFloat("fl_master", 1f);
            Voices = PlayerPrefs.GetFloat("fl_voices", 1f);
            Music = PlayerPrefs.GetFloat("fl_music", 1f);
            Effects = PlayerPrefs.GetFloat("fl_effects", 1f);
            Sensitivity = PlayerPrefs.GetFloat("fl_sens", 1f);
            Brightness = PlayerPrefs.GetInt("fl_bright", 0);
            Quality = PlayerPrefs.GetInt("fl_quality", 2);
            Subtitles = PlayerPrefs.GetInt("fl_subs", 1) == 1;
            TextSize = PlayerPrefs.GetInt("fl_text", 1);
            Language = PlayerPrefs.GetString("fl_lang", "en");
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("fl_master", Master);
            PlayerPrefs.SetFloat("fl_voices", Voices);
            PlayerPrefs.SetFloat("fl_music", Music);
            PlayerPrefs.SetFloat("fl_effects", Effects);
            PlayerPrefs.SetFloat("fl_sens", Sensitivity);
            PlayerPrefs.SetInt("fl_bright", Brightness);
            PlayerPrefs.SetInt("fl_quality", Quality);
            PlayerPrefs.SetInt("fl_subs", Subtitles ? 1 : 0);
            PlayerPrefs.SetInt("fl_text", TextSize);
            PlayerPrefs.SetString("fl_lang", Language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>L'ultimo punto della storia raggiunto (0 = inizio).</summary>
        public static int Checkpoint
        {
            get => PlayerPrefs.GetInt("fl_checkpoint", 0);
            set { PlayerPrefs.SetInt("fl_checkpoint", value); PlayerPrefs.Save(); }
        }
    }
}
