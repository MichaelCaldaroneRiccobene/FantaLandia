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
            ChairVariant("Sedia con giacca", true); Shoes(); Plant(); Picture("Quadro A", "#c9a24a", "#3a6a8a", "#d8b060"); Picture("Quadro B", "#3a2a1a", "#7a9a5a", "#e0d0a0");
            Picture("Quadro C", "#e8e2d2", "#a03a3a", "#2e4a6a"); WallClock(); LaundryBasket(); Backpack();
            Generator(); Radio(); FuelCan(); WaterHeater(); WashingMachine(); Workbench(); InteriorDoor(); GarageDoor();
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


        /// <summary>Divano a tre posti (2.1 x 0.9 x 0.9 m, cubetti da 5 cm): piedini, cuscini separati e un po' consumati, bottoni, cuscino decorativo storto, plaid sul bracciolo.</summary>
        public static GameObject Sofa()
        {
            var v = new VoxelBuilder(0.05f);
            int fabric = v.C("#8a7244"), dark = v.C("#76603a"), cushion = v.C("#9a8050"), worn = v.C("#ab9262"), wood = v.C("#3c2a1a"),
                piping = v.C("#6e5a34"), button = v.C("#5a4828"), pillow = v.C("#3a5a6a"), pillowB = v.C("#c9b98f"), plaidA = v.C("#7a2e2e"), plaidB = v.C("#2e4a3a"), crumb = v.C("#c9a86a");
            foreach (int x in new[] { 1, 39 }) foreach (int z in new[] { 1, 15 }) v.Fill(x, 0, z, x + 2, 2, z + 2, wood);  // piedini
            v.Fill(0, 2, 0, 42, 8, 18, fabric);                                   // base
            v.Fill(0, 7, 17, 42, 8, 18, piping);                                  // profilo davanti
            v.Fill(0, 8, 0, 42, 18, 4, fabric);                                   // schienale
            v.Carve(0, 17, 0, 1, 18, 4); v.Carve(41, 17, 0, 42, 18, 4);
            v.Fill(0, 8, 4, 4, 12, 18, fabric); v.Fill(0, 12, 4, 4, 13, 17, dark); // braccioli arrotondati
            v.Fill(38, 8, 4, 42, 12, 18, fabric); v.Fill(38, 12, 4, 42, 13, 17, dark);
            int[][] seats = { new[] { 4, 14 }, new[] { 15, 27 }, new[] { 28, 38 } };
            foreach (var s in seats)
            {
                v.Fill(s[0], 8, 4, s[1], 9, 17, cushion);                          // cuscino seduta
                v.Fill(s[0], 9, 4, s[1], 10, 16, cushion);
                v.Fill(s[0], 10, 4, s[1], 15, 7, cushion);                         // cuscino schienale, inclinato
                v.Fill(s[0], 15, 4, s[1], 17, 6, cushion);
                v.Set((s[0] + s[1]) / 2, 13, 6, button);
            }
            // il posto di mezzo è il più usato: tessuto consumato e qualche briciola
            v.Paint(18, 9, 9, 24, 10, 14, worn); v.Set(20, 10, 13, crumb); v.Set(23, 10, 10, crumb);
            // cuscino decorativo storto nell'angolo sinistro
            v.Fill(5, 10, 7, 10, 15, 9, pillow); v.Carve(5, 14, 7, 6, 15, 9); v.Carve(9, 10, 7, 10, 11, 9); v.Set(7, 12, 9, pillowB);
            // plaid a quadri buttato sul bracciolo destro, che pende fuori
            for (int z = 4; z < 18; z++)
            {
                int c = ((z / 2) % 2 == 0) ? plaidA : plaidB;
                v.Fill(38, 13, z, 42, 14, z + 1, c);
                v.Fill(42, 6, z, 43, 13, z + 1, c);
            }
            return Save("Divano", v, BottomCenter(v, 42, 18));
        }


        /// <summary>Tavolino da salotto (1 x 0.6 x 0.4 m, cubetti da 5 cm): ripiano basso con riviste, sopra lattine, telecomando e guida TV.</summary>
        public static GameObject CoffeeTable()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#6a4527"), edge = v.C("#553620"), mag1 = v.C("#c23b3b"), mag2 = v.C("#3b6fc2"), mag3 = v.C("#e0c040"),
                can = v.C("#b02a24"), canB = v.C("#2a4aa0"), silver = v.C("#c8c8cc"), remote = v.C("#1c1c1e"), red = v.C("#d03030"), ring = v.C("#4a2e1a");
            v.Fill(0, 6, 0, 20, 8, 12, wood);
            v.Fill(0, 6, 0, 20, 7, 12, edge);
            foreach (int x in new[] { 1, 17 }) foreach (int z in new[] { 1, 9 }) v.Fill(x, 0, z, x + 2, 6, z + 2, wood);
            v.Fill(2, 2, 2, 18, 3, 10, wood);                                     // ripiano basso
            v.Fill(4, 3, 3, 10, 4, 8, mag2); v.Fill(5, 4, 3, 11, 5, 8, mag1);     // riviste impilate
            v.Fill(12, 3, 4, 16, 4, 9, mag3);
            v.Fill(3, 8, 3, 4, 10, 4, can); v.Set(3, 10, 3, silver);             // lattina in piedi
            v.Fill(5, 8, 7, 8, 9, 8, canB); v.Set(8, 8, 7, silver);              // lattina rovesciata
            v.Fill(10, 8, 5, 14, 9, 9, mag1); v.Fill(11, 8, 6, 13, 9, 8, mag3);   // guida TV
            v.Fill(15, 8, 3, 16, 9, 7, remote); v.Set(15, 9, 3, red);            // telecomando
            v.Set(16, 8, 9, ring); v.Set(17, 8, 9, ring); v.Set(16, 8, 10, ring); // alone di un bicchiere
            return Save("Tavolino", v, BottomCenter(v, 20, 12));
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


        /// <summary>Libreria (0.9 x 1.8 x 0.3 m, cubetti da 5 cm): libri di altezze diverse, uno inclinato, foto, piantina, videocassette, trofeo.</summary>
        public static GameObject Bookshelf()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5d3e25"), woodDark = v.C("#4a311d"), frame = v.C("#c9a24a"), photo = v.C("#7aa0c0"), pot = v.C("#a0522d"), leaf = v.C("#3f7a3a"),
                vhs = v.C("#151517"), label = v.C("#e8e2d2"), gold = v.C("#d4af37");
            string[] books = { "#8b2b2b", "#2b5d8b", "#3f7a3a", "#c9a24a", "#6b3a7a", "#d8d2c0", "#3a3a3a", "#b0603a" };
            v.Fill(0, 0, 0, 18, 36, 6, wood);
            v.Fill(0, 34, 0, 18, 36, 7, woodDark);                                // cornice in alto
            for (int s = 0; s < 4; s++)
            {
                int y = 2 + s * 8;
                v.Carve(1, y, 1, 17, y + 7, 6);
                int x = 1;
                while (x < 17)
                {
                    int kind = (x * 7 + s * 5) % 11;
                    if (s == 1 && x >= 11 && x < 15) { v.Fill(12, y, 2, 14, y + 4, 3, frame); v.Fill(12, y + 1, 3, 14, y + 3, 4, photo); x = 15; continue; }
                    if (s == 2 && x >= 12) { v.Fill(13, y, 2, 16, y + 2, 5, pot); v.Fill(13, y + 2, 2, 16, y + 5, 5, leaf); v.Set(14, y + 5, 3, leaf); break; }
                    if (s == 3 && x >= 9 && x < 13) { for (int k = 0; k < 4; k++) { v.Fill(9 + k, y, 1, 10 + k, y + 4, 5, vhs); v.Set(9 + k, y + 2, 5, label); } x = 13; continue; }
                    if (s == 0 && x == 14) { v.Fill(14, y, 2, 16, y + 1, 4, wood); v.Fill(14, y + 1, 2, 16, y + 4, 3, gold); x = 17; continue; }
                    if (kind == 3) { x++; continue; }                             // un buco
                    int h = 4 + (x * 3 + s) % 3;
                    v.Fill(x, y, 1, x + 1, y + h, 6, v.C(books[(x + s * 3) % books.Length]));
                    x++;
                }
                // un libro inclinato appoggiato all'ultimo
                if (s == 0) { v.Fill(10, 2, 1, 11, 6, 6, v.C(books[5])); v.Fill(11, 5, 1, 12, 8, 6, v.C(books[5])); }
            }
            return Save("Libreria", v, BottomCenter(v, 18, 6));
        }

        // ------------------------------------------------------------------ cucina e pranzo


        /// <summary>Tavolo da pranzo (1.8 x 0.9 x 0.8 m, cubetti da 5 cm): gambe tornite, runner, fruttiera, tazza, giornale.</summary>
        public static GameObject DiningTable()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#7a5230"), woodDark = v.C("#5e3e22"), runner = v.C("#c9b48a"), bowl = v.C("#8a5a30"), apple = v.C("#b02a24"), banana = v.C("#e3c23a"),
                pear = v.C("#7aa03a"), mug = v.C("#efe9dc"), paper = v.C("#d8d2c0"), ink = v.C("#5a5a5a"), salt = v.C("#f2f2f2"), pepper = v.C("#3a3a3a");
            v.Fill(0, 14, 0, 36, 16, 18, wood);
            v.Fill(0, 14, 0, 36, 15, 18, woodDark);
            v.Fill(2, 12, 2, 34, 14, 16, woodDark);                               // fascia sotto il piano
            foreach (int x in new[] { 2, 32 }) foreach (int z in new[] { 2, 14 })
            {
                v.Fill(x, 0, z, x + 2, 12, z + 2, wood);
                v.Fill(x - 1, 4, z - 1, x + 3, 5, z + 3, woodDark);              // anello tornito
                v.Fill(x - 1, 9, z - 1, x + 3, 10, z + 3, woodDark);
            }
            v.Fill(4, 16, 7, 32, 17, 11, runner);                                 // runner
            v.Fill(15, 17, 7, 21, 18, 11, bowl); v.Carve(16, 17, 8, 20, 18, 10);  // fruttiera
            v.Set(16, 17, 8, apple); v.Set(17, 18, 9, apple); v.Fill(18, 17, 8, 20, 18, 9, banana); v.Set(19, 18, 9, pear);
            v.Fill(6, 16, 3, 8, 18, 5, mug);                                      // tazza dimenticata
            v.Fill(26, 16, 12, 32, 17, 17, paper); v.Fill(27, 16, 13, 31, 17, 14, ink); v.Fill(27, 16, 15, 30, 17, 16, ink);
            v.Set(23, 17, 8, salt); v.Set(24, 17, 8, pepper);
            return Save("Tavolo", v, BottomCenter(v, 36, 18));
        }


        public static GameObject Chair() => ChairVariant("Sedia", false);

        /// <summary>Sedia da pranzo (0.5 x 0.5 m, seduta a 0.5 m, cubetti da 5 cm): traversi, cuscino, schienale a stecche. Con 'jacket' una giacca appesa allo schienale.</summary>
        public static GameObject ChairVariant(string name, bool jacket)
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#7a5230"), woodDark = v.C("#5e3e22"), pad = v.C("#8a3a3a"), padDark = v.C("#6e2e2e"), coat = v.C("#2e4a6a"), coatDark = v.C("#243a54");
            foreach (int x in new[] { 0, 9 }) foreach (int z in new[] { 0, 9 }) v.Fill(x, 0, z, x + 1, 8, z + 1, wood);
            v.Fill(0, 3, 0, 1, 4, 10, woodDark); v.Fill(9, 3, 0, 10, 4, 10, woodDark); v.Fill(0, 2, 9, 10, 3, 10, woodDark); // traversi
            v.Fill(0, 8, 0, 10, 9, 10, wood);                                     // seduta
            v.Fill(1, 9, 1, 9, 10, 9, pad); v.Paint(1, 9, 8, 9, 10, 9, padDark);  // cuscino
            v.Fill(0, 9, 0, 1, 20, 1, wood); v.Fill(9, 9, 0, 10, 20, 1, wood);    // montanti
            v.Fill(0, 18, 0, 10, 20, 1, wood); v.Fill(1, 20, 0, 9, 21, 1, wood);  // traversa alta arrotondata
            foreach (int x in new[] { 3, 5, 7 }) v.Fill(x, 10, 0, x + 1, 18, 1, woodDark); // stecche
            if (jacket)
            {
                v.Fill(0, 11, -1, 10, 21, 0, coat);                               // giacca appesa dietro
                v.Fill(-1, 6, -1, 1, 20, 1, coat); v.Fill(9, 6, -1, 11, 20, 1, coat); // maniche che pendono ai lati
                v.Fill(2, 9, -2, 8, 11, -1, coatDark);                            // orlo
                v.Fill(3, 19, 1, 7, 21, 2, coatDark);                             // colletto
            }
            return Save(name, v, BottomCenter(v, 10, 10));
        }


        /// <summary>Frigorifero color mandorla (0.8 x 1.8 x 0.7 m, cubetti da 5 cm): freezer, maniglie cromate, calamite, disegni e una foto attaccati, griglia in basso.</summary>
        public static GameObject Fridge()
        {
            var v = new VoxelBuilder(0.05f);
            int body = v.C("#e3dcc4"), side = v.C("#d2cab0"), line = v.C("#a9a18a"), chrome = v.C("#c9ccd0"), grille = v.C("#2a2a2a"), paper = v.C("#f2efe6"),
                crayonR = v.C("#d13b3b"), crayonB = v.C("#3b6fd1"), crayonY = v.C("#e3c23a"), photo = v.C("#7aa0c0"), m1 = v.C("#d13b3b"), m2 = v.C("#3bb0d1"), m3 = v.C("#4ab04a"), logo = v.C("#8a8a8a");
            v.Fill(0, 0, 0, 16, 36, 14, body);
            v.Paint(0, 0, 0, 1, 36, 14, side); v.Paint(15, 0, 0, 16, 36, 14, side);
            v.Carve(0, 35, 13, 16, 36, 14);                                       // spigolo arrotondato
            v.Fill(1, 0, 13, 15, 2, 14, grille);
            v.Paint(0, 24, 13, 16, 25, 14, line);                                 // freezer
            v.Fill(1, 26, 14, 2, 33, 15, chrome); v.Fill(1, 12, 14, 2, 22, 15, chrome); // maniglie
            v.Fill(7, 33, 14, 10, 34, 15, logo);
            // disegno di un bambino (casa e sole), lista della spesa, foto
            v.Fill(5, 15, 14, 11, 21, 15, paper);
            v.Set(6, 16, 15, crayonB); v.Set(7, 17, 15, crayonB); v.Set(8, 16, 15, crayonB); v.Fill(6, 18, 15, 9, 19, 16, crayonR); v.Set(10, 20, 15, crayonY);
            v.Fill(11, 9, 14, 14, 14, 15, paper); for (int y = 10; y < 14; y++) v.Set(12, y, 15, line);
            v.Fill(6, 27, 14, 9, 30, 15, photo);
            v.Set(7, 21, 15, m1); v.Set(12, 14, 15, m2); v.Set(7, 30, 15, m3); v.Set(13, 28, 15, m1); v.Set(4, 6, 15, m2);
            return Save("Frigo", v, BottomCenter(v, 16, 14));
        }


        /// <summary>Cucina con forno (0.7 x 1.1 x 0.6 m, cubetti da 5 cm): fornelli a spirale, pentola, alzatina con orologio, manopole, vetro del forno, maniglia.</summary>
        public static GameObject Stove()
        {
            var v = new VoxelBuilder(0.05f);
            int body = v.C("#e8e4da"), top = v.C("#2a2a2a"), coil = v.C("#5a5a5a"), glass = v.C("#141414"), knob = v.C("#cfcfcf"), chrome = v.C("#c9ccd0"),
                clock = v.C("#40ff70"), pot = v.C("#9a9ca0"), potDark = v.C("#6a6c70"), kick = v.C("#3a3a3a");
            v.Fill(0, 0, 0, 14, 18, 12, body);
            v.Fill(1, 0, 11, 13, 2, 12, kick);
            v.Fill(0, 17, 0, 14, 18, 12, top);
            foreach (int x in new[] { 2, 9 }) foreach (int z in new[] { 2, 7 })   // fornelli a spirale
            {
                v.Fill(x, 18, z, x + 3, 19, z + 3, coil); v.Set(x + 1, 18, z + 1, top);
            }
            v.Fill(9, 18, 7, 12, 21, 10, pot); v.Fill(8, 20, 8, 9, 21, 9, potDark); v.Fill(9, 21, 7, 12, 22, 10, potDark); // pentola con coperchio
            v.Fill(0, 18, 0, 14, 23, 2, body);                                    // alzatina
            v.Fill(5, 20, 2, 9, 22, 3, glass); v.Set(6, 21, 3, clock); v.Set(7, 21, 3, clock);
            for (int x = 1; x < 14; x += 3) v.Set(x, 15, 12, knob);               // manopole
            v.Fill(2, 4, 12, 12, 12, 13, glass);                                  // vetro del forno
            v.Fill(2, 13, 13, 12, 14, 14, chrome);                                // maniglia
            return Save("Fornello", v, BottomCenter(v, 14, 12));
        }


        /// <summary>Bancone con lavello (2 x 0.9 x 0.6 m, cubetti da 5 cm): ante a pannello, lavello con piatti sporchi, rubinetto, scolapiatti, detersivo, rotolo di carta.</summary>
        public static GameObject CounterSink()
        {
            var v = new VoxelBuilder(0.05f);
            CounterBase(v);
            int steel = v.C("#a8acb0"), steelDark = v.C("#7a7e82"), tap = v.C("#d0d4d8"), plate = v.C("#f2f2ee"), sauce = v.C("#a0402a"), mug = v.C("#3b6fc2"),
                rack = v.C("#c9ccd0"), soap = v.C("#3aa04a"), towel = v.C("#f2efe6");
            v.Fill(12, 17, 2, 28, 18, 12, steel);                                  // lavello
            v.Carve(13, 17, 3, 27, 18, 11);
            v.Fill(13, 15, 3, 27, 16, 11, steelDark);                             // fondo della vasca (più in basso)
            v.Carve(13, 16, 3, 27, 17, 11);
            v.Fill(15, 16, 4, 19, 17, 8, plate); v.Set(16, 16, 5, sauce); v.Set(17, 16, 6, sauce); // piatti sporchi
            v.Fill(21, 16, 6, 23, 18, 8, mug);
            v.Fill(19, 18, 0, 21, 22, 2, tap); v.Fill(19, 21, 2, 21, 22, 5, tap); // rubinetto
            v.Fill(31, 18, 2, 38, 19, 10, rack);                                  // scolapiatti
            for (int x = 32; x < 38; x += 2) v.Fill(x, 19, 3, x + 1, 23, 9, plate);
            v.Fill(9, 18, 3, 11, 22, 5, soap);
            v.Fill(3, 18, 2, 6, 23, 5, towel);
            return Save("Bancone lavello", v, BottomCenter(v, 40, 12));
        }

        /// <summary>Base comune dei banconi (2 x 0.9 x 0.6 m, cubetti da 5 cm, fronte +Z): zoccolo, ante a pannello con maniglie, cassetti, piano con sbalzo.</summary>
        public static void CounterBase(VoxelBuilder v)
        {
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), panel = v.C("#d0c9b6"), handle = v.C("#3a3a3a"), top = v.C("#b6a487"), topEdge = v.C("#9a8a70"), plinth = v.C("#4a4540");
            v.Fill(0, 0, 0, 40, 16, 12, body);
            v.Fill(0, 0, 10, 40, 2, 12, plinth); v.Carve(0, 0, 11, 40, 2, 12);    // zoccolo rientrato
            v.Fill(-1, 16, 0, 41, 18, 13, top);
            v.Fill(-1, 16, 12, 41, 17, 13, topEdge);
            for (int i = 0; i < 4; i++)
            {
                int x0 = i * 10;
                v.Fill(x0 + 1, 3, 12, x0 + 9, 11, 13, door);                      // anta
                v.Fill(x0 + 2, 4, 13, x0 + 8, 10, 14, panel);                     // pannello in rilievo
                v.Set(i % 2 == 0 ? x0 + 7 : x0 + 2, 9, 14, handle);
                v.Fill(x0 + 1, 12, 12, x0 + 9, 15, 13, door);                     // cassetto
                v.Fill(x0 + 4, 13, 13, x0 + 6, 14, 14, handle);
            }
        }


        /// <summary>Isola della cucina (2 x 0.9 x 0.9 m, cubetti da 5 cm), ante su entrambi i lati; sulla destra fruttiera e giornale (la sinistra resta libera per la birra).</summary>
        public static GameObject Island()
        {
            var v = new VoxelBuilder(0.05f);
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), panel = v.C("#d0c9b6"), top = v.C("#b6a487"), topEdge = v.C("#9a8a70"), handle = v.C("#3a3a3a"), plinth = v.C("#4a4540"),
                bowl = v.C("#c9c4b6"), orange = v.C("#e08a2a"), apple = v.C("#b02a24"), paper = v.C("#d8d2c0"), ink = v.C("#5a5a5a"), keys = v.C("#c9a24a");
            v.Fill(0, 0, 2, 40, 16, 16, body);
            v.Fill(0, 0, 2, 40, 2, 3, plinth); v.Fill(0, 0, 15, 40, 2, 16, plinth);
            v.Fill(-2, 16, 0, 42, 18, 18, top);
            v.Fill(-2, 16, 0, 42, 17, 1, topEdge); v.Fill(-2, 16, 17, 42, 17, 18, topEdge);
            for (int i = 0; i < 4; i++)
            {
                int x0 = i * 10;
                foreach (int z in new[] { 16, 1 })
                {
                    int out1 = z == 16 ? 17 : 0;
                    v.Fill(x0 + 1, 3, z, x0 + 9, 15, z + 1, door);
                    v.Fill(x0 + 2, 4, out1, x0 + 8, 14, out1 + 1, panel);
                    v.Set(x0 + 5, 12, z == 16 ? 18 : -1, handle);
                }
            }
            v.Fill(28, 18, 6, 34, 19, 12, bowl); v.Carve(29, 18, 7, 33, 19, 11);
            v.Set(29, 18, 7, orange); v.Set(30, 19, 8, orange); v.Set(31, 18, 9, apple); v.Set(32, 18, 8, orange);
            v.Fill(35, 18, 3, 40, 19, 9, paper); v.Fill(36, 18, 4, 39, 19, 5, ink); v.Fill(36, 18, 6, 38, 19, 7, ink);
            v.Fill(25, 18, 13, 27, 19, 14, keys);
            return Save("Isola cucina", v, BottomCenter(v, 40, 18));
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


        /// <summary>Letto matrimoniale (1.6 x 2.2 m, cubetti da 5 cm): testiera a pannelli, materasso, piumone sgualcito con pieghe, lenzuolo risvoltato, cuscini con l'impronta.</summary>
        public static GameObject BedDouble()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5a3c24"), woodDark = v.C("#45301d"), mattress = v.C("#e8e4da"), sheet = v.C("#f2efe6"), duvet = v.C("#3a5a7a"), duvetDark = v.C("#2e4a66"),
                pillow = v.C("#f2efe6"), dent = v.C("#dcd8cc"), book = v.C("#8b2b2b");
            v.Fill(0, 2, 0, 32, 6, 44, wood);
            foreach (int x in new[] { 0, 30 }) foreach (int z in new[] { 1, 42 }) v.Fill(x, 0, z, x + 2, 2, z + 2, woodDark);
            v.Fill(1, 6, 1, 31, 10, 44, mattress);
            v.Fill(1, 10, 2, 31, 11, 44, sheet);
            v.Fill(0, 6, 0, 32, 22, 2, wood);                                     // testiera
            v.Fill(3, 9, 2, 15, 20, 3, woodDark); v.Fill(17, 9, 2, 29, 20, 3, woodDark);
            v.Fill(0, 22, 0, 32, 23, 3, woodDark);
            for (int z = 14; z < 44; z++)                                         // piumone con pieghe
            {
                int h = 12 + ((z % 7 == 0 || z % 11 == 3) ? 1 : 0);
                v.Fill(0, 11, z, 32, h, z + 1, (z / 3) % 4 == 0 ? duvetDark : duvet);
            }
            v.Fill(0, 4, 44, 32, 12, 45, duvet);                                  // pende dai piedi del letto
            v.Fill(0, 4, 14, 1, 11, 44, duvet); v.Fill(31, 4, 14, 32, 11, 44, duvet);
            v.Fill(1, 11, 14, 31, 13, 16, sheet);                                 // lenzuolo risvoltato
            v.Fill(3, 11, 3, 14, 14, 9, pillow); v.Fill(18, 11, 3, 29, 14, 9, pillow);
            v.Paint(6, 13, 5, 11, 14, 8, dent); v.Paint(21, 13, 5, 26, 14, 8, dent);
            v.Fill(22, 13, 20, 26, 14, 23, book);                                 // un libro lasciato sul letto
            return Save("Letto matrimoniale", v, BottomCenter(v, 32, 44));
        }


        /// <summary>Letto singolo (1 x 2 m, cubetti da 5 cm): coperta sfatta, cuscino storto, una maglietta buttata sopra.</summary>
        public static GameObject BedSingle()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#6a4628"), woodDark = v.C("#523620"), mattress = v.C("#e8e4da"), sheet = v.C("#f2efe6"), blanket = v.C("#8a3a3a"), blanketDark = v.C("#6e2e2e"),
                pillow = v.C("#f2efe6"), shirt = v.C("#3a6aa0"), shirtDark = v.C("#2e5480");
            v.Fill(0, 2, 0, 20, 6, 40, wood);
            foreach (int x in new[] { 0, 18 }) foreach (int z in new[] { 1, 38 }) v.Fill(x, 0, z, x + 2, 2, z + 2, woodDark);
            v.Fill(1, 6, 1, 19, 10, 40, mattress);
            v.Fill(1, 10, 1, 19, 11, 40, sheet);
            v.Fill(0, 6, 0, 20, 16, 2, wood); v.Fill(0, 16, 0, 20, 17, 3, woodDark);
            for (int z = 12; z < 40; z++)
            {
                int x0 = z < 20 ? 3 + (z % 3) : 0;                                // coperta tirata da una parte
                v.Fill(x0, 11, z, 20, 12 + ((z % 5 == 0) ? 1 : 0), z + 1, (z / 4) % 3 == 0 ? blanketDark : blanket);
            }
            v.Fill(19, 4, 12, 21, 11, 40, blanket);                               // pende di lato
            v.Fill(3, 11, 3, 15, 14, 8, pillow); v.Carve(3, 13, 3, 5, 14, 5);
            v.Fill(6, 12, 26, 13, 13, 32, shirt); v.Fill(4, 12, 27, 6, 13, 29, shirtDark); v.Fill(13, 12, 27, 15, 13, 29, shirtDark);
            return Save("Letto singolo", v, BottomCenter(v, 20, 40));
        }


        /// <summary>Comodino (0.4 x 0.5 m, cubetti da 5 cm): cassetto con pomello, abat-jour, sveglia con le cifre rosse, un libro.</summary>
        public static GameObject Nightstand()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5a3c24"), woodDark = v.C("#45301d"), knob = v.C("#c9a24a"), shade = v.C("#efe0b0"), baseC = v.C("#3a3630"),
                clock = v.C("#1a1a1c"), digit = v.C("#ff3030"), book = v.C("#2b5d8b");
            v.Fill(0, 1, 0, 8, 10, 8, wood);
            foreach (int x in new[] { 0, 7 }) foreach (int z in new[] { 0, 7 }) v.Set(x, 0, z, woodDark);
            v.Fill(1, 6, 8, 7, 9, 9, woodDark); v.Set(4, 7, 9, knob);             // cassetto
            v.Fill(1, 2, 8, 7, 5, 9, woodDark);
            v.Fill(1, 10, 1, 3, 11, 3, baseC); v.Fill(1, 11, 1, 3, 13, 3, baseC); // abat-jour
            v.Fill(0, 13, 0, 4, 16, 4, shade); v.Carve(1, 13, 1, 3, 14, 3);
            v.Fill(5, 10, 4, 8, 12, 6, clock); v.Set(6, 11, 6, digit); v.Set(7, 11, 6, digit);
            v.Fill(4, 10, 0, 8, 11, 3, book);
            return Save("Comodino", v, BottomCenter(v, 8, 8));
        }


        /// <summary>Armadio (1.2 x 2 x 0.6 m, cubetti da 5 cm): piedini, ante a pannelli, cornice in alto, pomelli, una valigia sopra.</summary>
        public static GameObject Wardrobe()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5d3e25"), panel = v.C("#4f341f"), line = v.C("#3a2716"), knob = v.C("#c9a24a"), suitcase = v.C("#6a5a3a"), strap = v.C("#2a2a2a");
            v.Fill(0, 2, 0, 24, 38, 12, wood);
            foreach (int x in new[] { 1, 21 }) foreach (int z in new[] { 1, 9 }) v.Fill(x, 0, z, x + 2, 2, z + 2, line);
            v.Fill(-1, 38, -1, 25, 40, 13, panel);                                // cornice
            v.Paint(11, 3, 11, 13, 37, 12, line);                                 // fessura tra le ante
            foreach (int x0 in new[] { 2, 14 })
            {
                v.Fill(x0, 22, 12, x0 + 8, 35, 13, panel);                        // pannelli in rilievo
                v.Fill(x0, 5, 12, x0 + 8, 19, 13, panel);
            }
            v.Set(10, 20, 13, knob); v.Set(13, 20, 13, knob);
            v.Fill(3, 40, 2, 15, 45, 10, suitcase); v.Fill(8, 40, 1, 10, 45, 11, strap); v.Fill(8, 45, 5, 10, 46, 7, strap);
            return Save("Armadio", v, BottomCenter(v, 24, 12));
        }


        /// <summary>Scrivania (1.2 x 0.8 x 0.6 m, cubetti da 5 cm): cassettiera, computer beige anni '90 con tastiera, lampada, fogli, matite, lattina.</summary>
        public static GameObject Desk()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#7a5230"), drawer = v.C("#6a4628"), knob = v.C("#c9a24a"), paper = v.C("#f2efe6"), beige = v.C("#d6cdb4"), beigeDark = v.C("#b8ae94"),
                screen = v.C("#1a2a3a"), glow = v.C("#3a6a8a"), key = v.C("#e8e2d0"), lamp = v.C("#2a2a2a"), bulb = v.C("#efe0b0"), pencil = v.C("#e3c23a"), can = v.C("#3aa04a");
            v.Fill(0, 14, 0, 24, 16, 12, wood);
            v.Fill(0, 0, 0, 2, 14, 12, wood);
            v.Fill(16, 0, 0, 24, 14, 12, drawer);
            for (int k = 0; k < 3; k++) { v.Fill(17, 1 + k * 4, 12, 23, 4 + k * 4, 13, wood); v.Set(20, 2 + k * 4, 13, knob); }
            v.Fill(4, 16, 1, 13, 24, 9, beige);                                   // monitor CRT
            v.Fill(5, 17, 9, 12, 23, 10, beigeDark); v.Fill(6, 18, 10, 11, 22, 11, screen); v.Set(7, 21, 11, glow); v.Set(8, 21, 11, glow);
            v.Fill(6, 16, 0, 11, 18, 1, beigeDark);
            v.Fill(4, 16, 9, 14, 17, 11, key);                                    // tastiera (sporge davanti al monitor)
            v.Fill(20, 16, 2, 22, 17, 4, lamp); v.Fill(20, 17, 3, 21, 22, 4, lamp); v.Fill(18, 22, 3, 21, 23, 6, lamp); v.Set(19, 21, 5, bulb);
            v.Fill(15, 16, 6, 20, 17, 11, paper); v.Fill(16, 17, 7, 20, 17 + 1, 10, paper);
            v.Set(17, 18, 8, pencil); v.Set(18, 18, 8, pencil);
            v.Fill(22, 16, 8, 23, 18, 9, can);
            return Save("Scrivania", v, BottomCenter(v, 24, 12));
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

        /// <summary>
        /// Stereo portatile anni '90 (50 x 25 x 12 cm, cubetti da 2.5 cm): due casse tonde con il cono al centro,
        /// pannello argentato con la piastra delle cassette, tasti sopra (uno rosso), manico e antenna.
        /// Il davanti è il lato -Z.
        /// </summary>
        public static GameObject Radio()
        {
            var v = new VoxelBuilder(0.025f);
            int body = v.C("#2b2b2e"), silver = v.C("#9a9ca0"), grill = v.C("#141416"), cone = v.C("#3c3c42"), cap = v.C("#77787e"),
                tape = v.C("#4f5e66"), reel = v.C("#d8d2c0"), key = v.C("#c9c9c9"), red = v.C("#b02a24"), handle = v.C("#1e1e20"), metal = v.C("#b8b8bc"),
                dust = v.C("#6d6a62");
            v.Fill(0, 0, 0, 20, 9, 5, body);
            // casse: griglia scura, cono e cupola centrale
            foreach (int cx in new[] { 4, 15 })
                for (int x = cx - 4; x <= cx + 4; x++)
                    for (int y = 0; y <= 9; y++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - 4.5f) * (y - 4.5f));
                        if (d > 3.6f || x < 0 || x > 19 || y > 8) continue;
                        v.Set(x, y, 0, d < 1f ? cap : d < 2.2f ? cone : grill);
                    }
            // pannello centrale con la piastra delle cassette (finestrella e due bobine)
            v.Fill(8, 1, 0, 12, 8, 1, silver);
            v.Fill(9, 3, 0, 11, 6, 1, tape);
            v.Set(9, 4, 0, reel); v.Set(10, 4, 0, reel);
            // tasti sopra (registrazione rossa) e manopole
            for (int x = 7; x < 13; x++) v.Set(x, 9, 2, x == 7 ? red : key);
            v.Set(2, 9, 1, metal); v.Set(17, 9, 1, metal);
            // manico
            v.Fill(1, 9, 2, 2, 13, 3, handle); v.Fill(18, 9, 2, 19, 13, 3, handle); v.Fill(1, 12, 2, 19, 13, 3, handle);
            // antenna sfilata, un po' storta
            for (int y = 9; y < 20; y++) v.Set(19 - (y > 15 ? 1 : 0), y, 4, metal);
            // polvere sopra (è in cantina da anni)
            v.Set(4, 9, 3, dust); v.Set(5, 9, 4, dust); v.Set(14, 9, 3, dust); v.Set(15, 9, 1, dust);
            return Save("Radio", v, BottomCenter(v, 20, 5));
        }

        // ------------------------------------------------------------------ oggetti che fanno "vissuto"

        /// <summary>Un paio di scarpe da ginnastica buttate per terra (cubetti da 2.5 cm).</summary>
        public static GameObject Shoes()
        {
            var v = new VoxelBuilder(0.025f);
            int white = v.C("#e8e8e2"), sole = v.C("#3a3a3a"), red = v.C("#c0392b"), lace = v.C("#f2f2f2");
            foreach (var o in new[] { new Vector2Int(0, 0), new Vector2Int(5, 2) })
            {
                v.Fill(o.x, 0, o.y, o.x + 4, 1, o.y + 11, sole);
                v.Fill(o.x, 1, o.y, o.x + 4, 3, o.y + 11, white);
                v.Fill(o.x, 3, o.y, o.x + 4, 5, o.y + 4, white);                   // tallone
                v.Fill(o.x + 1, 3, o.y + 4, o.x + 3, 4, o.y + 9, lace);
                v.Fill(o.x, 2, o.y + 3, o.x + 4, 3, o.y + 7, red);                 // banda colorata
            }
            return Save("Scarpe", v, BottomCenter(v, 9, 13), false);
        }

        /// <summary>Pianta in vaso un po' trascurata (foglie gialle), cubetti da 5 cm.</summary>
        public static GameObject Plant()
        {
            var v = new VoxelBuilder(0.05f);
            int pot = v.C("#a0522d"), rim = v.C("#8a4524"), soil = v.C("#3a2a1a"), leaf = v.C("#3f7a3a"), leaf2 = v.C("#2f6a2e"), dry = v.C("#b0a040");
            v.Fill(1, 0, 1, 6, 5, 6, pot); v.Fill(0, 5, 0, 7, 6, 7, rim); v.Fill(1, 5, 1, 6, 6, 6, soil);
            var rng = new System.Random(3);
            for (int i = 0; i < 70; i++)
            {
                int x = rng.Next(-2, 9), z = rng.Next(-2, 9), y = 6 + rng.Next(0, 13);
                float d = Mathf.Sqrt((x - 3) * (x - 3) + (z - 3) * (z - 3));
                if (d > 1.5f + (y - 6) * 0.35f) continue;
                v.Set(x, y, z, i % 11 == 0 ? dry : i % 2 == 0 ? leaf : leaf2);
            }
            v.Fill(3, 6, 3, 4, 12, 4, leaf2);
            return Save("Pianta", v, BottomCenter(v, 7, 7));
        }

        /// <summary>Quadro da parete (cornice e un paesaggio/astratto a blocchi), cubetti da 3 cm. Il retro è a z = 0, il davanti +Z.</summary>
        public static GameObject Picture(string name, string frameHex, string aHex, string bHex)
        {
            var v = new VoxelBuilder(0.03f);
            int frame = v.C(frameHex), a = v.C(aHex), b = v.C(bHex), c = v.C("#2a2a2a");
            v.Fill(0, 0, 0, 18, 14, 1, frame);
            for (int x = 1; x < 17; x++)
                for (int y = 1; y < 13; y++)
                {
                    float wave = 5f + 2f * Mathf.Sin(x * 0.6f + name.Length);
                    v.Set(x, y, 1, y < wave ? a : (x * 3 + y) % 9 == 0 ? c : b);
                }
            for (int x = 0; x < 18; x++) { v.Set(x, 0, 1, frame); v.Set(x, 13, 1, frame); }
            for (int y = 0; y < 14; y++) { v.Set(0, y, 1, frame); v.Set(17, y, 1, frame); }
            return Save(name, v, new Vector3(9f * v.size, 7f * v.size, 0f), false);
        }

        /// <summary>Orologio da parete tondo (cubetti da 2 cm), lancette ferme alle 9 e 5.</summary>
        public static GameObject WallClock()
        {
            var v = new VoxelBuilder(0.02f);
            int rim = v.C("#3a3630"), face = v.C("#efe9dc"), hand = v.C("#1a1a1a"), red = v.C("#c0392b");
            for (int x = 0; x < 15; x++)
                for (int y = 0; y < 15; y++)
                {
                    float d = Mathf.Sqrt((x - 7) * (x - 7) + (y - 7) * (y - 7));
                    if (d <= 7.2f) v.Set(x, y, 0, rim);
                    if (d <= 6.2f) v.Set(x, y, 1, face);
                    else if (d <= 7.2f) v.Set(x, y, 1, rim);
                }
            for (int i = 0; i < 4; i++) v.Set(7 - i, 7, 2, hand);                 // ore: le 9
            for (int i = 0; i < 6; i++) v.Set(7, 7 + i, 2, hand);                 // minuti: un po' dopo
            v.Set(8, 12, 2, hand);
            v.Set(7, 7, 2, red);
            return Save("Orologio", v, new Vector3(7.5f * v.size, 7.5f * v.size, 0f), false);
        }

        /// <summary>Cesto del bucato pieno di vestiti (cubetti da 5 cm).</summary>
        public static GameObject LaundryBasket()
        {
            var v = new VoxelBuilder(0.05f);
            int basket = v.C("#d8d2c0"), dark = v.C("#b8b2a0");
            string[] clothes = { "#3a6aa0", "#c0392b", "#efe9dc", "#3a3a3a", "#4ab04a" };
            v.Fill(0, 0, 0, 10, 8, 7, basket); v.Carve(1, 1, 1, 9, 8, 6);
            for (int y = 2; y < 8; y += 2) v.Paint(0, y, 0, 10, y + 1, 7, dark);
            for (int x = 1; x < 9; x++) for (int z = 1; z < 6; z++) v.Fill(x, 1, z, x + 1, 7 + (x * 3 + z) % 3, z + 1, v.C(clothes[(x + z * 2) % clothes.Length]));
            v.Fill(8, 6, 6, 9, 9, 8, v.C(clothes[1]));                            // una manica che esce
            return Save("Cesto bucato", v, BottomCenter(v, 10, 7));
        }

        /// <summary>Zaino appoggiato per terra (cubetti da 4 cm).</summary>
        public static GameObject Backpack()
        {
            var v = new VoxelBuilder(0.04f);
            int body = v.C("#2e4a3a"), dark = v.C("#243a2e"), zip = v.C("#c9a24a"), strap = v.C("#1a1a1a");
            v.Fill(0, 0, 0, 8, 11, 5, body);
            v.Fill(1, 1, 5, 7, 6, 6, dark); v.Fill(2, 6, 5, 6, 7, 6, zip);         // tasca davanti
            v.Fill(1, 10, 0, 7, 11, 5, dark); v.Fill(2, 11, 2, 6, 12, 3, strap);
            v.Fill(1, 2, -1, 2, 10, 0, strap); v.Fill(6, 2, -1, 7, 10, 0, strap);
            return Save("Zaino", v, BottomCenter(v, 8, 5));
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
