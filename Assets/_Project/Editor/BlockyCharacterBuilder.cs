using System.Collections.Generic;
using FantaLandia.Characters.Body;
using UnityEditor;
using UnityEngine;
using BodyPart = FantaLandia.Characters.Body.BodyPart;

namespace FantaLandia.EditorTools
{
    /// <summary>
    /// Genera i modelli umani a blocchi (maschio e femmina in intimo) come prefab.
    /// Ogni parte del corpo è una mesh separata attaccata al suo giunto, con strati distinti:
    /// "Pelle" e "Intimo" (poi "Abbigliamento", "Protesi"...). Menu: FantaLandia > Crea modelli personaggi.
    /// </summary>
    public static class BlockyCharacterBuilder
    {
        public const string Folder = "Assets/_Project/Generated/Characters";

        /// <summary>Ingrandimento della testa rispetto alle proporzioni reali.</summary>
        public const float HeadScale = 1.28f;

        /// <summary>Proporzioni e colori di un modello. Misure in metri.</summary>
        public class Shape
        {
            public string id, displayName;
            public bool female;

            public float ankleH, shinLen, thighLen, hipJointDrop, hipJointX;
            public float thighW, shinW, footW, footLen;
            public float pelvisW, pelvisD, pelvisH;
            public float absW, absD, absH;
            public float chestW, chestD, chestH;
            public float shoulderX, shoulderDrop;
            public float upperArmLen, upperArmW, forearmLen, forearmW, handLen;
            public float neckW, neckH;
            public float headW, headH, headD;

            public Color skin, hair, lips, pupil, underwear, trim;

            // aspetto (personaggi della storia)
            public HairStyle hairStyle = HairStyle.Default;
            public BeardStyle beard = BeardStyle.Default;
            [Tooltip("Pancia: 0 piatta, 1 bella tonda")]
            public float belly;
            public Outfit outfit;

            public float Height => ankleH + shinLen + thighLen + hipJointDrop + pelvisH * 0.5f + absH + chestH + neckH + headH;
        }

        public static Shape Male() => new Shape
        {
            id = "Umano_Maschio", displayName = "Umano maschio", female = false,
            ankleH = 0.08f, shinLen = 0.41f, thighLen = 0.41f, hipJointDrop = 0.05f, hipJointX = 0.095f,
            thighW = 0.15f, shinW = 0.12f, footW = 0.10f, footLen = 0.25f,
            pelvisW = 0.33f, pelvisD = 0.20f, pelvisH = 0.20f,
            absW = 0.30f, absD = 0.18f, absH = 0.20f,
            chestW = 0.40f, chestD = 0.22f, chestH = 0.28f,
            shoulderX = 0.245f, shoulderDrop = 0.05f,
            upperArmLen = 0.29f, upperArmW = 0.105f, forearmLen = 0.26f, forearmW = 0.09f, handLen = 0.10f,
            neckW = 0.11f, neckH = 0.08f,
            headW = 0.19f, headH = 0.20f, headD = 0.21f,
            skin = new Color(0.80f, 0.62f, 0.47f), hair = new Color(0.20f, 0.13f, 0.08f),
            lips = new Color(0.62f, 0.40f, 0.33f), pupil = new Color(0.25f, 0.17f, 0.10f),
            underwear = new Color(0.16f, 0.22f, 0.40f), trim = new Color(0.85f, 0.85f, 0.85f),
        };

        public static Shape Female() => new Shape
        {
            id = "Umano_Femmina", displayName = "Umana femmina", female = true,
            ankleH = 0.075f, shinLen = 0.38f, thighLen = 0.39f, hipJointDrop = 0.05f, hipJointX = 0.095f,
            thighW = 0.145f, shinW = 0.11f, footW = 0.09f, footLen = 0.22f,
            pelvisW = 0.36f, pelvisD = 0.20f, pelvisH = 0.20f,
            absW = 0.27f, absD = 0.17f, absH = 0.19f,
            chestW = 0.33f, chestD = 0.19f, chestH = 0.25f,
            shoulderX = 0.20f, shoulderDrop = 0.05f,
            upperArmLen = 0.27f, upperArmW = 0.085f, forearmLen = 0.24f, forearmW = 0.075f, handLen = 0.09f,
            neckW = 0.09f, neckH = 0.075f,
            headW = 0.175f, headH = 0.19f, headD = 0.195f,
            skin = new Color(0.93f, 0.76f, 0.64f), hair = new Color(0.55f, 0.25f, 0.10f),
            lips = new Color(0.78f, 0.36f, 0.40f), pupil = new Color(0.22f, 0.52f, 0.36f),
            underwear = new Color(0.55f, 0.12f, 0.18f), trim = new Color(0.25f, 0.05f, 0.08f),
        };

        // ------------------------------------------------------------------ aspetto: capelli, barba, vestiti

        public enum HairStyle { Default, Short, Buzz, Long, Mullet, Curly, Bob, Ponytail, Bald, Spiky }
        public enum BeardStyle { Default, None, Stubble, Full, Mustache }
        public enum Top { None, TShirt, LongSleeve, Hoodie, Flannel, Tank, Jersey, Cardigan, Robe }
        public enum Bottom { None, Jeans, Shorts, Slacks, Sweatpants }
        public enum Shoes { None, Sneakers, HighTops, Slippers, Loafers }
        public enum Hat { None, CapBackwards, Cap }

        /// <summary>Vestiti del personaggio (strato "Abbigliamento" sopra pelle e intimo).</summary>
        public class Outfit
        {
            public Top top = Top.None;
            public Color topColor = Color.white, topColor2 = Color.white;
            public Bottom bottom = Bottom.None;
            public Color bottomColor = new Color(0.2f, 0.3f, 0.5f);
            public Shoes shoes = Shoes.None;
            public Color shoeColor = Color.white, shoeColor2 = Color.white;
            public Hat hat = Hat.None;
            public Color hatColor = Color.red;
            public bool glasses;
            public Color glassesColor = new Color(0.1f, 0.1f, 0.1f);

            public bool LongSleeves => top == Top.LongSleeve || top == Top.Hoodie || top == Top.Flannel || top == Top.Cardigan || top == Top.Robe;
            public bool ShortSleeves => top == Top.TShirt || top == Top.Jersey;
            public bool LongPants => bottom == Bottom.Jeans || bottom == Bottom.Slacks || bottom == Bottom.Sweatpants;
        }

        static Material Cloth(Color c) => EditorAssetUtil.Mat("Stoffa " + ColorUtility.ToHtmlStringRGB(c), c, 0.12f);
        static Material Cloth(Color c, float darken) => Cloth(new Color(c.r * darken, c.g * darken, c.b * darken));

