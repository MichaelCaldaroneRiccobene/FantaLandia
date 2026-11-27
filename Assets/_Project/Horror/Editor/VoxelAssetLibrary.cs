using System.Collections.Generic;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Voxel;
using UnityEditor;
using UnityEngine;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Oggetti voxel della casa anni '90 (TV a tubo, porta, birra, bancone...), salvati come prefab.
    /// Ogni oggetto è disegnato a cubetti colorati con il VoxelBuilder.
    /// Menu: FantaLandia Horror > Crea oggetti voxel.
    /// </summary>
    public static class VoxelAssetLibrary
    {
        public const string Folder = "Assets/_Project/Horror/Generated/Voxel";

        [MenuItem("FantaLandia Horror/Crea oggetti voxel")]
        public static void BuildAll()
        {
            Tv(); TvCabinet(); Door(); BeerPack(); KitchenCounter(); Armchair();
            FloorLamp(); LightSwitchPlate(); Flashlight(); StreetLight(); Notebook();
            AssetDatabase.SaveAssets();
            Debug.Log("[FantaLandia Horror] Oggetti voxel creati in " + Folder);
        }

        // ------------------------------------------------------------------ salvataggio

        /// <summary>Crea il prefab: mesh a cubetti, un materiale per colore, collider a scatola.</summary>
        public static GameObject Save(string name, VoxelBuilder v, Vector3 pivot, bool collider = true, System.Func<Color, Material> materialFor = null)
        {
            EditorAssetUtil.EnsureFolder(Folder);
            Mesh mesh = v.Build(name, pivot, out Color[] colors);
            string meshPath = $"{Folder}/{name}_Mesh.asset";
            // la mesh esistente viene aggiornata al suo posto: gli oggetti già piazzati nelle scene non la perdono
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                mats[i] = (materialFor != null ? materialFor(colors[i]) : null)
                          ?? EditorAssetUtil.Mat("Voxel " + ColorUtility.ToHtmlStringRGB(colors[i]), colors[i], 0.15f);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (collider)
            {
                Vector3 size = mesh.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 0.6f)
                {
                    // mobili grandi: la collisione segue la forma vera a cubetti, così gli oggetti appoggiati sopra
                    // (torcia, telefono...) restano raggiungibili e non finiscono "dentro" una scatola più grande
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
                else
                {
                    // oggetti piccoli (si possono prendere, lanciare, far cadere): una scatola semplice
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = mesh.bounds.center;
                    bc.size = mesh.bounds.size;
                }
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static GameObject Load(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
            if (p == null) { BuildAll(); p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab"); }
            return p;
        }

        public static Vector3 BottomCenter(VoxelBuilder v, int sx, int sz) => new Vector3(sx * 0.5f * v.size, 0f, sz * 0.5f * v.size);

        // ------------------------------------------------------------------ TV a tubo catodico

        const float TvVoxel = 0.06f;

        /// <summary>
        /// TV anni '90 (cubetti da 6 cm, 0.72 x 0.54 x 0.42 m): plastica marrone scuro, cornice, schermo incassato,
        /// pannello comandi argentato con manopole rossa e verde, griglia, gobba del tubo dietro e antenna.
        /// Il fronte guarda verso +Z. Lo schermo vero (quad) va davanti all'incasso: vedi ScreenRect.
        /// </summary>
        public static GameObject Tv()
        {
            var v = new VoxelBuilder(TvVoxel);
            int casing = v.C("#3b342e"), casingDark = v.C("#2c2723"), bezel = v.C("#231f1c"), glass = v.C("#141a19");
            int silver = v.C("#8d8e94"), red = v.C("#b3332e"), green = v.C("#3a9a48"), grille = v.C("#1c1a18");
            int logo = v.C("#c4b27a"), antenna = v.C("#b9bcc2"), black = v.C("#111111");

            v.Fill(0, 0, 0, 12, 9, 7, casing);             // mobile
            v.Fill(2, 1, -3, 10, 8, 0, casingDark);        // gobba del tubo
            v.Fill(0, 0, 6, 12, 9, 7, bezel);              // cornice
            v.Carve(1, 2, 6, 8, 8, 7);                     // incasso schermo
            v.Fill(1, 2, 5, 8, 8, 6, glass);
            v.Fill(9, 1, 6, 11, 8, 7, silver);             // pannello comandi
            v.Set(9, 6, 7, red);                           // manopola canali
            v.Set(9, 4, 7, green);                         // manopola volume
            v.Set(10, 2, 6, grille); v.Set(9, 1, 6, grille);
            v.Set(3, 1, 6, logo); v.Set(4, 1, 6, logo);    // marchio
            for (int x = 1; x < 12; x += 2) v.Set(x, 8, 3, casingDark); // feritoie

            v.Fill(5, 9, 2, 7, 10, 4, black);              // base antenna
            for (int i = 0; i < 5; i++)
            {
                v.Set(5 - (i + 1) / 2, 10 + i, 3, antenna);
                v.Set(6 + (i + 1) / 2, 10 + i, 3, antenna);
            }
            return Save("TV", v, BottomCenter(v, 12, 7));
        }

        /// <summary>Rettangolo dello schermo (centro e dimensioni) nello spazio del prefab TV, per il quad.</summary>
        public static void ScreenRect(out Vector3 center, out Vector2 size)
        {
            const float s = TvVoxel;
            Vector3 pivot = new Vector3(12 * 0.5f * s, 0f, 7 * 0.5f * s);
            center = new Vector3((1 + 8) * 0.5f * s, (2 + 8) * 0.5f * s, 6 * s + 0.002f) - pivot;
            size = new Vector2(7 * s, 6 * s);
        }


        /// <summary>Mobile basso per la TV (1 x 0.5 x 0.5 m, cubetti da 5 cm): videoregistratore con l'orologio verde, videocassette, ante con pomelli, piedini.</summary>
        public static GameObject TvCabinet()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5a3c24"), woodDark = v.C("#45301d"), knob = v.C("#c9a24a"), vcr = v.C("#1a1a1c"), led = v.C("#40ff70"), slot = v.C("#0a0a0a"),
                tape = v.C("#151517"), label = v.C("#e8e2d2"), label2 = v.C("#d13b3b");
            v.Fill(0, 1, 0, 20, 10, 10, wood);
            foreach (int x in new[] { 0, 18 }) foreach (int z in new[] { 0, 8 }) v.Fill(x, 0, z, x + 2, 1, z + 2, woodDark);
            v.Carve(1, 6, 1, 19, 9, 10);                                          // ripiano aperto
            v.Fill(2, 6, 2, 12, 8, 9, vcr); v.Fill(3, 7, 9, 8, 8, 10, slot); v.Set(10, 7, 9, led); v.Set(11, 7, 9, led);
            for (int k = 0; k < 3; k++) { v.Fill(13 + k * 2, 6, 2, 15 + k * 2, 9, 8, tape); v.Set(13 + k * 2, 8, 8, k == 1 ? label2 : label); }
            v.Fill(1, 2, 9, 10, 5, 10, woodDark); v.Fill(10, 2, 9, 19, 5, 10, woodDark); // ante
            v.Set(9, 3, 10, knob); v.Set(10, 3, 10, knob);
            return Save("Mobile TV", v, BottomCenter(v, 20, 10));
        }

        // ------------------------------------------------------------------ porta

        /// <summary>
        /// Porta di casa (cubetti da 10 cm: 0.9 x 2.1 m, spessa 10 cm): montanti scuri, quattro pannelli,
        /// finestrella, pomelli d'ottone sui due lati. Il cardine è sul bordo sinistro (x = 0).
        /// </summary>
        public static GameObject Door()
        {
            var v = new VoxelBuilder(0.1f);
            int frame = v.C("#5e3f24"), panel = v.C("#82593a"), brass = v.C("#c9a24a"), glassC = v.C("#8fa9b8");

            v.Fill(0, 0, 0, 9, 21, 1, frame);
            v.Paint(1, 1, 0, 4, 6, 1, panel);              // pannelli
            v.Paint(5, 1, 0, 8, 6, 1, panel);
            v.Paint(1, 7, 0, 4, 13, 1, panel);
            v.Paint(5, 7, 0, 8, 13, 1, panel);
            v.Paint(2, 15, 0, 4, 19, 1, glassC);           // finestrella a 4 vetri
            v.Paint(5, 15, 0, 7, 19, 1, glassC);
            v.Paint(2, 17, 0, 7, 18, 1, frame);
            v.Set(7, 10, -1, brass);                       // pomelli
            v.Set(7, 10, 1, brass);
            return Save("Porta", v, new Vector3(0f, 0f, 0.5f * v.size));
        }

        // ------------------------------------------------------------------ birra

        /// <summary>Confezione da 6 lattine (cubetti da 3 cm): lattine argento con fascia rossa, cartone con manico.</summary>
        public static GameObject BeerPack()
        {
            var v = new VoxelBuilder(0.03f);
            int can = v.C("#c9cacf"), label = v.C("#b3211e"), top = v.C("#8e8f95"), card = v.C("#a3743f"), cardDark = v.C("#7f5a30");

            for (int cx = 0; cx < 3; cx++)
                for (int cz = 0; cz < 2; cz++)
                {
                    int x = cx * 2, z = cz * 2;
                    v.Fill(x, 0, z, x + 2, 4, z + 2, can);
                    v.Fill(x, 1, z, x + 2, 3, z + 2, label);
                    v.Fill(x, 4, z, x + 2, 5, z + 2, top);
                }
            v.Fill(-1, -1, -1, 7, 0, 5, card);              // fondo
            v.Fill(-1, 0, -1, 7, 1, 0, card);               // fascia bassa
            v.Fill(-1, 0, 4, 7, 1, 5, card);
            v.Fill(-1, 0, -1, 0, 1, 5, card);
            v.Fill(6, 0, -1, 7, 1, 5, card);
            v.Fill(-1, 5, 1, 7, 7, 3, cardDark);            // manico
            v.Carve(2, 6, 1, 4, 7, 3);                      // foro per le dita
            return Save("Birra", v, new Vector3(3f * v.size, -1f * v.size, 2f * v.size));
        }

        /// <summary>Distanza polso → base della confezione quando la si porta per il manico.</summary>
        public const float BeerHang = 0.24f;

        // ------------------------------------------------------------------ cucina e soggiorno


        /// <summary>Bancone della cucina (2 x 0.9 x 0.6 m, cubetti da 5 cm): ante a pannello, cassetti, piano con sbalzo; macchina del caffè e posta sulle estremità (al centro restano telefono e torcia).</summary>
        public static GameObject KitchenCounter()
        {
            var v = new VoxelBuilder(0.05f);
            VoxelFurniture.CounterBase(v);
            int black = v.C("#1c1c1e"), pot = v.C("#3a2a1a"), glassC = v.C("#6a7a80"), red = v.C("#d03030"), mail = v.C("#f2efe6"), mail2 = v.C("#e8d8a8"), jar = v.C("#c9a24a");
            v.Fill(34, 18, 2, 39, 26, 7, black); v.Carve(35, 18, 4, 38, 22, 7);   // macchina del caffè
            v.Fill(35, 18, 4, 38, 21, 7, glassC); v.Fill(35, 18, 5, 38, 20, 7, pot); v.Set(38, 24, 7, red);
            v.Fill(31, 18, 8, 33, 21, 10, jar);
            v.Fill(1, 18, 3, 6, 19, 8, mail); v.Fill(2, 19, 4, 6, 20, 7, mail2);
            return Save("Bancone", v, BottomCenter(v, 40, 12));
        }


        /// <summary>Poltrona bordeaux anni '90 (0.9 x 0.9 m, seduta a 0.5 m, cubetti da 5 cm): piedini, cuscini, centrino sullo schienale, telecomando sul bracciolo. Il fronte guarda +Z.</summary>
        public static GameObject Armchair()
        {
            var v = new VoxelBuilder(0.05f);
            int fabric = v.C("#6b2329"), dark = v.C("#581c22"), cushion = v.C("#7d2b32"), worn = v.C("#8d3a40"), wood = v.C("#3c2a1a"), doily = v.C("#e8e2d2"),
                remote = v.C("#1c1c1e"), red = v.C("#d03030"), button = v.C("#4a1418");
            foreach (int x in new[] { 1, 15 }) foreach (int z in new[] { 1, 15 }) v.Fill(x, 0, z, x + 2, 2, z + 2, wood);
            v.Fill(0, 2, 0, 18, 8, 18, fabric);
            v.Fill(0, 7, 17, 18, 8, 18, dark);
            v.Fill(0, 8, 0, 18, 18, 4, fabric); v.Carve(0, 17, 0, 1, 18, 4); v.Carve(17, 17, 0, 18, 18, 4);
            v.Fill(0, 8, 4, 3, 12, 18, fabric); v.Fill(0, 12, 4, 3, 13, 17, dark);
            v.Fill(15, 8, 4, 18, 12, 18, fabric); v.Fill(15, 12, 4, 18, 13, 17, dark);
            v.Fill(3, 8, 4, 15, 9, 17, cushion); v.Fill(3, 9, 4, 15, 10, 16, cushion);
            v.Paint(6, 9, 8, 12, 10, 14, worn);                                  // seduta consumata (è la poltrona di Tom)
            v.Fill(3, 10, 4, 15, 16, 7, cushion); v.Fill(3, 16, 4, 15, 17, 6, cushion);
            v.Set(6, 13, 6, button); v.Set(11, 13, 6, button);
            v.Fill(5, 17, 0, 13, 18, 4, doily); v.Fill(6, 15, 0, 12, 17, 1, doily); // centrino
            v.Fill(15, 13, 9, 17, 14, 13, remote); v.Set(16, 13, 9, red);
            return Save("Poltrona", v, BottomCenter(v, 18, 18));
        }

        /// <summary>Lampada da terra (cubetti da 8 cm): base, stelo alto 1.44 m, paralume color crema.</summary>
        public static GameObject FloorLamp()
        {
            var v = new VoxelBuilder(0.08f);
            int metal = v.C("#2f2b27");
            v.Fill(0, 0, 0, 3, 1, 3, metal);
            v.Fill(1, 1, 1, 2, 18, 2, metal);
            Save("Lampada stelo", v, BottomCenter(v, 3, 3));

            var shade = new VoxelBuilder(0.08f);
            int cream = shade.C("#efe0b0");
            shade.Fill(0, 0, 0, 4, 3, 4, cream);
            shade.Carve(1, 0, 1, 3, 2, 3);
            return Save("Lampada paralume", shade, BottomCenter(shade, 4, 4), false);
        }

        /// <summary>Placca dell'interruttore (cubetti da 2 cm).</summary>
        public static GameObject LightSwitchPlate()
        {
            var v = new VoxelBuilder(0.02f);
            int plate = v.C("#e8e2d0"), lever = v.C("#cfc8b4");
            v.Fill(0, 0, 0, 4, 6, 1, plate);
            v.Fill(1, 2, 1, 3, 4, 2, lever);
            return Save("Interruttore", v, new Vector3(2f * v.size, 3f * v.size, 0f));
        }

        /// <summary>Torcia (cubetti da 3 cm): corpo nero, testa grigia più larga, vetro giallino, pulsante rosso. Punta verso +Z.</summary>
        public static GameObject Flashlight()
        {
            var v = new VoxelBuilder(0.03f);
            int body = v.C("#1b1b1d"), head = v.C("#3d3e42"), lens = v.C("#fff2a8"), button = v.C("#b02a2a");
            v.Fill(0, 0, 0, 2, 2, 5, body);
            v.Fill(-1, -1, 5, 3, 3, 7, head);
            v.Fill(0, 0, 7, 2, 2, 8, lens);
            v.Set(0, 2, 3, button);
            return Save("Torcia", v, new Vector3(1f * v.size, 1f * v.size, 4f * v.size));
        }

        /// <summary>
        /// Il taccuino di Muark aperto (28 x 20 cm, cubetti da 1 cm): copertina di cuoio consumato, due pagine a righe
        /// azzurre con il margine rosso, spirale al centro, elastico e segnalibro. Le pagine guardano +Y (sopra a y = 2 cm),
        /// il lato lontano è +Z. Il menu ci scrive sopra.
        /// </summary>
        public static GameObject Notebook()
        {
            var v = new VoxelBuilder(0.01f) { shadeVariation = 0.015f }; // carta liscia, non a scacchi
            int leather = v.C("#5a3a22"), leatherDark = v.C("#45301d"), worn = v.C("#7a5236"), paper = v.C("#efe7d2"), paperShade = v.C("#e2d8c0"),
                line = v.C("#c9d6e2"), margin = v.C("#d08080"), ring = v.C("#9a9ca0"), band = v.C("#1a1a1a"), ribbon = v.C("#8a1f1f"), coffee = v.C("#c9b48e");
            v.Fill(0, 0, 0, 28, 1, 20, leather);
            for (int x = 0; x < 28; x++) { v.Set(x, 0, 0, leatherDark); v.Set(x, 0, 19, leatherDark); }
            v.Set(1, 0, 1, worn); v.Set(26, 0, 18, worn); v.Set(2, 0, 18, worn);
            v.Fill(1, 1, 1, 13, 2, 19, paper);                                    // pagina sinistra
            v.Fill(15, 1, 1, 27, 2, 19, paper);                                   // pagina destra
            for (int x = 12; x < 16; x++) for (int z = 1; z < 19; z++) v.Set(x, 1, z, paperShade); // piega verso la spirale
            for (int z = 2; z < 19; z += 2) v.Set(14, 2, z, ring);                // spirale
            v.Fill(13, 1, 0, 16, 2, 1, leatherDark);
            // righe azzurre (ogni 1 cm, sotto il testo) e margine rosso sulla destra
            for (int z = 2; z < 17; z++)
                for (int x = 1; x < 27; x++)
                    if (x != 13 && x != 14 && v.Has(x, 1, z) && z % 1 == 0 && (z % 2 == 0)) v.Set(x, 1, z, line);
            for (int z = 1; z < 19; z++) { v.Set(16, 1, z, margin); v.Set(2, 1, z, margin); }
            v.Fill(27, 0, 8, 28, 2, 9, band); v.Fill(27, 0, 12, 28, 2, 13, band);   // elastico
            v.Fill(20, 1, -3, 21, 2, 1, ribbon);                                 // segnalibro che pende
            v.Set(5, 1, 15, coffee); v.Set(6, 1, 15, coffee); v.Set(5, 1, 16, coffee); // macchia di caffè
            // lucina da lettura a molletta sul bordo in alto a destra: pinza, collo flessibile, testina con la lampadina
            int clip = v.C("#2a2a2c"), neck = v.C("#3a3a3e"), bulb = v.C("#fff0c0");
            v.Fill(24, -1, 19, 27, 3, 21, clip);
            for (int y = 3; y < 7; y++) v.Set(25, y, 20, neck);
            v.Set(25, 7, 19, neck); v.Set(25, 7, 18, neck);
            v.Fill(24, 6, 15, 27, 8, 18, clip);
            v.Fill(24, 5, 16, 27, 6, 17, bulb);
            return Save("Taccuino", v, new Vector3(14f * v.size, 0f, 10f * v.size), false,
                c => c.r > 0.95f && c.g > 0.9f && c.b > 0.7f ? EditorAssetUtil.Emissive("Lucina taccuino", c, 0.8f) : null);
        }

        /// <summary>Lampione di periferia (cubetti da 15 cm).</summary>
        public static GameObject StreetLight()
        {
            var v = new VoxelBuilder(0.15f);
            int pole = v.C("#3a3d42"), lamp = v.C("#f3e3a0");
            v.Fill(0, 0, 0, 1, 30, 1, pole);
            v.Fill(0, 29, 1, 1, 30, 3, pole);
            v.Fill(0, 28, 3, 1, 29, 5, pole);
            v.Fill(0, 27, 3, 1, 28, 5, lamp);
            // la lampada brilla davvero
            return Save("Lampione", v, new Vector3(0.5f * v.size, 0f, 0.5f * v.size), true,
                c => c.r > 0.8f && c.g > 0.7f ? EditorAssetUtil.Emissive("Lampione acceso " + ColorUtility.ToHtmlStringRGB(c), c, 3f) : null);
        }
    }
}
