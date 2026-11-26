using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Story;
using FantaLandia.Horror.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FantaLandia.Horror.EditorTools.HouseKit;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Menu "FantaLandia Horror > Crea casa e quartiere": la casa di Tom (villetta americana a due piani con
    /// cantina, garage, veranda e giardino), le case dei vicini, la strada con auto e lampioni.
    ///
    /// Casa di Tom (coordinate in metri, il fronte guarda -Z verso la strada):
    ///  - ingombro x -7..7, z 0..11; piano terra a F0, primo piano a F1, cantina a FB
    ///  - piano terra: soggiorno (sx davanti), ingresso con scala (centro), bagnetto e porta cantina (dx davanti),
    ///    sala da pranzo (sx dietro), cucina con isola (dx dietro), porta laterale e porta del garage
    ///  - primo piano: corridoio, camera matrimoniale, cameretta, camera di Tom, bagno, ripostiglio
    ///  - cantina: generatore, scaffali, caldaia, lavatrice, banco da lavoro, scatoloni
    /// </summary>
    public static class HouseSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Horror/Scenes/Casa_Tom.unity";

        const float F0 = 0.3f, H = 3.0f, S = 0.25f, F1 = F0 + H, F2 = F1 + H, FB = F0 - 2.8f;
        const float X0 = -7f, X1 = 7f, Z0 = 0f, Z1 = 11f, ET = 0.25f, IT = 0.15f;
        const float FenceHeight = 1.2f;

        static Material siding, trim, interior, interiorUp, wood, tiles, concrete, basementWall, roof, glass, grass,
                        asphalt, sidewalk, porchWood, fenceWood, deckWood, garageFloor, bathTiles, kitchenTiles, railing, stairSide, stairTread;

        static readonly List<Light> houseLights = new List<Light>();

        [MenuItem("FantaLandia Horror/Crea casa e quartiere")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorAssetUtil.EnsureFolder("Assets/_Project/Horror/Scenes");
            VoxelAssetLibrary.BuildAll();
            VoxelFurniture.BuildAll();
            Dictionary<string, GameObject> cast = HorrorCast.BuildAll();
            Materials();
            houseLights.Clear();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            HorrorTestSceneBuilder.SetupNight();

            Transform world = Group("Mondo", null);
            Transform street = Group("Strada e lotti", world);
            Step("terreno", () => BuildGround(street));
            Step("strada", () => BuildStreet(street));

            Transform house = Group("Casa di Tom", world);
            HorrorDoor frontDoor = null;
            CrtTv tv = null;
            Transform armchair = null;
            Step("struttura della casa", () => frontDoor = BuildHouseStructure(house));
            Step("arredamento", () => tv = BuildFurniture(house, out armchair, out _));
            Step("garage", () => BuildGarage(house));
            Step("veranda", () => BuildPorch(house));
            Step("giardino", () => BuildYard(house));

            Transform hood = Group("Vicinato", world);
            Step("vicinato", () => BuildNeighbors(hood));
            if (armchair == null) armchair = HorrorTestSceneBuilder.Marker("Poltrona (mancante)", world, new Vector3(-4.7f, F0, 5.1f));

            // personaggi e sistemi
            CharacterMotor tom = HorrorTestSceneBuilder.BuildTom(world, cast["tom"], new Vector3(-3.4f, F0, 1.7f), 150f);
            HorrorTestSceneBuilder.BuildPlayer(cast["muark"], new Vector3(0f, 0.08f, -9.8f), 0f);
            Step("personaggi", () => BuildCast(world, cast));
            HorrorTestSceneBuilder.BuildNavMesh();

            var power = new GameObject("Contatore della casa").AddComponent<HousePower>();
            power.lights = houseLights.ToArray();
            power.tvs = new[] { tv };
            power.switches = new LightSwitch[0];
            power.glowRenderers = new Renderer[0];

            var seq = new GameObject("Sequenza arrivo").AddComponent<TestIntroSequence>();
            seq.door = frontDoor;
            seq.tom = tom;
            seq.tomAtDoor = HorrorTestSceneBuilder.Marker("Tom alla porta", world, new Vector3(0f, F0, 1.4f));
            seq.tomBesideDoor = HorrorTestSceneBuilder.Marker("Tom accanto alla porta", world, new Vector3(-1.3f, F0, 1.0f));
            seq.tomCloseDoor = HorrorTestSceneBuilder.Marker("Tom chiude la porta", world, new Vector3(0.3f, F0, 1.05f));
            Vector3 chairDir = armchair.rotation * Vector3.forward;
            seq.chairFront = HorrorTestSceneBuilder.Marker("Davanti alla poltrona", world, armchair.position + chairDir * 0.85f);
            seq.chairSeat = HorrorTestSceneBuilder.Marker("Sedile poltrona", world, armchair.position);
            seq.chairSeat.rotation = armchair.rotation;
            seq.seatHeight = 0.5f;

            // erba al vento (si piega quando ci si passa in mezzo)
            Material grassMat = EditorAssetUtil.WindMat("Erba al vento", Color.white, 0.06f, 0f, 1f, true);
            foreach (var area in new[] { (new Vector2(-45f, -9f), new Vector2(45f, 23f), 11), (new Vector2(-45f, -40f), new Vector2(45f, -19f), 29) })
            {
                var grassGo = new GameObject("Prato d'erba");
                grassGo.transform.SetParent(world, false);
                var field = grassGo.AddComponent<GrassField>();
                field.material = grassMat;
                field.areaMin = area.Item1;
                field.areaMax = area.Item2;
                field.seed = area.Item3;
            }

            var amb = new GameObject("Ambiente notturno (audio)").AddComponent<AmbientSound>();
            amb.clip = AmbientClip.Night;
            amb.volume = 0.3f;

            var dbg = new GameObject("Tasti di prova").AddComponent<HorrorDebugKeys>();
            dbg.frontDoor = frontDoor;
            dbg.tv = tv;
            dbg.power = power;
            dbg.switches = new LightSwitch[0];

            // controllo finale: oggetti senza mesh (se ce ne sono, qualcosa è andato storto nella creazione dei prefab)
            int missing = 0;
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf.sharedMesh == null) { missing++; if (missing <= 10) Debug.LogWarning("[FantaLandia Horror] Senza mesh: " + mf.name, mf); }
            if (missing > 0) Debug.LogWarning($"[FantaLandia Horror] {missing} oggetti senza mesh");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[FantaLandia Horror] Casa e quartiere creati: " + ScenePath);
        }

        /// <summary>Esegue un pezzo della costruzione: se va in errore lo segnala e continua con il resto.</summary>
        static void Step(string what, System.Action action)
        {
            try { action(); }
            catch (System.Exception e) { Debug.LogError($"[FantaLandia Horror] Errore costruendo {what}: {e}"); }
        }

        // ------------------------------------------------------------------ personaggi

        /// <summary>
        /// Jef chiuso nel bagnetto, Roy nascosto di lato alla casa (per lo scherzo alla porta),
        /// i vicini sulle loro pedane appoggiati alla ringhiera che chiacchierano.
        /// </summary>
        static void BuildCast(Transform world, Dictionary<string, GameObject> cast)
        {
            Transform people = Group("Personaggi", world);
            HorrorTestSceneBuilder.BuildNpc(people, cast["jef"], "Jef", "jef", new Vector3(5.6f, F0, 1.9f), 200f);
            // Roy è nascosto del tutto: la sequenza lo attiverà quando bussa alla porta
            HorrorTestSceneBuilder.BuildNpc(people, cast["roy"], "Roy", "roy", new Vector3(-8.3f, 0f, 1.2f), 90f).gameObject.SetActive(false);

            // vicini: pedana alta 0.6 m, corrimano a 1.58 m sul lato verso il fondo del giardino (z = 13)
            Neighbor(people, cast["frank"], "Frank", "frank", new Vector3(-27.1f, 0.6f, 12.3f), new Vector3(-27.1f, 1.58f, 12.95f),
                     new[] { "frank|Martha, hai chiuso il gas?", "martha|Sì Frank, come ogni sera.", "frank|Questi ragazzi di Tom fanno sempre tardi." });
            Neighbor(people, cast["martha"], "Martha", "martha", new Vector3(-25.1f, 0.6f, 12.3f), new Vector3(-25.1f, 1.58f, 12.95f), null);
            Neighbor(people, cast["gary"], "Gary", "gary", new Vector3(24.9f, 0.6f, 12.3f), new Vector3(24.9f, 1.58f, 12.95f),
                     new[] { "gary|Linda, la partita è finita?", "linda|Ma ti pare l'ora, Gary?", "gary|Bella serata, comunque." });
            Neighbor(people, cast["linda"], "Linda", "linda", new Vector3(27.1f, 0.6f, 12.3f), new Vector3(27.1f, 1.58f, 12.95f), null);
        }

        static void Neighbor(Transform parent, GameObject prefab, string name, string id, Vector3 pos, Vector3 rail, string[] chatter)
        {
            CharacterMotor m = HorrorTestSceneBuilder.BuildNpc(parent, prefab, name, id, pos, 0f);
            var actor = m.GetComponent<NpcActor>();
            actor.leanOnStart = true;
            actor.leanPoint = rail;
            actor.ambientLines = chatter;
        }

        // ------------------------------------------------------------------ materiali

        static void Materials()
        {
            siding = M("Rivestimento", "#9fb2bd");
            trim = M("Finiture bianche", "#eeece4");
            interior = M("Pareti piano terra", "#cdbf9f");
            interiorUp = M("Pareti primo piano", "#bfc3ad");
            wood = M("Parquet", "#6b4a30", 0.3f);
            tiles = M("Piastrelle", "#c9c2b0", 0.4f);
            kitchenTiles = M("Piastrelle cucina", "#d9cf9e", 0.4f);
            bathTiles = M("Piastrelle bagno", "#a9c2c8", 0.5f);
            concrete = M("Cemento cantina", "#6e6c68");
            basementWall = M("Muro cantina", "#7d7a73");
            roof = M("Tegole", "#3b3c42");
            glass = EditorAssetUtil.Glass("Casa Vetro", new Color(0.55f, 0.7f, 0.8f));
            grass = M("Prato", "#2a3d20");
            asphalt = M("Asfalto", "#1d1d20");
            sidewalk = M("Marciapiede", "#6c6a66");
            porchWood = M("Veranda", "#7a5a3c");
            fenceWood = M("Staccionata", "#6b5034");
            deckWood = M("Pedana", "#806040");
            garageFloor = M("Pavimento garage", "#5a5854");
            railing = M("Ringhiera", "#efede6");
            stairSide = M("Fianco scala", "#5a3c26", 0.2f);
            stairTread = M("Pedata scala", "#8a6040", 0.35f);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ terreno e strada

        static void BuildGround(Transform parent)
        {
            // terra spessa con lo scavo della cantina della casa di Tom
            const float E = 70f, bottom = FB - 0.6f;
            Floor("Prato ovest", parent, new Vector3(-E, bottom, -E), new Vector3(X0, 0f, E), grass, Surface.Grass);
            Floor("Prato est", parent, new Vector3(X1, bottom, -E), new Vector3(E, 0f, E), grass, Surface.Grass);
            Floor("Prato davanti", parent, new Vector3(X0, bottom, -E), new Vector3(X1, 0f, Z0), grass, Surface.Grass);
            Floor("Prato dietro", parent, new Vector3(X0, bottom, Z1), new Vector3(X1, 0f, E), grass, Surface.Grass);
        }

        static void BuildStreet(Transform parent)
        {
            Floor("Strada", parent, new Vector3(-70f, 0f, -17.5f), new Vector3(70f, 0.01f, -10.5f), asphalt, Surface.Concrete);
            Floor("Marciapiede", parent, new Vector3(-70f, 0f, -10.5f), new Vector3(70f, 0.08f, -9f), sidewalk, Surface.Concrete);
            Floor("Marciapiede di fronte", parent, new Vector3(-70f, 0f, -19f), new Vector3(70f, 0.08f, -17.5f), sidewalk, Surface.Concrete);
            for (float x = -66f; x < 70f; x += 6f) // strisce
                Box("Striscia", parent, new Vector3(x, 0.01f, -14.1f), new Vector3(x + 3f, 0.015f, -13.9f), trim);

            GameObject lampPrefab = VoxelAssetLibrary.Load("Lampione");
            foreach (float x in new[] { -38f, -13f, 13f, 38f })
            {
                StreetLamp(parent, lampPrefab, new Vector3(x, 0.08f, -9.6f), 180f);
                StreetLamp(parent, lampPrefab, new Vector3(x + 12f, 0.08f, -17.9f), 0f);
            }

        }

        static void StreetLamp(Transform parent, GameObject prefab, Vector3 pos, float yaw)
        {
            GameObject lamp = Place(prefab, parent, pos, yaw);
            var l = new GameObject("Luce lampione").AddComponent<Light>();
            l.transform.SetParent(lamp.transform, false);
            l.transform.localPosition = new Vector3(0f, 4.0f, 0.6f);
            l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            l.type = LightType.Spot;
            l.spotAngle = 115f;
            l.range = 13f;
            l.intensity = 2.6f;
            l.color = new Color(1f, 0.8f, 0.5f);
            l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ casa di Tom: struttura

        static HorrorDoor BuildHouseStructure(Transform house)
        {
            Transform s = Group("Struttura", house);

            // solai
            Slab(s, "Pavimento piano terra", X0, X1, Z0, Z1, F0 - S, F0, wood, new Rect(3.72f, 3.01f, 1.4f, 2.64f));
            foreach (Transform t in s) if (t.name.StartsWith("Pavimento piano terra")) t.gameObject.AddComponent<FootstepSurface>().surface = Surface.Wood;
            Floor("Piastrelle cucina", s, new Vector3(-1f, F0, 6.075f), new Vector3(6.75f, F0 + 0.01f, 10.75f), kitchenTiles, Surface.Concrete);
            Floor("Piastrelle bagno", s, new Vector3(3.575f, F0, 0.25f), new Vector3(6.75f, F0 + 0.01f, 2.925f), bathTiles, Surface.Concrete);
            Slab(s, "Pavimento primo piano", X0, X1, Z0, Z1, F1 - S, F1, wood, new Rect(2.1f, 1.5f, 1.325f, 4.2f));
            foreach (Transform t in s) if (t.name.StartsWith("Pavimento primo piano")) t.gameObject.AddComponent<FootstepSurface>().surface = Surface.Wood;
            Floor("Piastrelle bagno sopra", s, new Vector3(3.575f, F1, 0.25f), new Vector3(6.75f, F1 + 0.01f, 3.925f), bathTiles, Surface.Concrete);
            Box("Soffitto primo piano", s, new Vector3(X0, F2 - S, Z0), new Vector3(X1, F2, Z1), trim);

            // cantina
            Floor("Pavimento cantina", s, new Vector3(X0, FB - 0.3f, Z0), new Vector3(X1, FB, Z1), concrete, Surface.Concrete);
            float bh = F0 - S - FB;
            Box("Muro cantina fronte", s, new Vector3(X0, FB, Z0), new Vector3(X1, FB + bh, Z0 + 0.3f), basementWall);
            Box("Muro cantina retro", s, new Vector3(X0, FB, Z1 - 0.3f), new Vector3(X1, FB + bh, Z1), basementWall);
            Box("Muro cantina sx", s, new Vector3(X0, FB, Z0), new Vector3(X0 + 0.3f, FB + bh, Z1), basementWall);
            Box("Muro cantina dx", s, new Vector3(X1 - 0.3f, FB, Z0), new Vector3(X1, FB + bh, Z1), basementWall);
            // scala della cantina: larga 1.4 m, parte subito oltre la porta della cucina e scende dritta verso -Z
            Stairs(s, "Scala cantina", false, 5.65f, -1, 3.72f, 5.12f, F0, 11, -2.8f / 11f, 0.24f, FB, porchWood, Surface.Wood); // centrata sulla porta

            // scala per il primo piano (nell'ingresso, sale verso +Z)
            // contro il muro destro dell'ingresso, lasciando libero lo spazio davanti alla porta
            Stairs(s, "Scala", false, 1.5f, 1, 2.1f, 3.425f, F0, 15, 0.2f, 0.28f, F0, stairSide, Surface.Wood);
            StairDetails(s);

            // muri esterni: nucleo (colore interno) + rivestimento esterno (azzurro) con le stesse aperture
            var front0 = new[] { new Opening(-0.47f, 0.47f, F0, F0 + 2.15f, OpeningKind.Door), Window(-4.6f, 2f, F0 + 0.85f, F0 + 2.2f), Window(5.2f, 0.8f, F0 + 1.5f, F0 + 2.1f) };
            var front1 = new[] { Window(-4.6f, 2f), Window(0.6f, 1f), Window(5.2f, 0.8f, 1.4f, 2.0f) };
            var back0 = new[] { Window(-4.5f, 1.8f, F0 + 0.85f, F0 + 2.2f), Window(2.5f, 1.6f, F0 + 1.15f, F0 + 2.15f) };
            var back1 = new[] { Window(-4.5f, 1.8f), Window(2.5f, 1.8f) };
            var left0 = new[] { Window(3f, 1.8f, F0 + 0.85f, F0 + 2.2f), Window(8.5f, 1.8f, F0 + 0.85f, F0 + 2.2f) };
            var left1 = new[] { Window(3f, 1.6f), Window(8.5f, 1.6f) };
            var right0 = new[] { new Opening(4.95f, 5.85f, F0, F0 + 2.15f, OpeningKind.Door), new Opening(8f, 8.9f, F0, F0 + 2.15f, OpeningKind.Door), Window(9.9f, 1f, F0 + 1.15f, F0 + 2.15f) };
            var right1 = new[] { Window(9f, 1.6f) };

            ExteriorWall(s, "Fronte terra", true, Z0 + ET * 0.5f, -1f, X0, X1, 0f, F1, front0);
            ExteriorWall(s, "Fronte sopra", true, Z0 + ET * 0.5f, -1f, X0, X1, F1, H, front1);
            ExteriorWall(s, "Retro terra", true, Z1 - ET * 0.5f, 1f, X0, X1, 0f, F1, back0);
            ExteriorWall(s, "Retro sopra", true, Z1 - ET * 0.5f, 1f, X0, X1, F1, H, back1);
            ExteriorWall(s, "Lato sx terra", false, X0 + ET * 0.5f, -1f, Z0 + ET, Z1 - ET, 0f, F1, left0);
            ExteriorWall(s, "Lato sx sopra", false, X0 + ET * 0.5f, -1f, Z0 + ET, Z1 - ET, F1, H, left1);
            ExteriorWall(s, "Lato dx terra", false, X1 - ET * 0.5f, 1f, Z0 + ET, Z1 - ET, 0f, F1, right0);
            ExteriorWall(s, "Lato dx sopra", false, X1 - ET * 0.5f, 1f, Z0 + ET, Z1 - ET, F1, H, right1);

            // muri interni piano terra
            float ih = F1 - S - F0;
            Wall(s, "Muro soggiorno", false, -2f, Z0 + ET, 6f, F0, ih, IT, interior, new[] { Arch(1.2f, 4.8f) }, null, trim);
            Wall(s, "Muro centrale", true, 6f, X0 + ET, X1 - ET, F0, ih, IT, interior,
                 new[] { Arch(-6f, -3f), Arch(-1.5f, 1.5f), HouseKit.Door(4.42f, 0.84f) }, null, trim); // porta cantina dalla cucina
            Wall(s, "Muro bagno", false, 3.5f, Z0 + ET, 6f, F0, ih, IT, interior,
                 new[] { HouseKit.Door(0.82f, 0.84f) }, null, trim);
            Wall(s, "Muro bagno-cantina", true, 3f, 3.5f, X1 - ET, F0, ih, IT, interior, null);

            // muri interni primo piano
            float uh = F2 - S - F1;
            Wall(s, "Muro camere", false, -2f, Z0 + ET, Z1 - ET, F1, uh, IT, interiorUp,
                 new[] { HouseKit.Door(4.6f, 0.84f), HouseKit.Door(6.5f, 0.84f) }, null, trim);
            Wall(s, "Muro matrimoniale-cameretta", true, 6f, X0 + ET, -2f, F1, uh, IT, interiorUp, null);
            Wall(s, "Muro camera Tom", true, 7f, -2f, X1 - ET, F1, uh, IT, interiorUp, new[] { HouseKit.Door(0.7f, 0.84f) }, null, trim);
            Wall(s, "Muro bagno sopra", false, 3.5f, Z0 + ET, 7f, F1, uh, IT, interiorUp,
                 new[] { HouseKit.Door(0.85f, 0.84f), HouseKit.Door(6.5f, 0.84f) }, null, trim);
            Wall(s, "Muro bagno-ripostiglio", true, 4f, 3.5f, X1 - ET, F1, uh, IT, interiorUp, null);

            // tetto a gradoni (colmo lungo X) con timpani
            SteppedRoof(s, "Tetto", X0 - 0.45f, X1 + 0.45f, Z0 - 0.45f, Z1 + 0.45f, F2, 10, 0.34f, true, roof, siding);

            // porte
            HorrorDoor front = DoorAt(s, "Porta d'ingresso", VoxelAssetLibrary.Load("Porta"), new Vector3(-0.45f, F0, Z0 + ET * 0.5f), 0f);
            front.knockLocal = new Vector3(0.45f, 1.62f, -0.055f);
            front.locked = true;
            front.knockable = true;
            DoorAt(s, "Porta laterale", VoxelAssetLibrary.Load("Porta"), new Vector3(X1 - ET * 0.5f, F0, 8.0f), -90f);
            GameObject inner = VoxelFurniture.Get("Porta interna");
            HorrorDoor bath = DoorAt(s, "Porta bagno (Jef)", inner, new Vector3(3.5f, F0, 0.82f + 0.4f), 90f);
            bath.locked = true;
            bath.lockedText = "È occupato...";
            DoorAt(s, "Porta cantina", inner, new Vector3(4.42f + 0.4f, F0, 6f), 180f); // dalla cucina verso la scala della cantina
            DoorAt(s, "Porta garage", inner, new Vector3(X1 - ET * 0.5f, F0, 5.85f), 90f);
            DoorAt(s, "Porta camera matrimoniale", inner, new Vector3(-2f, F1, 4.6f - 0.4f), -90f);
            DoorAt(s, "Porta cameretta", inner, new Vector3(-2f, F1, 6.5f - 0.4f), -90f);
            DoorAt(s, "Porta camera Tom", inner, new Vector3(0.7f - 0.4f, F1, 7f), 0f);
            DoorAt(s, "Porta bagno sopra", inner, new Vector3(3.5f, F1, 0.85f + 0.4f), 90f);
            DoorAt(s, "Porta ripostiglio", inner, new Vector3(3.5f, F1, 6.5f + 0.4f), 90f);

            // luci delle stanze (plafoniere)
            Color warm = Warm;
            RoomLight(s, "Luce soggiorno", new Vector3(-4.5f, F1 - S, 3f), 7f, 1.5f, warm, true);
            RoomLight(s, "Luce ingresso", new Vector3(0.8f, F1 - S, 2.5f), 5f, 1.1f, warm, true);
            RoomLight(s, "Luce sala da pranzo", new Vector3(-4f, F1 - S, 8.5f), 6f, 1.2f, warm, false);
            RoomLight(s, "Luce cucina", new Vector3(2.8f, F1 - S, 8.5f), 7f, 1.5f, new Color(1f, 0.95f, 0.85f), true);
            RoomLight(s, "Luce bagno", new Vector3(5.2f, F1 - S, 1.5f), 3.5f, 1f, warm, false);
            RoomLight(s, "Luce disimpegno", new Vector3(5.2f, F1 - S, 5.4f), 3.5f, 0.6f, warm, false);
            RoomLight(s, "Luce corridoio sopra", new Vector3(0.5f, F2 - S, 3.5f), 6f, 0.9f, warm, false);
            RoomLight(s, "Luce camera Tom", new Vector3(2.5f, F2 - S, 9f), 6f, 0.9f, warm, false);
            return front;
        }

        /// <summary>
        /// Dettagli della scala: pedate chiare con il bordo che sporge, battiscopa sul muro, fascione inclinato sul lato
        /// aperto, due colonnine per gradino, corrimano continuo in pendenza, pilastrini in basso e in alto,
        /// ringhiera al primo piano.
        /// </summary>
        static void StairDetails(Transform s)
        {
            Transform g = Group("Dettagli scala", s);
            const float x0 = 2.1f, x1 = 3.425f, z0 = 1.5f, run = 0.28f, rise = 0.2f;
            const int steps = 15;
            for (int i = 0; i < steps; i++)
            {
                float z = z0 + i * run, top = F0 + (i + 1) * rise;
                Box("Pedata", g, new Vector3(x0 - 0.02f, top, z - 0.03f), new Vector3(x1, top + 0.025f, z + run), stairTread);
            }
            // fascione inclinato sul lato aperto e battiscopa inclinato contro il muro
            SlopedBox("Fascione", g, new Vector3(x0 - 0.03f, F0 + 0.05f, z0 - 0.1f), new Vector3(x0 - 0.03f, F0 + steps * rise + 0.05f, z0 + steps * run - 0.1f), 0.05f, 0.3f, porchWood);
            SlopedBox("Battiscopa scala", g, new Vector3(x1 - 0.012f, F0 + 0.12f, z0), new Vector3(x1 - 0.012f, F0 + steps * rise + 0.12f, z0 + steps * run), 0.02f, 0.12f, porchWood);
            // colonnine e corrimano (altezza 0.9 m sopra il filo dei gradini)
            float railBase = 0.95f;
            for (int i = 0; i < steps; i++)
            {
                float top = F0 + (i + 1) * rise;
                foreach (float f in new[] { 0.07f, 0.19f })
                {
                    float z = z0 + i * run + f;
                    float railY = F0 + railBase + (z - z0) / run * rise;
                    Box("Colonnina", g, new Vector3(x0 + 0.02f, top + 0.02f, z - 0.02f), new Vector3(x0 + 0.06f, railY, z + 0.02f), railing);
                }
            }
            SlopedBox("Corrimano", g, new Vector3(x0 + 0.04f, F0 + railBase + 0.03f, z0 - 0.1f),
                      new Vector3(x0 + 0.04f, F0 + railBase + 0.03f + steps * rise, z0 + steps * run - 0.1f), 0.08f, 0.07f, porchWood);
            Box("Pilastrino basso", g, new Vector3(x0 - 0.03f, F0, z0 - 0.2f), new Vector3(x0 + 0.11f, F0 + 1.1f, z0 - 0.06f), porchWood);
            Box("Pomolo", g, new Vector3(x0 - 0.01f, F0 + 1.1f, z0 - 0.18f), new Vector3(x0 + 0.09f, F0 + 1.18f, z0 - 0.08f), stairTread);
            Box("Pilastrino alto", g, new Vector3(x0 - 0.03f, F1, z0 + steps * run - 0.14f), new Vector3(x0 + 0.11f, F1 + 1.05f, z0 + steps * run), porchWood);
            // ringhiera al primo piano attorno all'apertura
            Railing(g, new Vector3(x0 + 0.04f, F1, z0 - 0.02f), new Vector3(x0 + 0.04f, F1, z0 + steps * run - 0.14f));
            Railing(g, new Vector3(x0 + 0.04f, F1, z0 - 0.02f), new Vector3(x1, F1, z0 - 0.02f));
        }

        /// <summary>Ringhiera dritta: colonnine ogni 15 cm e corrimano.</summary>
        static void Railing(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Vector3 dir = d / len;
            for (float t = 0f; t <= len; t += 0.15f)
            {
                Vector3 p = a + dir * t;
                Box("Colonnina", parent, p + new Vector3(-0.025f, 0f, -0.025f), p + new Vector3(0.025f, 0.9f, 0.025f), railing);
            }
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
            Box("Corrimano", parent, lo + new Vector3(-0.045f, 0.9f, -0.045f), hi + new Vector3(0.045f, 0.97f, 0.045f), porchWood);
        }

        /// <summary>Muro esterno: nucleo con il colore interno + rivestimento esterno sottile con le stesse aperture.</summary>
        static void ExteriorWall(Transform parent, string name, bool alongX, float fixedCoord, float outward, float from, float to,
                                 float y0, float height, Opening[] openings)
        {
            Material inside = y0 >= F1 - 0.01f ? interiorUp : interior;
            Wall(parent, name, alongX, fixedCoord, from, to, y0, height, ET, inside, openings, glass, trim);
            float clad = fixedCoord + outward * (ET * 0.5f + 0.02f);
            float f = alongX ? from - 0.04f : from - 0.29f;
            float t = alongX ? to + 0.04f : to + 0.29f;
            Wall(parent, name + " rivestimento", alongX, clad, f, t, y0, height, 0.04f, siding, openings);
            if (y0 < 0.01f) // fascia bianca alla base
            {
                float b = fixedCoord + outward * (ET * 0.5f + 0.045f);
                Vector3 min = alongX ? new Vector3(f, 0f, b - 0.01f) : new Vector3(b - 0.01f, 0f, f);
                Vector3 max = alongX ? new Vector3(t, 0.25f, b + 0.01f) : new Vector3(b + 0.01f, 0.25f, t);
                Box(name + " zoccolo", parent, min, max, trim);
            }
        }

        static HorrorDoor DoorAt(Transform parent, string name, GameObject prefab, Vector3 hingePos, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = hingePos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var hinge = new GameObject("Cardine").transform;
            hinge.SetParent(root.transform, false);
            Place(prefab, hinge, Vector3.zero, 0f);
            var door = root.AddComponent<HorrorDoor>();
            door.hinge = hinge;
            door.hingeSign = 1f;
            HorrorTestSceneBuilder.SetLayer(root, HorrorTestSceneBuilder.DoorLayer);
            return door;
        }

        static void RoomLight(Transform parent, string name, Vector3 ceilingPoint, float range, float intensity, Color color, bool shadows)
        {
            Place(VoxelFurniture.Get("Plafoniera"), parent, ceilingPoint, 0f);
            houseLights.Add(PointLight(parent, name, ceilingPoint + Vector3.down * 0.35f, range, intensity, color, shadows));
        }

        // ------------------------------------------------------------------ casa di Tom: arredamento

        static CrtTv BuildFurniture(Transform house, out Transform armchair, out PlaceSpot island)
        {
            Transform f = Group("Arredamento", house);

            // soggiorno
            Place(VoxelFurniture.Get("Tappeto"), f, new Vector3(-4.4f, F0, 3f), 90f);
            Place(VoxelAssetLibrary.Load("Mobile TV"), f, new Vector3(-6.45f, F0, 3f), 90f);
            CrtTv tv = BuildTv(f, new Vector3(-6.42f, F0 + 0.5f, 3f), 90f);
            Place(VoxelFurniture.Get("Divano"), f, new Vector3(-2.75f, F0, 3f), -90f);
            Place(VoxelFurniture.Get("Tavolino"), f, new Vector3(-4.4f, F0, 3f), 90f);
            armchair = Place(VoxelAssetLibrary.Load("Poltrona"), f, new Vector3(-4.7f, F0, 5.1f), -136f).transform;
            Place(VoxelAssetLibrary.Load("Lampada stelo"), f, new Vector3(-6.35f, F0, 5.45f), 0f);
            GameObject shade = Place(VoxelAssetLibrary.Load("Lampada paralume"), f, new Vector3(-6.35f, F0 + 1.44f, 5.45f), 0f);
            houseLights.Add(PointLight(shade.transform, "Luce lampada", new Vector3(0f, 0.1f, 0f), 4f, 0.9f, new Color(1f, 0.75f, 0.45f), false));
            Place(VoxelFurniture.Get("Libreria"), f, new Vector3(-3f, F0, 0.4f), 0f);


            // sala da pranzo
            Place(VoxelFurniture.Get("Tavolo"), f, new Vector3(-4.2f, F0, 8.5f), 0f);
            foreach (float x in new[] { -5.0f, -3.4f })
            {
                Place(VoxelFurniture.Get("Sedia"), f, new Vector3(x, F0, 7.6f), 0f);
                Place(VoxelFurniture.Get("Sedia"), f, new Vector3(x, F0, 9.4f), 180f);
            }

            // cucina
            GameObject counter = Place(VoxelAssetLibrary.Load("Bancone"), f, new Vector3(0.5f, F0, 10.4f), 180f);
            Place(VoxelFurniture.Get("Bancone lavello"), f, new Vector3(2.5f, F0, 10.4f), 180f);
            Place(VoxelFurniture.Get("Fornello"), f, new Vector3(3.9f, F0, 10.45f), 180f);
            Place(VoxelFurniture.Get("Frigo"), f, new Vector3(6.3f, F0, 10.35f), 180f);
            Place(VoxelFurniture.Get("Bidone"), f, new Vector3(5.1f, F0, 10.4f), 0f);
            GameObject isl = Place(VoxelFurniture.Get("Isola cucina"), f, new Vector3(2.6f, F0, 7.9f), 0f);
            island = isl.AddComponent<PlaceSpot>();
            island.acceptsItemId = "birra";
            island.where = "sull'isola della cucina";
            var point = new GameObject("Punto birra").transform;
            point.SetParent(isl.transform, false);
            point.localPosition = new Vector3(-0.4f, 0.9f, 0f);
            island.placePoint = point;
            var stand = new GameObject("Dove stare").transform;
            stand.SetParent(isl.transform, false);
            stand.localPosition = new Vector3(-0.2f, 0f, -0.95f);
            island.standPoint = stand;
            Place(VoxelFurniture.Get("Telefono"), f, counter.transform.localPosition + new Vector3(-0.5f, 0.9f, 0.05f), 180f);
            GameObject torch = Place(VoxelAssetLibrary.Load("Torcia"), f, counter.transform.localPosition + new Vector3(0.4f, 0.9f, 0f), 60f);
            torch.AddComponent<FlashlightPickup>();

            // bagnetto (Jef è chiuso dentro)
            Place(VoxelFurniture.Get("WC"), f, new Vector3(6.3f, F0, 1.2f), -90f);
            Place(VoxelFurniture.Get("Lavandino"), f, new Vector3(5f, F0, 0.25f), 0f);

            // primo piano
            Place(VoxelFurniture.Get("Letto matrimoniale"), f, new Vector3(-4.4f, F1, 0.25f + 1.1f), 0f);
            Place(VoxelFurniture.Get("Comodino"), f, new Vector3(-5.6f, F1, 0.5f), 0f);
            Place(VoxelFurniture.Get("Comodino"), f, new Vector3(-3.2f, F1, 0.5f), 0f);
            Place(VoxelFurniture.Get("Armadio"), f, new Vector3(-6.45f, F1, 5.2f), 90f);
            Place(VoxelFurniture.Get("Letto singolo"), f, new Vector3(-5.75f, F1, 9.4f), 90f);
            Place(VoxelFurniture.Get("Scrivania"), f, new Vector3(-3.2f, F1, 10.4f), 180f);
            Place(VoxelFurniture.Get("Letto singolo"), f, new Vector3(5.75f, F1, 9.2f), -90f);
            Place(VoxelFurniture.Get("Scrivania"), f, new Vector3(0.2f, F1, 10.4f), 180f);
            Place(VoxelFurniture.Get("Libreria"), f, new Vector3(4.6f, F1, 10.6f), 180f);
            Place(VoxelFurniture.Get("Armadio"), f, new Vector3(-1.6f, F1, 9.4f), 90f);
            Place(VoxelFurniture.Get("Vasca"), f, new Vector3(5.1f, F1, 0.7f), 0f);
            Place(VoxelFurniture.Get("WC"), f, new Vector3(6.3f, F1, 2.6f), -90f);
            Place(VoxelFurniture.Get("Lavandino"), f, new Vector3(4.4f, F1, 3.92f), 180f);
            Place(VoxelFurniture.Get("Scaffale"), f, new Vector3(6.45f, F1, 5.5f), -90f);

            // cantina: sporca e piena di roba, luce spenta (la accenderà il generatore)
            Transform b = Group("Cantina", f);
            Place(VoxelFurniture.Get("Generatore"), b, new Vector3(-5.3f, FB, 9.9f), 180f);
            Place(VoxelFurniture.Get("Tanica"), b, new Vector3(-4.3f, FB, 10.3f), 20f);
            Place(VoxelFurniture.Get("Scaffale"), b, new Vector3(-6.45f, FB, 2.8f), 90f);
            Place(VoxelFurniture.Get("Scaffale"), b, new Vector3(-6.45f, FB, 6.2f), 90f);
            Place(VoxelFurniture.Get("Caldaia"), b, new Vector3(5.6f, FB, 10.2f), 0f);
            Place(VoxelFurniture.Get("Lavatrice"), b, new Vector3(3.6f, FB, 10.3f), 180f);
            Place(VoxelFurniture.Get("Banco da lavoro"), b, new Vector3(0.2f, FB, 10.4f), 180f);
            Vector3[] boxes = { new Vector3(-3.5f, 0f, 1.2f), new Vector3(-2.6f, 0f, 1.0f), new Vector3(-3.1f, 0.3f, 1.1f), new Vector3(1.5f, 0f, 2.2f),
                                new Vector3(-1.2f, 0f, 6.8f), new Vector3(2.4f, 0f, 7.6f), new Vector3(-5.6f, 0f, 8.3f), new Vector3(0.5f, 0f, 1.0f) };
            string[] kinds = { "Scatola grande", "Scatola", "Scatola piccola" };
            for (int i = 0; i < boxes.Length; i++)
                Place(VoxelFurniture.Get(kinds[i % kinds.Length]), b, new Vector3(boxes[i].x, FB + boxes[i].y, boxes[i].z), i * 23f);
            foreach (Vector3 p in new[] { new Vector3(-3f, F0 - S, 3f), new Vector3(0f, F0 - S, 8f), new Vector3(4.5f, F0 - S, 7f) })
            {
                Place(VoxelFurniture.Get("Lampadina"), b, p, 0f);
                var bulb = PointLight(b, "Luce cantina (spenta)", p + Vector3.down * 0.5f, 6f, 1f, new Color(1f, 0.85f, 0.6f), false);
                bulb.enabled = false;
            }
            return tv;
        }

        static CrtTv BuildTv(Transform parent, Vector3 pos, float yaw)
        {
            GameObject tvGo = Place(VoxelAssetLibrary.Load("TV"), parent, pos, yaw);
            var tv = tvGo.AddComponent<CrtTv>();
            VoxelAssetLibrary.ScreenRect(out Vector3 sc, out Vector2 ss);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Schermo";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(tvGo.transform, false);
            quad.transform.localPosition = sc;
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(ss.x, ss.y, 1f);
            tv.screen = quad.GetComponent<Renderer>();
            tv.screenLight = PointLight(tvGo.transform, "Luce schermo", new Vector3(0f, 0.3f, 0.6f), 4.5f, 1.2f, Color.white, false);
            return tv;
        }

        // ------------------------------------------------------------------ garage, veranda, giardino

        static void BuildGarage(Transform house)
        {
            Transform g = Group("Garage", house);
            const float gx0 = 7f, gx1 = 12.5f, gz0 = 0.6f, gz1 = 6.8f, gh = 3f;
            Floor("Pavimento garage", g, new Vector3(gx0, 0f, gz0), new Vector3(gx1, 0.1f, gz1), garageFloor, Surface.Concrete);
            Wall(g, "Garage fronte", true, gz0 + 0.1f, gx0, gx1, 0f, gh, 0.2f, siding, new[] { new Opening(8f, 10.7f, 0f, 2.3f, OpeningKind.Door) }, null, trim);
            Wall(g, "Garage retro", true, gz1 - 0.1f, gx0, gx1, 0f, gh, 0.2f, siding, null);
            Wall(g, "Garage lato", false, gx1 - 0.1f, gz0, gz1, 0f, gh, 0.2f, siding, new[] { Window(3.7f, 1.2f, 1.2f, 2.1f) }, glass, trim);
            SteppedRoof(g, "Tetto garage", gx0 - 0.1f, gx1 + 0.35f, gz0 - 0.35f, gz1 + 0.35f, gh, 5, 0.3f, true, roof, siding);
            Place(VoxelFurniture.Get("Portone garage"), g, new Vector3(9.35f, 0f, gz0 + 0.05f), 0f);
            Place(VoxelFurniture.Get("Scaffale"), g, new Vector3(12.1f, 0.1f, 3.8f), -90f);
            Place(VoxelFurniture.Get("Scatola grande"), g, new Vector3(11.6f, 0.1f, 6f), 10f);
            Floor("Vialetto garage", g, new Vector3(8f, 0f, -9f), new Vector3(10.7f, 0.03f, gz0), sidewalk, Surface.Concrete);
            Place(VoxelFurniture.Get("Bidone"), g, new Vector3(11.3f, 0.03f, -0.4f), 0f);
            Place(VoxelFurniture.Get("Bidone"), g, new Vector3(11.95f, 0.03f, -0.4f), 0f);
        }

        static void BuildPorch(Transform house)
        {
            Transform p = Group("Veranda", house);
            Floor("Pavimento veranda", p, new Vector3(-3f, 0f, -2.4f), new Vector3(3f, F0, 0f), porchWood, Surface.Wood);
            Floor("Gradino", p, new Vector3(-1f, 0f, -2.8f), new Vector3(1f, 0.15f, -2.4f), porchWood, Surface.Wood);
            Box("Tetto veranda", p, new Vector3(-3.3f, 3.1f, -2.7f), new Vector3(3.3f, 3.25f, 0f), trim);
            Box("Tetto veranda tegole", p, new Vector3(-3.35f, 3.25f, -2.75f), new Vector3(3.35f, 3.35f, 0f), roof);
            Place(VoxelFurniture.Get("Pilastro veranda"), p, new Vector3(-2.85f, F0, -2.25f), 0f);
            Place(VoxelFurniture.Get("Pilastro veranda"), p, new Vector3(2.85f, F0, -2.25f), 0f);
            Box("Ringhiera veranda sx", p, new Vector3(-3f, F0 + 0.8f, -2.4f), new Vector3(-1.1f, F0 + 0.88f, -2.32f), railing);
            Box("Ringhiera veranda dx", p, new Vector3(1.1f, F0 + 0.8f, -2.4f), new Vector3(3f, F0 + 0.88f, -2.32f), railing);
            for (float x = -2.9f; x < -1.1f; x += 0.25f) Box("Colonnina", p, new Vector3(x, F0, -2.38f), new Vector3(x + 0.05f, F0 + 0.8f, -2.33f), railing);
            for (float x = 1.2f; x < 3f; x += 0.25f) Box("Colonnina", p, new Vector3(x, F0, -2.38f), new Vector3(x + 0.05f, F0 + 0.8f, -2.33f), railing);
            houseLights.Add(PointLight(p, "Luce veranda", new Vector3(0.9f, 2.5f, -0.35f), 6f, 1.3f, new Color(1f, 0.8f, 0.5f), true));
            Box("Lampada veranda", p, new Vector3(0.82f, 2.45f, -0.3f), new Vector3(0.98f, 2.65f, -0.25f), EditorAssetUtil.Emissive("Casa Lampada accesa", new Color(1f, 0.85f, 0.55f), 2f));
        }

        static void BuildYard(Transform house)
        {
            Transform y = Group("Giardino", house);
            Floor("Vialetto", y, new Vector3(-0.6f, 0f, -9f), new Vector3(0.6f, 0.03f, -2.8f), sidewalk, Surface.Concrete);
            Place(VoxelFurniture.Get("Cassetta posta"), y, new Vector3(-1.4f, 0f, -8.6f), 0f);
            foreach (var bp in new[] { new Vector3(-5.6f, 0f, -0.75f), new Vector3(-4.3f, 0f, -0.75f), new Vector3(4.3f, 0f, -0.75f), new Vector3(5.7f, 0f, -0.75f) })
                Place(VoxelFurniture.Get("Cespuglio"), y, bp, 0f);
            Place(VoxelFurniture.Get("Albero"), y, new Vector3(-10f, 0f, -5f), 0f);

            // retro: pedana con barbecue e sedie, alberi, staccionata alta
            Floor("Pedana", y, new Vector3(-4f, 0f, Z1), new Vector3(2f, 0.15f, 14f), deckWood, Surface.Wood);
            Place(VoxelFurniture.Get("Barbecue"), y, new Vector3(1.2f, 0.15f, 13.2f), 0f);
            Place(VoxelFurniture.Get("Sedia da giardino"), y, new Vector3(-3f, 0.15f, 12.9f), 30f);
            Place(VoxelFurniture.Get("Sedia da giardino"), y, new Vector3(-1.6f, 0.15f, 13.3f), -20f);
            Place(VoxelFurniture.Get("Albero"), y, new Vector3(8f, 0f, 17f), 0f);
            Place(VoxelFurniture.Get("Albero"), y, new Vector3(-9f, 0f, 19f), 0f);
            Floor("Vialetto laterale", y, new Vector3(7f, 0f, 7.2f), new Vector3(9f, 0.03f, 9.6f), sidewalk, Surface.Concrete);

            Fence(y, "Staccionata retro", new Vector3(-13f, 0f, 22f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            Fence(y, "Staccionata sinistra", new Vector3(-13f, 0f, 1f), new Vector3(-13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            Fence(y, "Staccionata destra", new Vector3(13f, 0f, 1f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
        }

        // ------------------------------------------------------------------ vicinato

        static void BuildNeighbors(Transform hood)
        {
            // vicini ai lati (stessa strada) e tre case di fronte
            // (le staccionate sui confini in comune le costruisce una casa sola)
            Shell(hood, "Casa vicini sinistra", new Vector3(-26f, 0f, 0f), 0f, "#c9b98f", true, new[] { true, false, true, false, true }, true, false);
            Shell(hood, "Casa vicini destra", new Vector3(26f, 0f, 0f), 0f, "#a9b99a", true, new[] { false, true, true, true, false }, false, true);
            Shell(hood, "Casa di fronte A", new Vector3(-26f, 0f, -27f), 180f, "#d8c8b0", false, new[] { false, false, true, false, false }, true, true);
            Shell(hood, "Casa di fronte B", new Vector3(0f, 0f, -27f), 180f, "#b8a8a8", false, new[] { true, false, false, false, true }, false, false);
            Shell(hood, "Casa di fronte C", new Vector3(26f, 0f, -27f), 180f, "#e2dfc9", false, new[] { false, false, false, true, false }, true, true);

            foreach (var t in new[] { new Vector3(-38f, 0f, 14f), new Vector3(36f, 0f, 16f), new Vector3(-14f, 0f, -36f), new Vector3(14f, 0f, -38f), new Vector3(40f, 0f, -6f) })
                Place(VoxelFurniture.Get("Albero"), hood, t, 0f);
        }

        /// <summary>
        /// Casa dei vicini "a guscio" (niente interni): due piani con finestre accese o buie, porta, veranda,
        /// tetto a gradoni. Fronte verso -Z prima della rotazione. Con 'deck' ha una pedana sul retro con ringhiera.
        /// </summary>
        static void Shell(Transform parent, string name, Vector3 frontCenter, float yaw, string sidingHex, bool deck, bool[] lit,
                          bool fenceLeft, bool fenceRight)
        {
            Transform h = Group(name, parent);
            h.localPosition = frontCenter;
            h.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material side = M("Rivestimento " + sidingHex, sidingHex);
            Material on = EditorAssetUtil.Emissive("Casa Finestra accesa", new Color(0.95f, 0.75f, 0.45f), 1.6f);
            Material off = M("Finestra buia", "#141a22", 0.8f);
            Material W(int i) => lit[i % lit.Length] ? on : off;

            const float w = 6.5f, d = 10f, fh = 2.9f;
            Box("Base", h, new Vector3(-w, -0.1f, 0f), new Vector3(w, 0.3f, d), concrete);
            for (int floor = 0; floor < 2; floor++)
            {
                float y0 = 0.3f + floor * fh;
                Wall(h, "Fronte", true, 0.12f, -w, w, y0, fh, 0.24f, side,
                     floor == 0 ? new[] { Window(-4f, 1.8f), new Opening(-0.45f, 0.45f, 0f, 2.15f, OpeningKind.Door), Window(4f, 1.8f) }
                                : new[] { Window(-4f, 1.6f), Window(0f, 1f), Window(4f, 1.6f) }, W(floor * 3), trim);
                Wall(h, "Retro", true, d - 0.12f, -w, w, y0, fh, 0.24f, side, new[] { Window(-3.5f, 1.6f), Window(3.5f, 1.6f) }, W(floor * 3 + 1), trim);
                Wall(h, "Lato A", false, -w + 0.12f, 0.24f, d - 0.24f, y0, fh, 0.24f, side, new[] { Window(5f, 1.4f) }, W(floor + 2), trim);
                Wall(h, "Lato B", false, w - 0.12f, 0.24f, d - 0.24f, y0, fh, 0.24f, side, new[] { Window(5f, 1.4f) }, W(floor + 4), trim);
            }
            Box("Soffitto", h, new Vector3(-w, 0.3f + 2 * fh - 0.2f, 0f), new Vector3(w, 0.3f + 2 * fh, d), trim);
            SteppedRoof(h, "Tetto", -w - 0.4f, w + 0.4f, -0.4f, d + 0.4f, 0.3f + 2 * fh, 9, 0.33f, true, roof, side);
            Place(VoxelAssetLibrary.Load("Porta"), h, new Vector3(-0.45f, 0.3f, 0.12f), 0f);
            Box("Veranda", h, new Vector3(-2.5f, 0f, -2f), new Vector3(2.5f, 0.3f, 0f), porchWood);
            Box("Tetto veranda", h, new Vector3(-2.7f, 3f, -2.2f), new Vector3(2.7f, 3.15f, 0f), trim);
            Place(VoxelFurniture.Get("Pilastro veranda"), h, new Vector3(-2.3f, 0.3f, -1.85f), 0f);
            Place(VoxelFurniture.Get("Pilastro veranda"), h, new Vector3(2.3f, 0.3f, -1.85f), 0f);
            if (lit[0]) PointLight(h, "Luce veranda", new Vector3(0.8f, 2.5f, -0.4f), 5f, 1f, new Color(1f, 0.8f, 0.5f), false);
            Floor("Vialetto", h, new Vector3(-0.6f, 0f, -9f), new Vector3(0.6f, 0.03f, -2f), sidewalk, Surface.Concrete);
            Floor("Passo carraio", h, new Vector3(4.5f, 0f, -9f), new Vector3(7.5f, 0.03f, 0f), sidewalk, Surface.Concrete);
            Place(VoxelFurniture.Get("Cassetta posta"), h, new Vector3(-1.4f, 0f, -8.6f), 0f);

            // giardino sul retro recintato
            Fence(h, "Staccionata retro", new Vector3(-13f, 0f, 22f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            if (fenceLeft) Fence(h, "Staccionata lato A", new Vector3(-13f, 0f, 1f), new Vector3(-13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            if (fenceRight) Fence(h, "Staccionata lato B", new Vector3(13f, 0f, 1f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);

            if (!deck) return;
            // pedana sul retro con ringhiera (i vicini ci si appoggeranno per guardare l'esplosione)
            Floor("Pedana", h, new Vector3(-3f, 0f, d), new Vector3(3f, 0.6f, d + 3f), deckWood, Surface.Wood);
            Box("Ringhiera pedana", h, new Vector3(-3f, 1.5f, d + 2.95f), new Vector3(3f, 1.58f, d + 3.05f), railing);
            Box("Ringhiera pedana sx", h, new Vector3(-3.05f, 1.5f, d), new Vector3(-2.95f, 1.58f, d + 3f), railing);
            Box("Ringhiera pedana dx", h, new Vector3(2.95f, 1.5f, d), new Vector3(3.05f, 1.58f, d + 3f), railing);
            for (float x = -2.9f; x < 3f; x += 0.4f) Box("Colonnina", h, new Vector3(x, 0.6f, d + 2.97f), new Vector3(x + 0.06f, 1.5f, d + 3.03f), railing);
        }
    }
}