        /// <summary>Capelli secondo lo stile (Default: lunghi per lei, corti con ciuffo per lui).</summary>
        static void Hair(Shape s, Mats m, BoxMesh b, FaceParts f)
        {
            float W = s.headW, H = s.headH, D = s.headD, face = D * 0.5f;
            HairStyle style = s.hairStyle != HairStyle.Default ? s.hairStyle : (s.female ? HairStyle.Long : HairStyle.Short);
            Material h = m.hair;
            switch (style)
            {
                case HairStyle.Short:
                case HairStyle.Mullet:
                case HairStyle.Spiky:
                    b.AddRounded(new Vector3(0f, H + 0.008f, -0.004f), new Vector3(W + 0.024f, 0.055f, D + 0.024f), h, 0.018f);
                    if (style != HairStyle.Spiky) b.AddRounded(new Vector3(W * 0.08f, H + 0.03f, D * 0.3f), new Vector3(W * 0.7f, 0.04f, 0.06f), h, 0.012f);
                    b.AddRounded(new Vector3(0f, H * 0.72f, -face - 0.009f), new Vector3(W + 0.024f, H * 0.55f, 0.03f), h, 0.01f);
                    for (int c = -1; c <= 1; c += 2)
                    {
                        b.Add(new Vector3(c * (W * 0.5f + 0.007f), H * 0.83f, -0.025f), new Vector3(0.014f, H * 0.32f, D * 0.6f), h);
                        b.Add(new Vector3(c * (W * 0.5f + 0.004f), H * 0.55f, 0.02f), new Vector3(0.012f, H * 0.18f, 0.02f), h); // basette
                    }
                    if (style == HairStyle.Mullet) // corto davanti, lungo dietro: anni '90 puri (la parte lunga oscilla)
                        f.Dyn(new Vector3(0f, H * 0.75f, -face - 0.014f))
                         .AddRounded(new Vector3(0f, H * 0.3f, -face - 0.014f), new Vector3(W * 0.9f, H * 1.0f, 0.04f), h, 0.012f);
                    if (style == HairStyle.Spiky)
                        for (int x = -2; x <= 2; x++)
                            for (int z = -1; z <= 1; z++)
                                b.Add(new Vector3(x * W * 0.18f, H + 0.05f + ((x + z) & 1) * 0.015f, z * D * 0.28f), new Vector3(0.028f, 0.05f, 0.028f), h);
                    break;

                case HairStyle.Buzz:
                    b.AddRounded(new Vector3(0f, H * 0.82f + 0.004f, -0.004f), new Vector3(W + 0.012f, H * 0.38f, D + 0.012f), h, 0.016f);
                    break;

                case HairStyle.Bald: // corona di capelli ai lati e dietro
                    b.AddRounded(new Vector3(0f, H * 0.6f, -face - 0.006f), new Vector3(W + 0.014f, H * 0.3f, 0.02f), h, 0.006f);
                    for (int c = -1; c <= 1; c += 2)
                        b.Add(new Vector3(c * (W * 0.5f + 0.006f), H * 0.6f, -0.03f), new Vector3(0.014f, H * 0.28f, D * 0.6f), h);
                    break;

                case HairStyle.Long:
                    b.AddRounded(new Vector3(0f, H + 0.01f, -0.005f), new Vector3(W + 0.03f, 0.06f, D + 0.03f), h, 0.022f);
                    b.AddRounded(new Vector3(0f, H * 0.9f, -0.02f), new Vector3(W + 0.036f, H * 0.32f, D), h, 0.02f);
                    b.AddRounded(new Vector3(-W * 0.12f, H * 0.86f, face + 0.004f), new Vector3(W * 0.62f, 0.035f, 0.02f), h, 0.008f);
                    b.AddRounded(new Vector3(W * 0.24f, H * 0.9f, face + 0.002f), new Vector3(W * 0.4f, 0.028f, 0.018f), h, 0.008f);
                    BoxMesh longHair = f.Dyn(new Vector3(0f, H * 0.95f, -face - 0.012f));
                    longHair.AddRounded(new Vector3(0f, H * 0.3f, -face - 0.012f), new Vector3(W + 0.05f, H * 1.6f, 0.055f), h, 0.02f);
                    for (int c = -1; c <= 1; c += 2)
                    {
                        b.AddRounded(new Vector3(c * (W * 0.5f + 0.014f), H * 0.42f, 0f), new Vector3(0.026f, H * 1.05f, D * 0.75f), h, 0.01f);
                        b.AddRounded(new Vector3(c * (W * 0.5f + 0.01f), H * 0.0f, D * 0.2f), new Vector3(0.03f, H * 0.7f, 0.04f), h, 0.01f);
                    }
                    break;

                case HairStyle.Curly: // permanente vaporosa
                    b.AddRounded(new Vector3(0f, H + 0.025f, -0.01f), new Vector3(W + 0.08f, 0.12f, D + 0.07f), h, 0.03f);
                    f.Dyn(new Vector3(0f, H * 0.95f, -face - 0.025f))
                     .AddRounded(new Vector3(0f, H * 0.55f, -face - 0.025f), new Vector3(W + 0.09f, H * 1.0f, 0.07f), h, 0.025f);
                    for (int c = -1; c <= 1; c += 2)
                        b.AddRounded(new Vector3(c * (W * 0.5f + 0.03f), H * 0.6f, -0.01f), new Vector3(0.055f, H * 0.85f, D * 0.85f), h, 0.02f);
                    b.AddRounded(new Vector3(0f, H * 0.9f, face + 0.006f), new Vector3(W * 0.85f, 0.04f, 0.025f), h, 0.01f);
                    break;

                case HairStyle.Bob:
                    b.AddRounded(new Vector3(0f, H + 0.012f, -0.005f), new Vector3(W + 0.034f, 0.065f, D + 0.034f), h, 0.022f);
                    f.Dyn(new Vector3(0f, H * 0.92f, -face - 0.012f))
                     .AddRounded(new Vector3(0f, H * 0.55f, -face - 0.012f), new Vector3(W + 0.04f, H * 0.8f, 0.045f), h, 0.015f);
                    for (int c = -1; c <= 1; c += 2)
                        b.AddRounded(new Vector3(c * (W * 0.5f + 0.016f), H * 0.55f, 0.005f), new Vector3(0.03f, H * 0.8f, D * 0.85f), h, 0.01f);
                    b.AddRounded(new Vector3(0f, H * 0.88f, face + 0.005f), new Vector3(W * 0.95f, 0.04f, 0.02f), h, 0.008f);
                    break;

                case HairStyle.Ponytail:
                    b.AddRounded(new Vector3(0f, H + 0.006f, -0.004f), new Vector3(W + 0.02f, 0.05f, D + 0.02f), h, 0.018f);
                    b.AddRounded(new Vector3(0f, H * 0.75f, -face - 0.008f), new Vector3(W + 0.02f, H * 0.5f, 0.025f), h, 0.01f);
                    BoxMesh tail = f.Dyn(new Vector3(0f, H * 0.68f, -face - 0.04f));
                    tail.AddRounded(new Vector3(0f, H * 0.45f, -face - 0.05f), new Vector3(0.05f, 0.24f, 0.05f), h, 0.012f);
                    tail.Add(new Vector3(0f, H * 0.68f, -face - 0.035f), new Vector3(0.04f, 0.02f, 0.03f), m.lips);
                    break;
            }
        }

        // --- vestiti per parte del corpo (null = niente su quella parte)

        static BoxMesh ClothChest(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || o.top == Top.None) return null;
            var b = new BoxMesh();
            Material c1 = Cloth(o.topColor), c2 = Cloth(o.topColor2);
            float W = s.chestW, D = s.chestD, H = s.chestH, t = 0.022f;
            b.Add(new Vector3(0f, H * 0.2f, 0f), new Vector3(W * 0.85f + 2f * t, H * 0.4f + 0.002f, D * 0.95f + 2f * t), c1);
            b.AddRounded(new Vector3(0f, H * 0.7f, 0f), new Vector3(W + 2f * t, H * 0.6f, D + 2f * t), c1, 0.025f);
            b.Add(new Vector3(0f, H + 0.01f, -0.01f), new Vector3(W * 0.55f + 2f * t, 0.04f + t, D * 0.6f + 2f * t), c1);
            if (s.female)
                for (int c = -1; c <= 1; c += 2)
                    b.AddRounded(new Vector3(c * 0.068f, H * 0.5f, D * 0.5f + 0.03f), new Vector3(0.14f, 0.125f, 0.085f), c1, 0.018f);
            float front = D * 0.5f + t + 0.002f;

            switch (o.top)
            {
                case Top.TShirt:
                    b.Add(new Vector3(0f, H * 0.62f, front + (s.female ? 0.04f : 0f)), new Vector3(0.12f, 0.08f, 0.004f), c2); // stampa
                    break;
                case Top.Jersey: // numero davanti e dietro
                    foreach (float z in new[] { front, -front })
                        for (int d = 0; d < 2; d++)
                        {
                            float x = (d - 0.5f) * 0.07f;
                            b.Add(new Vector3(x, H * 0.55f, z), new Vector3(0.012f, 0.09f, 0.004f), c2);
                            b.Add(new Vector3(x + 0.02f, H * 0.55f + 0.045f, z), new Vector3(0.04f, 0.012f, 0.004f), c2);
                        }
                    break;
                case Top.Flannel: // quadri: strisce verticali e orizzontali, bottoni
                    for (float x = -W * 0.5f; x <= W * 0.5f; x += 0.07f)
                    {
                        b.Add(new Vector3(x, H * 0.5f, front), new Vector3(0.018f, H, 0.003f), c2);
                        b.Add(new Vector3(x, H * 0.5f, -front), new Vector3(0.018f, H, 0.003f), c2);
                    }
                    for (float y = 0.05f; y < H; y += 0.08f)
                    {
                        b.Add(new Vector3(0f, y, front + 0.001f), new Vector3(W + 2f * t, 0.016f, 0.003f), c2);
                        b.Add(new Vector3(0f, y, -front - 0.001f), new Vector3(W + 2f * t, 0.016f, 0.003f), c2);
                    }
                    for (float y = 0.04f; y < H - 0.02f; y += 0.06f) b.Add(new Vector3(0f, y, front + 0.004f), new Vector3(0.012f, 0.012f, 0.004f), Cloth(o.topColor2, 0.6f));
                    b.Add(new Vector3(-0.03f, H - 0.01f, D * 0.35f), new Vector3(0.04f, 0.03f, 0.05f), c1); // colletto
                    b.Add(new Vector3(0.03f, H - 0.01f, D * 0.35f), new Vector3(0.04f, 0.03f, 0.05f), c1);
                    break;
                case Top.Hoodie: // cappuccio dietro al collo e lacci
                    b.AddRounded(new Vector3(0f, H + 0.02f, -D * 0.35f), new Vector3(W * 0.6f, 0.09f, 0.1f), c1, 0.02f);
                    for (int c = -1; c <= 1; c += 2) b.Add(new Vector3(c * 0.03f, H * 0.8f, front + 0.002f), new Vector3(0.008f, 0.11f, 0.004f), c2);
                    break;
                case Top.Cardigan: // aperto davanti: maglia chiara sotto, bottoni
                    b.Add(new Vector3(0f, H * 0.5f, front), new Vector3(0.07f, H, 0.004f), c2);
                    for (float y = 0.04f; y < H; y += 0.07f) b.Add(new Vector3(0.05f, y, front + 0.002f), new Vector3(0.012f, 0.012f, 0.004f), Cloth(o.topColor, 0.6f));
                    break;
                case Top.Robe: // vestaglia: risvolti a V
                    for (int i = 0; i < 4; i++)
                    {
                        float y = H - i * 0.05f, x = 0.015f + i * 0.012f;
                        b.Add(new Vector3(-x, y - 0.02f, front), new Vector3(0.025f, 0.05f, 0.004f), c2);
                        b.Add(new Vector3(x, y - 0.02f, front), new Vector3(0.025f, 0.05f, 0.004f), c2);
                    }
                    break;
            }
            return b;
        }

