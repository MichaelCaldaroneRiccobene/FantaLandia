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

        /// <summary>Mobile basso per la TV (cubetti da 10 cm, 1 x 0.5 x 0.5 m) con videoregistratore sul ripiano.</summary>
        public static GameObject TvCabinet()
        {
            var v = new VoxelBuilder(0.1f);
            int wood = v.C("#5a3c24"), woodDark = v.C("#45301d"), knob = v.C("#c9a24a");
            int vcr = v.C("#1a1a1c"), led = v.C("#40ff70");

            v.Fill(0, 0, 0, 10, 5, 5, wood);
            v.Fill(0, 0, 4, 10, 1, 5, woodDark);           // zoccolo
            v.Carve(1, 3, 1, 9, 4, 5);                     // ripiano aperto
            v.Fill(2, 3, 1, 7, 4, 4, vcr);                 // videoregistratore
            v.Set(6, 3, 4, led);
            v.Fill(1, 1, 4, 9, 3, 5, woodDark);            // ante
            v.Set(4, 2, 5, knob); v.Set(5, 2, 5, knob);    // pomelli
            return Save("Mobile TV", v, BottomCenter(v, 10, 5));
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

        /// <summary>Bancone della cucina (cubetti da 10 cm, 2 x 0.9 x 0.6 m): ante bianche, piano in laminato, maniglie.</summary>
        public static GameObject KitchenCounter()
        {
            var v = new VoxelBuilder(0.1f);
            int body = v.C("#d9d3c3"), door = v.C("#e6e0d0"), handle = v.C("#3a3a3a"), top = v.C("#b6a487"), plinth = v.C("#4a4540");

            v.Fill(0, 0, 0, 20, 8, 6, body);
            v.Fill(0, 0, 5, 20, 1, 6, plinth);
            v.Fill(0, 8, 0, 20, 9, 7, top);                 // piano con sbalzo davanti
            for (int i = 0; i < 4; i++)
            {
                int x0 = i * 5;
                v.Fill(x0 + 1, 1, 6, x0 + 4, 5, 7, door);   // ante
                v.Fill(x0 + 1, 6, 6, x0 + 4, 7, 7, door);   // cassetti
                v.Set(x0 + 3, 4, 7, handle);
                v.Set(x0 + 2, 6, 7, handle);
            }
            return Save("Bancone", v, BottomCenter(v, 20, 6));
        }

        /// <summary>Poltrona bordeaux anni '90 (cubetti da 10 cm, seduta a 0.5 m) con cuscini e piedini. Il fronte guarda +Z.</summary>
        public static GameObject Armchair()
        {
            var v = new VoxelBuilder(0.1f);
            int fabric = v.C("#6b2329"), cushion = v.C("#7d2b32"), wood = v.C("#3c2a1a");

            v.Fill(0, 1, 0, 9, 4, 9, fabric);               // base
            v.Fill(0, 4, 0, 9, 9, 2, fabric);               // schienale
            v.Fill(0, 4, 2, 2, 6, 9, fabric);               // braccioli
            v.Fill(7, 4, 2, 9, 6, 9, fabric);
            v.Fill(2, 4, 2, 7, 5, 8, cushion);              // cuscino seduta
            v.Fill(2, 5, 2, 7, 8, 3, cushion);              // cuscino schiena
            v.Set(0, 0, 0, wood); v.Set(8, 0, 0, wood); v.Set(0, 0, 8, wood); v.Set(8, 0, 8, wood);
            return Save("Poltrona", v, BottomCenter(v, 9, 9));
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
