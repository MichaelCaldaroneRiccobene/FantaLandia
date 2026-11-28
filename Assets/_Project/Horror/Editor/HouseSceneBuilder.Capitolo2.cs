using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Story;
using FantaLandia.Horror.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static FantaLandia.Horror.EditorTools.HouseKit;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Menu "FantaLandia Horror > Crea capitolo 2": la stessa casa di Tom, un'ora dopo. Il primo piano è stato strappato
    /// via (restano monconi di muro frastagliati, pezzi di solaio penzolanti, travi spezzate, mobili caduti dal piano di
    /// sopra), tutto è coperto di polvere e calcinacci; la cantina è diventata un rifugio (la poltrona e il divano di Tom
    /// portati giù, coperte, patatine, bottiglie d'acqua, la radio, il generatore acceso).
    /// Lontano, dietro le case, aspetta accucciata la creatura.
    /// </summary>
    public static partial class HouseSceneBuilder
    {
        static HorrorDoor garageDoor;
        static readonly List<Transform> bulbPivots = new List<Transform>();
        static Seat seatTom2, seatRoy2, seatPlayer2, seatJef2;
        static RadioTuner radio2;
        static Transform tomTorch2, pizzaBox2;
        static CharacterMotor pizzaBoy2;
        static readonly List<Transform> shelfPoints2 = new List<Transform>();
        static Material rubbleConcrete, rubblePlaster, rubbleBrick, rubbleWood, rubbleInsulation, dustPatch, rockMat, rockDark;

        [MenuItem("FantaLandia Horror/Crea capitolo 2")]
        public static void BuildChapter2()
        {
            ch2 = true;
            try { BuildScene(); }
            finally { ch2 = false; }
        }

        static void ResetChapter2()
        {
            garageDoor = null;
            bulbPivots.Clear();
            seatTom2 = seatRoy2 = seatPlayer2 = seatJef2 = null;
            radio2 = null;
            tomTorch2 = pizzaBox2 = null;
            pizzaBoy2 = null;
            shelfPoints2.Clear();
        }

        // ------------------------------------------------------------------ materiali impolverati

        static string Dusty(string hex, float k = 0.4f)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            Color d = Color.Lerp(c, new Color(0.56f, 0.53f, 0.48f), k);
            return "#" + ColorUtility.ToHtmlStringRGB(d);
        }

        /// <summary>Nel capitolo 2 le pareti, le finiture e i pavimenti del piano terra sono grigi di polvere.</summary>
        static void DustyMaterials()
        {
            const ProceduralTextures.Kind T0 = ProceduralTextures.Kind.Clapboard;
            siding = MT("Rivestimento (polvere)", Dusty("#9fb2bd", 0.3f), T0, 1f, 0.05f, 0.3f);
            trim = MT("Finiture bianche (polvere)", Dusty("#eeece4", 0.35f), ProceduralTextures.Kind.Plaster, 1f, 0.05f, 0.25f);
            interior = MT("Pareti piano terra (polvere)", Dusty("#cdbf9f", 0.35f), ProceduralTextures.Kind.Plaster, 1.2f, 0.03f, 0.3f);
            interiorUp = MT("Pareti primo piano (strappate)", Dusty("#bfc3ad", 0.3f), ProceduralTextures.Kind.Wallpaper, 0.9f, 0.03f, 0.3f);
            wood = MT("Parquet (polvere)", Dusty("#6b4a30", 0.45f), ProceduralTextures.Kind.Boards, 0.9f, 0.05f, 0.3f);
            rubbleConcrete = MT("Calcinacci cemento", "#8a857a", ProceduralTextures.Kind.Concrete, 1f, 0.05f, 0.35f);
            rubblePlaster = MT("Calcinacci intonaco", "#c4bba6", ProceduralTextures.Kind.Plaster, 1f, 0.03f, 0.3f);
            rubbleBrick = MT("Calcinacci mattoni", "#7a4234", ProceduralTextures.Kind.Brick, 0.8f, 0.05f, 0.3f);
            rubbleWood = MT("Assi spezzate", "#5a4028", ProceduralTextures.Kind.WoodGrain, 0.8f, 0.1f, 0.2f);
            rubbleInsulation = M("Lana di vetro", "#d9a3a0");
            dustPatch = M("Polvere sul pavimento", "#9a9488");
            rockMat = MT("Roccia", "#5f5a52", ProceduralTextures.Kind.Concrete, 1.3f, 0.06f, 0.45f);
            rockDark = MT("Roccia scura", "#45413b", ProceduralTextures.Kind.Concrete, 0.9f, 0.04f, 0.5f);
        }

        /// <summary>Inclina un oggetto già piazzato (rotazione locale in più, in gradi).</summary>
        static GameObject Tilt(GameObject go, float x, float y, float z)
        {
            if (go != null) go.transform.localRotation = go.transform.localRotation * Quaternion.Euler(x, y, z);
            return go;
        }

        static GameObject NoCollider(GameObject go)
        {
            var c = go != null ? go.GetComponent<Collider>() : null;
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        // ------------------------------------------------------------------ le rovine del piano terra

        /// <summary>
        /// Quello che resta del primo piano: monconi di muro frastagliati sopra il piano terra, mensole di solaio spezzate
        /// lungo i muri, travi crollate, una lastra di pavimento caduta sul tavolo, cumuli di calcinacci (e dove c'era la
        /// scala un mucchio enorme), polvere ovunque. Restano liberi i passaggi: cantina → cucina → ingresso → porta, e
        /// cucina → porta sul retro.
        /// </summary>
        static void BuildRuins(Transform s)
        {
            Transform r = Group("Rovine del primo piano", s);
            var rng = new System.Random(2002);
            float R() => (float)rng.NextDouble();

            // monconi di muro sui quattro lati
            Stubs(r, true, Z0, -1f, X0, X1, rng);
            Stubs(r, true, Z1, 1f, X0, X1, rng);
            Stubs(r, false, X0, -1f, Z0, Z1, rng);
            Stubs(r, false, X1, 1f, Z0, Z1, rng);
            // un pezzo di muro interno di sopra rimasto in piedi (la camera di Tom)
            Box("Moncone muro camera", r, new Vector3(-2.08f, F1 - S, 7.2f), new Vector3(-1.92f, F1 + 1.4f, 9.1f), interiorUp);
            Box("Moncone muro camera (rotto)", r, new Vector3(-2.08f, F1 + 1.4f, 7.2f), new Vector3(-1.92f, F1 + 0.6f, 8.3f), interiorUp);

            // travi spezzate che pendono dentro le stanze
            Material beam = rubbleWood;
            SlopedBox("Trave crollata", r, new Vector3(-6.7f, F1 - 0.1f, 2.0f), new Vector3(-4.5f, F0 + 0.15f, 1.3f), 0.16f, 0.26f, beam);
            SlopedBox("Trave crollata", r, new Vector3(-2.1f, F1 - 0.1f, 4.6f), new Vector3(-3.9f, F0 + 0.25f, 5.6f), 0.16f, 0.26f, beam);
            SlopedBox("Trave crollata", r, new Vector3(6.7f, F1 - 0.1f, 4.0f), new Vector3(5.6f, F0 + 0.2f, 2.6f), 0.16f, 0.26f, beam);
            SlopedBox("Trave crollata", r, new Vector3(-6.7f, F1 - 0.1f, 9.6f), new Vector3(-5.0f, F0 + 0.95f, 9.1f), 0.16f, 0.26f, beam);
            SlopedBox("Trave crollata", r, new Vector3(6.7f, F1 - 0.1f, 9.9f), new Vector3(5.5f, F0 + 0.2f, 8.7f), 0.16f, 0.26f, beam);
            NoCollider(SlopedBox("Trave che penzola", r, new Vector3(2.5f, F1, 10.75f), new Vector3(2.3f, F0 + 2.25f, 9.7f), 0.14f, 0.22f, beam));
            // lastre del pavimento di sopra cadute giù: una sul tavolo della sala da pranzo, una in soggiorno
            SlopedBox("Lastra di solaio crollata", r, new Vector3(-5.5f, F1 - 0.1f, 10.6f), new Vector3(-4.6f, F0 + 0.78f, 7.7f), 2.6f, 0.24f, wood);
            NoCollider(SlopedBox("Lastra di solaio (sotto)", r, new Vector3(-5.5f, F1 - 0.32f, 10.6f), new Vector3(-4.6f, F0 + 0.57f, 7.7f), 2.6f, 0.06f, trim));
            SlopedBox("Lastra di solaio crollata", r, new Vector3(-6.6f, F0 + 1.5f, 5.8f), new Vector3(-3.7f, F0 + 0.1f, 4.5f), 2.0f, 0.24f, wood);
            // la moquette rosa dell'isolamento penzola dai bordi
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(X0 + 0.5f, X1 - 0.5f, R());
                bool front = R() < 0.5f;
                float z = front ? Z0 + 0.4f : Z1 - 0.4f;
                NoCollider(Box("Isolamento che penzola", r, new Vector3(x - 0.25f, F1 - 0.9f - R() * 0.5f, z - 0.04f), new Vector3(x + 0.25f, F1 - S, z + 0.04f), rubbleInsulation));
            }

            // cumuli di calcinacci (il più grande dove c'era la scala per il primo piano)
            Heap(r, new Vector3(2.75f, F0, 3.3f), 1.15f, 70, rng);
            Heap(r, new Vector3(-6.0f, F0, 0.9f), 0.8f, 30, rng);
            Heap(r, new Vector3(-3.0f, F0, 5.3f), 0.6f, 20, rng);
            Heap(r, new Vector3(-6.1f, F0, 7.2f), 0.75f, 26, rng);
            Heap(r, new Vector3(-3.4f, F0, 10.2f), 0.55f, 18, rng);
            Heap(r, new Vector3(6.2f, F0, 9.4f), 0.6f, 20, rng);
            Heap(r, new Vector3(5.6f, F0, 1.6f), 0.7f, 24, rng);
            Heap(r, new Vector3(2.9f, F0, 0.7f), 0.4f, 12, rng);
            Heap(r, new Vector3(-0.2f, F0, 8.9f), 0.35f, 8, rng);
            // sassolini sparsi dappertutto (senza collisione, solo da guardare)
            for (int i = 0; i < 160; i++)
            {
                float x = Mathf.Lerp(X0 + ET + 0.1f, X1 - ET - 0.1f, R()), z = Mathf.Lerp(Z0 + ET + 0.1f, Z1 - ET - 0.1f, R());
                if (x > 3.7f && x < 5.15f && z > 3f && z < 5.7f) continue; // il buco della scala della cantina
                float sz = 0.03f + R() * 0.1f;
                var go = Box("Sassolino", r, new Vector3(x - sz, F0, z - sz * 0.8f), new Vector3(x + sz, F0 + sz * 0.9f, z + sz * 0.8f), RubbleMat(rng));
                go.transform.localRotation = Quaternion.Euler(R() * 30f, R() * 360f, R() * 30f);
                NoCollider(go);
            }
            // polvere sul pavimento: chiazze grigie sottili
            for (int i = 0; i < 70; i++)
            {
                float x = Mathf.Lerp(X0 + ET, X1 - ET, R()), z = Mathf.Lerp(Z0 + ET, Z1 - ET, R());
                if (x > 3.7f && x < 5.15f && z > 3f && z < 5.7f) continue;
                float w = 0.4f + R() * 1.6f, d = 0.3f + R() * 1.4f;
                var go = Box("Polvere", r, new Vector3(x - w * 0.5f, F0 + 0.012f + R() * 0.004f, z - d * 0.5f), new Vector3(x + w * 0.5f, F0 + 0.02f, z + d * 0.5f), dustPatch);
                go.transform.localRotation = Quaternion.Euler(0f, R() * 180f, 0f);
                NoCollider(go);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Material RubbleMat(System.Random rng)
        {
            double v = rng.NextDouble();
            return v < 0.38 ? rubbleConcrete : v < 0.66 ? rubblePlaster : v < 0.82 ? rubbleBrick : v < 0.95 ? rubbleWood : rubbleInsulation;
        }

        /// <summary>
        /// I monconi frastagliati di un muro esterno del primo piano: tratti di altezza diversa (qualche buco del tutto),
        /// più alti agli angoli; nucleo con la carta da parati di sopra, rivestimento azzurro fuori, mensola di solaio dentro.
        /// </summary>
        static void Stubs(Transform parent, bool alongX, float face, float outward, float from, float to, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            float a = from;
            while (a < to - 0.05f)
            {
                float w = Mathf.Min(to - a, 0.35f + R() * 1.5f);
                float b = a + w;
                bool corner = a - from < 0.8f || to - b < 0.8f;
                float h = corner ? 1.2f + R() * 1.4f : (R() < 0.3f ? 0f : R() < 0.75f ? 0.1f + R() * 0.6f : 0.6f + R() * 1.6f);
                if (h > 0.01f)
                {
                    float inner = face - outward * ET, outer = face + outward * 0.04f;
                    Vector3 min, max, cmin, cmax;
                    if (alongX)
                    {
                        min = new Vector3(a, F1, Mathf.Min(face, inner)); max = new Vector3(b, F1 + h, Mathf.Max(face, inner));
                        cmin = new Vector3(a - 0.02f, F1, Mathf.Min(face, outer)); cmax = new Vector3(b + 0.02f, F1 + h, Mathf.Max(face, outer));
                    }
                    else
                    {
                        min = new Vector3(Mathf.Min(face, inner), F1, a); max = new Vector3(Mathf.Max(face, inner), F1 + h, b);
                        cmin = new Vector3(Mathf.Min(face, outer), F1, a - 0.02f); cmax = new Vector3(Mathf.Max(face, outer), F1 + h, b + 0.02f);
                    }
                    Box("Moncone di muro", parent, min, max, interiorUp);
                    NoCollider(Box("Moncone (rivestimento)", parent, cmin, cmax, siding));
                    // il bordo rotto: un pezzo storto in cima
                    var top = Box("Bordo rotto", parent, (min + max) * 0.5f + Vector3.up * (h * 0.5f) - new Vector3(0.12f, 0.08f, 0.12f),
                                  (min + max) * 0.5f + Vector3.up * (h * 0.5f) + new Vector3(0.12f, 0.14f, 0.12f), R() < 0.5f ? interiorUp : rubblePlaster);
                    top.transform.localRotation = Quaternion.Euler(R() * 40f - 20f, R() * 90f, R() * 40f - 20f);
                    NoCollider(top);
                }
                // la mensola di solaio rimasta attaccata al muro (sporge dentro la stanza)
                float d = R() < 0.35f ? 0f : 0.2f + R() * (h > 0.01f ? 1.1f : 0.4f);
                if (d > 0.01f)
                {
                    float inner = face - outward * (ET + d), wall = face - outward * ET;
                    Vector3 lmin, lmax;
                    if (alongX) { lmin = new Vector3(a, F1 - S, Mathf.Min(inner, wall)); lmax = new Vector3(b, F1, Mathf.Max(inner, wall)); }
                    else { lmin = new Vector3(Mathf.Min(inner, wall), F1 - S, a); lmax = new Vector3(Mathf.Max(inner, wall), F1, b); }
                    Box("Solaio rimasto", parent, lmin, lmax, wood);
                    NoCollider(Box("Solaio rimasto (sotto)", parent, lmin - Vector3.up * 0.012f, new Vector3(lmax.x, lmin.y, lmax.z), trim));
                }
                a = b;
            }
        }

        /// <summary>Un cumulo di calcinacci: pezzi grandi sotto e al centro, piccoli in cima e ai bordi.</summary>
        static void Heap(Transform parent, Vector3 center, float radius, int count, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            Transform h = Group("Cumulo di macerie", parent);
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Pow(R(), 0.7f);
                float ang = R() * Mathf.PI * 2f;
                float dist = t * radius;
                float y = center.y + (1f - t) * radius * 0.55f * R();
                float size = Mathf.Lerp(0.5f, 0.12f, t) * (0.6f + R() * 0.7f);
                Vector3 p = center + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                p.y = y;
                Material m = RubbleMat(rng);
                bool plank = m == rubbleWood;
                Vector3 half = plank ? new Vector3(size * 0.15f, size * 0.1f, size * 1.6f) : new Vector3(size, size * (0.4f + R() * 0.5f), size * (0.6f + R() * 0.5f)) * 0.5f;
                var go = Box(plank ? "Asse spezzata" : "Calcinaccio", h, p - half, p + half, m);
                go.transform.localRotation = Quaternion.Euler(R() * 70f - 35f, R() * 360f, R() * 70f - 35f);
                if (size < 0.3f) NoCollider(go);
            }
        }

        /// <summary>I mobili del primo piano caduti giù: l'armadio in soggiorno, il letto di Tom, la vasca in cucina.</summary>
        static void FallenFurniture(Transform f)
        {
            Tilt(Place(VoxelFurniture.Get("Armadio"), f, new Vector3(-5.5f, F0 + 0.3f, 0.95f), -90f), -86f, 0f, 0f);
            Tilt(Place(VoxelFurniture.Get("Letto singolo"), f, new Vector3(-3.5f, F0 + 0.35f, 6.9f), 20f), 0f, 0f, 22f);
            Tilt(Place(VoxelFurniture.Get("Vasca"), f, new Vector3(6.0f, F0 + 0.3f, 8.0f), 80f), 12f, 0f, -18f);
            Tilt(Place(VoxelFurniture.Get("Scrivania"), f, new Vector3(-6.0f, F0 + 0.75f, 4.0f), 70f), 0f, 0f, 180f);
            Tilt(Place(VoxelFurniture.Get("Comodino"), f, new Vector3(1.9f, F0 + 0.25f, 4.7f), 30f), 90f, 0f, 0f);
            Tilt(Place(VoxelFurniture.Get("Cesto bucato"), f, new Vector3(5.1f, F0 + 0.2f, 2.2f), 0f), 0f, 0f, 95f);
            Place(VoxelFurniture.Get("Scarpe"), f, new Vector3(-1.4f, F0, 4.9f), 150f);
        }

        // ------------------------------------------------------------------ il rifugio in cantina

        static void BuildBunker(Transform b)
        {
            Transform r = Group("Rifugio", b);
            // il divano e la poltrona di Tom, portati giù dal salotto
            Place(VoxelFurniture.Get("Divano"), r, new Vector3(-1.2f, FB, 4.3f), 0f);
            Transform arm = Place(VoxelAssetLibrary.Load("Poltrona"), r, new Vector3(1.3f, FB, 5.3f), -100f).transform;
            seatPlayer2 = Seat2(r, "Posto: divano sinistra (Muark)", new Vector3(-1.8f, FB, 4.45f), 0f, new Vector3(-1.8f, FB, 5.2f), "Siediti sul divano");
            Seat2(r, "Posto: divano centro", new Vector3(-1.2f, FB, 4.45f), 0f, new Vector3(-1.2f, FB, 5.2f), "Siediti sul divano");
            seatRoy2 = Seat2(r, "Posto: divano destra (Roy)", new Vector3(-0.6f, FB, 4.45f), 0f, new Vector3(-0.6f, FB, 5.2f), "Siediti sul divano");
            Vector3 dir = arm.rotation * Vector3.forward;
            seatTom2 = Seat2(r, "Posto: poltrona (Tom)", arm.position + dir * 0.14f, arm.eulerAngles.y, arm.position + dir * 0.85f, "Siediti in poltrona");
            // Jef in disparte, su una cassa
            Place(VoxelFurniture.Get("Scatola grande"), r, new Vector3(-4.4f, FB, 6.6f), 30f);
            seatJef2 = Seat2(r, "Posto: cassa (Jef)", new Vector3(-4.4f, FB, 6.6f), 125f, new Vector3(-4.4f, FB, 6.6f) + Quaternion.Euler(0f, 125f, 0f) * Vector3.forward * 0.7f, "Siediti sulla cassa");
            seatJef2.height = 0.42f;

            // la radio sulla cassa davanti al divano, con la torcia accanto
            Place(VoxelFurniture.Get("Scatola grande"), r, new Vector3(-1.6f, FB, 6.05f), 8f);
            GameObject radio = Place(VoxelFurniture.Get("Radio"), r, new Vector3(-1.6f, FB + 0.4f, 6.05f), 180f);
            if (radio != null)
            {
                if (radio.GetComponentInChildren<Collider>() == null) radio.AddComponent<BoxCollider>();
                radio2 = radio.AddComponent<RadioTuner>();
            }
            GameObject torch = Place(VoxelAssetLibrary.Load("Torcia"), r, new Vector3(-1.22f, FB + 0.4f, 6.12f), 70f);
            if (torch != null) tomTorch2 = torch.transform;

            // coperte, un sacco a pelo, un cuscino
            Material plaid = M("Coperta a quadri", "#7a3a34"), blue = M("Coperta blu", "#3e4e6e"), bag = M("Sacco a pelo", "#4a6a3a"), pillow = M("Cuscino", "#d8d2c2");
            NoCollider(Box("Coperta sul divano", r, new Vector3(-1.5f, FB + 0.46f, 4.15f), new Vector3(-0.85f, FB + 0.53f, 4.7f), plaid));
            NoCollider(Box("Coperta (piega)", r, new Vector3(-2.35f, FB + 0.5f, 3.85f), new Vector3(-2.1f, FB + 0.75f, 4.75f), plaid));
            NoCollider(Box("Coperta per terra", r, new Vector3(-5.1f, FB + 0.005f, 5.5f), new Vector3(-3.8f, FB + 0.03f, 6.25f), blue));
            NoCollider(Box("Sacco a pelo", r, new Vector3(-3.6f, FB + 0.005f, 7.0f), new Vector3(-2.9f, FB + 0.13f, 7.75f), bag));
            NoCollider(Box("Cuscino", r, new Vector3(-3.55f, FB + 0.13f, 7.45f), new Vector3(-2.95f, FB + 0.24f, 7.72f), pillow));

            // patatine (qualche busta aperta e vuota) e lattine di birra del capitolo 1
            Material chips = M("Busta patatine", "#e3b52a"), chipsRed = M("Busta patatine rossa", "#c0302a"), can = M("Lattina", "#b9bcc0", 0.6f);
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            foreach (var (p, m) in new[] { (new Vector3(-0.25f, FB + 0.04f, 5.65f), chips), (new Vector3(-1.95f, FB + 0.44f, 6.15f), chipsRed),
                                           (new Vector3(-1.0f, FB + 0.04f, 5.45f), chips), (new Vector3(0.55f, FB + 0.04f, 4.75f), chipsRed) })
            {
                var go = Box("Busta di patatine", r, p - new Vector3(0.11f, 0.035f, 0.15f), p + new Vector3(0.11f, 0.035f, 0.15f), m);
                go.transform.localRotation = Quaternion.Euler(R() * 20f, R() * 360f, R() * 20f);
                NoCollider(go);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(0.7f + R() * 0.8f, FB, 3.9f + R() * 0.7f);
                bool lying = i % 2 == 1;
                var go = Box("Lattina", r, p + new Vector3(-0.033f, 0f, -0.033f), p + new Vector3(0.033f, 0.12f, 0.033f), can);
                if (lying) go.transform.localRotation = Quaternion.Euler(90f, R() * 360f, 0f);
                NoCollider(go);
            }
            // bottiglie d'acqua: una fila in piedi e un paio vuote per terra
            Material water = EditorAssetUtil.Glass("Casa Bottiglia d'acqua", new Color(0.55f, 0.75f, 0.95f), 0.5f), cap = M("Tappo", "#2a5ab0");
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new Vector3(0.1f + (i % 4) * 0.09f, FB, 6.6f + (i / 4) * 0.09f);
                bool empty = i >= 5;
                if (empty) p = new Vector3(-0.6f + i * 0.2f, FB + 0.035f, 6.1f);
                var body = Box("Bottiglia", r, p + new Vector3(-0.035f, 0f, -0.035f), p + new Vector3(0.035f, 0.26f, 0.035f), water);
                var top = Box("Tappo", body.transform, new Vector3(-0.3f, 0.5f, -0.3f), new Vector3(0.3f, 0.6f, 0.3f), cap);
                NoCollider(top);
                if (empty) body.transform.localRotation = Quaternion.Euler(90f, 30f + i * 50f, 0f);
                NoCollider(body);
            }
            // dove tintinnano i barattoli durante le scosse
            shelfPoints2.Add(HorrorTestSceneBuilder.Marker("Barattoli (scaffale)", r, new Vector3(-6.3f, FB + 1.3f, 2.8f)));
            shelfPoints2.Add(HorrorTestSceneBuilder.Marker("Barattoli (scaffale)", r, new Vector3(-6.3f, FB + 1.3f, 6.2f)));
            shelfPoints2.Add(HorrorTestSceneBuilder.Marker("Attrezzi (banco)", r, new Vector3(0.2f, FB + 1.0f, 10.3f)));
        }

        static Seat Seat2(Transform parent, string name, Vector3 seatPos, float yaw, Vector3 frontPos, string label)
        {
            Seat s = MakeSeat(parent, name, seatPos, yaw, frontPos, label);
            s.group = "cantina";
            return s;
        }

        // ------------------------------------------------------------------ personaggi del capitolo 2

        static void BuildCast2(Transform world, Dictionary<string, GameObject> cast)
        {
            Transform people = Group("Personaggi", world);
            jefMotor = HorrorTestSceneBuilder.BuildNpc(people, cast["jef"], "Jef", "jef", new Vector3(-4.4f, FB, 7.3f), 125f);
            royMotor = HorrorTestSceneBuilder.BuildNpc(people, cast["roy"], "Roy", "roy", new Vector3(-0.6f, FB, 5.3f), 0f);
            // il fattorino della pizza, sul portico (la regia lo fa comparire quando bussa)
            if (cast.TryGetValue("fattorino", out GameObject boyPrefab))
            {
                pizzaBoy2 = HorrorTestSceneBuilder.BuildNpc(people, boyPrefab, "Fattorino", "fattorino", new Vector3(0.15f, F0, -1.45f), 0f);
                Transform box = Group("Cartoni della pizza", people);
                Material cardboard = M("Cartone pizza", "#cfae7c"), logo = M("Logo pizzeria", "#b8262a");
                NoCollider(Box("Cartone", box, new Vector3(-0.21f, 0f, -0.21f), new Vector3(0.21f, 0.05f, 0.21f), cardboard));
                NoCollider(Box("Cartone", box, new Vector3(-0.2f, 0.05f, -0.2f), new Vector3(0.2f, 0.1f, 0.2f), cardboard));
                NoCollider(Box("Logo", box, new Vector3(-0.08f, 0.1f, -0.08f), new Vector3(0.08f, 0.104f, 0.08f), logo));
                box.position = pizzaBoy2.transform.position + Vector3.up * 1.05f + Vector3.forward * 0.36f;
                pizzaBox2 = box;
            }
        }

        // ------------------------------------------------------------------ la regia, i macigni, la creatura

        static void BuildChapter2Director(Transform world, Transform marks, HorrorDoor frontDoor, CrtTv tv, CharacterMotor tom, SmokeColumn smoke, NeighborhoodPower hood)
        {
            Transform M2(string n, Vector3 p, float yaw = 0f) { var t = HorrorTestSceneBuilder.Marker(n, marks, p); t.rotation = Quaternion.Euler(0f, yaw, 0f); return t; }
            var d = new GameObject("Regia del capitolo 2").AddComponent<Chapter2Director>();
            d.frontDoor = frontDoor;
            d.backDoor = backDoor;
            d.cellarDoor = cellarDoor;
            d.blockedDoors = new[] { sideDoor, garageDoor };
            d.tom = tom;
            d.roy = royMotor;
            d.jef = jefMotor;
            d.pizzaBoy = pizzaBoy2;
            d.pizzaBox = pizzaBox2;
            d.seatTom = seatTom2;
            d.seatRoy = seatRoy2;
            d.seatPlayer = seatPlayer2;
            d.seatJef = seatJef2;
            d.radio = radio2;
            d.generator = generator;
            d.cellarBulbs = cellarBulbs.ToArray();
            d.bulbPivots = bulbPivots.ToArray();
            d.tomTorch = tomTorch2;
            d.shelfPoints = shelfPoints2.ToArray();
            d.basementFloorY = FB;
            d.ceilingY = F0 - S;
            d.basementBounds = new Bounds(new Vector3(0f, FB + 1.2f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0 - 0.6f, 2.6f, Z1 - Z0 - 0.6f));
            d.houseBounds = new Bounds(new Vector3((X0 + X1) * 0.5f, 1f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0, 8f, Z1 - Z0));

            d.stairsBottom = M2("Cantina: in fondo alla scala", new Vector3(4.4f, FB, 2.7f), 180f);
            d.stairsTop = M2("Cucina: in cima alla scala", new Vector3(4.42f, F0, 6.9f), 0f);
            d.frontInside = M2("Ingresso: davanti alla porta", new Vector3(0f, F0, 1.2f), 180f);
            d.backInside = M2("Sala da pranzo: davanti alla porta sul retro", new Vector3(-1.5f, F0, 10.1f), 0f);
            d.gardenTom = M2("Giardino: Tom", new Vector3(-1.0f, 0f, 17.6f));
            d.gardenRoy = M2("Giardino: Roy", new Vector3(0.7f, 0f, 17.2f));
            d.gardenJef = M2("Giardino: Jef", new Vector3(-2.6f, 0f, 16.9f));
            d.gardenPlayer = M2("Giardino: Muark", new Vector3(-0.8f, 0f, 16.3f));
            d.playerBasementStart = M2("Cantina: Muark all'inizio", new Vector3(-1.8f, FB + 0.05f, 5.3f), 180f);
            d.screamsPoint = M2("Urla lontane", new Vector3(55f, 3f, 40f));
            d.tvView = M2("Epilogo: davanti alla TV", new Vector3(-4.6f, F0, 3.0f), -90f);

            // il frammento di roccia che finirà davanti alla porta (e il varco chiuso dopo l'impatto)
            d.rockFront = Rock(world, "Frammento di roccia (porta)", new Vector3(0.15f, F0 + 1.25f, -1.55f), 1.45f, 3).transform;
            d.rockFront.gameObject.SetActive(false);
            d.rockFinal = Rock(world, "Frammento di roccia (finale)", new Vector3(0f, -40f, 0f), 1.7f, 9).transform;
            d.rockFinal.gameObject.SetActive(false);
            var gap = new GameObject("Varco della porta (chiuso dal masso)");
            gap.transform.SetParent(world, false);
            gap.transform.position = new Vector3(0f, F0 + 1.1f, 0.1f);
            var col = gap.AddComponent<BoxCollider>();
            col.size = new Vector3(1.1f, 2.3f, 0.5f);
            col.enabled = false;
            gap.layer = 2; // ferma il giocatore ma non i raggi (sangue, interazioni)
            d.frontBlocker = col;
            var porch = world.Find("Casa di Tom/Veranda");
            var roofParts = new List<GameObject>();
            if (porch != null) foreach (Transform t in porch) if (t.name.StartsWith("Tetto veranda")) roofParts.Add(t.gameObject);
            d.porchRoof = roofParts.ToArray();

            // altri pezzi caduti nel quartiere
            Rock(world, "Frammento nel prato", new Vector3(-5.6f, 0.5f, -5.2f), 1.1f, 11);
            Rock(world, "Frammento in strada", new Vector3(9.5f, 0.6f, -13.6f), 1.4f, 12);
            Rock(world, "Frammento dai vicini", new Vector3(-19f, 0.9f, 6f), 1.6f, 13);
            Rock(world, "Frammento oltre la staccionata", new Vector3(17f, 0.6f, 19f), 1.2f, 14);

            // il terreno lontano (sotto la creatura accucciata non si vede il vuoto)
            NoCollider(Box("Terreno lontano", world, new Vector3(-400f, -2f, 70f), new Vector3(400f, -0.03f, 420f), M("Terra lontana", "#1a1c18")));
            d.creature = BuildCreature(world, new Vector3(95f, 0f, 150f), -144f);

            d.tv = tv;
            d.smoke = smoke;
            d.neighborhood = hood;
            d.bloodMaterial = BloodMaterial();
            d.dustMaterial = SmokeMat("Polvere delle macerie", false, 0.8f, 0.55f);
            d.rockMaterial = rockMat;

            Reverb(world, "Eco cantina", new Vector3(0f, FB + 0.6f, (Z0 + Z1) * 0.5f), 3f, 4.5f, AudioReverbPreset.StoneCorridor);
        }

        /// <summary>Un frammento di roccia: un nucleo e tanti blocchi storti attorno, due toni di grigio, spigoli vivi.</summary>
        static GameObject Rock(Transform parent, string name, Vector3 center, float radius, int seed)
        {
            var rng = new System.Random(seed * 131);
            float R() => (float)rng.NextDouble();
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = center;
            var core = Box("Nucleo", root.transform, -Vector3.one * radius * 0.7f, Vector3.one * radius * 0.7f, rockMat);
            core.transform.localRotation = Quaternion.Euler(R() * 90f, R() * 90f, R() * 90f);
            for (int i = 0; i < 11; i++)
            {
                Vector3 off = RandomInBall(rng) * radius * 0.55f;
                Vector3 half = new Vector3(0.35f + R() * 0.45f, 0.3f + R() * 0.45f, 0.35f + R() * 0.45f) * radius * 0.75f;
                var go = Box("Blocco", root.transform, off - half, off + half, R() < 0.35f ? rockDark : rockMat);
                go.transform.localRotation = Quaternion.Euler(R() * 180f, R() * 180f, R() * 180f);
                if (i > 5) NoCollider(go);
            }
            root.transform.rotation = Quaternion.Euler(R() * 360f, R() * 360f, R() * 360f);
            return root;
        }

        static Vector3 RandomInBall(System.Random rng)
        {
            Vector3 v;
            do v = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
            while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.01f);
            return v;
        }

        /// <summary>
        /// La creatura gigante (una sessantina di metri), solo sagoma: gambe lunghe e un po' piegate, busto massiccio e
        /// ingobbito con una fila di spuntoni sulla schiena, testa piccola con la mascella, braccia lunghissime con artigli.
        /// </summary>
        static GiantCreature BuildCreature(Transform world, Vector3 pos, float yaw)
        {
            Material m = SilhouetteMaterial();
            var root = new GameObject("Creatura gigante (lontana)");
            root.transform.SetParent(world, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var gc = root.AddComponent<GiantCreature>();

            Transform Pivot(string n, Transform parent, Vector3 local, Vector3 euler)
            {
                var t = new GameObject(n).transform;
                t.SetParent(parent, false);
                t.localPosition = local;
                t.localRotation = Quaternion.Euler(euler);
                return t;
            }
            void Part(string n, Transform parent, Vector3 min, Vector3 max) => NoCollider(Box(n, parent, min, max, m)).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            Transform hips = Pivot("Anca", root.transform, new Vector3(0f, 27f, 0f), Vector3.zero);
            foreach (int side in new[] { -1, 1 })
            {
                Transform thigh = Pivot(side < 0 ? "Coscia sx" : "Coscia dx", hips, new Vector3(4.6f * side, 0f, 0f), new Vector3(-14f, 0f, 5f * side));
                Part("Coscia", thigh, new Vector3(-2.4f, -14.5f, -2.6f), new Vector3(2.4f, 1f, 2.6f));
                Transform shin = Pivot("Stinco", thigh, new Vector3(0f, -14f, 0f), new Vector3(24f, 0f, 0f));
                Part("Stinco", shin, new Vector3(-1.8f, -14f, -1.9f), new Vector3(1.8f, 0f, 1.9f));
                Part("Piede", shin, new Vector3(-2.2f, -15.2f, -1.6f), new Vector3(2.2f, -13.6f, 5.5f));
            }
            Transform torso = Pivot("Busto", hips, Vector3.zero, Vector3.zero);
            Part("Ventre", torso, new Vector3(-6f, -1f, -4f), new Vector3(6f, 10f, 4.5f));
            Part("Petto", torso, new Vector3(-8.5f, 9f, -5f), new Vector3(8.5f, 22f, 5.5f));
            Part("Gobba", torso, new Vector3(-6.5f, 17f, -7f), new Vector3(6.5f, 24.5f, 0f));
            Part("Collo", torso, new Vector3(-2.4f, 21f, -0.5f), new Vector3(2.4f, 25.5f, 3.5f));
            for (int i = 0; i < 7; i++)
            {
                var sp = NoCollider(Box("Spuntone", torso, new Vector3(-0.5f, 0f, -1.2f), new Vector3(0.5f, 3.2f + (i % 3) * 0.8f, 1.2f), m));
                sp.transform.localPosition = new Vector3(0f, 8f + i * 2.4f, -5.5f - (i > 3 ? 1.2f : 0f));
                sp.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                sp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Transform head = Pivot("Testa", torso, new Vector3(0f, 25f, 2.6f), Vector3.zero);
            Part("Cranio", head, new Vector3(-2.6f, -0.6f, -1.5f), new Vector3(2.6f, 3.2f, 4.6f));
            Part("Corno sx", head, new Vector3(-2.4f, 2.6f, -1.8f), new Vector3(-1.6f, 5.2f, -0.6f));
            Part("Corno dx", head, new Vector3(1.6f, 2.6f, -1.8f), new Vector3(2.4f, 5.2f, -0.6f));
            Transform jaw = Pivot("Mascella", head, new Vector3(0f, -0.4f, -0.6f), Vector3.zero);
            Part("Mascella", jaw, new Vector3(-2.3f, -2.2f, 0f), new Vector3(2.3f, -0.3f, 5f));
            Transform[] arms = new Transform[2];
            foreach (int side in new[] { -1, 1 })
            {
                Transform sh = Pivot(side < 0 ? "Spalla sx" : "Spalla dx", torso, new Vector3(9.5f * side, 20f, 0.5f), new Vector3(-6f, 0f, 4f * side));
                Part("Braccio", sh, new Vector3(-1.9f, -18f, -1.9f), new Vector3(1.9f, 1.5f, 1.9f));
                Transform fore = Pivot("Avambraccio", sh, new Vector3(0f, -18f, 0f), new Vector3(-18f, 0f, 0f));
                Part("Avambraccio", fore, new Vector3(-1.5f, -17f, -1.5f), new Vector3(1.5f, 0f, 1.5f));
                for (int k = -1; k <= 1; k++)
                {
                    var claw = NoCollider(Box("Artiglio", fore, new Vector3(-0.35f, -4.5f, -0.35f), new Vector3(0.35f, 0f, 0.35f), m));
                    claw.transform.localPosition = new Vector3(k * 0.95f, -17f, 0.6f);
                    claw.transform.localRotation = Quaternion.Euler(-18f, 0f, k * 10f);
                    claw.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                arms[side < 0 ? 0 : 1] = sh;
            }
            gc.hips = hips;
            gc.torso = torso;
            gc.head = head;
            gc.jaw = jaw;
            gc.armL = arms[0];
            gc.armR = arms[1];
            return gc;
        }

        static Material SilhouetteMaterial()
        {
            string path = $"{EditorAssetUtil.MaterialFolder}/Sagoma creatura.mat";
            EditorAssetUtil.EnsureFolder(EditorAssetUtil.MaterialFolder);
            Shader sh = Shader.Find("FantaLandia/Silhouette");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.028f, 0.024f, 0.03f));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.028f, 0.024f, 0.03f));
            if (m.HasProperty("_GlowColor")) m.SetColor("_GlowColor", new Color(0.32f, 0.11f, 0.04f));
            if (m.HasProperty("_GlowTop")) m.SetFloat("_GlowTop", 26f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Il materiale del sangue: Lit con il ritaglio (alpha clip) e quattro macchie diverse in una texture 2x2
        /// (goccia centrale irregolare, schizzi e scie intorno), rosso scuro e lucido come il sangue fresco.
        /// </summary>
        static Material BloodMaterial()
        {
            const string texPath = "Assets/_Project/Horror/Generated/Texture/Sangue.asset";
            EditorAssetUtil.EnsureFolder("Assets/_Project/Horror/Generated/Texture");
            const int S = 256, C = 128;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            bool create = tex == null;
            if (create) tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "Sangue" };
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[S * S];
            for (int cell = 0; cell < 4; cell++)
            {
                var rng = new System.Random(500 + cell * 17);
                var blobs = new List<Vector3> { new Vector3(0.5f, 0.5f, 0.17f + (float)rng.NextDouble() * 0.07f) };
                int n = 14 + rng.Next(16);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.14f + (float)rng.NextDouble() * 0.3f;
                    blobs.Add(new Vector3(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r, 0.012f + (float)rng.NextDouble() * 0.04f));
                    if (i % 3 == 0)
                        for (float k = 0.25f; k < 1f; k += 0.08f)
                            blobs.Add(new Vector3(0.5f + Mathf.Cos(a) * r * k, 0.5f + Mathf.Sin(a) * r * k, 0.01f + 0.03f * (1f - k)));
                }
                int ox = (cell % 2) * C, oy = (cell / 2) * C;
                for (int y = 0; y < C; y++)
                    for (int x = 0; x < C; x++)
                    {
                        float u = (x + 0.5f) / C, v = (y + 0.5f) / C, best = 0f;
                        foreach (var b in blobs)
                        {
                            float dd = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y)) / b.z;
                            best = Mathf.Max(best, 1f - dd);
                        }
                        // ai bordi della cella sempre trasparente (niente righe con il filtro)
                        if (x < 2 || y < 2 || x > C - 3 || y > C - 3) best = 0f;
                        float edge = Mathf.Clamp01(1f - best * 3f);
                        px[(oy + y) * S + ox + x] = new Color(Mathf.Lerp(0.36f, 0.2f, edge), 0.015f, 0.012f, best > 0f ? 1f : 0f);
                    }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            if (create) AssetDatabase.CreateAsset(tex, texPath);
            else EditorUtility.SetDirty(tex);

            string path = $"{EditorAssetUtil.MaterialFolder}/Sangue.mat";
            EditorAssetUtil.EnsureFolder(EditorAssetUtil.MaterialFolder);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Smoothness", 0.8f);
            m.SetFloat("_Metallic", 0f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
