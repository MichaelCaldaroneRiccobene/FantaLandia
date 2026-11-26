using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Story;
using FantaLandia.World;
using Unity.AI.Navigation;
using UnityEngine.AI;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using FantaLandia.Horror.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Menu "FantaLandia Horror > Crea scena test prima persona": un pezzo di casa anni '90 di notte
    /// per provare controller, corpo visibile, porte, oggetti, TV, luci, torcia, dialoghi e audio.
    /// </summary>
    public static class HorrorTestSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Horror/Scenes/Test_PrimaPersona.unity";
        const float FloorY = 0.1f, WallH = 3.3f, WallT = 0.15f;

        /// <summary>Layer delle porte e dei personaggi: cliccabili, ma fuori dalla NavMesh.</summary>
        public const int DoorLayer = 6, CharacterLayer = 7;

        static Material grass, concrete, asphalt, woodFloor, wall, ceiling, porchWood, baseboard, railing;

        [MenuItem("FantaLandia Horror/Crea scena test prima persona")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorAssetUtil.EnsureFolder("Assets/_Project/Horror/Scenes");

            VoxelAssetLibrary.BuildAll();
            GameObject malePrefab = BlockyCharacterBuilder.BuildMale();
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupNight();

            Transform world = new GameObject("Mondo").transform;
            BuildOutside(world);
            HorrorDoor door = BuildHouseShell(world);
            CrtTv tv;
            LightSwitch sw;
            BuildInterior(world, out tv, out sw);
            CharacterMotor tom = BuildTom(world, malePrefab);
            BuildPlayer(malePrefab);
            BuildNavMesh();

            var seq = new GameObject("Sequenza arrivo").AddComponent<TestIntroSequence>();
            seq.door = door;
            seq.tom = tom;
            seq.tomAtDoor = Marker("Tom alla porta", world, new Vector3(0f, FloorY, 1.3f));
            seq.tomInLiving = Marker("Tom in soggiorno", world, new Vector3(-1.4f, FloorY, 5.2f));

            var amb = new GameObject("Ambiente notturno (audio)").AddComponent<AmbientSound>();
            amb.clip = AmbientClip.Night;
            amb.volume = 0.3f;

            var dbg = new GameObject("Tasti di prova").AddComponent<HorrorDebugKeys>();
            dbg.frontDoor = door;
            dbg.tv = tv;
            dbg.switches = new[] { sw };

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[FantaLandia Horror] Scena di test creata: " + ScenePath);
        }

        // ------------------------------------------------------------------ notte

        static void SetupNight()
        {
            var moon = new GameObject("Luna").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.45f, 0.55f, 0.85f);
            moon.intensity = 0.18f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.05f, 0.075f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.07f);
        }

        // ------------------------------------------------------------------ esterno

        static void BuildOutside(Transform world)
        {
            Transform o = Group("Esterno", world);
            Surface(Box("Prato", o, new Vector3(-25f, -0.3f, -25f), new Vector3(25f, 0f, 25f), grass), Audio.Surface.Grass);
            Surface(Box("Marciapiede", o, new Vector3(-25f, 0f, -10f), new Vector3(25f, 0.03f, -8.5f), concrete), Audio.Surface.Concrete);
            Surface(Box("Strada", o, new Vector3(-25f, -0.05f, -17f), new Vector3(25f, 0.01f, -10f), asphalt), Audio.Surface.Concrete);
            Surface(Box("Vialetto", o, new Vector3(-0.6f, 0f, -8.5f), new Vector3(0.6f, 0.03f, -2f), concrete), Audio.Surface.Concrete);
            Surface(Box("Veranda", o, new Vector3(-2f, -0.2f, -2f), new Vector3(2f, FloorY, 0f), porchWood), Audio.Surface.Wood);

            GameObject lamp = Instance(VoxelAssetLibrary.Load("Lampione"), o, new Vector3(5f, 0f, -9.5f), 0f);
            var l = new GameObject("Luce lampione").AddComponent<Light>();
            l.transform.SetParent(lamp.transform, false);
            l.transform.localPosition = new Vector3(0f, 4.0f, 0.55f);
            l.type = LightType.Spot;
            l.spotAngle = 110f;
            l.range = 14f;
            l.intensity = 3f;
            l.color = new Color(1f, 0.82f, 0.5f);
            l.shadows = LightShadows.Soft;
            l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ------------------------------------------------------------------ casa

        static HorrorDoor BuildHouseShell(Transform world)
        {
            Transform h = Group("Casa", world);
            Surface(Box("Pavimento", h, new Vector3(-5f, -0.2f, 0f), new Vector3(5f, FloorY, 9f), woodFloor), Audio.Surface.Wood);
            float y0 = FloorY, y1 = FloorY + WallH;
            const float doorHalf = 0.48f, doorH = 2.15f;

            // muro frontale con il vano della porta
            Box("Muro frontale sx", h, new Vector3(-5f, y0, 0f), new Vector3(-doorHalf, y1, WallT), wall);
            Box("Muro frontale dx", h, new Vector3(doorHalf, y0, 0f), new Vector3(5f, y1, WallT), wall);
            Box("Architrave", h, new Vector3(-doorHalf, y0 + doorH, 0f), new Vector3(doorHalf, y1, WallT), wall);
            Box("Muro retro", h, new Vector3(-5f, y0, 9f - WallT), new Vector3(5f, y1, 9f), wall);
            Box("Muro sinistro", h, new Vector3(-5f, y0, 0f), new Vector3(-5f + WallT, y1, 9f), wall);
            Box("Muro destro", h, new Vector3(5f - WallT, y0, 0f), new Vector3(5f, y1, 9f), wall);
            Box("Soffitto", h, new Vector3(-5f, y1, 0f), new Vector3(5f, y1 + 0.1f, 9f), ceiling);
            Box("Battiscopa", h, new Vector3(-5f + WallT, y0, 9f - WallT - 0.02f), new Vector3(5f - WallT, y0 + 0.1f, 9f - WallT), baseboard);

            // porta d'ingresso: il cardine è a sinistra del vano, +Z = dentro casa
            var doorRoot = new GameObject("Porta d'ingresso");
            doorRoot.transform.SetParent(h, false);
            doorRoot.transform.localPosition = new Vector3(-0.45f, FloorY, WallT * 0.5f);
            var hinge = new GameObject("Cardine").transform;
            hinge.SetParent(doorRoot.transform, false);
            Instance(VoxelAssetLibrary.Load("Porta"), hinge, Vector3.zero, 0f);
            var door = doorRoot.AddComponent<HorrorDoor>();
            door.hinge = hinge;
            door.hingeSign = 1f;
            door.locked = true;
            door.knockable = true;
            door.knockLocal = new Vector3(0.45f, 1.4f, -0.03f);
            SetLayer(doorRoot, DoorLayer);
            return door;
        }

        static void BuildInterior(Transform world, out CrtTv tv, out LightSwitch sw)
        {
            Transform r = Group("Arredamento", world);

            // cucina: bancone contro il muro di fondo, fronte verso la stanza
            GameObject counter = Instance(VoxelAssetLibrary.Load("Bancone"), r, new Vector3(-3.8f, FloorY, 8.55f), 180f);
            var spot = counter.AddComponent<PlaceSpot>();
            spot.acceptsItemId = "birra";
            spot.where = "sul bancone";
            var point = new GameObject("Punto birra").transform;
            point.SetParent(counter.transform, false);
            point.localPosition = new Vector3(0.3f, 0.9f, 0f);
            spot.placePoint = point;

            GameObject torch = Instance(VoxelAssetLibrary.Load("Torcia"), r, new Vector3(-4.4f, FloorY + 0.9f, 8.6f), 70f);
            torch.AddComponent<FlashlightPickup>();

            // soggiorno: mobile e TV contro il muro sinistro, poltrona davanti
            Instance(VoxelAssetLibrary.Load("Mobile TV"), r, new Vector3(-4.6f, FloorY, 3.5f), 90f);
            GameObject tvGo = Instance(VoxelAssetLibrary.Load("TV"), r, new Vector3(-4.45f, FloorY + 0.5f, 3.5f), 90f);
            tv = tvGo.AddComponent<CrtTv>();
            VoxelAssetLibrary.ScreenRect(out Vector3 sc, out Vector2 ss);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Schermo";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(tvGo.transform, false);
            quad.transform.localPosition = sc;
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // il quad guarda verso -Z di default
            quad.transform.localScale = new Vector3(ss.x, ss.y, 1f);
            tv.screen = quad.GetComponent<Renderer>();
            var tvLight = new GameObject("Luce schermo").AddComponent<Light>();
            tvLight.transform.SetParent(tvGo.transform, false);
            tvLight.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            tvLight.type = LightType.Point;
            tvLight.range = 4.5f;
            tvLight.intensity = 1.2f;
            tv.screenLight = tvLight;

            Instance(VoxelAssetLibrary.Load("Poltrona"), r, new Vector3(-2.3f, FloorY, 3.5f), -90f);

            // luci: plafoniera e lampada da terra, comandate dall'interruttore vicino alla porta
            var ceilingLight = new GameObject("Plafoniera").AddComponent<Light>();
            ceilingLight.transform.SetParent(r, false);
            ceilingLight.transform.localPosition = new Vector3(-1f, FloorY + WallH - 0.3f, 4.5f);
            ceilingLight.type = LightType.Point;
            ceilingLight.range = 9f;
            ceilingLight.intensity = 1.6f;
            ceilingLight.color = new Color(1f, 0.85f, 0.6f);
            ceilingLight.shadows = LightShadows.Soft;

            Instance(VoxelAssetLibrary.Load("Lampada stelo"), r, new Vector3(-4.5f, FloorY, 5.6f), 0f);
            GameObject shade = Instance(VoxelAssetLibrary.Load("Lampada paralume"), r, new Vector3(-4.5f, FloorY + 1.4f, 5.6f), 0f);
            var lampLight = new GameObject("Luce lampada").AddComponent<Light>();
            lampLight.transform.SetParent(shade.transform, false);
            lampLight.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            lampLight.type = LightType.Point;
            lampLight.range = 4f;
            lampLight.intensity = 1.1f;
            lampLight.color = new Color(1f, 0.75f, 0.45f);

            GameObject plate = Instance(VoxelAssetLibrary.Load("Interruttore"), r, new Vector3(0.85f, FloorY + 1.2f, WallT + 0.005f), 0f);
            sw = plate.AddComponent<LightSwitch>();
            sw.lights = new[] { ceilingLight, lampLight };
            sw.glowRenderers = new Renderer[0];

            // piccola scala verso un soppalco (per provare i piedi sui gradini)
            Transform st = Group("Scala di prova", r);
            for (int i = 0; i < 6; i++)
            {
                float z0 = 4.7f + i * 0.3f;
                Surface(Box($"Gradino {i + 1}", st, new Vector3(3f, FloorY, z0), new Vector3(4.4f, FloorY + (i + 1) * 0.2f, z0 + 0.3f), porchWood), Audio.Surface.Wood);
            }
            Surface(Box("Soppalco", st, new Vector3(2.5f, FloorY, 6.5f), new Vector3(5f - WallT, FloorY + 1.2f, 9f - WallT), porchWood), Audio.Surface.Wood);
            Box("Ringhiera sx", st, new Vector3(2.5f, FloorY + 1.2f, 6.45f), new Vector3(3f, FloorY + 2.1f, 6.5f), railing);
            Box("Ringhiera dx", st, new Vector3(4.4f, FloorY + 1.2f, 6.45f), new Vector3(5f - WallT, FloorY + 2.1f, 6.5f), railing);
        }

        // ------------------------------------------------------------------ personaggi

        static CharacterMotor BuildTom(Transform world, GameObject malePrefab)
        {
            var tom = new GameObject("Tom");
            tom.transform.SetParent(world, false);
            tom.transform.localPosition = new Vector3(-1.4f, FloorY, 5.2f);
            tom.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);

            var agent = tom.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = 1.3f;
            agent.acceleration = 8f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.05f;
            agent.autoTraverseOffMeshLink = false;
            agent.enabled = false; // lo attiva CharacterMotor dopo la costruzione della NavMesh
            var motor = tom.AddComponent<CharacterMotor>();
            motor.walkSpeed = 1.3f;
            motor.jogSpeed = 2.8f;
            motor.sprintSpeed = 4.5f;

            var col = tom.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(malePrefab);
            model.name = "Modello";
            model.transform.SetParent(tom.transform, false);
            var anim = model.GetComponent<ProceduralBodyAnimator>();
            if (anim != null) anim.footIgnoreLayers = (1 << 2) | (1 << CharacterLayer);
            SetLayer(tom, CharacterLayer);

            tom.AddComponent<NpcSpeaker>().speakerId = "tom";
            var talk = tom.AddComponent<Talkable>();
            talk.displayName = "Tom";
            talk.lines = new[]
            {
                new Talkable.Line { speaker = "tom", text = "Allora, la birra l'hai messa sul bancone?" },
            };
            talk.choices = new[] { "Sì, è in cucina.", "Dov'è Jef?" };
            talk.replies = new[]
            {
                new Talkable.Line { speaker = "tom", text = "Grande. Adesso manca solo la pizza." },
                new Talkable.Line { speaker = "tom", text = "Sempre in bagno. Ha mangiato messicano, credo." },
            };
            return motor;
        }

        static void BuildNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << 2) | (1 << DoorLayer) | (1 << CharacterLayer));
            go.AddComponent<RuntimeNavMeshBaker>();
        }

        static Transform Marker(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static void BuildPlayer(GameObject malePrefab)
        {
            var root = new GameObject("Muark (giocatore)");
            root.layer = 2;
            root.transform.position = new Vector3(0f, 0.05f, -6f);
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;

            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.68f, 0.12f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<RetroScreen>();

            var hold = new GameObject("Mano (oggetti)").transform;
            hold.SetParent(camGo.transform, false);
            hold.localPosition = new Vector3(0.22f, -0.24f, 0.45f);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(malePrefab);
            model.name = "Corpo";
            model.transform.SetParent(root.transform, false);

            var fpc = root.AddComponent<FirstPersonController>();
            fpc.cam = cam;
            fpc.body = model.GetComponent<BodyRig>();

            var spotGo = new GameObject("Fascio torcia");
            spotGo.transform.SetParent(root.transform, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 55f;
            spot.range = 18f;
            spot.intensity = 3f;
            spot.color = new Color(1f, 0.93f, 0.78f);
            spot.shadows = LightShadows.Soft;
            var fl = root.AddComponent<PlayerFlashlight>();
            fl.spot = spot;

            var interactor = root.AddComponent<Interactor>();
            interactor.cam = cam;
            interactor.holdPoint = hold;
            interactor.body = fpc.body;
            root.AddComponent<PlayerActions>();

            // la birra in mano all'inizio
            GameObject beer = Instance(VoxelAssetLibrary.Load("Birra"), null, root.transform.position, 0f);
            var item = beer.AddComponent<PickupItem>();
            item.itemId = "birra";
            item.displayName = "la birra";
            item.holdEuler = new Vector3(0f, -20f, 0f);
            item.carryInLeftHand = true; // per il manico, lungo il fianco
            item.handOffset = new Vector3(0f, -0.25f, 0f);
            item.handEuler = new Vector3(0f, 90f, 0f);
            item.handleHeight = 0.19f;
            interactor.startHeld = item;
        }

        // ------------------------------------------------------------------ utilità

        static void CreateMaterials()
        {
            grass = EditorAssetUtil.Mat("Horror Prato", new Color(0.16f, 0.24f, 0.12f));
            concrete = EditorAssetUtil.Mat("Horror Cemento", new Color(0.42f, 0.42f, 0.4f));
            asphalt = EditorAssetUtil.Mat("Horror Asfalto", new Color(0.12f, 0.12f, 0.13f));
            woodFloor = EditorAssetUtil.Mat("Horror Parquet", new Color(0.42f, 0.28f, 0.17f), 0.3f);
            wall = EditorAssetUtil.Mat("Horror Carta da parati", new Color(0.72f, 0.66f, 0.52f));
            ceiling = EditorAssetUtil.Mat("Horror Soffitto", new Color(0.82f, 0.8f, 0.74f));
            porchWood = EditorAssetUtil.Mat("Horror Legno veranda", new Color(0.5f, 0.38f, 0.26f));
            baseboard = EditorAssetUtil.Mat("Horror Battiscopa", new Color(0.35f, 0.24f, 0.15f));
            railing = EditorAssetUtil.Mat("Horror Ringhiera", new Color(0.3f, 0.2f, 0.13f));
            AssetDatabase.SaveAssets();
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localScale = max - min;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Surface(GameObject go, Audio.Surface s)
        {
            go.AddComponent<FootstepSurface>().surface = s;
            return go;
        }

        static GameObject Instance(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }
    }
}