        static BoxMesh ClothAbdomen(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || o.top == Top.None) return null;
            var b = new BoxMesh();
            Material c1 = Cloth(o.topColor), c2 = Cloth(o.topColor2);
            float W = s.absW, D = s.absD, H = s.absH, t = 0.02f;
            b.Add(new Vector3(0f, H * 0.5f, 0f), new Vector3(W + 0.024f + 2f * t, H + 0.004f, D + 2f * t), c1);
            if (s.belly > 0f) b.AddRounded(new Vector3(0f, H * 0.42f, D * 0.5f + s.belly * 0.035f), new Vector3(W * 0.8f + 2f * t, H * 0.75f + t, s.belly * 0.07f + 2f * t), c1, 0.02f);
            float front = D * 0.5f + t + 0.002f + (s.belly > 0f ? s.belly * 0.07f : 0f);
            switch (o.top)
            {
                case Top.Hoodie: b.Add(new Vector3(0f, H * 0.3f, front), new Vector3(W * 0.6f, H * 0.35f, 0.01f), Cloth(o.topColor, 0.8f)); break; // tasca
                case Top.Flannel:
                    for (float x = -W * 0.5f; x <= W * 0.5f; x += 0.07f) b.Add(new Vector3(x, H * 0.5f, front), new Vector3(0.018f, H, 0.003f), c2);
                    for (float y = 0.03f; y < H; y += 0.06f) b.Add(new Vector3(0f, y, front + 0.003f), new Vector3(0.012f, 0.012f, 0.004f), Cloth(o.topColor2, 0.6f));
                    break;
                case Top.Cardigan: b.Add(new Vector3(0f, H * 0.5f, front), new Vector3(0.07f, H, 0.004f), c2); break;
                case Top.Robe: b.Add(new Vector3(0f, H * 0.4f, 0f), new Vector3(W + 2f * t + 0.02f, 0.04f, D + 2f * t + 0.02f + (s.belly * 0.14f)), c2); break; // cintura
            }
            return b;
        }

        static BoxMesh ClothPelvis(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null) return null;
            var b = new BoxMesh();
            float W = s.pelvisW, D = s.pelvisD, H = s.pelvisH;
            if (o.bottom != Bottom.None)
            {
                Material p = Cloth(o.bottomColor);
                b.AddRounded(Vector3.zero, new Vector3(W + 0.03f, H, D + 0.03f), p, 0.02f);
                b.Add(new Vector3(0f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.9f + 0.03f, H * 0.6f + 0.03f, 0.065f), p); // glutei
                if (!s.female) b.Add(new Vector3(0f, -0.065f, D * 0.5f + 0.012f), new Vector3(0.1f, 0.08f, 0.045f), p);
                if (o.bottom == Bottom.Jeans || o.bottom == Bottom.Slacks)
                {
                    b.Add(new Vector3(0f, H * 0.5f - 0.015f, 0f), new Vector3(W + 0.036f, 0.03f, D + 0.036f), Cloth(new Color(0.23f, 0.15f, 0.09f))); // cintura
                    b.Add(new Vector3(0f, H * 0.5f - 0.015f, D * 0.5f + 0.02f), new Vector3(0.035f, 0.026f, 0.008f), Cloth(new Color(0.75f, 0.75f, 0.72f)));
                }
                if (o.bottom == Bottom.Jeans)
                    for (int c = -1; c <= 1; c += 2) // tasche dietro
                        b.Add(new Vector3(c * W * 0.22f, -0.01f, -D * 0.5f - 0.046f), new Vector3(W * 0.3f, H * 0.35f, 0.004f), Cloth(o.bottomColor, 0.85f));
            }
            // maglia fuori dai pantaloni (o vestaglia lunga)
            if (o.top != Top.None && o.top != Top.Tank)
            {
                Material c1 = Cloth(o.topColor);
                bool robe = o.top == Top.Robe;
                float hem = robe ? H : 0.07f;
                b.Add(new Vector3(0f, H * 0.5f - hem * 0.5f + 0.01f, 0f), new Vector3(W + 0.05f, hem, D + 0.05f + (s.female ? 0f : 0.02f)), c1);
            }
            return b.IsEmpty ? null : b;
        }

        static BoxMesh ClothUpperArm(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || !(o.LongSleeves || o.ShortSleeves)) return null;
            var b = new BoxMesh();
            Material c1 = Cloth(o.topColor);
            float L = s.upperArmLen, w = s.upperArmW;
            float len = o.LongSleeves ? L : L * 0.55f;
            b.Add(new Vector3(0f, -len * 0.5f, 0f), new Vector3(w + 0.05f, len, w + 0.05f), c1);
            b.Add(new Vector3(0f, -0.035f, 0f), new Vector3(w + 0.06f, 0.1f, w + 0.05f), c1);
            if (o.top == Top.Jersey) b.Add(new Vector3(0f, -len + 0.01f, 0f), new Vector3(w + 0.056f, 0.02f, w + 0.056f), Cloth(o.topColor2));
            if (o.top == Top.Flannel)
                for (float y = -0.05f; y > -len; y -= 0.08f) b.Add(new Vector3(0f, y, 0f), new Vector3(w + 0.054f, 0.016f, w + 0.054f), Cloth(o.topColor2));
            return b;
        }

        static BoxMesh ClothForearm(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || !o.LongSleeves) return null;
            var b = new BoxMesh();
            Material c1 = Cloth(o.topColor);
            float L = s.forearmLen, w = s.forearmW;
            b.Add(new Vector3(0f, -L * 0.275f, 0f), new Vector3(w + 0.045f, L * 0.55f + 0.01f, w + 0.045f), c1);
            b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.8f + 0.045f, L * 0.5f, w * 0.8f + 0.045f), c1);
            b.Add(new Vector3(0f, -0.02f, -w * 0.5f - 0.01f), new Vector3(w * 0.5f + 0.02f, 0.05f, 0.03f), c1);
            Material cuff = o.top == Top.Hoodie || o.top == Top.Cardigan ? Cloth(o.topColor, 0.8f) : c1;
            b.Add(new Vector3(0f, -L + 0.015f, 0f), new Vector3(w * 0.8f + 0.05f, 0.03f, w * 0.8f + 0.05f), cuff);
            if (o.top == Top.Flannel)
                for (float y = -0.04f; y > -L; y -= 0.08f) b.Add(new Vector3(0f, y, 0f), new Vector3(w + 0.049f, 0.016f, w + 0.049f), Cloth(o.topColor2));
            return b;
        }

        static BoxMesh ClothThigh(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null) return null;
            var b = new BoxMesh();
            float L = s.thighLen, w = s.thighW;
            if (o.bottom != Bottom.None)
            {
                Material p = Cloth(o.bottomColor);
                if (o.bottom == Bottom.Shorts)
                    b.Add(new Vector3(0f, -L * 0.35f + 0.015f, 0f), new Vector3(w + 0.045f, L * 0.7f + 0.03f, w + 0.045f), p);
                else
                {
                    b.Add(new Vector3(0f, -L * 0.3f + 0.015f, 0f), new Vector3(w + 0.04f, L * 0.6f + 0.03f, w + 0.04f), p);
                    b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.85f + 0.04f, L * 0.5f, w * 0.85f + 0.04f), p);
                }
            }
            if (o.top == Top.Robe) // la vestaglia scende davanti e dietro le cosce
            {
                Material r = Cloth(o.topColor);
                b.Add(new Vector3(0f, -L * 0.3f, w * 0.5f + 0.03f), new Vector3(w + 0.06f, L * 0.62f, 0.02f), r);
                b.Add(new Vector3(0f, -L * 0.3f, -w * 0.5f - 0.03f), new Vector3(w + 0.06f, L * 0.62f, 0.02f), r);
            }
            return b.IsEmpty ? null : b;
        }

        static BoxMesh ClothShin(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || !o.LongPants) return null;
            var b = new BoxMesh();
            Material p = Cloth(o.bottomColor);
            float L = s.shinLen, w = s.shinW;
            b.Add(new Vector3(0f, -L * 0.25f, 0f), new Vector3(w + 0.045f, L * 0.5f, w + 0.045f), p);
            b.Add(new Vector3(0f, -L * 0.72f, 0f), new Vector3(w * 0.75f + 0.055f, L * 0.56f, w * 0.75f + 0.055f), p);
            b.Add(new Vector3(0f, -L * 0.3f, -w * 0.5f - 0.012f), new Vector3(w * 0.7f + 0.02f, L * 0.3f, 0.032f), p); // polpaccio
            b.AddRounded(new Vector3(0f, -0.005f, 0.004f), new Vector3(w * 1.04f + 0.05f, 0.11f, w * 1.04f + 0.05f), p, 0.022f); // ginocchio
            if (o.bottom == Bottom.Sweatpants) b.Add(new Vector3(0f, -L + 0.03f, 0f), new Vector3(w * 0.75f + 0.06f, 0.04f, w * 0.75f + 0.06f), Cloth(o.bottomColor, 0.8f));
            return b;
        }

        static BoxMesh ClothFoot(Shape s, int side)
        {
            Outfit o = s.outfit;
            if (o == null || o.shoes == Shoes.None) return null;
            var b = new BoxMesh();
            Material c1 = Cloth(o.shoeColor), c2 = Cloth(o.shoeColor2);
            float A = s.ankleH, fw = s.footW, fl = s.footLen;
            float zMin = -0.06f, zEnd = zMin + fl;
            float zc = (zMin + zEnd) * 0.5f, len = zEnd - zMin;
            switch (o.shoes)
            {
                case Shoes.Sneakers:
                case Shoes.HighTops:
                    b.AddRounded(new Vector3(0f, -A * 0.5f, zc), new Vector3(fw + 0.03f, A * 0.9f + 0.01f, len + 0.03f), c1, 0.01f);
                    b.Add(new Vector3(0f, -A + 0.01f, zc), new Vector3(fw + 0.036f, 0.022f, len + 0.036f), c2);        // suola
                    b.Add(new Vector3(0f, -A * 0.08f, zc + len * 0.15f), new Vector3(fw * 0.5f, 0.012f, len * 0.35f), c2); // lacci
                    b.Add(new Vector3(side * (fw * 0.5f + 0.016f), -A * 0.45f, zc), new Vector3(0.004f, A * 0.3f, len * 0.5f), c2); // baffo laterale
                    if (o.shoes == Shoes.HighTops)
                        b.Add(new Vector3(0f, A * 0.15f, 0f), new Vector3(s.shinW * 0.75f + 0.05f, A * 1.1f, s.shinW * 0.75f + 0.05f), c1);
                    break;
                case Shoes.Slippers:
                    b.AddRounded(new Vector3(0f, -A * 0.55f, zc), new Vector3(fw + 0.05f, A * 0.8f, len + 0.04f), c1, 0.02f);
                    break;
                case Shoes.Loafers:
                    b.AddRounded(new Vector3(0f, -A * 0.55f, zc), new Vector3(fw + 0.026f, A * 0.8f, len + 0.03f), c1, 0.012f);
                    b.Add(new Vector3(0f, -A + 0.008f, zc), new Vector3(fw + 0.03f, 0.016f, len + 0.034f), c2);
                    break;
            }
            return b;
        }

        /// <summary>Cappello e occhiali (vanno sotto la testa e vengono ingranditi come lei).</summary>
        static BoxMesh ClothHead(Shape s)
        {
            Outfit o = s.outfit;
            if (o == null || (o.hat == Hat.None && !o.glasses)) return null;
            var b = new BoxMesh();
            float W = s.headW, H = s.headH, D = s.headD, face = D * 0.5f;
            if (o.hat != Hat.None)
            {
                Material c = Cloth(o.hatColor);
                b.AddRounded(new Vector3(0f, H + 0.028f, 0f), new Vector3(W + 0.045f, 0.075f, D + 0.045f), c, 0.025f);
                float bz = o.hat == Hat.CapBackwards ? -face - 0.055f : face + 0.055f;
                b.Add(new Vector3(0f, H - 0.004f, bz), new Vector3(W * 0.62f, 0.014f, 0.1f), c);
                b.Add(new Vector3(0f, H + 0.07f, 0f), new Vector3(0.02f, 0.012f, 0.02f), Cloth(o.hatColor, 0.7f));
            }
            if (o.glasses)
            {
                Material g = Cloth(o.glassesColor);
                float ex = W * (s.female ? 0.23f : 0.24f), ey = H * 0.55f, z = face + 0.014f;
                for (int c = -1; c <= 1; c += 2)
                {
                    float x = c * ex;
                    b.Add(new Vector3(x, ey + 0.019f, z), new Vector3(0.054f, 0.007f, 0.006f), g);
                    b.Add(new Vector3(x, ey - 0.018f, z), new Vector3(0.054f, 0.007f, 0.006f), g);
                    b.Add(new Vector3(x - 0.026f, ey, z), new Vector3(0.006f, 0.04f, 0.006f), g);
                    b.Add(new Vector3(x + 0.026f, ey, z), new Vector3(0.006f, 0.04f, 0.006f), g);
                    b.Add(new Vector3(c * (W * 0.5f + 0.006f), ey + 0.01f, z * 0.45f), new Vector3(0.006f, 0.006f, D * 0.55f), g); // stanghette
                }
                b.Add(new Vector3(0f, ey + 0.012f, z), new Vector3(0.02f, 0.006f, 0.006f), g); // ponte
            }
            return b;
        }

        class Mats
        {
            public Material skin, shade, hair, lips, lipsLower, eyeWhite, pupil, dark, lash, brow, blush, stubble, mouth, under, trim;
        }

        [MenuItem("FantaLandia/Crea modelli personaggi")]
        public static void BuildAll()
        {
            BuildMale();
            BuildFemale();
            Debug.Log("[FantaLandia] Modelli creati in " + Folder);
        }

        public static GameObject BuildMale() => Build(Male());
        public static GameObject BuildFemale() => Build(Female());

        // ------------------------------------------------------------------ costruzione

        public static GameObject Build(Shape s)
        {
            EditorAssetUtil.EnsureFolder(Folder);
            var m = new Mats
            {
                skin = EditorAssetUtil.Mat($"{s.id} Pelle", s.skin),
                shade = EditorAssetUtil.Mat($"{s.id} Pelle ombra", s.skin * 0.86f),
                hair = EditorAssetUtil.Mat($"{s.id} Capelli", s.hair, 0.25f),
                lips = EditorAssetUtil.Mat($"{s.id} Labbra", s.lips),
                eyeWhite = EditorAssetUtil.Mat("Occhi bianco", new Color(0.95f, 0.95f, 0.92f), 0.5f),
                pupil = EditorAssetUtil.Mat($"{s.id} Iride", s.pupil, 0.6f),
                under = EditorAssetUtil.Mat($"{s.id} Intimo", s.underwear, 0.2f),
                trim = EditorAssetUtil.Mat($"{s.id} Intimo bordo", s.trim, 0.2f),
                lipsLower = EditorAssetUtil.Mat($"{s.id} Labbro inferiore", Color.Lerp(s.lips, Color.white, 0.12f), 0.35f),
                dark = EditorAssetUtil.Mat("Pupilla", new Color(0.03f, 0.03f, 0.04f), 0.8f),
                mouth = EditorAssetUtil.Mat("Interno bocca", new Color(0.25f, 0.07f, 0.07f)),
                lash = EditorAssetUtil.Mat("Ciglia", new Color(0.07f, 0.05f, 0.05f)),
                brow = EditorAssetUtil.Mat($"{s.id} Sopracciglia", s.hair * 0.75f),
                blush = EditorAssetUtil.Mat($"{s.id} Guance", Color.Lerp(s.skin, new Color(0.95f, 0.5f, 0.5f), 0.35f)),
                stubble = EditorAssetUtil.Mat($"{s.id} Barba", Color.Lerp(s.hair, s.skin, 0.35f)),
            };

            string meshPath = $"{Folder}/{s.id}_Mesh.asset";
            string prefabPath = $"{Folder}/{s.id}.prefab";
            var meshes = new List<Mesh>();

            var root = new GameObject(s.displayName);
            var rig = root.AddComponent<BodyRig>();
            rig.joints = new Transform[BodyRig.PartCount];
            rig.height = s.Height;
            rig.female = s.female;
            root.AddComponent<ProceduralBodyAnimator>();

            // --- scheletro
            float hipsY = s.ankleH + s.shinLen + s.thighLen + s.hipJointDrop;
            Transform hips = Joint(rig, BodyPart.Pelvis, root.transform, new Vector3(0f, hipsY, 0f));
            Transform spine = Joint(rig, BodyPart.Abdomen, hips, new Vector3(0f, s.pelvisH * 0.5f, 0f));
            Transform chest = Joint(rig, BodyPart.Chest, spine, new Vector3(0f, s.absH, 0f));
            Transform neck = Joint(rig, BodyPart.Neck, chest, new Vector3(0f, s.chestH, 0f));
            Transform head = Joint(rig, BodyPart.Head, neck, new Vector3(0f, s.neckH, 0f));

            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? -1 : 1; // -1 = sinistra (-X), +1 = destra
                BodyPart upper = side < 0 ? BodyPart.UpperArmL : BodyPart.UpperArmR;
                BodyPart fore = side < 0 ? BodyPart.ForearmL : BodyPart.ForearmR;
                BodyPart hand = side < 0 ? BodyPart.HandL : BodyPart.HandR;
                BodyPart thigh = side < 0 ? BodyPart.ThighL : BodyPart.ThighR;
                BodyPart shin = side < 0 ? BodyPart.ShinL : BodyPart.ShinR;
                BodyPart foot = side < 0 ? BodyPart.FootL : BodyPart.FootR;

                Transform shoulder = Joint(rig, upper, chest, new Vector3(side * s.shoulderX, s.chestH - s.shoulderDrop, 0f));
                Transform elbow = Joint(rig, fore, shoulder, new Vector3(0f, -s.upperArmLen, 0f));
                Transform wrist = Joint(rig, hand, elbow, new Vector3(0f, -s.forearmLen, 0f));
                Transform hip = Joint(rig, thigh, hips, new Vector3(side * s.hipJointX, -s.hipJointDrop, 0f));
                Transform knee = Joint(rig, shin, hip, new Vector3(0f, -s.thighLen, 0f));
                Transform ankle = Joint(rig, foot, knee, new Vector3(0f, -s.shinLen, 0f));

                Layers(shoulder, UpperArm(s, m), null, meshes, $"{s.id} {upper}", ClothUpperArm(s));
                Layers(elbow, Forearm(s, m), null, meshes, $"{s.id} {fore}", ClothForearm(s));
                Layers(wrist, Hand(s, m), null, meshes, $"{s.id} {hand}");
                Layers(hip, Thigh(s, m), null, meshes, $"{s.id} {thigh}", ClothThigh(s));
                Layers(knee, Shin(s, m), null, meshes, $"{s.id} {shin}", ClothShin(s));
                Layers(ankle, Foot(s, m, side), null, meshes, $"{s.id} {foot}", ClothFoot(s, side));
            }

            // --- busto e testa
            PelvisLayers(s, m, out BoxMesh pelvisSkin, out BoxMesh pelvisUnder);
            Layers(hips, pelvisSkin, pelvisUnder, meshes, $"{s.id} Pelvis", ClothPelvis(s));
            Layers(spine, Abdomen(s, m), null, meshes, $"{s.id} Abdomen", ClothAbdomen(s));
            ChestLayers(s, m, out BoxMesh chestSkin, out BoxMesh chestUnder);
            Layers(chest, chestSkin, chestUnder, meshes, $"{s.id} Chest", ClothChest(s));
            Layers(neck, Neck(s, m), null, meshes, $"{s.id} Neck");
            Layers(head, Head(s, m, out FaceParts faceParts), null, meshes, $"{s.id} Head", ClothHead(s));
            Transform headClothes = head.Find("Abbigliamento");
            if (headClothes != null) headClothes.localScale = Vector3.one * HeadScale;
            // Testa più grande (stile): ingrandisce viso e capelli insieme, partendo dal collo.
            Transform headSkin = head.Find("Pelle");
            if (headSkin != null)
            {
                headSkin.localScale = Vector3.one * HeadScale;
                BuildFace(root, headSkin, faceParts, meshes, s.id);
            }

            // --- salvataggio: tutte le mesh in un unico asset. Le mesh già esistenti vengono aggiornate al loro
            // posto (stesso nome), così i personaggi già piazzati nelle scene non le perdono.
            var existing = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(meshPath))
                if (o is Mesh em && !existing.ContainsKey(em.name)) existing[em.name] = em;
            bool fileExists = AssetDatabase.LoadMainAssetAtPath(meshPath) != null;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh nm = mf.sharedMesh;
                if (nm == null) continue;
                if (existing.TryGetValue(nm.name, out Mesh em))
                {
                    if (em == nm) continue;
                    EditorUtility.CopySerialized(nm, em);
                    em.name = nm.name;
                    EditorUtility.SetDirty(em);
                    mf.sharedMesh = em;
                }
                else
                {
                    if (!fileExists) { AssetDatabase.CreateAsset(nm, meshPath); fileExists = true; }
                    else AssetDatabase.AddObjectToAsset(nm, meshPath);
                    existing[nm.name] = nm;
                }
            }
            AssetDatabase.SaveAssets();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Transform Joint(BodyRig rig, BodyPart part, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(BodyRig.DisplayName(part)).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            rig.joints[(int)part] = t;
            return t;
        }

        static void Layers(Transform joint, BoxMesh skin, BoxMesh under, List<Mesh> meshes, string meshName, BoxMesh clothes = null)
        {
            Visual(joint, "Pelle", skin, meshes, meshName + " Pelle");
            Visual(joint, "Intimo", under, meshes, meshName + " Intimo");
            Visual(joint, "Abbigliamento", clothes, meshes, meshName + " Abbigliamento");
        }

        static void Visual(Transform joint, string layer, BoxMesh b, List<Mesh> meshes, string meshName)
        {
            if (b == null || b.IsEmpty) return;
            var go = new GameObject(layer);
            go.transform.SetParent(joint, false);
            Mesh mesh = b.Build(meshName);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = b.Materials;
        }

        // ------------------------------------------------------------------ parti del corpo (coordinate locali al giunto)

        static void PelvisLayers(Shape s, Mats m, out BoxMesh skin, out BoxMesh under)
        {
            skin = new BoxMesh();
            under = new BoxMesh();
            float W = s.pelvisW, D = s.pelvisD, H = s.pelvisH;

            skin.AddRounded(new Vector3(0f, 0f, 0f), new Vector3(W, H, D), m.skin, 0.02f);
            // glutei
            skin.Add(new Vector3(-W * 0.22f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.42f, H * 0.6f, 0.03f), m.skin);
            skin.Add(new Vector3(W * 0.22f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.42f, H * 0.6f, 0.03f), m.skin);

            // slip: copre il bacino dal basso fino a metà, con elastico in vita
            float briefsH = H * 0.65f, briefsY = -H * 0.5f + briefsH * 0.5f;
            under.Add(new Vector3(0f, briefsY, 0f), new Vector3(W + 0.014f, briefsH, D + 0.014f), m.under);
            under.Add(new Vector3(0f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.9f + 0.012f, H * 0.6f + 0.01f, 0.042f), m.under);
            under.Add(new Vector3(0f, briefsY + briefsH * 0.5f + 0.011f, 0f), new Vector3(W + 0.02f, s.female ? 0.016f : 0.022f, D + 0.02f), m.trim);
            if (s.female)
                under.Add(new Vector3(0f, briefsY + briefsH * 0.5f, D * 0.5f + 0.014f), new Vector3(0.03f, 0.02f, 0.01f), m.trim); // fiocchetto
            else
                under.Add(new Vector3(0f, -0.065f, D * 0.5f + 0.012f), new Vector3(0.08f, 0.06f, 0.025f), m.under);
        }

        static BoxMesh Abdomen(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float W = s.absW, D = s.absD, H = s.absH;
            b.Add(new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), m.skin);
            if (s.belly > 0f) b.AddRounded(new Vector3(0f, H * 0.42f, D * 0.5f + s.belly * 0.035f), new Vector3(W * 0.8f, H * 0.75f, s.belly * 0.07f), m.skin, 0.02f);
            b.Add(new Vector3(0f, 0.035f, D * 0.5f + 0.002f + s.belly * 0.07f), new Vector3(0.012f, 0.014f, 0.006f), m.shade); // ombelico
            if (!s.female)
            {
                for (int r = 0; r < 3; r++)
                    for (int c = -1; c <= 1; c += 2)
                        b.Add(new Vector3(c * 0.032f, 0.075f + r * 0.045f, D * 0.5f + 0.004f), new Vector3(0.05f, 0.038f, 0.01f), m.shade);
            }
            // fianchi
            b.Add(new Vector3(-W * 0.5f - 0.006f, H * 0.35f, 0f), new Vector3(0.012f, H * 0.5f, D * 0.8f), m.skin);
            b.Add(new Vector3(W * 0.5f + 0.006f, H * 0.35f, 0f), new Vector3(0.012f, H * 0.5f, D * 0.8f), m.skin);
            return b;
        }

        static void ChestLayers(Shape s, Mats m, out BoxMesh skin, out BoxMesh under)
        {
            skin = new BoxMesh();
            under = s.female ? new BoxMesh() : null;
            float W = s.chestW, D = s.chestD, H = s.chestH;

            skin.Add(new Vector3(0f, H * 0.2f, 0f), new Vector3(W * 0.85f, H * 0.4f, D * 0.95f), m.skin);  // costole
            skin.AddRounded(new Vector3(0f, H * 0.7f, 0f), new Vector3(W, H * 0.6f, D), m.skin, 0.025f); // spalle
            skin.Add(new Vector3(0f, H + 0.01f, -0.01f), new Vector3(W * 0.55f, 0.04f, D * 0.6f), m.skin); // trapezio
            skin.Add(new Vector3(0f, H * 0.62f, -D * 0.5f - 0.008f), new Vector3(W * 0.7f, H * 0.5f, 0.016f), m.skin); // scapole

            if (!s.female)
            {
                for (int c = -1; c <= 1; c += 2)
                {
                    skin.Add(new Vector3(c * 0.085f, H * 0.6f, D * 0.5f + 0.008f), new Vector3(0.15f, 0.09f, 0.02f), m.skin);         // pettorali
                    skin.Add(new Vector3(c * 0.085f, H * 0.6f - 0.04f, D * 0.5f + 0.02f), new Vector3(0.014f, 0.014f, 0.006f), m.shade); // capezzoli
                }
                return;
            }

            // seno e reggiseno
            float cupY = H * 0.5f, cupX = 0.068f;
            for (int c = -1; c <= 1; c += 2)
            {
                skin.AddRounded(new Vector3(c * cupX, cupY, D * 0.5f + 0.025f), new Vector3(0.115f, 0.10f, 0.06f), m.skin, 0.015f);
                under.AddRounded(new Vector3(c * cupX, cupY, D * 0.5f + 0.03f), new Vector3(0.125f, 0.11f, 0.066f), m.under, 0.016f); // coppe
                under.Add(new Vector3(c * 0.07f, (cupY + 0.055f + H) * 0.5f, D * 0.5f + 0.006f),
                          new Vector3(0.018f, H - cupY - 0.055f, 0.01f), m.under);                                              // spalline davanti
                under.Add(new Vector3(c * 0.07f, H * 0.62f, -D * 0.5f - 0.02f), new Vector3(0.018f, H * 0.55f, 0.008f), m.under); // spalline dietro
            }
            float bandY = cupY - 0.055f;
            under.Add(new Vector3(0f, bandY, 0f), new Vector3(W * 0.85f + 0.012f, 0.035f, D * 0.95f + 0.012f), m.under);   // fascia
            under.Add(new Vector3(0f, cupY, D * 0.5f + 0.035f), new Vector3(0.03f, 0.025f, 0.02f), m.trim);              // centro
            under.Add(new Vector3(0f, bandY, -D * 0.475f - 0.01f), new Vector3(0.04f, 0.03f, 0.01f), m.trim);           // gancio
        }

        static BoxMesh Neck(Shape s, Mats m)
        {
            var b = new BoxMesh();
            b.Add(new Vector3(0f, s.neckH * 0.5f, 0.005f), new Vector3(s.neckW, s.neckH + 0.03f, s.neckW * 0.95f), m.skin);
            if (!s.female)
                b.Add(new Vector3(0f, s.neckH * 0.5f, s.neckW * 0.475f + 0.01f), new Vector3(0.02f, 0.02f, 0.01f), m.skin); // pomo d'Adamo
            return b;
        }

        /// <summary>Parti mobili del viso (ognuna è una mesh con il suo perno), animate da FaceAnimator.</summary>
        class FaceParts
        {
            public BoxMesh irisL, irisR, lidL, lidR, browL, browR, lowerLip, mouthInside;
            public Vector3 irisLPos, irisRPos, lidLPos, lidRPos, browLPos, browRPos, lowerLipPos, mouthInsidePos;
            /// <summary>Capelli che pendono (lunghi, coda, mullet): oscillano con la fisica dei capelli.</summary>
            public BoxMesh hairDyn;
            public Vector3 hairDynPos;

            public BoxMesh Dyn(Vector3 pivot)
            {
                if (hairDyn == null) { hairDyn = new BoxMesh(pivot); hairDynPos = pivot; }
                return hairDyn;
            }
        }

        static BoxMesh Head(Shape s, Mats m, out FaceParts face)
        {
            return s.female ? FemaleHead(s, m, out face) : MaleHead(s, m, out face);
        }

        /// <summary>Viso femminile: lineamenti morbidi, occhi grandi con ciglia, labbra piene, guance rosate.</summary>
        static BoxMesh FemaleHead(Shape s, Mats m, out FaceParts f)
        {
            var b = new BoxMesh();
            f = new FaceParts();
            float W = s.headW, H = s.headH, D = s.headD;
            float face = D * 0.5f;
            float jawFront = 0.01f + D * 0.42f;

            b.AddRounded(new Vector3(0f, H * 0.64f, 0f), new Vector3(W, H * 0.72f, D), m.skin, 0.024f);              // cranio
            b.AddRounded(new Vector3(0f, H * 0.25f, 0.01f), new Vector3(W * 0.8f, H * 0.42f, D * 0.84f), m.skin, 0.022f); // guance e mascella
            b.AddRounded(new Vector3(0f, H * 0.075f, jawFront - 0.008f), new Vector3(W * 0.32f, H * 0.12f, 0.03f), m.skin, 0.008f); // mento
            for (int c = -1; c <= 1; c += 2)
                b.Add(new Vector3(c * W * 0.35f, H * 0.42f, face - 0.008f), new Vector3(0.035f, 0.028f, 0.02f), m.skin); // zigomi

            b.Add(new Vector3(0f, H * 0.5f, face + 0.005f), new Vector3(0.016f, 0.05f, 0.012f), m.skin);           // naso
            b.AddRounded(new Vector3(0f, H * 0.405f, face + 0.012f), new Vector3(0.026f, 0.018f, 0.022f), m.skin, 0.005f);

            float ey = H * 0.55f;
            for (int c = -1; c <= 1; c += 2)
            {
                float ex = c * W * 0.23f;
                b.Add(new Vector3(ex, ey, face + 0.001f), new Vector3(0.046f, 0.028f, 0.006f), m.eyeWhite);
                b.Add(new Vector3(ex, ey + 0.017f, face + 0.004f), new Vector3(0.052f, 0.008f, 0.01f), m.lash);            // ciglia
                b.Add(new Vector3(ex + c * 0.028f, ey + 0.021f, face + 0.003f), new Vector3(0.012f, 0.006f, 0.008f), m.lash); // codina
                b.Add(new Vector3(ex, ey - 0.016f, face + 0.002f), new Vector3(0.04f, 0.004f, 0.006f), m.shade);           // palpebra inferiore
                b.Add(new Vector3(c * W * 0.3f, H * 0.38f, face + 0.0015f), new Vector3(0.03f, 0.016f, 0.004f), m.blush);  // guance
                b.Add(new Vector3(c * (W * 0.5f + 0.006f), H * 0.5f, -0.01f), new Vector3(0.016f, 0.04f, 0.03f), m.skin);  // orecchie

                // iride + pupilla + riflesso (si muovono per guardare)
                Vector3 irisPos = new Vector3(ex - c * 0.002f, ey - 0.001f, face + 0.004f);
                var iris = new BoxMesh(irisPos);
                iris.Add(irisPos, new Vector3(0.024f, 0.026f, 0.006f), m.pupil);
                iris.Add(irisPos + new Vector3(0f, 0f, 0.0025f), new Vector3(0.011f, 0.014f, 0.004f), m.dark);
                iris.Add(irisPos + new Vector3(c * 0.006f, 0.007f, 0.0045f), new Vector3(0.006f, 0.006f, 0.003f), m.eyeWhite);

                // palpebra (perno sul bordo superiore dell'occhio: scala in Y per sbattere)
                Vector3 lidPos = new Vector3(ex, ey + 0.015f, face + 0.0085f);
                var lid = new BoxMesh(lidPos);
                lid.Add(lidPos + new Vector3(0f, -0.015f, 0f), new Vector3(0.05f, 0.031f, 0.004f), m.skin);

                // sopracciglio sottile e arcuato
                Vector3 browPos = new Vector3(ex, ey + 0.038f, face + 0.004f);
                var brow = new BoxMesh(browPos);
                brow.Add(browPos + new Vector3(-c * 0.004f, 0f, 0f), new Vector3(0.032f, 0.007f, 0.008f), m.brow);
                brow.Add(browPos + new Vector3(c * 0.024f, -0.003f, 0f), new Vector3(0.018f, 0.006f, 0.008f), m.brow);

                if (c < 0) { f.irisL = iris; f.irisLPos = irisPos; f.lidL = lid; f.lidLPos = lidPos; f.browL = brow; f.browLPos = browPos; }
                else { f.irisR = iris; f.irisRPos = irisPos; f.lidR = lid; f.lidRPos = lidPos; f.browR = brow; f.browRPos = browPos; }
            }

            // bocca: labbro superiore fisso, interno e labbro inferiore mobili
            b.AddRounded(new Vector3(0f, H * 0.215f, jawFront + 0.004f), new Vector3(0.046f, 0.011f, 0.012f), m.lips, 0.003f);
            f.mouthInsidePos = new Vector3(0f, H * 0.205f, jawFront + 0.001f);
            f.mouthInside = new BoxMesh(f.mouthInsidePos);
            f.mouthInside.Add(f.mouthInsidePos + new Vector3(0f, -0.007f, 0f), new Vector3(0.04f, 0.014f, 0.006f), m.mouth);
            f.lowerLipPos = new Vector3(0f, H * 0.19f, jawFront + 0.005f);
            f.lowerLip = new BoxMesh(f.lowerLipPos);
            f.lowerLip.AddRounded(f.lowerLipPos, new Vector3(0.042f, 0.014f, 0.014f), m.lipsLower, 0.004f);

            Hair(s, m, b, f);
            return b;
        }

        /// <summary>Viso maschile: mascella squadrata, arcata sopraccigliare, barba corta.</summary>
        static BoxMesh MaleHead(Shape s, Mats m, out FaceParts f)
        {
            var b = new BoxMesh();
            f = new FaceParts();
            float W = s.headW, H = s.headH, D = s.headD;
            float face = D * 0.5f;
            float jawFront = 0.01f + D * 0.44f;
            float beardFront = 0.012f + D * 0.445f;

            b.AddRounded(new Vector3(0f, H * 0.65f, 0f), new Vector3(W, H * 0.7f, D), m.skin, 0.018f);                 // cranio
            b.AddRounded(new Vector3(0f, H * 0.2f, 0.01f), new Vector3(W * 0.9f, H * 0.4f, D * 0.88f), m.skin, 0.012f);  // mascella
            b.AddRounded(new Vector3(0f, H * 0.07f, jawFront - 0.012f), new Vector3(W * 0.42f, H * 0.12f, 0.03f), m.skin, 0.006f); // mento (sotto la barba)
            float ey = H * 0.55f;
            b.Add(new Vector3(0f, ey + 0.03f, face + 0.004f), new Vector3(W * 0.8f, 0.018f, 0.014f), m.skin);           // arcata sopraccigliare

            b.Add(new Vector3(0f, H * 0.5f, face + 0.008f), new Vector3(0.024f, 0.06f, 0.016f), m.skin);               // naso
            b.AddRounded(new Vector3(0f, H * 0.4f, face + 0.016f), new Vector3(0.034f, 0.024f, 0.028f), m.skin, 0.006f);

            for (int c = -1; c <= 1; c += 2)
            {
                float ex = c * W * 0.24f;
                b.Add(new Vector3(ex, ey, face + 0.001f), new Vector3(0.04f, 0.02f, 0.006f), m.eyeWhite);
                b.Add(new Vector3(c * W * 0.36f, H * 0.42f, face - 0.008f), new Vector3(0.035f, 0.03f, 0.02f), m.skin);      // zigomi
                b.Add(new Vector3(c * (W * 0.5f + 0.008f), H * 0.5f, -0.01f), new Vector3(0.02f, 0.055f, 0.04f), m.skin);     // orecchie

                Vector3 irisPos = new Vector3(ex - c * 0.002f, ey, face + 0.004f);
                var iris = new BoxMesh(irisPos);
                iris.Add(irisPos, new Vector3(0.019f, 0.019f, 0.006f), m.pupil);
                iris.Add(irisPos + new Vector3(0f, 0f, 0.0025f), new Vector3(0.009f, 0.011f, 0.004f), m.dark);
                iris.Add(irisPos + new Vector3(c * 0.005f, 0.004f, 0.0045f), new Vector3(0.005f, 0.005f, 0.003f), m.eyeWhite);

                Vector3 lidPos = new Vector3(ex, ey + 0.011f, face + 0.0085f);
                var lid = new BoxMesh(lidPos);
                lid.Add(lidPos + new Vector3(0f, -0.011f, 0f), new Vector3(0.044f, 0.023f, 0.004f), m.skin);

                Vector3 browPos = new Vector3(ex, ey + 0.031f, face + 0.012f);
                var brow = new BoxMesh(browPos);
                brow.Add(browPos, new Vector3(0.05f, 0.013f, 0.012f), m.brow);

                if (c < 0) { f.irisL = iris; f.irisLPos = irisPos; f.lidL = lid; f.lidLPos = lidPos; f.browL = brow; f.browLPos = browPos; }
                else { f.irisR = iris; f.irisRPos = irisPos; f.lidR = lid; f.lidRPos = lidPos; f.browR = brow; f.browRPos = browPos; }
            }

            // barba (o mento rasato), baffi e bocca
            BeardStyle beard = s.beard == BeardStyle.Default ? BeardStyle.Stubble : s.beard;
            Material jawMat = beard == BeardStyle.Stubble ? m.stubble : beard == BeardStyle.Full ? m.hair : m.skin;
            b.AddRounded(new Vector3(0f, H * 0.14f, 0.012f), new Vector3(W * 0.91f, H * 0.24f, D * 0.89f), jawMat, 0.01f);
            if (beard == BeardStyle.Full)
                b.AddRounded(new Vector3(0f, H * 0.1f, 0.02f), new Vector3(W * 0.96f, H * 0.3f, D * 0.9f), m.hair, 0.015f);
            if (beard != BeardStyle.None)
                b.Add(new Vector3(0f, H * 0.255f, beardFront + 0.003f), new Vector3(W * (beard == BeardStyle.Mustache ? 0.38f : 0.32f), beard == BeardStyle.Mustache ? 0.018f : 0.012f, 0.012f),
                      beard == BeardStyle.Stubble ? m.stubble : m.hair);
            b.Add(new Vector3(0f, H * 0.215f, beardFront + 0.004f), new Vector3(W * 0.3f, 0.011f, 0.01f), m.lips);
            f.mouthInsidePos = new Vector3(0f, H * 0.208f, beardFront + 0.002f);
            f.mouthInside = new BoxMesh(f.mouthInsidePos);
            f.mouthInside.Add(f.mouthInsidePos + new Vector3(0f, -0.006f, 0f), new Vector3(W * 0.27f, 0.012f, 0.006f), m.mouth);
            f.lowerLipPos = new Vector3(0f, H * 0.2f, beardFront + 0.005f);
            f.lowerLip = new BoxMesh(f.lowerLipPos);
            f.lowerLip.Add(f.lowerLipPos, new Vector3(W * 0.26f, 0.008f, 0.012f), m.lipsLower);

            Hair(s, m, b, f);
            return b;
        }

        /// <summary>Crea le parti mobili del viso sotto lo strato "Pelle" della testa e le collega al FaceRig.</summary>
        static void BuildFace(GameObject root, Transform headSkin, FaceParts f, List<Mesh> meshes, string id)
        {
            var rig = root.AddComponent<FaceRig>();
            rig.irisL = FacePart(headSkin, "Iride sinistra", f.irisL, f.irisLPos, meshes, id);
            rig.irisR = FacePart(headSkin, "Iride destra", f.irisR, f.irisRPos, meshes, id);
            rig.lidL = FacePart(headSkin, "Palpebra sinistra", f.lidL, f.lidLPos, meshes, id);
            rig.lidR = FacePart(headSkin, "Palpebra destra", f.lidR, f.lidRPos, meshes, id);
            rig.browL = FacePart(headSkin, "Sopracciglio sinistro", f.browL, f.browLPos, meshes, id);
            rig.browR = FacePart(headSkin, "Sopracciglio destro", f.browR, f.browRPos, meshes, id);
            rig.lowerLip = FacePart(headSkin, "Labbro inferiore", f.lowerLip, f.lowerLipPos, meshes, id);
            rig.mouthInside = FacePart(headSkin, "Interno bocca", f.mouthInside, f.mouthInsidePos, meshes, id);
            if (f.hairDyn != null && !f.hairDyn.IsEmpty)
            {
                Transform hair = FacePart(headSkin, "Capelli mobili", f.hairDyn, f.hairDynPos, meshes, id);
                root.AddComponent<HairPhysics>().hair = hair;
            }
            root.AddComponent<FaceAnimator>();
        }

        static Transform FacePart(Transform parent, string name, BoxMesh b, Vector3 pivot, List<Mesh> meshes, string id)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            Mesh mesh = b.Build($"{id} {name}");
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = b.Materials;
            return go.transform;
        }

        static BoxMesh UpperArm(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.upperArmLen, w = s.upperArmW;
            b.Add(new Vector3(0f, -L * 0.5f, 0f), new Vector3(w, L, w), m.skin);
            b.Add(new Vector3(0f, -0.035f, 0f), new Vector3(w + 0.035f, 0.09f, w + 0.025f), m.skin);                         // deltoide
            b.Add(new Vector3(0f, -L * 0.5f, w * 0.5f + 0.006f), new Vector3(w * 0.65f, L * 0.4f, s.female ? 0.008f : 0.015f), m.skin); // bicipite
            return b;
        }

        static BoxMesh Forearm(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.forearmLen, w = s.forearmW;
            b.Add(new Vector3(0f, -L * 0.275f, 0f), new Vector3(w, L * 0.55f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.8f, L * 0.5f, w * 0.8f), m.skin);
            b.Add(new Vector3(0f, -0.02f, -w * 0.5f - 0.008f), new Vector3(w * 0.5f, 0.04f, 0.02f), m.skin); // gomito
            return b;
        }

        static BoxMesh Hand(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float P = s.handLen, k = P / 0.1f;
            float palmW = 0.085f * k, palmT = 0.035f * k;

            b.Add(new Vector3(0f, -P * 0.45f, 0f), new Vector3(palmT, P * 0.9f, palmW), m.skin);              // palmo
            b.Add(new Vector3(0f, -P * 0.86f, 0f), new Vector3(palmT + 0.004f, 0.012f, palmW), m.shade);      // nocche

            // quattro dita, dal mignolo (dietro) all'indice (davanti); il pollice è davanti
            float[] lengths = { 0.8f, 0.95f, 1.0f, 0.92f };
            float fw = palmW / 4f - 0.003f, fl = P * 0.75f;
            for (int i = 0; i < 4; i++)
            {
                float len = fl * lengths[i];
                float z = (i - 1.5f) * (palmW / 4f);
                b.Add(new Vector3(0f, -P * 0.9f - len * 0.5f + 0.005f, z), new Vector3(palmT * 0.85f, len, fw), m.skin);
            }
            b.Add(new Vector3(0f, -P * 0.4f, palmW * 0.5f + 0.012f), new Vector3(palmT * 0.85f, P * 0.55f, 0.025f * k), m.skin); // pollice
            return b;
        }

        static BoxMesh Thigh(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.thighLen, w = s.thighW;
            b.Add(new Vector3(0f, -L * 0.3f + 0.015f, 0f), new Vector3(w, L * 0.6f + 0.03f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.85f, L * 0.5f, w * 0.85f), m.skin);
            b.Add(new Vector3(0f, -L + 0.02f, w * 0.425f + 0.008f), new Vector3(w * 0.55f, 0.06f, 0.02f), m.skin); // rotula
            return b;
        }

        static BoxMesh Shin(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.shinLen, w = s.shinW;
            b.Add(new Vector3(0f, -L * 0.25f, 0f), new Vector3(w, L * 0.5f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.72f, 0f), new Vector3(w * 0.75f, L * 0.56f, w * 0.75f), m.skin);
            b.Add(new Vector3(0f, -L * 0.3f, -w * 0.5f - 0.009f), new Vector3(w * 0.7f, L * 0.3f, 0.022f), m.skin); // polpaccio
            b.AddRounded(new Vector3(0f, -0.005f, 0.004f), new Vector3(w * 1.04f, 0.1f, w * 1.04f), m.skin, 0.02f); // ginocchio: copre lo stacco quando la gamba si piega
            return b;
        }

        static BoxMesh Foot(Shape s, Mats m, int side)
        {
            var b = new BoxMesh();
            float A = s.ankleH, fw = s.footW, fl = s.footLen;
            float zMin = -0.06f, zMax = zMin + fl * 0.72f;
            float inner = -side; // verso il centro del corpo

            b.Add(new Vector3(0f, -A * 0.2f, 0f), new Vector3(s.shinW * 0.75f, A * 0.5f, s.shinW * 0.75f), m.skin);          // caviglia
            b.Add(new Vector3(0f, -A + A * 0.425f, (zMin + zMax) * 0.5f), new Vector3(fw, A * 0.85f, zMax - zMin), m.skin);   // piede
            float toeL = fl * 0.28f, toeY = -A + A * 0.25f, toeZ = zMax + toeL * 0.5f;
            b.Add(new Vector3(inner * fw * 0.32f, toeY, toeZ), new Vector3(fw * 0.34f, A * 0.5f, toeL), m.skin);              // alluce
            b.Add(new Vector3(-inner * fw * 0.18f, toeY - A * 0.03f, toeZ - toeL * 0.08f), new Vector3(fw * 0.6f, A * 0.44f, toeL * 0.84f), m.skin); // dita
            b.Add(new Vector3(0f, -A + 0.01f, zMin + 0.01f), new Vector3(fw * 0.9f, 0.02f, 0.03f), m.shade);                  // tallone
            return b;
        }

        // ------------------------------------------------------------------ mesh a blocchi

        /// <summary>Accumula scatole (con faccette piatte) raggruppate per materiale in sotto-mesh.</summary>
        class BoxMesh
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<List<int>> triangles = new List<List<int>>();
            readonly List<Material> materials = new List<Material>();

            /// <summary>Le coordinate passate ad Add sono spostate di -origin (perno della mesh).</summary>
            readonly Vector3 origin;

            public BoxMesh() { }
            public BoxMesh(Vector3 origin) { this.origin = origin; }

            public bool IsEmpty => vertices.Count == 0;
            public Material[] Materials => materials.ToArray();

            static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            public void Add(Vector3 center, Vector3 size, Material mat)
            {
                int sub = materials.IndexOf(mat);
                if (sub < 0)
                {
                    materials.Add(mat);
                    triangles.Add(new List<int>());
                    sub = materials.Count - 1;
                }
                Vector3 h = size * 0.5f;
                center -= origin;
                foreach (Vector3 n in FaceNormals)
                {
                    Vector3 v = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                    Vector3 u = Vector3.Cross(n, v); // a destra guardando la faccia: triangoli in senso orario
                    float hn = Extent(n, h), hu = Extent(u, h), hv = Extent(v, h);
                    Vector3 c = center + n * hn;
                    int start = vertices.Count;
                    vertices.Add(c - u * hu - v * hv);
                    vertices.Add(c - u * hu + v * hv);
                    vertices.Add(c + u * hu + v * hv);
                    vertices.Add(c + u * hu - v * hv);
                    for (int i = 0; i < 4; i++) normals.Add(n);
                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                    var t = triangles[sub];
                    t.Add(start); t.Add(start + 1); t.Add(start + 2);
                    t.Add(start); t.Add(start + 2); t.Add(start + 3);
                }
            }

            /// <summary>Scatola con gli spigoli smussati (tre scatole incrociate): forme più morbide restando a blocchi.</summary>
            public void AddRounded(Vector3 center, Vector3 size, Material mat, float bevel)
            {
                float b2 = Mathf.Min(bevel * 2f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
                Add(center, new Vector3(size.x, size.y - b2, size.z - b2), mat);
                Add(center, new Vector3(size.x - b2, size.y, size.z - b2), mat);
                Add(center, new Vector3(size.x - b2, size.y - b2, size.z), mat);
            }

            static float Extent(Vector3 axis, Vector3 h) => Mathf.Abs(axis.x) * h.x + Mathf.Abs(axis.y) * h.y + Mathf.Abs(axis.z) * h.z;

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
