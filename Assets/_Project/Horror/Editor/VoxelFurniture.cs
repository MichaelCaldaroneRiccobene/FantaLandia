using FantaLandia.EditorTools;
using FantaLandia.Horror.Voxel;
using UnityEditor;
using UnityEngine;
using static FantaLandia.Horror.EditorTools.VoxelAssetLibrary;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Mobili e oggetti voxel della casa americana anni '90 e del quartiere (cubetti grandi, stile "cubettoso").
    /// Ogni oggetto ha il fronte verso +Z e il perno al centro della base, salvo dove indicato.
    /// Menu: FantaLandia Horror > Crea mobili voxel.
    /// </summary>
    public static class VoxelFurniture
    {
        [MenuItem("FantaLandia Horror/Crea mobili voxel")]
        public static void BuildAll()
        {
            Sofa(); CoffeeTable(); Rug(); DiningTable(); Chair(); Fridge(); Stove(); CounterSink(); Island();
            Toilet(); BathSink(); Bathtub(); BedDouble(); BedSingle(); Nightstand(); Wardrobe(); Desk(); Bookshelf();
            Phone(); HallTable(); MetalShelf(); Box("Scatola", 4, 3, 4); Box("Scatola piccola", 3, 2, 3); Box("Scatola grande", 5, 4, 5);
            Generator(); FuelCan(); WaterHeater(); WashingMachine(); Workbench(); InteriorDoor(); GarageDoor();
            Mailbox(); Tree(); Bush(); Car("Auto rossa", "#a3262a", false); Car("Auto blu", "#2a4a8a", false);
            Car("Station wagon verde", "#3c5e3a", true); Car("Station wagon beige", "#b9a77f", true); Car("Auto bianca", "#d8d8d2", false);
            Grill(); LawnChair(); TrashCan(); CeilingLamp(); Bulb(); PorchPillar();
            AssetDatabase.SaveAssets();
            Debug.Log("[FantaLandia Horror] Mobili voxel creati in " + Folder);
        }

        public static GameObject Get(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
            if (p == null) { BuildAll(); p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab"); }
            return p;
        }

        // ------------------------------------------------------------------ soggiorno

        /// <summary>Divano a tre posti (2.1 x 0.85 x 0.9 m) in velluto color senape.</summary>
        public static GameObject Sofa()
        {
            var v = new VoxelBuilder(0.1f);
            int fabric = v.C("#8a7244"), cushion = v.C("#9a8050"), wood = v.C("#3c2a1a");
            v.Fill(0, 1, 0, 21, 4, 9, fabric);
            v.Fill(0, 4, 0, 21, 9, 2, fabric);               // schienale
            v.Fill(0, 4, 2, 2, 6, 9, fabric);                // braccioli
            v.Fill(19, 4, 2, 21, 6, 9, fabric);
            for (int i = 0; i < 3; i++)
            {
                int x = 2 + i * 6;
                v.Fill(x, 4, 2, x + 5, 5, 8, cushion);       // cuscini seduta (con fessure)
                v.Fill(x, 5, 2, x + 5, 8, 3, cushion);       // cuscini schienale
            }
            v.Set(0, 0, 0, wood); v.Set(20, 0, 0, wood); v.Set(0, 0, 8, wood); v.Set(20, 0, 8, wood);
            return Save("Divano", v, BottomCenter(v, 21, 9));
        }

        public static GameObject CoffeeTable()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#6a4527"), mag1 = v.C("#c23b3b"), mag2 = v.C("#3b6fc2");
            v.Fill(0, 3, 0, 10, 4, 6, wood);
            v.Fill(0, 0, 0, 1, 3, 1, wood); v.Fill(9, 0, 0, 10, 3, 1, wood); v.Fill(0, 0, 5, 1, 3, 6, wood); v.Fill(9, 0, 5, 10, 3, 6, wood);
            v.Set(2, 4, 2, mag1); v.Set(3, 4, 2, mag1); v.Set(6, 4, 3, mag2);
            return Save("Tavolino", v, BottomCenter(v, 10, 6));
        }

        /// <summary>Tappeto a motivi (cubetti da 10 cm ma alti 1: va appena sopra il pavimento).</summary>
        public static GameObject Rug()
        {
            var v = new VoxelBuilder(0.1f);
            int a = v.C("#7a2e2e"), b = v.C("#b38a52"), c = v.C("#2e3e6a");
            for (int x = 0; x < 24; x++)
                for (int z = 0; z < 16; z++)
                {
                    bool border = x == 0 || z == 0 || x == 23 || z == 15;
                    bool inner = (x + z) % 4 == 0 || (x - z + 32) % 4 == 0;
                    v.Set(x, 0, z, border ? c : inner ? b : a);
                }
            var go = Save("Tappeto", v, new Vector3(12f * v.size, 0.095f, 8f * v.size), false);
            return go;
        }

        public static GameObject HallTable()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5a3c24");
            v.Fill(0, 7, 0, 8, 8, 4, wood);
            v.Fill(0, 0, 0, 1, 7, 1, wood); v.Fill(7, 0, 0, 8, 7, 1, wood); v.Fill(0, 0, 3, 1, 7, 4, wood); v.Fill(7, 0, 3, 8, 7, 4, wood);
            v.Fill(1, 5, 0, 7, 6, 4, wood);                  // ripiano basso
            return Save("Tavolino ingresso", v, BottomCenter(v, 8, 4));
        }

        /// <summary>Telefono fisso anni '90 (cubetti da 3 cm): corpo beige, cornetta, tastiera.</summary>
        public static GameObject Phone()
        {
            var v = new VoxelBuilder(0.03f);
            int body = v.C("#d9cfb4"), dark = v.C("#3a3630"), key = v.C("#efe9dc"), red = v.C("#c0392b");
            v.Fill(0, 0, 0, 6, 2, 5, body);
            v.Fill(0, 2, 0, 6, 3, 2, body);
            v.Fill(0, 3, 1, 6, 4, 2, dark);                  // cornetta
            v.Set(0, 3, 0, dark); v.Set(5, 3, 0, dark);
            for (int x = 1; x < 5; x++) for (int z = 2; z < 4; z++) v.Set(x, 2, z, key);
            v.Set(5, 2, 4, red);
            return Save("Telefono", v, BottomCenter(v, 6, 5));
        }

        public static GameObject Bookshelf()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5d3e25");
            string[] books = { "#8b2b2b", "#2b5d8b", "#3f7a3a", "#c9a24a", "#6b3a7a", "#d8d2c0" };
            v.Fill(0, 0, 0, 9, 18, 3, wood);
            for (int s = 0; s < 4; s++)
            {
                int y = 1 + s * 4;
                v.Carve(1, y, 1, 8, y + 3, 3);
                for (int x = 1; x < 8; x++)
                {
                    int h = 2 + (x * 7 + s * 3) % 2;
                    v.Fill(x, y, 1, x + 1, y + h, 3, v.C(books[(x + s * 2) % books.Length]));
                }
            }
            return Save("Libreria", v, BottomCenter(v, 9, 3));
        }

        // ------------------------------------------------------------------ cucina e pranzo

        public static GameObject DiningTable()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#7a5230");
            v.Fill(0, 7, 0, 18, 8, 9, wood);
            v.Fill(1, 0, 1, 2, 7, 2, wood); v.Fill(16, 0, 1, 17, 7, 2, wood); v.Fill(1, 0, 7, 2, 7, 8, wood); v.Fill(16, 0, 7, 17, 7, 8, wood);
            return Save("Tavolo", v, BottomCenter(v, 18, 9));
        }

        public static GameObject Chair()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#7a5230"), seat = v.C("#6a4628");
            v.Fill(0, 4, 0, 5, 5, 5, seat);
            v.Fill(0, 5, 0, 5, 10, 1, wood);                 // schienale
            v.Carve(1, 6, 0, 4, 9, 1);
            v.Fill(0, 0, 0, 1, 4, 1, wood); v.Fill(4, 0, 0, 5, 4, 1, wood); v.Fill(0, 0, 4, 1, 4, 5, wood); v.Fill(4, 0, 4, 5, 4, 5, wood);
            return Save("Sedia", v, BottomCenter(v, 5, 5));
        }

        /// <summary>Frigorifero color mandorla (0.8 x 1.8 x 0.7 m) con freezer sopra.</summary>
        public static GameObject Fridge()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#e3dcc4"), line = v.C("#b9b19a"), handle = v.C("#7a7466"), magnet = v.C("#d13b3b"), magnet2 = v.C("#3bb0d1");
            v.Fill(0, 0, 0, 8, 18, 7, body);
            v.Paint(0, 12, 6, 8, 13, 7, line);               // divisione freezer
            v.Fill(1, 13, 7, 2, 16, 8, handle);
            v.Fill(1, 6, 7, 2, 11, 8, handle);
            v.Set(5, 9, 7, magnet); v.Set(4, 14, 7, magnet2);
            return Save("Frigo", v, BottomCenter(v, 8, 7));
        }

        public static GameObject Stove()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#e8e4da"), top = v.C("#2a2a2a"), burner = v.C("#5a5a5a"), glass = v.C("#1a1a1a"), knob = v.C("#cfcfcf");
            v.Fill(0, 0, 0, 7, 9, 6, body);
            v.Paint(0, 8, 0, 7, 9, 6, top);
            v.Set(1, 9, 1, burner); v.Set(5, 9, 1, burner); v.Set(1, 9, 4, burner); v.Set(5, 9, 4, burner);
            v.Paint(1, 2, 5, 6, 6, 6, glass);                // vetro forno
            for (int x = 1; x < 6; x += 2) v.Set(x, 7, 6, knob);
            v.Fill(0, 9, 0, 7, 11, 1, body);                 // alzatina
            return Save("Fornello", v, BottomCenter(v, 7, 6));
        }

        /// <summary>Bancone con lavello (2 m), variante del bancone normale.</summary>
        public static GameObject CounterSink()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), handle = v.C("#3a3a3a"), top = v.C("#b6a487"), plinth = v.C("#4a4540");
            int steel = v.C("#a8acb0"), tap = v.C("#d0d4d8");
            v.Fill(0, 0, 0, 20, 8, 6, body);
            v.Fill(0, 0, 5, 20, 1, 6, plinth);
            v.Fill(0, 8, 0, 20, 9, 7, top);
            v.Fill(6, 8, 2, 14, 9, 6, steel);
            v.Carve(7, 8, 3, 13, 9, 6);                      // vasca
            v.Fill(9, 9, 1, 11, 11, 2, tap);                 // rubinetto
            v.Set(9, 11, 2, tap); v.Set(10, 11, 2, tap); v.Set(10, 11, 3, tap);
            for (int i = 0; i < 4; i++)
            {
                int x0 = i * 5;
                v.Fill(x0 + 1, 1, 6, x0 + 4, 7, 7, door);
                v.Set(x0 + 3, 5, 7, handle);
            }
            return Save("Bancone lavello", v, BottomCenter(v, 20, 6));
        }

        /// <summary>Isola della cucina (2 x 0.9 x 0.9 m), ante su entrambi i lati.</summary>
        public static GameObject Island()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), top = v.C("#b6a487"), handle = v.C("#3a3a3a");
            v.Fill(0, 0, 1, 20, 8, 8, body);
            v.Fill(-1, 8, 0, 21, 9, 9, top);
            for (int i = 0; i < 4; i++)
            {
                int x0 = i * 5;
                v.Fill(x0 + 1, 1, 8, x0 + 4, 7, 9, door);
                v.Fill(x0 + 1, 1, 0, x0 + 4, 7, 1, door);
                v.Set(x0 + 3, 5, 9, handle);
            }
            return Save("Isola cucina", v, BottomCenter(v, 20, 9));
        }

        // ------------------------------------------------------------------ bagno

        public static GameObject Toilet()
        {
            var v = new VoxelBuilder(0.06f);
            int white = v.C("#efefea"), seat = v.C("#e2e2dc"), dark = v.C("#cfd6d8");
            v.Fill(2, 0, 2, 6, 5, 7, white);                 // base
            v.Fill(1, 5, 1, 7, 6, 9, seat);                  // tavoletta
            v.Carve(2, 5, 3, 6, 6, 8);
            v.Paint(2, 4, 3, 6, 5, 8, dark);
            v.Fill(1, 5, 0, 7, 12, 2, white);                // cassetta
            return Save("WC", v, BottomCenter(v, 8, 9));
        }

        public static GameObject BathSink()
        {
            var v = new VoxelBuilder(0.06f);
            int white = v.C("#efefea"), tap = v.C("#c9ccd0"), mirror = v.C("#9fb3bd");
            v.Fill(3, 0, 1, 6, 13, 4, white);                // colonna
            v.Fill(0, 13, 0, 9, 15, 7, white);               // lavabo
            v.Carve(1, 14, 2, 8, 15, 6);
            v.Fill(4, 15, 0, 5, 17, 2, tap);
            v.Fill(1, 19, 0, 8, 29, 1, mirror);              // specchio
            return Save("Lavandino", v, new Vector3(4.5f * v.size, 0f, 0f));
        }

        public static GameObject Bathtub()
        {
            var v = new VoxelBuilder(0.1f);
            int white = v.C("#efefea"), tap = v.C("#c9ccd0");
            v.Fill(0, 0, 0, 17, 6, 8, white);
            v.Carve(1, 1, 1, 16, 6, 7);
            v.Set(1, 6, 4, tap); v.Set(1, 7, 4, tap);
            return Save("Vasca", v, BottomCenter(v, 17, 8));
        }

        // ------------------------------------------------------------------ camere

        public static GameObject BedDouble()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5a3c24"), mattress = v.C("#e8e4da"), blanket = v.C("#3a5a7a"), pillow = v.C("#f2efe6");
            v.Fill(0, 0, 0, 16, 3, 21, wood);
            v.Fill(1, 3, 1, 15, 5, 21, mattress);
            v.Fill(1, 5, 7, 15, 6, 21, blanket);
            v.Fill(0, 3, 21, 16, 6, 22, blanket);            // bordo coperta
            v.Fill(2, 5, 2, 7, 6, 5, pillow); v.Fill(9, 5, 2, 14, 6, 5, pillow);
            v.Fill(0, 3, 0, 16, 10, 1, wood);                // testiera
            return Save("Letto matrimoniale", v, BottomCenter(v, 16, 22));
        }

        public static GameObject BedSingle()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#6a4628"), mattress = v.C("#e8e4da"), blanket = v.C("#8a3a3a"), pillow = v.C("#f2efe6");
            v.Fill(0, 0, 0, 10, 3, 20, wood);
            v.Fill(1, 3, 1, 9, 5, 20, mattress);
            v.Fill(1, 5, 6, 9, 6, 20, blanket);
            v.Fill(2, 5, 2, 8, 6, 5, pillow);
            v.Fill(0, 3, 0, 10, 8, 1, wood);
            return Save("Letto singolo", v, BottomCenter(v, 10, 20));
        }

        public static GameObject Nightstand()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5a3c24"), knob = v.C("#c9a24a"), lamp = v.C("#efe0b0"), lampBase = v.C("#3a3630");
            v.Fill(0, 0, 0, 4, 5, 4, wood);
            v.Set(2, 3, 4, knob);
            v.Set(2, 5, 2, lampBase);
            v.Fill(1, 6, 1, 4, 8, 4, lamp);
            return Save("Comodino", v, BottomCenter(v, 4, 4));
        }

        public static GameObject Wardrobe()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5d3e25"), line = v.C("#45301d"), knob = v.C("#c9a24a");
            v.Fill(0, 0, 0, 12, 20, 6, wood);
            v.Paint(6, 1, 5, 7, 19, 6, line);
            v.Set(5, 10, 6, knob); v.Set(7, 10, 6, knob);
            return Save("Armadio", v, BottomCenter(v, 12, 6));
        }

        public static GameObject Desk()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#7a5230"), drawer = v.C("#6a4628"), knob = v.C("#c9a24a"), paper = v.C("#f2efe6");
            v.Fill(0, 7, 0, 12, 8, 6, wood);
            v.Fill(0, 0, 0, 1, 7, 6, wood);
            v.Fill(8, 0, 0, 12, 7, 6, drawer);
            v.Set(10, 5, 6, knob); v.Set(10, 2, 6, knob);
            v.Set(3, 8, 3, paper); v.Set(4, 8, 3, paper);
            return Save("Scrivania", v, BottomCenter(v, 12, 6));
        }

        // ------------------------------------------------------------------ cantina e garage

        /// <summary>Scaffale metallico pieno di scatole e barattoli.</summary>
        public static GameObject MetalShelf()
        {
            var v = new VoxelBuilder(0.1f);
            int metal = v.C("#5c6066");
            string[] junk = { "#a3743f", "#8a6236", "#b48850", "#6b8a5a", "#c0392b", "#d8d2c0" };
            for (int y = 0; y < 19; y++) { v.Set(0, y, 0, metal); v.Set(11, y, 0, metal); v.Set(0, y, 3, metal); v.Set(11, y, 3, metal); }
            for (int s = 0; s < 4; s++)
            {
                int y = 1 + s * 5;
                v.Fill(0, y, 0, 12, y + 1, 4, metal);
                for (int x = 1; x < 11; x++)
                {
                    int h = 1 + (x * 5 + s * 3) % 4;
                    if ((x + s) % 4 == 3) continue;
                    v.Fill(x, y + 1, 1, x + 1, y + 1 + Mathf.Min(h, 3), 3, v.C(junk[(x * 3 + s) % junk.Length]));
                }
            }
            return Save("Scaffale", v, BottomCenter(v, 12, 4));
        }

        public static GameObject Box(string name, int sx, int sy, int sz)
        {
            var v = new VoxelBuilder(0.1f);
            int card = v.C("#a3743f"), tape = v.C("#c9b48a");
            v.Fill(0, 0, 0, sx, sy, sz, card);
            v.Paint(sx / 2, sy - 1, 0, sx / 2 + 1, sy, sz, tape);
            return Save(name, v, BottomCenter(v, sx, sz));
        }

        /// <summary>
        /// Generatore d'emergenza grande (1.4 x 1 x 0.9 m): telaio a tubi, motore, alternatore, serbatoio rosso,
        /// marmitta, pannello comandi sul davanti (+Z) con prese; la spia rossa la aggiunge la scena (luce vera).
        /// </summary>
        public static GameObject Generator()
        {
            var v = new VoxelBuilder(0.1f);
            int frame = v.C("#262626"), engine = v.C("#6f7277"), engineDark = v.C("#4a4d52"), tank = v.C("#a8261f"),
                cord = v.C("#111111"), label = v.C("#e3c23a"), outlet = v.C("#e8e4da"), panel = v.C("#1c1c1e"), rust = v.C("#6a3a22"), pipe = v.C("#3a3530");
            // telaio a tubi
            foreach (int x in new[] { 0, 13 }) foreach (int z in new[] { 0, 8 }) v.Fill(x, 0, z, x + 1, 10, z + 1, frame);
            v.Fill(0, 0, 0, 14, 1, 1, frame); v.Fill(0, 0, 8, 14, 1, 9, frame);
            v.Fill(0, 9, 0, 14, 10, 1, frame); v.Fill(0, 9, 8, 14, 10, 9, frame);
            v.Fill(0, 0, 0, 1, 1, 9, frame); v.Fill(13, 0, 0, 14, 1, 9, frame);
            // motore con alette
            v.Fill(1, 1, 1, 7, 6, 8, engine);
            for (int y = 2; y < 6; y += 2) v.Fill(1, y, 1, 7, y + 1, 8, engineDark);
            v.Fill(2, 6, 2, 6, 7, 7, engineDark);
            // alternatore
            v.Fill(7, 1, 2, 13, 6, 7, engineDark);
            v.Fill(8, 2, 1, 12, 5, 8, engine);
            // serbatoio rosso con tappo ed etichetta, qualche macchia di ruggine
            v.Fill(1, 7, 1, 13, 9, 8, tank);
            v.Fill(10, 9, 4, 12, 10, 6, frame);
            v.Fill(3, 8, 8, 7, 9, 8 + 1, label);
            v.Set(2, 7, 1, rust); v.Set(11, 8, 1, rust); v.Set(6, 7, 7, rust);
            // marmitta sul retro
            v.Fill(2, 3, 0, 6, 5, 1, pipe);
            // pannello comandi davanti con prese e (a destra) il posto della spia
            v.Fill(8, 1, 8, 13, 6, 9, panel);
            v.Set(9, 2, 8, outlet); v.Set(11, 2, 8, outlet); v.Set(9, 4, 8, outlet);
            // maniglia della corda d'avviamento
            v.Fill(1, 4, 8, 3, 5, 9, cord);
            return Save("Generatore", v, BottomCenter(v, 14, 9));
        }

        public static GameObject FuelCan()
        {
            var v = new VoxelBuilder(0.05f);
            int red = v.C("#b02a24"), cap = v.C("#e3c23a");
            v.Fill(0, 0, 0, 6, 7, 3, red);
            v.Fill(1, 7, 1, 4, 8, 2, red);
            v.Set(5, 7, 1, cap);
            return Save("Tanica", v, BottomCenter(v, 6, 3));
        }

        public static GameObject WaterHeater()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#d8d8d2"), pipe = v.C("#b87333"), panel = v.C("#3a3a3a");
            v.Fill(0, 0, 0, 6, 16, 6, body);
            v.Carve(0, 0, 0, 1, 16, 1); v.Carve(5, 0, 0, 6, 16, 1); v.Carve(0, 0, 5, 1, 16, 6); v.Carve(5, 0, 5, 6, 16, 6);
            v.Fill(2, 16, 2, 3, 20, 3, pipe); v.Fill(3, 16, 3, 4, 20, 4, pipe);
            v.Set(3, 3, 6, panel);
            return Save("Caldaia", v, BottomCenter(v, 6, 6));
        }

        public static GameObject WashingMachine()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#ecebe4"), door = v.C("#3a4048"), knob = v.C("#9aa0a6");
            v.Fill(0, 0, 0, 7, 9, 7, body);
            v.Fill(2, 2, 7, 5, 5, 8, door);
            v.Set(1, 8, 7, knob); v.Set(5, 8, 7, knob);
            return Save("Lavatrice", v, BottomCenter(v, 7, 7));
        }

        public static GameObject Workbench()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#8a6a46"), dark = v.C("#4a3826"), tool = v.C("#c0392b"), metal = v.C("#8a8e94"), board = v.C("#b49a74");
            v.Fill(0, 8, 0, 16, 9, 6, wood);
            v.Fill(0, 0, 0, 1, 8, 1, dark); v.Fill(15, 0, 0, 16, 8, 1, dark); v.Fill(0, 0, 5, 1, 8, 6, dark); v.Fill(15, 0, 5, 16, 8, 6, dark);
            v.Fill(1, 3, 0, 15, 4, 6, dark);
            v.Fill(0, 9, 0, 16, 18, 1, board);                // pannello portattrezzi
            v.Set(3, 13, 1, tool); v.Set(3, 14, 1, tool); v.Set(6, 12, 1, metal); v.Set(6, 13, 1, metal); v.Set(6, 14, 1, metal);
            v.Set(10, 15, 1, metal); v.Set(11, 15, 1, metal); v.Set(4, 9, 3, tool); v.Set(9, 9, 2, metal);
            return Save("Banco da lavoro", v, BottomCenter(v, 16, 6));
        }

        /// <summary>Porta interna bianca (0.8 x 2.1 m, spessa 10 cm). Cardine sul bordo sinistro (x = 0).</summary>
        public static GameObject InteriorDoor()
        {
            var v = new VoxelBuilder(0.1f);
            int white = v.C("#e6e2d6"), panel = v.C("#d2cdbf"), brass = v.C("#c9a24a");
            v.Fill(0, 0, 0, 8, 21, 1, white);
            v.Paint(1, 2, 0, 7, 9, 1, panel);
            v.Paint(1, 11, 0, 7, 19, 1, panel);
            v.Set(6, 10, -1, brass); v.Set(6, 10, 1, brass);
            return Save("Porta interna", v, new Vector3(0f, 0f, 0.5f * v.size));
        }

        /// <summary>Portone del garage sezionale (2.7 x 2.3 m) bianco a pannelli orizzontali.</summary>
        public static GameObject GarageDoor()
        {
            var v = new VoxelBuilder(0.1f);
            int white = v.C("#e8e6de"), line = v.C("#c4c1b6"), handle = v.C("#3a3a3a");
            v.Fill(0, 0, 0, 27, 23, 1, white);
            for (int y = 5; y < 23; y += 6) v.Paint(0, y, 0, 27, y + 1, 1, line);
            v.Set(13, 2, -1, handle);
            return Save("Portone garage", v, new Vector3(13.5f * v.size, 0f, 0.5f * v.size));
        }

        // ------------------------------------------------------------------ esterno

        public static GameObject Mailbox()
        {
            var v = new VoxelBuilder(0.08f);
            int post = v.C("#4a3826"), box = v.C("#2a2f3a"), flag = v.C("#c0392b");
            v.Fill(1, 0, 1, 2, 10, 2, post);
            v.Fill(0, 10, 0, 3, 12, 5, box);
            v.Fill(3, 11, 2, 4, 13, 3, flag);
            return Save("Cassetta posta", v, BottomCenter(v, 3, 3));
        }

        /// <summary>Albero (cubetti da 30 cm): tronco e chioma a strati, verde con variazioni.</summary>
        public static GameObject Tree()
        {
            var v = new VoxelBuilder(0.3f);
            int trunk = v.C("#4a3420"), leaf1 = v.C("#2c4a24"), leaf2 = v.C("#365a2a"), leaf3 = v.C("#22401e");
            v.Fill(4, 0, 4, 5, 8, 5, trunk);
            for (int y = 6; y < 15; y++)
            {
                int r = y < 9 ? 3 + (y - 6) : y < 12 ? 4 : 4 - (y - 11);
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                    {
                        if (x * x + z * z > r * r + 1) continue;
                        int c = (x * 3 + y * 5 + z * 7 + 99) % 3 == 0 ? leaf1 : (x + z + y) % 2 == 0 ? leaf2 : leaf3;
                        v.Set(4 + x, y, 4 + z, c);
                    }
            }
            // le foglie ondeggiano al vento (dalla base della chioma in su), il tronco no
            return Save("Albero", v, new Vector3(4.5f * v.size, 0f, 4.5f * v.size), true,
                c => c.g > c.r * 1.15f ? EditorAssetUtil.WindMat("Vento foglie " + ColorUtility.ToHtmlStringRGB(c), c, 0.09f, 1.7f, 2.8f) : null);
        }

        public static GameObject Bush()
        {
            var v = new VoxelBuilder(0.25f);
            int a = v.C("#2f4f2a"), b = v.C("#3b6234");
            v.Fill(0, 0, 0, 4, 2, 3, a);
            v.Fill(1, 2, 0, 3, 3, 3, b);
            v.Set(0, 2, 1, b);
            return Save("Cespuglio", v, BottomCenter(v, 4, 3), true,
                c => EditorAssetUtil.WindMat("Vento cespuglio " + ColorUtility.ToHtmlStringRGB(c), c, 0.04f, 0.1f, 0.6f));
        }

        /// <summary>Auto anni '90 (cubetti da 15 cm, 4.5 x 1.3 x 1.8 m): berlina o station wagon. Il muso guarda +X.</summary>
        public static GameObject Car(string name, string colorHex, bool wagon)
        {
            var v = new VoxelBuilder(0.15f);
            int body = v.C(colorHex), glass = v.C("#1c2630"), tyre = v.C("#151515"), rim = v.C("#9a9ea4");
            int head = v.C("#f2ecd0"), tail = v.C("#b01e1e"), bumper = v.C("#3a3a3a"), plate = v.C("#e8e6de");

            v.Fill(0, 1, 0, 30, 5, 12, body);                         // carrozzeria
            int cabinStart = wagon ? 3 : 8, cabinEnd = 21;
            v.Fill(cabinStart, 5, 1, cabinEnd, 8, 11, body);           // abitacolo
            v.Paint(cabinStart + 1, 5, 1, cabinEnd - 1, 8, 2, glass);  // finestrini laterali
            v.Paint(cabinStart + 1, 5, 10, cabinEnd - 1, 8, 11, glass);
            v.Paint(cabinEnd - 1, 5, 2, cabinEnd, 8, 10, glass);       // parabrezza
            v.Paint(cabinStart, 5, 2, cabinStart + 1, 8, 10, glass);   // lunotto
            v.Paint(13, 5, 1, 14, 8, 2, body);                         // montanti
            v.Paint(13, 5, 10, 14, 8, 11, body);
            foreach (int x in new[] { 4, 22 })                          // ruote
                foreach (int z in new[] { 0, 11 })
                {
                    v.Fill(x, 0, z, x + 4, 3, z + 1, tyre);
                    v.Set(x + 1, 1, z, rim); v.Set(x + 2, 1, z, rim);
                    if (z == 11) { v.Set(x + 1, 1, 11, rim); v.Set(x + 2, 1, 11, rim); }
                }
            v.Fill(29, 1, 0, 31, 2, 12, bumper);                       // paraurti
            v.Fill(-1, 1, 0, 1, 2, 12, bumper);
            v.Paint(29, 3, 1, 30, 4, 3, head); v.Paint(29, 3, 9, 30, 4, 11, head);  // fari
            v.Paint(0, 3, 1, 1, 4, 3, tail); v.Paint(0, 3, 9, 1, 4, 11, tail);      // stop
            v.Fill(-1, 2, 5, 0, 3, 7, plate);
            return Save(name, v, BottomCenter(v, 30, 12));
        }

        public static GameObject Grill()
        {
            var v = new VoxelBuilder(0.08f);
            int black = v.C("#1e1e1e"), grey = v.C("#5a5a5a");
            v.Fill(1, 0, 1, 2, 8, 2, grey); v.Fill(5, 0, 1, 6, 8, 2, grey); v.Fill(3, 0, 5, 4, 8, 6, grey);
            v.Fill(0, 8, 0, 7, 11, 7, black);
            v.Fill(1, 11, 1, 6, 12, 6, black);
            return Save("Barbecue", v, BottomCenter(v, 7, 7));
        }

        public static GameObject LawnChair()
        {
            var v = new VoxelBuilder(0.08f);
            int frame = v.C("#d8d8d2"), strap = v.C("#3a7a8a");
            v.Fill(0, 0, 0, 1, 5, 1, frame); v.Fill(6, 0, 0, 7, 5, 1, frame); v.Fill(0, 0, 6, 1, 5, 7, frame); v.Fill(6, 0, 6, 7, 5, 7, frame);
            v.Fill(0, 5, 0, 7, 6, 7, strap);
            v.Fill(0, 6, 0, 7, 12, 1, strap);
            return Save("Sedia da giardino", v, BottomCenter(v, 7, 7));
        }

        public static GameObject TrashCan()
        {
            var v = new VoxelBuilder(0.08f);
            int green = v.C("#2f5a35"), lid = v.C("#24452a");
            v.Fill(0, 0, 0, 7, 12, 7, green);
            v.Fill(-1, 12, -1, 8, 13, 8, lid);
            return Save("Bidone", v, BottomCenter(v, 7, 7));
        }

        /// <summary>Plafoniera (perno al centro del lato superiore, va attaccata al soffitto).</summary>
        public static GameObject CeilingLamp()
        {
            var v = new VoxelBuilder(0.06f);
            int glass = v.C("#f3e8c8"), metal = v.C("#c9a24a");
            v.Fill(0, 0, 0, 5, 2, 5, glass);
            v.Fill(1, 2, 1, 4, 3, 4, metal);
            return Save("Plafoniera", v, new Vector3(2.5f * v.size, 3f * v.size, 2.5f * v.size), false);
        }

        /// <summary>Lampadina appesa al filo (cantina). Perno in alto.</summary>
        public static GameObject Bulb()
        {
            var v = new VoxelBuilder(0.04f);
            int wire = v.C("#1a1a1a"), bulb = v.C("#f6f0d0");
            v.Fill(1, 3, 1, 2, 10, 2, wire);
            v.Fill(0, 0, 0, 3, 3, 3, bulb);
            return Save("Lampadina", v, new Vector3(1.5f * v.size, 10f * v.size, 1.5f * v.size), false);
        }

        public static GameObject PorchPillar()
        {
            var v = new VoxelBuilder(0.1f);
            int white = v.C("#ecebe4");
            v.Fill(0, 0, 0, 3, 1, 3, white);
            v.Fill(1, 1, 1, 2, 27, 2, white);
            v.Fill(0, 27, 0, 3, 28, 3, white);
            return Save("Pilastro veranda", v, BottomCenter(v, 3, 3));
        }
    }
}
