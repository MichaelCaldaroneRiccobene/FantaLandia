using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
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
    public static partial class HouseSceneBuilder
    {
        const string ScenePath1 = "Assets/_Project/Horror/Scenes/Casa_Tom.unity";
        const string ScenePath2 = "Assets/_Project/Horror/Scenes/Casa_Tom_Capitolo2.unity";
        static string ScenePath => ch2 ? ScenePath2 : ScenePath1;

        /// <summary>Si sta costruendo la casa del capitolo 2 (primo piano strappato via, rifugio in cantina).</summary>
        static bool ch2;

        const float F0 = 0.3f, H = 3.0f, S = 0.25f, F1 = F0 + H, F2 = F1 + H, FB = F0 - 2.8f;
        const float X0 = -7f, X1 = 7f, Z0 = 0f, Z1 = 11f, ET = 0.25f, IT = 0.15f;
        const float FenceHeight = 1.2f;

        static Material siding, trim, interior, interiorUp, wood, tiles, concrete, basementWall, roof, glass, grass,
                        asphalt, sidewalk, porchWood, fenceWood, deckWood, garageFloor, bathTiles, kitchenTiles, railing, stairSide, stairTread;

        static readonly List<Light> houseLights = new List<Light>();
        static readonly List<Light> cellarBulbs = new List<Light>();
        static HorrorDoor backDoor, bathDoor, cellarDoor, sideDoor;
        static GeneratorStarter generator;
        static Transform cellarRadio;
        static FlashlightPickup torchPickup;
        static CharacterMotor royMotor, jefMotor;
        static readonly List<NpcActor> neighborActors = new List<NpcActor>();

        [MenuItem("FantaLandia Horror/Crea casa e quartiere")]
        public static void Build()
        {
            ch2 = false;
            BuildScene();
        }

        static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorAssetUtil.EnsureFolder("Assets/_Project/Horror/Scenes");
            VoxelAssetLibrary.BuildAll();
            VoxelFurniture.BuildAll();
            Dictionary<string, GameObject> cast = HorrorCast.BuildAll();
            Materials();
            houseLights.Clear();
            cellarBulbs.Clear();
            neighborActors.Clear();
            backDoor = bathDoor = cellarDoor = sideDoor = null;
            generator = null;
            cellarRadio = null;
            royMotor = jefMotor = null;
            torchPickup = null;
            ResetChapter2();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            HorrorTestSceneBuilder.SetupNight();
            HorrorVisuals.Cones.Clear();
            Step("pipeline grafica", HorrorVisuals.ConfigurePipeline);

            Transform world = Group("Mondo", null);
            Transform street = Group("Strada e lotti", world);
            Step("terreno", () => BuildGround(street));
            Step("strada", () => BuildStreet(street));

            Transform house = Group("Casa di Tom", world);
            HorrorDoor frontDoor = null;
            CrtTv tv = null;
            Transform armchair = null;
            PlaceSpot island = null;
            Step("struttura della casa", () => frontDoor = BuildHouseStructure(house));
            Step("arredamento", () => tv = BuildFurniture(house, out armchair, out island));
            Step("garage", () => BuildGarage(house));
            Step("veranda", () => BuildPorch(house));
            Step("giardino", () => BuildYard(house));
            Step("pavimenti", () => BuildPatternFloors(house));
            if (!ch2) Step("dettagli esterni casa", () => HouseDressing.DecorateHouse(house, X0 - 0.04f, X1 + 0.04f, Z0 - 0.04f, Z1 + 0.04f, F2,
                Z0 - 0.45f, Z1 + 0.45f, X1 + 0.45f, F2 + 10 * 0.34f,
                new[] { new HouseDressing.WindowSpec(-4.6f, 2f, F0 + 0.85f, F0 + 2.2f), new HouseDressing.WindowSpec(-4.6f, 2f, F1 + 0.85f, F1 + 2.15f),
                        new HouseDressing.WindowSpec(0.6f, 1f, F1 + 0.85f, F1 + 2.15f) },
                3, new Vector2(-5.4f, 8f), true, 3.4f));

            Transform hood = Group("Vicinato", world);
            Step("vicinato", () => BuildNeighbors(hood));
            Step("cielo e nebbia", () =>
            {
                Light moon = null;
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) moon = l;
                HouseDressing.BuildSky(world, moon);
                var fog = new GameObject("Nebbia (fuori più fitta)").AddComponent<FogZone>();
                fog.transform.SetParent(world, false);
                fog.outsideDensity = 0.058f;
                fog.insideDensity = 0.025f;
                fog.indoor = new Bounds(new Vector3((X0 + X1) * 0.5f, 2f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0, 9f, Z1 - Z0));
            });
            if (armchair == null) armchair = HorrorTestSceneBuilder.Marker("Poltrona (mancante)", world, new Vector3(-4.7f, F0, 5.1f));

            // personaggi e sistemi
            CharacterMotor tom = HorrorTestSceneBuilder.BuildTom(world, cast["tom"], new Vector3(-3.4f, F0, 1.7f), 150f);
            // nella storia Tom non si "interroga": parla quando lo decide la regia (niente domanda sulla birra dopo il blackout)
            var tomTalk = tom.GetComponent<Talkable>();
            if (tomTalk != null) Object.DestroyImmediate(tomTalk);
            HorrorTestSceneBuilder.BuildPlayer(cast["muark"], ch2 ? new Vector3(-1.8f, FB + 0.05f, 5.3f) : new Vector3(0f, 0.08f, -9.8f), 0f);
            if (ch2) Step("niente birra in mano", () =>
            {
                // nel capitolo 2 la birra è finita da un pezzo: Muark ha le mani libere
                var it = Object.FindFirstObjectByType<Interactor>();
                if (it != null && it.startHeld != null) { Object.DestroyImmediate(it.startHeld.gameObject); it.startHeld = null; }
            });
            if (ch2) Step("personaggi (capitolo 2)", () => BuildCast2(world, cast));
            else Step("personaggi", () => BuildCast(world, cast));
            HorrorTestSceneBuilder.BuildNavMesh();

            var power = new GameObject("Contatore della casa").AddComponent<HousePower>();
            power.lights = houseLights.ToArray();
            power.tvs = new[] { tv };
            power.switches = new LightSwitch[0];
            power.glowRenderers = new Renderer[0];
            if (ch2) power.powered = false; // la corrente è saltata dal capitolo 1

            Transform marks = Group("Punti della storia", world);
            Transform M2(string n, Vector3 p, float yaw = 0f) { var t = HorrorTestSceneBuilder.Marker(n, marks, p); t.rotation = Quaternion.Euler(0f, yaw, 0f); return t; }
            StoryDirector story = null;
            if (!ch2)
            {
            story = new GameObject("Regia della storia").AddComponent<StoryDirector>();
            story.frontDoor = frontDoor;
            story.backDoor = backDoor;
            story.bathDoor = bathDoor;
            story.cellarDoor = cellarDoor;
            story.sideDoor = sideDoor;
            story.tom = tom;
            story.roy = royMotor;
            story.jef = jefMotor;
            story.neighbors = neighborActors.ToArray();
            story.tomAtDoor = M2("Tom alla porta", new Vector3(0f, F0, 1.4f));
            story.tomBesideDoor = M2("Tom accanto alla porta", new Vector3(-1.3f, F0, 1.0f));
            story.tomCloseDoor = M2("Tom chiude la porta", new Vector3(0.3f, F0, 1.05f));
            // posti a sedere del salotto: la poltrona di Tom e i tre posti del divano (rivolto verso la TV, -X)
            Vector3 chairDir = armchair.rotation * Vector3.forward;
            // in poltrona un po' avanti sul cuscino: le ginocchia escono dal bordo e gli stinchi scendono davanti
            story.seatTom = MakeSeat(marks, "Posto: poltrona", armchair.position + chairDir * 0.14f, armchair.rotation.eulerAngles.y,
                                     armchair.position + chairDir * 0.85f, "Siediti in poltrona");
            story.seatRoy = MakeSeat(marks, "Posto: divano destra", new Vector3(-2.9f, F0, 3.6f), -90f, new Vector3(-3.65f, F0, 3.6f), "Siediti sul divano");
            MakeSeat(marks, "Posto: divano centro", new Vector3(-2.9f, F0, 3.0f), -90f, new Vector3(-3.65f, F0, 3.0f), "Siediti sul divano");
            story.seatJef = MakeSeat(marks, "Posto: divano sinistra", new Vector3(-2.9f, F0, 2.4f), -90f, new Vector3(-3.65f, F0, 2.4f), "Siediti sul divano");
            story.standSpots = new[] { M2("In piedi: vicino alla poltrona", new Vector3(-3.6f, F0, 5.3f), -120f),
                                       M2("In piedi: vicino alla libreria", new Vector3(-3.9f, F0, 1.2f), -60f),
                                       M2("In piedi: vicino alla TV", new Vector3(-5.6f, F0, 1.4f), -10f) };
            story.upstairsFloorY = F1;
            story.basementBounds = new Bounds(new Vector3(0f, FB + 1.2f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0 - 0.6f, 2.6f, Z1 - Z0 - 0.6f));
            story.royDoorstep = M2("Roy sulla soglia", new Vector3(0.05f, F0, -0.6f), 0f);
            // in fondo al giardino, vicino alla staccionata, da dove si vede meglio l'incendio
            story.deckTom = M2("Giardino: Tom", new Vector3(-1.0f, 0f, 18.6f));
            story.deckRoy = M2("Giardino: Roy", new Vector3(0.7f, 0f, 18.2f));
            story.deckJef = M2("Giardino: Jef", new Vector3(-2.7f, 0f, 17.8f));
            story.livingRoom = M2("Soggiorno", new Vector3(-4.3f, F0, 3.2f), -90f);
            story.playerKitchen = M2("Prova: cucina", new Vector3(1.4f, F0, 7.2f), 90f);
            story.playerCellarTop = M2("Prova: in cima alla cantina", new Vector3(3.6f, F0, 6.9f), -90f);
            story.basementFloorY = FB;
            story.houseBounds = new Bounds(new Vector3((X0 + X1) * 0.5f, 3f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0, 12f, Z1 - Z0));
            story.island = island;
            story.tv = tv;
            story.house = power;
            story.generator = generator;
            story.cellarRadio = cellarRadio;
            story.torchPickup = torchPickup;
            story.highlightMaterial = HighlightMaterial();
            story.cellarBulbs = cellarBulbs.ToArray();
            }

            // l'esplosione: un edificio a qualche isolato, dietro i giardini, e altre case sullo sfondo
            SmokeColumn smokeCol = null;
            Step("sfondo e incendio", () => smokeCol = BuildBackground(world));
            if (story != null) story.smoke = smokeCol;

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

            if (!ch2)
            {
                var amb = new GameObject("Ambiente notturno (audio)").AddComponent<AmbientSound>();
                amb.clip = AmbientClip.Night;
                amb.volume = 0.3f;
                story.nightAmbience = amb;
            }
            Step("post-processing e atmosfera", () =>
            {
                Transform fx = Group("Grafica", world);
                Bounds inside = new Bounds(new Vector3((X0 + X1) * 0.5f, (F0 + F2) * 0.5f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0, F2 - F0 + 0.2f, Z1 - Z0));
                Bounds cellar = new Bounds(new Vector3((X0 + X1) * 0.5f, (FB + F0 - S) * 0.5f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0 - 0.6f, F0 - S - FB, Z1 - Z0 - 0.6f));
                HorrorVisuals.BuildVolumes(fx, inside, cellar);
                // polvere sospesa: si vede nel fascio della torcia e vicino alle lampade
                HorrorVisuals.Dust(fx, "soggiorno", new Bounds(new Vector3(-4.4f, F0 + 1.4f, 3f), new Vector3(4.6f, 2.6f, 5.6f)), 260);
                HorrorVisuals.Dust(fx, "cucina", new Bounds(new Vector3(2.5f, F0 + 1.4f, 8.5f), new Vector3(8f, 2.6f, 4.6f)), 200);
                HorrorVisuals.Dust(fx, "cantina", new Bounds(cellar.center, cellar.size - new Vector3(0.4f, 0.3f, 0.4f)), 700);
                if (!ch2) HorrorVisuals.Dust(fx, "corridoio sopra", new Bounds(new Vector3(0.5f, F1 + 1.4f, 4f), new Vector3(4.5f, 2.6f, 6f)), 160);
                else HorrorVisuals.Dust(fx, "macerie", new Bounds(new Vector3(0f, F0 + 1.6f, 5.5f), new Vector3(13f, 3f, 10f)), 900);
            });
            NeighborhoodPower hoodPower = null;
            Step("corrente del quartiere", () => hoodPower = BuildNeighborhoodPower(world, tv));
            if (story != null) story.neighborhood = hoodPower;
            if (ch2) Step("regia del capitolo 2", () => BuildChapter2Director(world, marks, frontDoor, tv, tom, smokeCol, hoodPower));

            // controllo finale: oggetti senza mesh (se ce ne sono, qualcosa è andato storto nella creazione dei prefab)
            int missing = 0;
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf.sharedMesh == null) { missing++; if (missing <= 10) Debug.LogWarning("[FantaLandia Horror] Senza mesh: " + mf.name, mf); }
            if (missing > 0) Debug.LogWarning($"[FantaLandia Horror] {missing} oggetti senza mesh");

            Step("menu, intro e impostazioni", () =>
            {
                var fpcPlayer = Object.FindFirstObjectByType<FantaLandia.Horror.Player.FirstPersonController>();
                if (fpcPlayer != null)
                {
                    var menu = fpcPlayer.gameObject.AddComponent<FantaLandia.Horror.UI.NotebookMenu>();
                    menu.notebookPrefab = VoxelAssetLibrary.Load("Taccuino");
                    menu.handFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Horror/UI/Fonts/Kalam-Regular.ttf");
                    menu.openOnStart = !ch2; // il capitolo 2 parte subito, senza menu
                }
                if (!ch2) // il logo solo all'avvio del gioco (capitolo 1)
                {
                    var intro = new GameObject("Intro (logo 641)").AddComponent<FantaLandia.Horror.UI.IntroSequence>();
                    intro.logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Horror/UI/Logo641.png");
                }
                new GameObject("Impostazioni").AddComponent<FantaLandia.Horror.UI.SettingsApplier>();
            });
            if (!ch2) Step("suoni della casa", () =>
            {
                var amb2 = new GameObject("Suoni della casa").AddComponent<HouseAmbience>();
                amb2.fridge = HorrorTestSceneBuilder.Marker("Ronzio del frigo", world, new Vector3(6.3f, F0 + 1f, 10.3f));
                amb2.clock = HorrorTestSceneBuilder.Marker("Tic-tac dell'orologio", world, new Vector3(5.0f, F0 + 2.05f, 10.7f));
                amb2.power = power;
                amb2.house = new Bounds(new Vector3((X0 + X1) * 0.5f, (F0 + F2) * 0.5f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0, F2 - F0, Z1 - Z0));
                // eco delle stanze
                Reverb(world, "Eco soggiorno", new Vector3(-4.3f, F0 + 1.4f, 3f), 3f, 5f, AudioReverbPreset.Livingroom);
                Reverb(world, "Eco bagno", new Vector3(5.2f, F0 + 1.3f, 1.5f), 1.2f, 2.2f, AudioReverbPreset.Bathroom);
                Reverb(world, "Eco cantina", new Vector3(0f, FB + 0.6f, (Z0 + Z1) * 0.5f), 3f, 4.5f, AudioReverbPreset.StoneCorridor);
            });

            EditorSceneManager.SaveScene(scene, ScenePath);
            // la scena nelle Build Settings: serve per "Ricomincia" dal taccuino e per la build del gioco
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(sc => sc.path == ScenePath);
            // il capitolo 1 è la prima scena (quella che parte), il capitolo 2 subito dopo
            if (ch2) scenes.Insert(scenes.Count > 0 && scenes[0].path == ScenePath1 ? 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            else scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[FantaLandia Horror] Casa e quartiere creati: " + ScenePath);
        }

        /// <summary>
        /// Lo sfondo dietro i giardini: file di case e alberi lontani (nella nebbia sono sagome scure contro il fuoco)
        /// e il vecchio edificio della centrale che esplode e brucia (tetto squarciato, finestre, ciminiera).
        /// </summary>
        static SmokeColumn BuildBackground(Transform world)
        {
            Transform bg = Group("Sfondo", world);
            Material dark = M("Sagoma lontana", "#2a2c30");
            Material darkRoof = M("Tetto lontano", "#1d1e22");
            Material brick = MT("Mattoni centrale", "#4a2c22", ProceduralTextures.Kind.Brick, 1.4f, 0.05f, 0.3f);
            Material burnt = M("Bruciato", "#120f0d");
            Material concreteDark = M("Cemento scuro", "#3a3a3a");

            // case lontane (lasciano libera la vista sull'incendio)
            foreach (var hpos in new[] { new Vector3(-40f, 0f, 36f), new Vector3(-27f, 0f, 38f), new Vector3(-14f, 0f, 37f), new Vector3(28f, 0f, 36f),
                                         new Vector3(41f, 0f, 38f), new Vector3(-34f, 0f, 56f), new Vector3(-16f, 0f, 60f), new Vector3(30f, 0f, 58f) })
            {
                Transform h = Group("Casa lontana", bg);
                h.localPosition = hpos;
                Box("Corpo", h, new Vector3(-5f, 0f, -4f), new Vector3(5f, 5.5f, 4f), dark);
                SteppedRoof(h, "Tetto", -5.4f, 5.4f, -4.4f, 4.4f, 5.5f, 7, 0.35f, true, darkRoof, dark);
            }
            foreach (var t in new[] { new Vector3(-20f, 0f, 30f), new Vector3(-6f, 0f, 31f), new Vector3(22f, 0f, 31f), new Vector3(34f, 0f, 46f), new Vector3(-28f, 0f, 47f), new Vector3(24f, 0f, 44f) })
                Place(VoxelFurniture.Get("Albero"), bg, t, t.x * 7f);

            // la vecchia centrale: 16 x 10 m, alta 8, tetto squarciato, finestre buie, ciminiera
            Transform c = Group("Vecchia centrale (incendio)", bg);
            c.localPosition = new Vector3(10f, 0f, 46f);
            Box("Muro fronte", c, new Vector3(-8f, 0f, -5f), new Vector3(8f, 8f, -4.6f), brick);
            Box("Muro retro", c, new Vector3(-8f, 0f, 4.6f), new Vector3(8f, 8f, 5f), brick);
            Box("Muro sx", c, new Vector3(-8f, 0f, -5f), new Vector3(-7.6f, 8f, 5f), brick);
            Box("Muro dx", c, new Vector3(7.6f, 0f, -5f), new Vector3(8f, 7f, 5f), brick);
            Box("Tetto rimasto", c, new Vector3(-8f, 7.7f, -5f), new Vector3(-2f, 8.1f, 5f), concreteDark);
            Box("Tetto crollato", c, new Vector3(3f, 6.2f, -5f), new Vector3(8f, 6.6f, 1f), burnt);
            Box("Interno bruciato", c, new Vector3(-7.6f, 0f, -4.6f), new Vector3(7.6f, 0.6f, 4.6f), burnt);
            Box("Ciminiera", c, new Vector3(5f, 0f, 2.5f), new Vector3(6.4f, 17f, 3.9f), brick);
            Box("Bordo ciminiera", c, new Vector3(4.85f, 16.6f, 2.35f), new Vector3(6.55f, 17.2f, 4.05f), concreteDark);
            var windowFires = new List<Vector3>();
            int wi = 0;
            for (float x = -6f; x <= 6f; x += 3f)
                foreach (float y in new[] { 1.6f, 4.8f })
                {
                    Box("Finestra", c, new Vector3(x - 0.7f, y, -5.05f), new Vector3(x + 0.7f, y + 1.6f, -4.9f), burnt);
                    if ((wi++ % 3) != 1) windowFires.Add(new Vector3(x, y + 0.4f, -5.3f));
                }

            var smokeCol = c.gameObject.AddComponent<SmokeColumn>();
            smokeCol.smokeMaterial = SmokeMat("Fumo", false, 0.7f, 0.6f);
            smokeCol.glowMaterial = SmokeMat("Bagliore incendio", true, 0.9f, 0.45f);
            smokeCol.roofHeight = 7.6f;
            smokeCol.roofFireSize = new Vector2(12f, 8f);
            smokeCol.windowFires = windowFires.ToArray();
            return smokeCol;
        }

        /// <summary>Un posto a sedere: 'seatPos' è il punto a terra sotto il sedile, 'yaw' la direzione da seduti.</summary>
        static Seat MakeSeat(Transform parent, string name, Vector3 seatPos, float yaw, Vector3 frontPos, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = seatPos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var front = new GameObject("Davanti").transform;
            front.SetParent(go.transform, false);
            front.position = frontPos;
            front.rotation = go.transform.rotation;
            // zona da guardare per sedersi: sopra il cuscino, un po' sporgente in avanti (vince sul collider del mobile)
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.75f, 0.15f);
            col.size = new Vector3(0.55f, 0.5f, 0.8f);
            go.layer = HorrorTestSceneBuilder.DoorLayer; // fuori dalla NavMesh, come le porte
            var seat = go.AddComponent<Seat>();
            seat.front = front;
            seat.height = 0.5f;
            seat.label = label;
            seat.group = "salotto";
            return seat;
        }

        /// <summary>Materiale del contorno luminoso che pulsa (per evidenziare gli oggetti da prendere).</summary>
        static Material HighlightMaterial()
        {
            string path = $"{EditorAssetUtil.MaterialFolder}/Evidenziatura.mat";
            EditorAssetUtil.EnsureFolder(EditorAssetUtil.MaterialFolder);
            Shader sh = Shader.Find("FantaLandia/Highlight");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.SetFloat("_Intensity", 0.7f); // solo il contorno, senza illuminare la stanza
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SmokeMat(string name, bool additive, float softness, float breakup)
        {
            string path = $"{EditorAssetUtil.MaterialFolder}/{name}.mat";
            EditorAssetUtil.EnsureFolder(EditorAssetUtil.MaterialFolder);
            Shader sh = Shader.Find("FantaLandia/Smoke");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (m.HasProperty("_SrcBlend"))
            {
                m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (m.HasProperty("_Softness")) m.SetFloat("_Softness", softness);
            if (m.HasProperty("_Breakup")) m.SetFloat("_Breakup", breakup);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// La corrente del quartiere: tutte le luci (lampioni, verande, lampade di casa) tranne luna, TV, lampadine
        /// della cantina; e tutti i renderer con un materiale luminoso (finestre accese, lampade), tranne la TV.
        /// </summary>
        static NeighborhoodPower BuildNeighborhoodPower(Transform world, CrtTv tv)
        {
            var np = new GameObject("Corrente del quartiere").AddComponent<NeighborhoodPower>();
            var lights = new List<Light>();
            foreach (var l in world.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional || cellarBulbs.Contains(l)) continue;
                if (generator != null && l.transform.IsChildOf(generator.transform)) continue; // la spia ha la sua batteria
                if (tv != null && l.transform.IsChildOf(tv.transform)) continue;
                lights.Add(l);
            }
            var glow = new List<Renderer>();
            foreach (var r in world.GetComponentsInChildren<Renderer>(true))
            {
                if (tv != null && r.transform.IsChildOf(tv.transform)) continue;
                if (generator != null && r.transform.IsChildOf(generator.transform)) continue;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.IsKeywordEnabled("_EMISSION")) { glow.Add(r); break; }
            }
            np.lights = lights.ToArray();
            np.glowRenderers = glow.ToArray();
            np.darkMaterial = M("Finestra buia", "#141a22", 0.8f);
            np.glowObjects = HorrorVisuals.Cones.ToArray();
            return np;
        }

        static void Reverb(Transform parent, string name, Vector3 pos, float minD, float maxD, AudioReverbPreset preset)
        {
            var z = new GameObject(name).AddComponent<AudioReverbZone>();
            z.transform.SetParent(parent, false);
            z.transform.position = pos;
            z.minDistance = minD;
            z.maxDistance = maxD;
            z.reverbPreset = preset;
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
            jefMotor = HorrorTestSceneBuilder.BuildNpc(people, cast["jef"], "Jef", "jef", new Vector3(5.6f, F0, 1.9f), 200f);
            // Roy è nascosto del tutto: la storia lo attiverà quando bussa alla porta
            royMotor = HorrorTestSceneBuilder.BuildNpc(people, cast["roy"], "Roy", "roy", new Vector3(-8.3f, 0f, 1.2f), 90f);
            royMotor.gameObject.SetActive(false);

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
            actor.onlyWhenPlayerOutside = true;
            neighborActors.Add(actor);
        }

        // ------------------------------------------------------------------ materiali

        static void Materials()
        {
            const ProceduralTextures.Kind T0 = ProceduralTextures.Kind.Clapboard;
            siding = MT("Rivestimento", "#9fb2bd", T0, 1f, 0.15f);
            trim = MT("Finiture bianche", "#eeece4", ProceduralTextures.Kind.Plaster, 1f, 0.25f, 0.06f);
            interior = MT("Pareti piano terra", "#cdbf9f", ProceduralTextures.Kind.Plaster, 1.2f, 0.08f, 0.1f);
            interiorUp = MT("Pareti primo piano", "#bfc3ad", ProceduralTextures.Kind.Wallpaper, 0.9f, 0.08f, 0.08f);
            wood = MT("Parquet", "#6b4a30", ProceduralTextures.Kind.Boards, 0.9f, 0.3f);
            tiles = M("Piastrelle", "#c9c2b0", 0.4f);
            kitchenTiles = M("Piastrelle cucina", "#d9cf9e", 0.4f);
            bathTiles = M("Piastrelle bagno", "#a9c2c8", 0.5f);
            concrete = MT("Cemento cantina", "#6e6c68", ProceduralTextures.Kind.Concrete, 2f, 0.1f, 0.2f);
            basementWall = MT("Muro cantina", "#7d7a73", ProceduralTextures.Kind.CinderBlock, 0.8f, 0.05f, 0.22f);
            roof = MT("Tegole", "#3b3c42", ProceduralTextures.Kind.Shingles, 1f, 0.12f, 0.15f);
            glass = EditorAssetUtil.Glass("Casa Vetro", new Color(0.55f, 0.7f, 0.8f));
            grass = MT("Prato", "#2e4322", ProceduralTextures.Kind.Grass, 2.5f, 0.05f, 0.25f);
            asphalt = MT("Asfalto", "#232327", ProceduralTextures.Kind.Asphalt, 2f, 0.2f, 0.15f);
            sidewalk = MT("Marciapiede", "#6c6a66", ProceduralTextures.Kind.Slabs, 2.4f, 0.12f, 0.15f);
            porchWood = MT("Veranda", "#7a5a3c", ProceduralTextures.Kind.Boards, 0.9f, 0.18f);
            fenceWood = MT("Staccionata", "#6b5034", ProceduralTextures.Kind.VerticalBoards, 1f, 0.08f, 0.18f);
            deckWood = MT("Pedana", "#806040", ProceduralTextures.Kind.Boards, 0.9f, 0.15f);
            garageFloor = MT("Pavimento garage", "#5a5854", ProceduralTextures.Kind.Concrete, 2f, 0.15f, 0.25f);
            railing = MT("Ringhiera", "#efede6", ProceduralTextures.Kind.WoodGrain, 1f, 0.2f, 0.05f);
            stairSide = MT("Fianco scala", "#5a3c26", ProceduralTextures.Kind.WoodGrain, 1f, 0.2f);
            stairTread = MT("Pedata scala", "#8a6040", ProceduralTextures.Kind.WoodGrain, 1f, 0.35f);
            if (ch2) DustyMaterials();
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
            l.intensity = 4.5f;
            l.range = 17f;
            l.color = new Color(1f, 0.8f, 0.5f);
            l.shadows = LightShadows.None;
            // un po' di luce anche sul palo e attorno alla lampada
            var glow = new GameObject("Alone lampione").AddComponent<Light>();
            glow.transform.SetParent(lamp.transform, false);
            glow.transform.localPosition = new Vector3(0f, 3.85f, 0.6f);
            glow.type = LightType.Point;
            glow.range = 4f;
            glow.intensity = 1.2f;
            glow.color = new Color(1f, 0.8f, 0.5f);
            // cono di luce nella nebbia sotto la lampada
            HorrorVisuals.LightCone(lamp.transform, new Vector3(0f, 3.95f, 0.6f), 3.9f, 2.6f, new Color(1f, 0.78f, 0.48f), 0.16f);
        }

        // ------------------------------------------------------------------ casa di Tom: struttura

        static HorrorDoor BuildHouseStructure(Transform house)
        {
            Transform s = Group("Struttura", house);

            // solai
            Slab(s, "Pavimento piano terra", X0, X1, Z0, Z1, F0 - S, F0, wood, new Rect(3.72f, 3.01f, 1.4f, 2.64f));
            foreach (Transform t in s) if (t.name.StartsWith("Pavimento piano terra")) t.gameObject.AddComponent<FootstepSurface>().surface = Surface.Wood;
            Hidden(Floor("Piastrelle cucina", s, new Vector3(-1f, F0, 6.075f), new Vector3(6.75f, F0 + 0.01f, 10.75f), kitchenTiles, Surface.Concrete));
            Hidden(Floor("Piastrelle bagno", s, new Vector3(3.575f, F0, 0.25f), new Vector3(6.75f, F0 + 0.01f, 2.925f), bathTiles, Surface.Concrete));
            if (!ch2) // nel capitolo 2 il primo piano non c'è più
            {
                Slab(s, "Pavimento primo piano", X0, X1, Z0, Z1, F1 - S, F1, wood, new Rect(2.1f, 1.5f, 1.325f, 4.2f));
                // al primo piano c'è la moquette: passi ovattati
                foreach (Transform t in s) if (t.name.StartsWith("Pavimento primo piano")) t.gameObject.AddComponent<FootstepSurface>().surface = Surface.Carpet;
                Hidden(Floor("Piastrelle bagno sopra", s, new Vector3(3.575f, F1, 0.25f), new Vector3(6.75f, F1 + 0.01f, 3.925f), bathTiles, Surface.Concrete));
                Box("Soffitto primo piano", s, new Vector3(X0, F2 - S, Z0), new Vector3(X1, F2, Z1), trim);
            }

            // cantina
            Floor("Pavimento cantina", s, new Vector3(X0, FB - 0.3f, Z0), new Vector3(X1, FB, Z1), concrete, Surface.Concrete);
            float bh = F0 - S - FB;
            Box("Muro cantina fronte", s, new Vector3(X0, FB, Z0), new Vector3(X1, FB + bh, Z0 + 0.3f), basementWall);
            Box("Muro cantina retro", s, new Vector3(X0, FB, Z1 - 0.3f), new Vector3(X1, FB + bh, Z1), basementWall);
            Box("Muro cantina sx", s, new Vector3(X0, FB, Z0), new Vector3(X0 + 0.3f, FB + bh, Z1), basementWall);
            Box("Muro cantina dx", s, new Vector3(X1 - 0.3f, FB, Z0), new Vector3(X1, FB + bh, Z1), basementWall);
            // scala della cantina: larga 1.4 m, parte subito oltre la porta della cucina e scende dritta verso -Z
            // centrata sulla porta; 10 gradini (l'undicesimo sarebbe alto zero, a filo del pavimento: sfarfallava)
            Stairs(s, "Scala cantina", false, 5.65f, -1, 3.72f, 5.12f, F0, 10, -2.8f / 11f, 0.24f, FB, porchWood, Surface.Wood);

            // scala per il primo piano (nell'ingresso, sale verso +Z)
            // contro il muro destro dell'ingresso, lasciando libero lo spazio davanti alla porta
            if (!ch2)
            {
                Stairs(s, "Scala", false, 1.5f, 1, 2.1f, 3.425f, F0, 15, 0.2f, 0.28f, F0, stairSide, Surface.Wood);
                StairDetails(s);
            }

            // muri esterni: nucleo (colore interno) + rivestimento esterno (azzurro) con le stesse aperture
            var front0 = new[] { new Opening(-0.47f, 0.47f, F0, F0 + 2.15f, OpeningKind.Door), Window(-4.6f, 2f, F0 + 0.85f, F0 + 2.2f), Window(5.2f, 0.8f, F0 + 1.5f, F0 + 2.1f) };
            var front1 = new[] { Window(-4.6f, 2f), Window(0.6f, 1f), Window(5.2f, 0.8f, 1.4f, 2.0f) };
            var back0 = new[] { Window(-4.5f, 1.8f, F0 + 0.85f, F0 + 2.2f), new Opening(-1.97f, -1.03f, F0, F0 + 2.15f, OpeningKind.Door), Window(2.5f, 1.6f, F0 + 1.15f, F0 + 2.15f) };
            var back1 = new[] { Window(-4.5f, 1.8f), Window(2.5f, 1.8f) };
            var left0 = new[] { Window(3f, 1.8f, F0 + 0.85f, F0 + 2.2f), Window(8.5f, 1.8f, F0 + 0.85f, F0 + 2.2f) };
            var left1 = new[] { Window(3f, 1.6f), Window(8.5f, 1.6f) };
            var right0 = new[] { new Opening(4.95f, 5.85f, F0, F0 + 2.15f, OpeningKind.Door), new Opening(8f, 8.9f, F0, F0 + 2.15f, OpeningKind.Door), Window(9.9f, 1f, F0 + 1.15f, F0 + 2.15f) };
            var right1 = new[] { Window(9f, 1.6f) };

            ExteriorWall(s, "Fronte terra", true, Z0 + ET * 0.5f, -1f, X0, X1, 0f, F1, front0);
            if (!ch2) ExteriorWall(s, "Fronte sopra", true, Z0 + ET * 0.5f, -1f, X0, X1, F1, H, front1);
            ExteriorWall(s, "Retro terra", true, Z1 - ET * 0.5f, 1f, X0, X1, 0f, F1, back0);
            if (!ch2) ExteriorWall(s, "Retro sopra", true, Z1 - ET * 0.5f, 1f, X0, X1, F1, H, back1);
            ExteriorWall(s, "Lato sx terra", false, X0 + ET * 0.5f, -1f, Z0 + ET, Z1 - ET, 0f, F1, left0);
            if (!ch2) ExteriorWall(s, "Lato sx sopra", false, X0 + ET * 0.5f, -1f, Z0 + ET, Z1 - ET, F1, H, left1);
            ExteriorWall(s, "Lato dx terra", false, X1 - ET * 0.5f, 1f, Z0 + ET, Z1 - ET, 0f, F1, right0);
            if (!ch2) ExteriorWall(s, "Lato dx sopra", false, X1 - ET * 0.5f, 1f, Z0 + ET, Z1 - ET, F1, H, right1);

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
            if (!ch2)
            {
            Wall(s, "Muro camere", false, -2f, Z0 + ET, Z1 - ET, F1, uh, IT, interiorUp,
                 new[] { HouseKit.Door(4.6f, 0.84f), HouseKit.Door(6.5f, 0.84f) }, null, trim);
            Wall(s, "Muro matrimoniale-cameretta", true, 6f, X0 + ET, -2f, F1, uh, IT, interiorUp, null);
            Wall(s, "Muro camera Tom", true, 7f, -2f, X1 - ET, F1, uh, IT, interiorUp, new[] { HouseKit.Door(0.7f, 0.84f) }, null, trim);
            Wall(s, "Muro bagno sopra", false, 3.5f, Z0 + ET, 7f, F1, uh, IT, interiorUp,
                 new[] { HouseKit.Door(0.85f, 0.84f), HouseKit.Door(6.5f, 0.84f) }, null, trim);
            Wall(s, "Muro bagno-ripostiglio", true, 4f, 3.5f, X1 - ET, F1, uh, IT, interiorUp, null);

            // tetto a gradoni (colmo lungo X) con timpani
            SteppedRoof(s, "Tetto", X0 - 0.45f, X1 + 0.45f, Z0 - 0.45f, Z1 + 0.45f, F2, 10, 0.34f, true, roof, siding);
            }

            // porte
            HorrorDoor front = DoorAt(s, "Porta d'ingresso", VoxelAssetLibrary.Load("Porta"), new Vector3(-0.45f, F0, Z0 + ET * 0.5f), 0f);
            front.knockLocal = new Vector3(0.45f, 1.62f, -0.055f);
            front.locked = true;
            front.knockable = true;
            sideDoor = DoorAt(s, "Porta laterale", VoxelAssetLibrary.Load("Porta"), new Vector3(X1 - ET * 0.5f, F0, 8.0f), -90f);
            // porta sul retro: dalla sala da pranzo alla pedana del giardino
            backDoor = DoorAt(s, "Porta sul retro", VoxelAssetLibrary.Load("Porta"), new Vector3(-1.05f, F0, Z1 - ET * 0.5f), 180f);
            GameObject inner = VoxelFurniture.Get("Porta interna");
            HorrorDoor bath = bathDoor = DoorAt(s, "Porta bagno (Jef)", inner, new Vector3(3.5f, F0, 0.82f + 0.4f), 90f);
            bath.locked = !ch2;
            bath.lockedText = "È occupato...";
            cellarDoor = DoorAt(s, "Porta cantina", inner, new Vector3(4.42f + 0.4f, F0, 6f), 180f); // dalla cucina verso la scala della cantina
            garageDoor = DoorAt(s, "Porta garage", inner, new Vector3(X1 - ET * 0.5f, F0, 5.85f), 90f);
            if (ch2) { BuildRuins(s); return front; } // niente porte di sopra, niente plafoniere: il soffitto non c'è più
            DoorAt(s, "Porta camera matrimoniale", inner, new Vector3(-2f, F1, 4.6f - 0.4f), -90f);
            DoorAt(s, "Porta cameretta", inner, new Vector3(-2f, F1, 6.5f - 0.4f), -90f);
            DoorAt(s, "Porta camera Tom", inner, new Vector3(0.7f - 0.4f, F1, 7f), 0f);
            DoorAt(s, "Porta bagno sopra", inner, new Vector3(3.5f, F1, 0.85f + 0.4f), 90f);
            DoorAt(s, "Porta ripostiglio", inner, new Vector3(3.5f, F1, 6.5f + 0.4f), 90f);

            // luci delle stanze (plafoniere)
            Color warm = Warm;
            RoomLight(s, "Luce soggiorno", new Vector3(-4.5f, F1 - S, 3f), 7f, 1.5f, warm, true);
            RoomLight(s, "Luce ingresso", new Vector3(0.8f, F1 - S, 2.5f), 5f, 1.1f, warm, true);
            RoomLight(s, "Luce sala da pranzo", new Vector3(-4f, F1 - S, 8.5f), 6f, 1.2f, warm, true);
            RoomLight(s, "Luce cucina", new Vector3(2.8f, F1 - S, 8.5f), 7f, 1.5f, new Color(1f, 0.95f, 0.85f), true);
            RoomLight(s, "Luce bagno", new Vector3(5.2f, F1 - S, 1.5f), 3.5f, 1f, warm, true);
            RoomLight(s, "Luce disimpegno", new Vector3(5.2f, F1 - S, 5.4f), 3.5f, 0.6f, warm, false);
            RoomLight(s, "Luce corridoio sopra", new Vector3(0.5f, F2 - S, 3.5f), 6f, 0.9f, warm, true);
            RoomLight(s, "Luce camera Tom", new Vector3(2.5f, F2 - S, 9f), 6f, 0.9f, warm, true);
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
        /// <summary>Tiene il collider (per il suono dei passi) ma non disegna la scatola: sopra c'è il pavimento a motivi.</summary>
        static void Hidden(GameObject go) { if (go != null) go.GetComponent<Renderer>().enabled = false; }

        /// <summary>Pavimenti a cubetti: parquet a listoni, cucina a scacchi anni '90, piastrelline nei bagni, moquette sopra, cemento macchiato in cantina.</summary>
        static void BuildPatternFloors(Transform house)
        {
            Transform f = Group("Pavimenti", house);
            float ix0 = X0 + ET, ix1 = X1 - ET, iz0 = Z0 + ET, iz1 = Z1 - ET;
            bool In(float x, float z, float a0, float a1, float b0, float b1) => x > a0 && x < a1 && z > b0 && z < b1;
            const float c = 0.15f;

            // piano terra
            HouseDressing.PatternFloor(f, "piano terra", ix0, ix1, iz0, iz1, F0, c, (i, k, x, z) =>
            {
                if (In(x, z, 3.72f, 5.12f, 3.01f, 5.65f)) return null;                       // buco scala cantina
                if (In(x, z, 3.5f, 7f, 0f, 2.925f))                                           // bagno: piastrelline
                    return ((i + k) % 2 == 0) ? "#a9c2c8" : "#dfe8ea";
                if (In(x, z, -1f, 7f, 6.075f, 11f))                                           // cucina: scacchi grandi
                    return (((i / 2) + (k / 2)) % 2 == 0) ? "#e8e0c8" : "#8a3a32";
                return HouseDressing.ParquetAt(i, k);
            });

            // primo piano
            if (!ch2) HouseDressing.PatternFloor(f, "primo piano", ix0, ix1, iz0, iz1, F1, c, (i, k, x, z) =>
            {
                if (In(x, z, 2.1f, 3.425f, 1.5f, 5.7f)) return null;                         // buco scala
                if (In(x, z, 3.5f, 7f, 0f, 3.925f)) return ((i + k) % 2 == 0) ? "#a9c2c8" : "#dfe8ea";
                if (x < -2f && z < 6f) return HouseDressing.Carpet("#6a5a7a", "#7a6a8a", "#5a4a6a", 1)(i, k);   // matrimoniale
                if (x < -2f) return HouseDressing.Carpet("#4a6a8a", "#5a7a9a", "#3e5a76", 2)(i, k);            // cameretta
                if (z > 7f) return HouseDressing.Carpet("#7a4a3a", "#8a5a48", "#663c2e", 3)(i, k);             // camera di Tom
                return HouseDressing.Carpet("#8a7a5a", "#9a8a6a", "#76684c", 4)(i, k);                         // corridoio
            });

            // cantina
            HouseDressing.PatternFloor(f, "cantina", X0 + 0.3f, X1 - 0.3f, Z0 + 0.3f, Z1 - 0.3f, FB, c,
                                       (i, k, x, z) => HouseDressing.ConcreteAt(i, k));
        }

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
            hinge.localPosition = new Vector3(0f, 0.015f, 0f); // un filo sopra il pavimento: l'anta non entra nel parquet
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
            Place(VoxelFurniture.Get("Tappeto"), f, new Vector3(-4.35f, F0, 3.05f), 94f);     // un po' storto, come sempre
            Place(VoxelAssetLibrary.Load("Mobile TV"), f, new Vector3(-6.45f, F0, 3f), 90f);
            CrtTv tv = BuildTv(f, new Vector3(-6.42f, F0 + 0.5f, 3f), 90f);
            armchair = null;
            if (!ch2)
            {
                Place(VoxelFurniture.Get("Divano"), f, new Vector3(-2.75f, F0, 3f), -90f);
                Place(VoxelFurniture.Get("Tavolino"), f, new Vector3(-4.4f, F0, 2.95f), 97f);
                armchair = Place(VoxelAssetLibrary.Load("Poltrona"), f, new Vector3(-4.7f, F0, 5.1f), -136f).transform;
                Place(VoxelAssetLibrary.Load("Lampada stelo"), f, new Vector3(-6.35f, F0, 5.45f), 0f);
                GameObject shade = Place(VoxelAssetLibrary.Load("Lampada paralume"), f, new Vector3(-6.35f, F0 + 1.44f, 5.45f), 0f);
                houseLights.Add(PointLight(shade.transform, "Luce lampada", new Vector3(0f, 0.1f, 0f), 4.5f, 1.1f, new Color(1f, 0.7f, 0.42f), true));
            }
            else
            {
                // la lampada a stelo per terra, il paralume rotolato via (divano e poltrona sono giù in cantina)
                Tilt(Place(VoxelAssetLibrary.Load("Lampada stelo"), f, new Vector3(-5.6f, F0 + 0.06f, 5.2f), 30f), 0f, 0f, 86f);
                Tilt(Place(VoxelAssetLibrary.Load("Lampada paralume"), f, new Vector3(-4.3f, F0, 5.6f), 0f), 70f, 0f, 20f);
            }
            Place(VoxelFurniture.Get("Libreria"), f, new Vector3(-3f, F0, 0.4f), 0f);
            // vita vissuta: pianta nell'angolo, zaino di qualcuno buttato accanto al divano, quadro sul muro
            Place(VoxelFurniture.Get("Pianta"), f, new Vector3(-6.35f, F0, 0.65f), 20f);
            Place(VoxelFurniture.Get("Zaino"), f, new Vector3(-2.55f, F0, 1.45f), -70f);
            Place(VoxelFurniture.Get("Quadro A"), f, new Vector3(-2.08f, F0 + 1.55f, 5.4f), -90f);
            // ingresso: scarpe lasciate lì, quadro accanto alla porta
            Place(VoxelFurniture.Get("Scarpe"), f, new Vector3(-1.05f, F0, 0.6f), 15f);
            Place(VoxelFurniture.Get("Quadro C"), f, new Vector3(1.7f, F0 + 1.5f, 0.26f), 0f);


            // sala da pranzo
            Place(VoxelFurniture.Get("Tavolo"), f, new Vector3(-4.2f, F0, 8.5f), 0f);
            // sedie non allineate: una tirata fuori e girata, una con la giacca sullo schienale
            if (ch2) Tilt(Place(VoxelFurniture.Get("Sedia"), f, new Vector3(-5.3f, F0 + 0.22f, 7.2f), -36f), 90f, 0f, 0f); // rovesciata
            else Place(VoxelFurniture.Get("Sedia"), f, new Vector3(-5.05f, F0, 7.55f), -6f);
            if (ch2) Tilt(Place(VoxelFurniture.Get("Sedia"), f, new Vector3(-2.9f, F0 + 0.22f, 7.0f), 64f), 0f, 0f, -90f);
            else Place(VoxelFurniture.Get("Sedia"), f, new Vector3(-3.3f, F0, 7.25f), 24f);
            Place(VoxelFurniture.Get("Sedia con giacca"), f, new Vector3(-4.95f, F0, 9.45f), 187f);
            Place(VoxelFurniture.Get("Sedia"), f, new Vector3(-3.45f, F0, 9.5f), 172f);
            Place(VoxelFurniture.Get("Pianta"), f, new Vector3(-6.35f, F0, 10.4f), -40f);
            Place(VoxelFurniture.Get("Quadro B"), f, new Vector3(-2.8f, F0 + 1.55f, 10.74f), 180f);
            Place(VoxelFurniture.Get("Orologio"), f, new Vector3(5.0f, F0 + 2.05f, 10.74f), 180f);

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
            if (!ch2) // nel capitolo 2 la torcia ce l'ha Tom, giù in cantina
            {
                GameObject torch = Place(VoxelAssetLibrary.Load("Torcia"), f, counter.transform.localPosition + new Vector3(0.4f, 0.9f, 0f), 60f);
                torchPickup = torch.AddComponent<FlashlightPickup>();
                // la torcia è piccola: una zona più grande (solo per mirarla con E) così si prende facilmente
                var grab = torch.AddComponent<BoxCollider>();
                grab.isTrigger = true;
                grab.size = new Vector3(0.3f, 0.2f, 0.4f);
            }

            // bagnetto (Jef è chiuso dentro)
            Place(VoxelFurniture.Get("WC"), f, new Vector3(6.3f, F0, 1.2f), -90f);
            Place(VoxelFurniture.Get("Lavandino"), f, new Vector3(5f, F0, 0.25f), 0f);

            // primo piano (nel capitolo 2 non c'è più: qualche mobile è caduto giù tra le macerie)
            if (ch2) FallenFurniture(f);
            else {
            Place(VoxelFurniture.Get("Letto matrimoniale"), f, new Vector3(-4.4f, F1, 0.25f + 1.1f), 0f);
            Place(VoxelFurniture.Get("Comodino"), f, new Vector3(-5.6f, F1, 0.5f), 0f);
            Place(VoxelFurniture.Get("Comodino"), f, new Vector3(-3.2f, F1, 0.5f), 0f);
            Place(VoxelFurniture.Get("Armadio"), f, new Vector3(-6.45f, F1, 5.2f), 90f);
            Place(VoxelFurniture.Get("Letto singolo"), f, new Vector3(-5.75f, F1, 9.4f), 90f);
            Place(VoxelFurniture.Get("Scrivania"), f, new Vector3(-3.2f, F1, 10.4f), 180f);
            Place(VoxelFurniture.Get("Cesto bucato"), f, new Vector3(-6.2f, F1, 2.7f), 12f);
            Place(VoxelFurniture.Get("Scarpe"), f, new Vector3(4.2f, F1, 8.2f), -35f);
            Place(VoxelFurniture.Get("Letto singolo"), f, new Vector3(5.75f, F1, 9.2f), -90f);
            Place(VoxelFurniture.Get("Scrivania"), f, new Vector3(0.2f, F1, 10.4f), 180f);
            Place(VoxelFurniture.Get("Libreria"), f, new Vector3(4.6f, F1, 10.6f), 180f);
            Place(VoxelFurniture.Get("Armadio"), f, new Vector3(-1.6f, F1, 9.4f), 90f);
            // vasca contro il muro in fondo (prima era davanti alla porta e non si entrava)
            Place(VoxelFurniture.Get("Vasca"), f, new Vector3(6.3f, F1, 1.3f), 90f);
            Place(VoxelFurniture.Get("WC"), f, new Vector3(6.35f, F1, 3.25f), -90f);
            Place(VoxelFurniture.Get("Lavandino"), f, new Vector3(4.4f, F1, 3.92f), 180f);
            Place(VoxelFurniture.Get("Scaffale"), f, new Vector3(6.45f, F1, 5.5f), -90f);
            }

            // cantina: sporca e piena di roba, luce spenta (la accenderà il generatore)
            Transform b = Group("Cantina", f);
            GameObject gen = Place(VoxelFurniture.Get("Generatore"), b, new Vector3(-5.3f, FB, 9.9f), 180f);
            if (gen != null)
            {
                generator = gen.AddComponent<GeneratorStarter>();
                // spia rossa sul pannello comandi: si vede al buio e indica dove andare
                Material lampOn = EditorAssetUtil.Emissive("Casa Spia rossa accesa", new Color(1f, 0.08f, 0.05f), 4f);
                Material lampOff = M("Spia rossa spenta", "#3a0a08", 0.6f);
                GameObject lamp = Box("Spia generatore", gen.transform, new Vector3(0.5f, 0.42f, 0.45f), new Vector3(0.6f, 0.52f, 0.49f), lampOn);
                Object.DestroyImmediate(lamp.GetComponent<Collider>());
                generator.indicatorLamp = lamp.GetComponent<Renderer>();
                generator.lampOn = lampOn;
                generator.lampOff = lampOff;
                generator.indicator = PointLight(gen.transform, "Luce spia", new Vector3(0.55f, 0.5f, 0.7f), 2.2f, 0.6f, new Color(1f, 0.1f, 0.05f), true);
            }
            Place(VoxelFurniture.Get("Tanica"), b, new Vector3(-4.3f, FB, 10.3f), 20f);
            Place(VoxelFurniture.Get("Scaffale"), b, new Vector3(-6.45f, FB, 2.8f), 90f);
            Place(VoxelFurniture.Get("Scaffale"), b, new Vector3(-6.45f, FB, 6.2f), 90f);
            Place(VoxelFurniture.Get("Caldaia"), b, new Vector3(5.6f, FB, 10.2f), 0f);
            Place(VoxelFurniture.Get("Lavatrice"), b, new Vector3(3.6f, FB, 10.3f), 180f);
            Place(VoxelFurniture.Get("Banco da lavoro"), b, new Vector3(0.2f, FB, 10.4f), 180f);
            BuildCellarClutter(b);
            if (ch2) BuildBunker(b);
            foreach (Vector3 p in new[] { new Vector3(-3f, F0 - S, 3f), new Vector3(0f, F0 - S, 8f), new Vector3(4.5f, F0 - S, 7f) })
            {
                // nel capitolo 2 la lampadina pende da un filo (oscilla con le scosse) ed è accesa: va il generatore
                Transform hang = b;
                Vector3 at = p;
                if (ch2)
                {
                    hang = new GameObject("Filo lampadina").transform;
                    hang.SetParent(b, false);
                    hang.localPosition = p;
                    at = Vector3.zero;
                    bulbPivots.Add(hang);
                }
                Place(VoxelFurniture.Get("Lampadina"), hang, at, 0f);
                var bulb = PointLight(hang, ch2 ? "Luce cantina" : "Luce cantina (spenta)", at + Vector3.down * 0.5f, 6f, ch2 ? 0.95f : 1.1f, new Color(1f, 0.8f, 0.52f), true);
                bulb.enabled = ch2;
                cellarBulbs.Add(bulb);
            }
            return tv;
        }

        /// <summary>
        /// La cantina sporca e incasinata: vecchi mobili coperti da teli, pile di scatoloni lungo i muri,
        /// travi e tubi al soffitto, ragnatele, muffa e colature d'umido sui muri, pozza d'acqua, assi per terra.
        /// Resta libero un passaggio dalla scala al generatore.
        /// </summary>
        static void BuildCellarClutter(Transform b)
        {
            Material sheet = M("Telo polveroso", "#7f7b70");
            Material mold = M("Muffa", "#2f3328");
            Material damp = M("Macchia umido", "#4c4a44");
            Material beam = M("Trave", "#3a2a1c");
            Material pipe = M("Tubo", "#5a5650", 0.5f);
            Material puddle = M("Pozza", "#0b0d10", 0.97f);
            Material plank = M("Asse vecchia", "#4e3a26");
            Material mattress = M("Materasso macchiato", "#8a8270");
            const float ix0 = X0 + 0.3f, ix1 = X1 - 0.3f, iz0 = Z0 + 0.3f, iz1 = Z1 - 0.3f;
            float ceil = F0 - S;
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();

            // vecchi mobili coperti dai teli
            GameObject sofa = Place(VoxelFurniture.Get("Divano"), b, new Vector3(-1.8f, FB, 1.05f), 0f);
            if (sofa != null) { Box("Telo", sofa.transform, new Vector3(-1.12f, 0f, -0.5f), new Vector3(1.12f, 0.96f, 0.5f), sheet); Box("Telo (piega)", sofa.transform, new Vector3(-0.4f, 0.96f, -0.45f), new Vector3(0.5f, 1.02f, 0.1f), sheet); }
            GameObject arm = Place(VoxelAssetLibrary.Load("Poltrona"), b, new Vector3(-4.3f, FB, 1.1f), 25f);
            if (arm != null) Box("Telo", arm.transform, new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 1.05f, 0.5f), sheet);
            Place(VoxelFurniture.Get("Armadio"), b, new Vector3(1.3f, FB, 0.7f), 0f);
            Place(VoxelFurniture.Get("Libreria"), b, new Vector3(-6.45f, FB, 8.1f), 90f);
            Place(VoxelAssetLibrary.Load("Lampada stelo"), b, new Vector3(-3.1f, FB, 0.65f), 0f);
            Place(VoxelFurniture.Get("Sedia"), b, new Vector3(2.7f, FB, 1.0f), 160f);
            Place(VoxelFurniture.Get("Sedia"), b, new Vector3(3.3f, FB, 0.9f), 200f);
            Place(VoxelFurniture.Get("Comodino"), b, new Vector3(6.2f, FB, 8.5f), -90f);
            GameObject oldTv = Place(VoxelAssetLibrary.Load("TV"), b, new Vector3(0.9f, FB + 0.4f, 2.4f), 200f);
            Place(VoxelFurniture.Get("Scatola grande"), b, new Vector3(0.9f, FB, 2.4f), 15f);
            Box("Materasso", b, new Vector3(6.45f, FB, 6.3f), new Vector3(6.68f, FB + 1.9f, 7.25f), mattress);
            Place(VoxelFurniture.Get("Tanica"), b, new Vector3(-3.9f, FB, 10.35f), -10f);
            // una vecchia radio per terra, girata verso la scala (a volte si accende da sola...)
            if (!ch2)
            {
                GameObject radio = Place(VoxelFurniture.Get("Radio"), b, new Vector3(-0.8f, FB, 8.6f), 200f);
                if (radio != null) cellarRadio = radio.transform;
            }

            // pile di scatoloni lungo i muri (non sul passaggio scala → generatore e non davanti alle cose)
            Vector2 stairFoot = new Vector2(4.4f, 3.0f), genFront = new Vector2(-5.3f, 8.9f);
            var taken = new List<Vector2> { new Vector2(-1.8f, 1.05f), new Vector2(-4.3f, 1.1f), new Vector2(1.3f, 0.7f), new Vector2(-6.45f, 8.1f),
                new Vector2(-3.1f, 0.65f), new Vector2(2.7f, 1.0f), new Vector2(3.3f, 0.9f), new Vector2(6.2f, 8.5f), new Vector2(0.9f, 2.4f),
                new Vector2(6.55f, 6.8f), new Vector2(-6.45f, 2.8f), new Vector2(-6.45f, 6.2f), new Vector2(5.6f, 10.2f), new Vector2(3.6f, 10.3f),
                new Vector2(0.2f, 10.4f), new Vector2(-5.3f, 9.9f), new Vector2(-3.9f, 10.35f), new Vector2(-0.8f, 8.6f) };
            string[] kinds = { "Scatola grande", "Scatola", "Scatola piccola" };
            float[] heights = { 0.4f, 0.3f, 0.2f };
            int placed = 0;
            for (int tries = 0; tries < 400 && placed < 26; tries++)
            {
                var p = new Vector2(Mathf.Lerp(ix0 + 0.35f, ix1 - 0.35f, R()), Mathf.Lerp(iz0 + 0.35f, iz1 - 0.35f, R()));
                float wallDist = Mathf.Min(Mathf.Min(p.x - ix0, ix1 - p.x), Mathf.Min(p.y - iz0, iz1 - p.y));
                if (wallDist > 1.3f && R() > 0.15f) continue;                                  // quasi tutte vicino ai muri
                if (DistToSegment(p, stairFoot, genFront) < 1.3f) continue;                    // il passaggio
                if (p.x > 3.4f && p.x < 5.6f && p.y > 1.8f && p.y < 6.2f) continue;            // la scala
                if (Vector2.Distance(p, genFront) < 1.4f) continue;                            // davanti al generatore
                if (ch2 && p.x > -5.4f && p.x < 2.4f && p.y > 3.2f && p.y < 7.8f) continue;     // il rifugio (capitolo 2)
                bool clash = false;
                foreach (var q in taken) if (Vector2.Distance(p, q) < 0.75f) { clash = true; break; }
                if (clash) continue;
                taken.Add(p);
                placed++;
                // una pila da 1 a 3 scatole, grandi sotto e piccole sopra
                float y = FB;
                int stack = 1 + rng.Next(3);
                for (int k = 0; k < stack; k++)
                {
                    int kind = Mathf.Min(2, k + rng.Next(2));
                    Place(VoxelFurniture.Get(kinds[kind]), b, new Vector3(p.x + (R() - 0.5f) * 0.08f, y, p.y + (R() - 0.5f) * 0.08f), R() * 360f);
                    y += heights[kind];
                }
            }

            // travi e tubi sotto il solaio (interrotti sopra la scala)
            for (float z = iz0 + 0.6f; z < iz1; z += 1.2f)
            {
                bool overStair = z > 2.9f && z < 5.8f;
                if (overStair)
                {
                    Box("Trave", b, new Vector3(ix0, ceil - 0.2f, z - 0.08f), new Vector3(3.6f, ceil, z + 0.08f), beam);
                    Box("Trave", b, new Vector3(5.25f, ceil - 0.2f, z - 0.08f), new Vector3(ix1, ceil, z + 0.08f), beam);
                }
                else Box("Trave", b, new Vector3(ix0, ceil - 0.2f, z - 0.08f), new Vector3(ix1, ceil, z + 0.08f), beam);
            }
            Box("Tubo", b, new Vector3(ix0, ceil - 0.32f, 9.6f), new Vector3(ix1, ceil - 0.24f, 9.68f), pipe);
            Box("Tubo", b, new Vector3(ix0, ceil - 0.36f, 9.85f), new Vector3(ix1, ceil - 0.3f, 9.91f), pipe);
            Box("Tubo caldaia", b, new Vector3(5.55f, FB + 1.4f, 9.85f), new Vector3(5.63f, ceil - 0.3f, 9.93f), pipe);

            // muffa e colature d'umido sui muri (sottili, appena staccate dal muro)
            for (int i = 0; i < 34; i++)
            {
                int wall = rng.Next(4);
                float along = R(), w = 0.3f + R() * 1.2f, h = 0.2f + R() * 0.9f;
                bool streak = R() < 0.45f;
                if (streak) { w = 0.06f + R() * 0.1f; h = 0.6f + R() * 1.4f; }
                float top = streak ? ceil - 0.05f : FB + 0.05f + R() * 1.6f + h;
                Material mat = streak ? damp : (R() < 0.6f ? mold : damp);
                Vector3 min, max;
                if (wall < 2)
                {
                    float x = Mathf.Lerp(ix0 + 0.3f, ix1 - 0.3f, along), z = wall == 0 ? iz0 + 0.004f : iz1 - 0.004f;
                    min = new Vector3(x - w * 0.5f, top - h, z - 0.003f); max = new Vector3(x + w * 0.5f, top, z + 0.003f);
                }
                else
                {
                    float z = Mathf.Lerp(iz0 + 0.3f, iz1 - 0.3f, along), x = wall == 2 ? ix0 + 0.004f : ix1 - 0.004f;
                    min = new Vector3(x - 0.003f, top - h, z - w * 0.5f); max = new Vector3(x + 0.003f, top, z + w * 0.5f);
                }
                Object.DestroyImmediate(Box(streak ? "Colatura" : "Muffa", b, min, max, mat).GetComponent<Collider>());
            }

            // ragnatele negli angoli tra muro e soffitto: fili sottili a raggiera con qualche giro (niente piani trasparenti)
            Material silk = M("Ragnatela", "#5e5e5a");
            var corners = new List<Vector3> { new Vector3(ix0, ceil - 0.2f, iz0), new Vector3(ix1, ceil - 0.2f, iz0), new Vector3(ix0, ceil - 0.2f, iz1), new Vector3(ix1, ceil - 0.2f, iz1) };
            for (int i = 0; i < 6; i++) corners.Add(new Vector3(R() < 0.5f ? ix0 : ix1, ceil - 0.2f, Mathf.Lerp(iz0 + 1f, iz1 - 1f, R())));
            foreach (Vector3 c0 in corners)
            {
                Transform webT = Group("Ragnatela", b);
                // i fili vanno dall'angolo verso il basso e verso l'interno, a ventaglio
                float sx = c0.x < 0f ? 1f : -1f, sz = c0.z < 5f ? 1f : -1f;
                bool onSideWall = c0.z > iz0 + 0.5f && c0.z < iz1 - 0.5f;
                var ends = new List<Vector3>();
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 5f, len = 0.35f + R() * 0.3f;
                    Vector3 dir = onSideWall
                        ? new Vector3(sx * Mathf.Sin(a * Mathf.PI * 0.5f), -Mathf.Cos(a * Mathf.PI * 0.5f), (R() - 0.5f) * 0.6f)
                        : new Vector3(sx * Mathf.Cos(a * Mathf.PI * 0.5f), -0.35f - R() * 0.4f, sz * Mathf.Sin(a * Mathf.PI * 0.5f));
                    Vector3 end = c0 + dir.normalized * len;
                    ends.Add(end);
                    Object.DestroyImmediate(SlopedBox("Filo", webT, c0, end, 0.006f, 0.006f, silk).GetComponent<Collider>());
                }
                for (int ring = 1; ring <= 2; ring++)
                    for (int k = 0; k < ends.Count - 1; k++)
                    {
                        float f = ring * 0.35f;
                        Object.DestroyImmediate(SlopedBox("Filo", webT, Vector3.Lerp(c0, ends[k], f), Vector3.Lerp(c0, ends[k + 1], f), 0.005f, 0.005f, silk).GetComponent<Collider>());
                    }
                foreach (var r in webT.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // pozza d'acqua sotto la caldaia e assi vecchie per terra
            // pozza d'acqua sotto la caldaia: forma irregolare, lucida (riflette le lampadine), con un alone di cemento bagnato
            HorrorVisuals.Puddle(b, new Vector3(4.95f, FB + 0.0065f, 9.25f), 0.62f, 3, puddle, M("Cemento bagnato", "#3a3936", 0.55f));
            HorrorVisuals.Puddle(b, new Vector3(3.95f, FB + 0.0065f, 8.75f), 0.25f, 7, puddle, M("Cemento bagnato", "#3a3936", 0.55f));
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector2(Mathf.Lerp(ix0 + 0.5f, ix1 - 0.5f, R()), Mathf.Lerp(iz0 + 0.5f, iz1 - 0.5f, R()));
                if (DistToSegment(p, stairFoot, genFront) < 0.9f) continue;
                float a = R() * Mathf.PI;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.6f + R() * 0.6f);
                Vector3 c = new Vector3(p.x, FB + 0.02f, p.y);
                SlopedBox("Asse", b, c - d, c + d, 0.16f, 0.03f, plank);
            }
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
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
            tv.screenLight = PointLight(tvGo.transform, "Luce schermo", new Vector3(0f, 0.3f, 0.6f), 5f, 1.4f, Color.white, true); // le ombre di chi guarda la TV sui muri
            // la "voce" della TV nei dialoghi: lo sguardo di chi ascolta va sullo schermo (il punto degli occhi è 1.6 m sopra)
            var voice = new GameObject("Voce TV");
            voice.transform.SetParent(tvGo.transform, false);
            voice.transform.position = quad.transform.position + Vector3.down * 1.6f;
            voice.AddComponent<NpcSpeaker>().speakerId = "tv";
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
            HorrorVisuals.LightCone(p, new Vector3(0.9f, 2.45f, -0.32f), 2.4f, 1.3f, new Color(1f, 0.8f, 0.5f), 0.1f);
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
            // lampada sopra la porta sul retro: illumina la pedana e il giardino (si spegne con il blackout)
            houseLights.Add(PointLight(y, "Luce sul retro", new Vector3(-1.5f, F0 + 2.45f, Z1 + 0.3f), 11f, 1.6f, Warm, true));
            Box("Lampada sul retro", y, new Vector3(-1.58f, F0 + 2.4f, Z1 + 0.02f), new Vector3(-1.42f, F0 + 2.6f, Z1 + 0.09f),
                EditorAssetUtil.Emissive("Casa Lampada accesa", new Color(1f, 0.85f, 0.55f), 2f));
            HorrorVisuals.LightCone(y, new Vector3(-1.5f, F0 + 2.4f, Z1 + 0.1f), 2.6f, 1.4f, new Color(1f, 0.8f, 0.5f), 0.1f);
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
            Material side = MT("Rivestimento " + sidingHex, sidingHex, ProceduralTextures.Kind.Clapboard, 1f, 0.12f, 0.15f);
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
            if (lit[0])
            {
                PointLight(h, "Luce veranda", new Vector3(0.8f, 2.5f, -0.4f), 5f, 1f, new Color(1f, 0.8f, 0.5f), false);
                HorrorVisuals.LightCone(h, new Vector3(0.8f, 2.45f, -0.4f), 2.4f, 1.2f, new Color(1f, 0.8f, 0.5f), 0.08f);
            }
            Floor("Vialetto", h, new Vector3(-0.6f, 0f, -9f), new Vector3(0.6f, 0.03f, -2f), sidewalk, Surface.Concrete);
            Floor("Passo carraio", h, new Vector3(4.5f, 0f, -9f), new Vector3(7.5f, 0.03f, 0f), sidewalk, Surface.Concrete);
            Place(VoxelFurniture.Get("Cassetta posta"), h, new Vector3(-1.4f, 0f, -8.6f), 0f);

            // giardino sul retro recintato
            Fence(h, "Staccionata retro", new Vector3(-13f, 0f, 22f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            if (fenceLeft) Fence(h, "Staccionata lato A", new Vector3(-13f, 0f, 1f), new Vector3(-13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);
            if (fenceRight) Fence(h, "Staccionata lato B", new Vector3(13f, 0f, 1f), new Vector3(13f, 0f, 22f), FenceHeight, fenceWood, fenceWood, true);

            HouseDressing.DecorateHouse(h, -w, w, 0f, d, 0.3f + 2 * fh, -0.4f, d + 0.4f, w + 0.4f, 0.3f + 2 * fh + 9 * 0.33f,
                new[] { new HouseDressing.WindowSpec(-4f, 1.8f, 1.15f, 2.45f), new HouseDressing.WindowSpec(4f, 1.8f, 1.15f, 2.45f),
                        new HouseDressing.WindowSpec(-4f, 1.6f, 4.05f, 5.35f), new HouseDressing.WindowSpec(4f, 1.6f, 4.05f, 5.35f) },
                name.Length * 7 + (int)frontCenter.x, new Vector2(name.Length % 2 == 0 ? 4.6f : -4.6f, 7f), lit[0] || lit[2], 2.7f);

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
