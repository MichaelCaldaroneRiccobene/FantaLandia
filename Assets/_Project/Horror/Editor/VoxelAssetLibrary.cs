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
            FloorLamp(); LightSwitchPlate(); Flashlight(); StreetLight();
            AssetDatabase.SaveAssets();
            Debug.Log("[FantaLandia Horror] Oggetti voxel creati in " + Folder);
        }

        // ------------------------------------------------------------------ salvataggio

        /// <summary>Crea il prefab: mesh a cubetti, un materiale per colore, collider a scatola.</summary>
        static GameObject Save(string name, VoxelBuilder v, Vector3 pivot, bool collider = true)
        {
            EditorAssetUtil.EnsureFolder(Folder);
            Mesh mesh = v.Build(name, pivot, out Color[] colors);
            string meshPath = $"{Folder}/{name}_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                mats[i] = EditorAssetUtil.Mat("Voxel " + ColorUtility.ToHtmlStringRGB(colors[i]), colors[i], 0.15f);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
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

        static Vector3 BottomCenter(VoxelBuilder v, int sx, int sz) => new Vector3(sx * 0.5f * v.size, 0f, sz * 0.5f * v.size);

        // ------------------------------------------------------------------ TV a tubo catodico

        /// <summary>
        /// TV anni '90 (cubetti da 4 cm): mobile di plastica marrone scuro, cornice, schermo incassato,
        /// pannello comandi argentato con manopole rosse e verdi, griglia dell'altoparlante,
        /// gobba del tubo catodico dietro e antenna "a orecchie di coniglio".
        /// Il fronte guarda verso +Z. Lo schermo vero (quad) va messo davanti all'incasso: vedi ScreenRect.
        /// </summary>
        public static GameObject Tv()
        {
            var v = new VoxelBuilder(0.04f);
            int casing = v.C("#3b342e"), casingDark = v.C("#2c2723"), bezel = v.C("#231f1c"), glass = v.C("#141a19");
            int silver = v.C("#8d8e94"), red = v.C("#b3332e"), green = v.C("#3a9a48"), black = v.C("#111111");
            int grille = v.C("#1c1a18"), logo = v.C("#c4b27a"), antenna = v.C("#b9bcc2");

            v.Fill(0, 0, 0, 18, 14, 10, casing);            // mobile
            v.Fill(3, 2, -5, 15, 12, 0, casingDark);        // gobba del tubo
            v.Fill(5, 4, -7, 13, 10, -5, casingDark);
            v.Fill(0, 0, 9, 18, 14, 10, bezel);             // cornice frontale
            v.Carve(2, 3, 9, 12, 12, 10);                   // incasso dello schermo
            v.Fill(2, 3, 8, 12, 12, 9, glass);              // vetro (dietro il quad dello schermo)

            v.Fill(13, 1, 9, 17, 13, 10, silver);           // pannello comandi
            v.Fill(14, 9, 10, 16, 11, 11, red);             // manopola canali
            v.Fill(14, 6, 10, 16, 8, 11, green);            // manopola volume
            v.Set(14, 4, 10, black); v.Set(15, 4, 10, black); // tasti
            for (int y = 2; y <= 3; y++) for (int x = 13; x < 17; x++) if ((x + y) % 2 == 0) v.Set(x, y, 9, grille);
            for (int x = 6; x < 9; x++) v.Set(x, 1, 9, logo);  // marchio

            for (int x = 0; x < 18; x += 2) v.Set(x, 13, 4, casingDark); // feritoie sopra
            for (int z = 1; z < 8; z += 2) { v.Set(0, 10, z, casingDark); v.Set(17, 10, z, casingDark); } // feritoie laterali

            v.Fill(8, 14, 3, 10, 15, 5, black);             // base antenna
            for (int i = 0; i < 9; i++)
            {
                v.Set(8 - i / 2, 15 + i, 4, antenna);
                v.Set(9 + i / 2, 15 + i, 4, antenna);
            }
            v.Set(4, 24, 4, black); v.Set(13, 24, 4, black); // punte

            return Save("TV", v, BottomCenter(v, 18, 10));
        }

        /// <summary>Rettangolo dello schermo (centro e dimensioni) nello spazio del prefab TV, per il quad.</summary>
        public static void ScreenRect(out Vector3 center, out Vector2 size)
        {
            const float s = 0.04f;
            Vector3 pivot = new Vector3(18 * 0.5f * s, 0f, 10 * 0.5f * s);
            center = new Vector3((2 + 12) * 0.5f * s, (3 + 12) * 0.5f * s, 9 * s + 0.002f) - pivot;
            size = new Vector2(10 * s, 9 * s);
        }

        /// <summary>Mobile basso per la TV (cubetti da 5 cm) con un videoregistratore sul ripiano.</summary>
        public static GameObject TvCabinet()
        {
            var v = new VoxelBuilder(0.05f);
            int wood = v.C("#5a3c24"), woodDark = v.C("#45301d"), knob = v.C("#c9a24a");
            int vcr = v.C("#1a1a1c"), vcrSlot = v.C("#050505"), led = v.C("#40ff70");

            v.Fill(0, 0, 0, 22, 10, 10, wood);
            v.Fill(0, 0, 0, 22, 1, 10, woodDark);           // zoccolo
            v.Fill(0, 9, 0, 22, 10, 11, woodDark);          // piano con bordo
            v.Carve(1, 5, 2, 21, 9, 10);                     // ripiano aperto
            v.Fill(3, 5, 3, 15, 7, 9, vcr);                  // videoregistratore
            v.Fill(5, 6, 9, 11, 7, 10, vcrSlot);             // fessura cassetta (sporge di poco)
            v.Set(13, 6, 9, led); v.Set(12, 6, 9, led);
            v.Fill(1, 1, 9, 11, 5, 10, woodDark);            // ante
            v.Fill(11, 1, 9, 21, 5, 10, woodDark);
            v.Fill(1, 1, 9, 2, 5, 10, wood);
            v.Set(9, 3, 10, knob); v.Set(12, 3, 10, knob);   // pomelli

            return Save("Mobile TV", v, BottomCenter(v, 22, 10));
        }

        // ------------------------------------------------------------------ porta

        /// <summary>
        /// Porta di casa (cubetti da 5 cm, 0.9 x 2.1 m, spessa 5 cm): montanti scuri, quattro pannelli,
        /// finestrella a quattro vetri, pomello d'ottone sui due lati. Il cardine è sul bordo sinistro (x = 0).
        /// </summary>
        public static GameObject Door()
        {
            var v = new VoxelBuilder(0.05f);
            int frame = v.C("#5e3f24"), panel = v.C("#82593a"), bevel = v.C("#6f4b2f"), brass = v.C("#c9a24a"), glassC = v.C("#8fa9b8");

            v.Fill(0, 0, 0, 18, 42, 1, frame);
            // pannelli in rilievo (stesso spessore, colori diversi)
            int[,] panels = { { 2, 3, 8, 13 }, { 10, 3, 16, 13 }, { 2, 15, 8, 27 }, { 10, 15, 16, 27 } };
            for (int i = 0; i < 4; i++)
            {
                v.Paint(panels[i, 0], panels[i, 1], 0, panels[i, 2], panels[i, 3], 1, bevel);
                v.Paint(panels[i, 0] + 1, panels[i, 1] + 1, 0, panels[i, 2] - 1, panels[i, 3] - 1, 1, panel);
            }
            // finestrella a 4 vetri
            v.Paint(4, 30, 0, 14, 39, 1, glassC);
            v.Paint(8, 30, 0, 10, 39, 1, frame);
            v.Paint(4, 34, 0, 14, 35, 1, frame);
            // pomelli
            v.Fill(15, 19, -1, 17, 21, 0, brass);
            v.Fill(15, 19, 1, 17, 21, 2, brass);

            return Save("Porta", v, new Vector3(0f, 0f, 0.5f * v.size));
        }

        // ------------------------------------------------------------------ birra

        /// <summary>Confezione da 6 lattine (cubetti da 2 cm): lattine argento con fascia rossa, cartone con manico.</summary>
        public static GameObject BeerPack()
        {
            var v = new VoxelBuilder(0.02f);
            int can = v.C("#c9cacf"), label = v.C("#b3211e"), top = v.C("#8e8f95"), card = v.C("#a3743f"), cardDark = v.C("#7f5a30"), white = v.C("#f2efe6");

            for (int cx = 0; cx < 3; cx++)
                for (int cz = 0; cz < 2; cz++)
                {
                    int x = cx * 3, z = cz * 3;
                    v.Fill(x, 0, z, x + 3, 6, z + 3, can);
                    v.Fill(x, 2, z, x + 3, 4, z + 3, label);
                    v.Set(x + 1, 3, z + (cz == 0 ? 0 : 2), white);   // logo
                    v.Fill(x, 6, z, x + 3, 7, z + 3, top);
                }
            // cartone: fondo e fascia bassa tutt'intorno
            v.Fill(-1, -1, -1, 10, 0, 7, card);
            v.Fill(-1, 0, -1, 10, 2, 0, card);
            v.Fill(-1, 0, 6, 10, 2, 7, card);
            v.Fill(-1, 0, -1, 0, 2, 7, card);
            v.Fill(9, 0, -1, 10, 2, 7, card);
            // manico
            v.Fill(-1, 7, 2, 10, 9, 4, cardDark);
            v.Carve(3, 8, 2, 6, 9, 4);                        // foro per le dita

            return Save("Birra", v, new Vector3(4.5f * v.size, -1f * v.size, 3f * v.size));
        }

        // ------------------------------------------------------------------ cucina e soggiorno

        /// <summary>Bancone della cucina (cubetti da 5 cm, 2 x 0.9 x 0.6 m): ante bianche, piano in laminato, maniglie.</summary>
        public static GameObject KitchenCounter()
        {
            var v = new VoxelBuilder(0.05f);
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), handle = v.C("#3a3a3a"), top = v.C("#b6a487"), plinth = v.C("#4a4540");

            v.Fill(0, 0, 0, 40, 17, 12, body);
            v.Fill(0, 0, 11, 40, 1, 12, plinth);
            v.Fill(-1, 17, -1, 41, 18, 13, top);              // piano con sbalzo
            for (int i = 0; i < 4; i++)
            {
                int x0 = 1 + i * 10;
                v.Fill(x0, 2, 12, x0 + 8, 11, 13, door);      // ante
                v.Fill(x0, 12, 12, x0 + 8, 16, 13, door);     // cassetti
                v.Set(x0 + 6, 9, 13, handle); v.Set(x0 + 6, 8, 13, handle);
                v.Set(x0 + 3, 14, 13, handle); v.Set(x0 + 4, 14, 13, handle);
            }
            return Save("Bancone", v, BottomCenter(v, 40, 12));
        }

        /// <summary>Poltrona bordeaux anni '90 (cubetti da 5 cm) con cuscini e piedini di legno. Il fronte guarda +Z.</summary>
        public static GameObject Armchair()
        {
            var v = new VoxelBuilder(0.05f);
            int fabric = v.C("#6b2329"), cushion = v.C("#7d2b32"), seam = v.C("#521a1f"), wood = v.C("#3c2a1a");

            v.Fill(0, 2, 0, 18, 8, 17, fabric);               // base
            v.Fill(0, 8, 0, 18, 18, 4, fabric);               // schienale
            v.Fill(0, 8, 4, 3, 13, 17, fabric);               // braccioli
            v.Fill(15, 8, 4, 18, 13, 17, fabric);
            v.Fill(3, 8, 4, 15, 10, 16, cushion);             // cuscino seduta
            v.Fill(3, 10, 4, 15, 17, 6, cushion);             // cuscino schiena
            v.Paint(3, 9, 4, 15, 10, 16, seam);
            v.Fill(1, 0, 1, 3, 2, 3, wood); v.Fill(15, 0, 1, 17, 2, 3, wood);
            v.Fill(1, 0, 14, 3, 2, 16, wood); v.Fill(15, 0, 14, 17, 2, 16, wood);
            return Save("Poltrona", v, BottomCenter(v, 18, 17));
        }

        /// <summary>Lampada da terra (cubetti da 4 cm): base, stelo, paralume color crema (che si "accende").</summary>
        public static GameObject FloorLamp()
        {
            var v = new VoxelBuilder(0.04f);
            int metal = v.C("#2f2b27");
            v.Fill(0, 0, 0, 7, 1, 7, metal);
            v.Fill(3, 1, 3, 4, 38, 4, metal);
            Save("Lampada stelo", v, BottomCenter(v, 7, 7));

            var shade = new VoxelBuilder(0.04f);
            int cream = shade.C("#efe0b0");
            shade.Fill(0, 0, 0, 9, 6, 9, cream);
            shade.Carve(1, 0, 1, 8, 5, 8);
            return Save("Lampada paralume", shade, BottomCenter(shade, 9, 9), false);
        }

        /// <summary>Placca dell'interruttore (cubetti da 1 cm).</summary>
        public static GameObject LightSwitchPlate()
        {
            var v = new VoxelBuilder(0.01f);
            int plate = v.C("#e8e2d0"), lever = v.C("#cfc8b4");
            v.Fill(0, 0, 0, 7, 11, 1, plate);
            v.Fill(2, 4, 1, 5, 7, 3, lever);
            return Save("Interruttore", v, new Vector3(3.5f * v.size, 5.5f * v.size, 0f));
        }

        /// <summary>Torcia (cubetti da 1.5 cm): corpo nero, testa grigia, vetro giallino, pulsante rosso. Punta verso +Z.</summary>
        public static GameObject Flashlight()
        {
            var v = new VoxelBuilder(0.015f);
            int body = v.C("#1b1b1d"), head = v.C("#3d3e42"), lens = v.C("#fff2a8"), button = v.C("#b02a2a");
            v.Fill(0, 0, 0, 3, 3, 10, body);
            v.Fill(-1, -1, 10, 4, 4, 13, head);
            v.Fill(0, 0, 13, 3, 3, 14, lens);
            v.Set(1, 3, 6, button);
            return Save("Torcia", v, new Vector3(1.5f * v.size, 0f, 7f * v.size));
        }

        /// <summary>Lampione di periferia (cubetti da 10 cm).</summary>
        public static GameObject StreetLight()
        {
            var v = new VoxelBuilder(0.1f);
            int pole = v.C("#3a3d42"), lamp = v.C("#f3e3a0");
            v.Fill(0, 0, 0, 1, 45, 1, pole);
            v.Fill(0, 44, 1, 1, 45, 5, pole);
            v.Fill(-1, 42, 4, 2, 44, 7, pole);
            v.Fill(-1, 41, 4, 2, 42, 7, lamp);
            return Save("Lampione", v, new Vector3(0.5f * v.size, 0f, 0.5f * v.size));
        }
    }
}
