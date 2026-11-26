using System.Collections.Generic;
using FantaLandia.EditorTools;
using UnityEditor;
using UnityEngine;
using static FantaLandia.EditorTools.BlockyCharacterBuilder;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// I personaggi dell'horror, anni '90: Muark (il giocatore), Tom, Jef, Roy e i vicini
    /// (a sinistra i Miller: Frank e Martha; a destra i Kowalski: Gary e Linda).
    /// Menu: FantaLandia Horror > Crea personaggi.
    /// </summary>
    public static class HorrorCast
    {
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }

        [MenuItem("FantaLandia Horror/Crea personaggi")]
        public static Dictionary<string, GameObject> BuildAll()
        {
            var d = new Dictionary<string, GameObject>
            {
                ["muark"] = Build(Muark()),
                ["tom"] = Build(Tom()),
                ["jef"] = Build(Jef()),
                ["roy"] = Build(Roy()),
                ["frank"] = Build(Frank()),
                ["martha"] = Build(Martha()),
                ["gary"] = Build(Gary()),
                ["linda"] = Build(Linda()),
            };
            Debug.Log("[FantaLandia Horror] Personaggi creati in " + Folder);
            return d;
        }

        /// <summary>Muark: camicia di flanella verde sopra, jeans, scarpe da ginnastica. Capelli castani corti.</summary>
        public static Shape Muark()
        {
            Shape s = Male();
            s.id = "Personaggio_Muark"; s.displayName = "Muark";
            s.skin = C("#cfa07c"); s.hair = C("#4a3020"); s.pupil = C("#4a6a3a");
            s.hairStyle = HairStyle.Short; s.beard = BeardStyle.Stubble;
            s.outfit = new Outfit
            {
                top = Top.Flannel, topColor = C("#2f5a3a"), topColor2 = C("#1a1a1a"),
                bottom = Bottom.Jeans, bottomColor = C("#3a5a86"),
                shoes = Shoes.Sneakers, shoeColor = C("#e8e8e2"), shoeColor2 = C("#c0392b"),
            };
            return s;
        }

        /// <summary>Tom, il padrone di casa: mullet biondo scuro, barba corta, maglietta rossa con stampa, jeans, scarpe alte.</summary>
        public static Shape Tom()
        {
            Shape s = Male();
            s.id = "Personaggio_Tom"; s.displayName = "Tom";
            s.skin = C("#c79a76"); s.hair = C("#8a6a3a"); s.pupil = C("#3a5a8a");
            s.hairStyle = HairStyle.Mullet; s.beard = BeardStyle.Stubble; s.belly = 0.35f;
            s.outfit = new Outfit
            {
                top = Top.TShirt, topColor = C("#a8322e"), topColor2 = C("#efe9dc"),
                bottom = Bottom.Jeans, bottomColor = C("#4a6a96"),
                shoes = Shoes.HighTops, shoeColor = C("#1e1e1e"), shoeColor2 = C("#efefea"),
            };
            return s;
        }

        /// <summary>Jef, quello silenzioso: pallido, alto e magro, capelli rasati da militare, occhiali, felpa nera, pantaloni scuri.</summary>
        public static Shape Jef()
        {
            Shape s = Male();
            s.id = "Personaggio_Jef"; s.displayName = "Jef";
            s.skin = C("#ecd2bd"); s.hair = C("#141414"); s.pupil = C("#2a2a2a");
            s.hairStyle = HairStyle.Buzz; s.beard = BeardStyle.None; // taglio da militare
            s.thighLen += 0.02f; s.shinLen += 0.02f;
            s.thighW *= 0.88f; s.shinW *= 0.9f; s.upperArmW *= 0.88f; s.forearmW *= 0.9f;
            s.chestW *= 0.92f; s.absW *= 0.9f; s.pelvisW *= 0.94f;
            s.outfit = new Outfit
            {
                top = Top.Hoodie, topColor = C("#1d1d22"), topColor2 = C("#9a9aa2"),
                bottom = Bottom.Slacks, bottomColor = C("#2c2c30"),
                shoes = Shoes.Sneakers, shoeColor = C("#4a4a4e"), shoeColor2 = C("#2a2a2a"),
                glasses = true, glassesColor = C("#151515"),
            };
            return s;
        }

        /// <summary>Roy, il burlone: capelli rasati, cappellino all'indietro, maglia da basket, jeans larghi, scarpe alte bianche.</summary>
        public static Shape Roy()
        {
            Shape s = Male();
            s.id = "Personaggio_Roy"; s.displayName = "Roy";
            s.skin = C("#8a5c3e"); s.hair = C("#121010"); s.pupil = C("#2a1a10");
            s.hairStyle = HairStyle.Buzz; s.beard = BeardStyle.None;
            s.thighLen -= 0.02f; s.shinLen -= 0.02f;
            s.outfit = new Outfit
            {
                top = Top.Jersey, topColor = C("#2a4aa0"), topColor2 = C("#f2efe6"),
                bottom = Bottom.Jeans, bottomColor = C("#2e4466"),
                shoes = Shoes.HighTops, shoeColor = C("#f2f2ee"), shoeColor2 = C("#2a4aa0"),
                hat = Hat.CapBackwards, hatColor = C("#c0392b"),
            };
            return s;
        }

        /// <summary>Frank Miller, vicino anziano: calvo con corona grigia, baffi, occhiali, cardigan marrone, pantaloni grigi, mocassini.</summary>
        public static Shape Frank()
        {
            Shape s = Male();
            s.id = "Personaggio_Frank"; s.displayName = "Frank";
            s.skin = C("#dbb49a"); s.hair = C("#b8b8b4"); s.pupil = C("#5a6a7a");
            s.hairStyle = HairStyle.Bald; s.beard = BeardStyle.Mustache; s.belly = 0.5f;
            s.outfit = new Outfit
            {
                top = Top.Cardigan, topColor = C("#6a4a2e"), topColor2 = C("#e2dccb"),
                bottom = Bottom.Slacks, bottomColor = C("#6a6a6e"),
                shoes = Shoes.Loafers, shoeColor = C("#3a2616"), shoeColor2 = C("#1e140c"),
                glasses = true, glassesColor = C("#6a5030"),
            };
            return s;
        }

        /// <summary>Martha Miller: permanente grigio-bianca, vestaglia rosa, pantofole.</summary>
        public static Shape Martha()
        {
            Shape s = Female();
            s.id = "Personaggio_Martha"; s.displayName = "Martha";
            s.skin = C("#e8c4ae"); s.hair = C("#d8d4cc"); s.pupil = C("#5a7a9a"); s.lips = C("#b06a70");
            s.hairStyle = HairStyle.Curly; s.belly = 0.25f;
            s.outfit = new Outfit
            {
                top = Top.Robe, topColor = C("#d89aaa"), topColor2 = C("#f2ece6"),
                bottom = Bottom.None,
                shoes = Shoes.Slippers, shoeColor = C("#e8b8c8"),
            };
            return s;
        }

        /// <summary>Gary Kowalski: robusto, barba folta, canottiera bianca, pantaloncini cachi, ciabatte.</summary>
        public static Shape Gary()
        {
            Shape s = Male();
            s.id = "Personaggio_Gary"; s.displayName = "Gary";
            s.skin = C("#d29a78"); s.hair = C("#7a3a20"); s.pupil = C("#4a3a20");
            s.hairStyle = HairStyle.Short; s.beard = BeardStyle.Full; s.belly = 1f;
            s.chestW *= 1.08f; s.absW *= 1.15f; s.pelvisW *= 1.08f; s.upperArmW *= 1.1f; s.thighW *= 1.08f;
            s.outfit = new Outfit
            {
                top = Top.Tank, topColor = C("#eeece4"),
                bottom = Bottom.Shorts, bottomColor = C("#a8946a"),
                shoes = Shoes.Slippers, shoeColor = C("#5a4030"),
            };
            return s;
        }

        /// <summary>Linda Kowalski: permanente biondo vaporosa, tuta anni '90 verde acqua e rosa, scarpe da ginnastica.</summary>
        public static Shape Linda()
        {
            Shape s = Female();
            s.id = "Personaggio_Linda"; s.displayName = "Linda";
            s.skin = C("#e2b090"); s.hair = C("#d8b060"); s.pupil = C("#3a8a8a"); s.lips = C("#c0405a");
            s.hairStyle = HairStyle.Curly;
            s.outfit = new Outfit
            {
                top = Top.LongSleeve, topColor = C("#3aa8a0"), topColor2 = C("#e85a9a"),
                bottom = Bottom.Sweatpants, bottomColor = C("#d85a8a"),
                shoes = Shoes.Sneakers, shoeColor = C("#f2f2ee"), shoeColor2 = C("#3aa8a0"),
            };
            return s;
        }
    }
}
