using System.Collections.Generic;
using FantaLandia.CameraSystem;
using FantaLandia.Characters;
using FantaLandia.Player;
using FantaLandia.UI;
using FantaLandia.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FantaLandia.EditorTools
{
    /// <summary>
    /// Menu "FantaLandia > Crea scena prototipo movimento": genera da zero una scena greybox per testare
    /// movimento, scale, scavalcamenti, salti e camera dentro un edificio a tre piani.
    /// Si può rigenerare quando si vuole (sovrascrive la scena).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string MatFolder = Root + "/Generated/Materials";
        const string ScenePath = Root + "/Scenes/Prototype_Movement.unity";

        // Casa: 12 x 10 m, piani da 3.2 m
        const float FloorH = 3.2f, Slab = 0.2f, WallT = 0.3f, HalfX = 6f, HalfZ = 5f;
        const int StairSteps = 16;
        const float StairRise = 0.2f, StairRun = 0.3f;

        static Material matGround, matStone, matWood, matWall, matRoof, matObstacle, matDark, matSelect, matBodyA, matBodyB;

        struct Opening
        {
            public float a, b, bottom, top;
            public Opening(float a, float b, float bottom, float top) { this.a = a; this.b = b; this.bottom = bottom; this.top = top; }
        }

        [MenuItem("FantaLandia/Crea scena prototipo movimento")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MatFolder);
            EnsureFolder(Root + "/Scenes");
            CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            Transform env = Group("Ambiente", null);
            Box("Terreno", env, new Vector3(-60f, -0.5f, -60f), new Vector3(60f, 0f, 60f), matGround);
            BuildObstacleCourse(env);
            BuildPlatform(env);
            BuildHouse(env, new Vector3(0f, 0f, 14f));
            BuildNavMesh();

            // Velocità diverse per provare il movimento di gruppo (G): la Compagna è più lenta.
            BuildCharacter("Viandante", new Vector3(-1f, 0f, -3f), matBodyA, 2.0f, 4.0f, 6.5f);
            BuildCharacter("Compagna", new Vector3(1f, 0f, -3f), matBodyB, 1.8f, 3.5f, 5.5f);
            BuildSystems();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[FantaLandia] Scena prototipo creata: " + ScenePath + " — premi Play.");
        }

        // ------------------------------------------------------------------ ambiente

        static void CreateLight()
        {
            var go = new GameObject("Sole");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        static void BuildObstacleCourse(Transform env)
        {
            Transform root = Group("Percorso ostacoli", env);
            AddObstacle(root, "Muretto basso 0.6 m", -10f, 0.6f);
            AddObstacle(root, "Muro medio 1.2 m", 0f, 1.2f);
            AddObstacle(root, "Muro alto 2.2 m", 10f, 2.2f);
        }

        static void AddObstacle(Transform root, string name, float x, float height)
        {
            const float z = -12f, len = 8f;
            Box(name, root, new Vector3(x - len / 2f, 0f, z - WallT / 2f), new Vector3(x + len / 2f, height, z + WallT / 2f), matObstacle);
            Link(root, "Link " + name, new Vector3(x, 0f, z - 1.1f), new Vector3(x, 0f, z + 1.1f), len - 1.5f, true, 0, height);
            Label(root, name, new Vector3(x, 0.05f, z - 2.2f));
        }

        static void BuildPlatform(Transform env)
        {
            Transform root = Group("Piattaforma", env);
            const float top = 2.2f;
            Box("Piattaforma 2.2 m", root, new Vector3(-26f, 0f, -2f), new Vector3(-20f, top, 2f), matStone);

            // Scala esterna sul lato sud: 11 gradini da 0.2 m, sale verso +Z
            Stairs(root, "Scala esterna", false, -2f - 11 * 0.35f, 0f, 11, 0.2f, 0.35f, -24f, -22f, matWood);

            // Arrampicata sul lato ovest (su e giù) e salto giù sul lato nord
            Link(root, "Arrampicata ovest", new Vector3(-26.9f, 0f, 0f), new Vector3(-25.3f, top, 0f), 3f, true, 0, 0f);
            Link(root, "Salto nord", new Vector3(-23f, top, 1.3f), new Vector3(-23f, 0f, 3.2f), 3.5f, false, 0, 0f);

            Label(root, "Piattaforma: scala, arrampicata (ovest), salto (nord)", new Vector3(-23f, 0.05f, -7f));
        }

        // ------------------------------------------------------------------ casa a tre piani

        static void BuildHouse(Transform env, Vector3 position)
        {
            Transform house = Group("Casa a tre piani", env);
            house.position = position;

            var vis = house.gameObject.AddComponent<BuildingVisibility>();
            vis.interiorCenter = new Vector3(0f, 5.25f, 0f);
            vis.interiorSize = new Vector3(HalfX * 2f, 11.5f, HalfZ * 2f);
            vis.floors.Clear();

            string[] names = { "Piano terra", "Primo piano", "Secondo piano", "Tetto" };
            var structure = new Transform[4];
            var walls = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Transform f = Group(names[i], house);
                structure[i] = Group("Struttura", f);
                walls[i] = Group("Muri", f);
                vis.floors.Add(new BuildingVisibility.Floor
                {
                    displayName = names[i],
                    baseHeight = i * FloorH,
                    structure = structure[i],
                    walls = walls[i],
                });
            }

            // Buchi nei solai per le scale (x, z, larghezza, profondità)
            var holeBack = new Rect(-5.4f, 2.9f, StairSteps * StairRun, 1.8f);
            var holeFront = new Rect(0.6f, -4.7f, StairSteps * StairRun, 1.8f);

            SlabWithHole(structure[1], "Solaio", 1 * FloorH, holeBack, matWood);
            SlabWithHole(structure[2], "Solaio", 2 * FloorH, holeFront, matWood);
            SlabWithHole(structure[3], "Tetto", 3 * FloorH, holeBack, matRoof);

            // Scale: terra -> 1° (retro), 1° -> 2° (fronte), 2° -> tetto (retro)
            Stairs(structure[0], "Scala piano terra", true, -5.4f, 0f, StairSteps, StairRise, StairRun, 2.9f, 4.7f, matWood);
            Stairs(structure[1], "Scala primo piano", true, 0.6f, FloorH, StairSteps, StairRise, StairRun, -4.7f, -2.9f, matWood);
            Stairs(structure[2], "Scala secondo piano", true, -5.4f, 2 * FloorH, StairSteps, StairRise, StairRun, 2.9f, 4.7f, matWood);

            // Muri con porte e finestre
            for (int f = 0; f < 3; f++)
            {
                float y0 = f * FloorH;
                float h = FloorH - Slab;
                var south = new List<Opening>();
                var north = new List<Opening>();
                var west = new List<Opening> { new Opening(-1f, 1f, 1f, 2.2f) };
                var east = new List<Opening>();

                if (f == 0) south.Add(new Opening(-1f, 1f, 0f, 2.4f));            // ingresso
                else south.Add(new Opening(-4f, -2f, 1f, 2.2f));                   // finestra
                if (f > 0) north.Add(new Opening(1.5f, 3.5f, 1f, 2.2f));           // finestra
                if (f == 1) east.Add(new Opening(-1f, 1f, 0f, 2.4f));              // porta del balcone
                else east.Add(new Opening(-1f, 1f, 1f, 2.2f));                     // finestra

                Wall(walls[f], "Muro sud", true, -HalfZ + WallT / 2f, -HalfX, HalfX, y0, h, south);
                Wall(walls[f], "Muro nord", true, HalfZ - WallT / 2f, -HalfX, HalfX, y0, h, north);
                Wall(walls[f], "Muro ovest", false, -HalfX + WallT / 2f, -HalfZ + WallT, HalfZ - WallT, y0, h, west);
                Wall(walls[f], "Muro est", false, HalfX - WallT / 2f, -HalfZ + WallT, HalfZ - WallT, y0, h, east);
            }

            // Balcone sul lato est del primo piano
            Box("Balcone", structure[1], new Vector3(HalfX, FloorH - Slab, -2.5f), new Vector3(HalfX + 2f, FloorH, 2.5f), matWood);
            Box("Ringhiera", structure[1], new Vector3(HalfX + 1.9f, FloorH, -2.5f), new Vector3(HalfX + 2f, FloorH + 1f, 2.5f), matDark);
            Box("Ringhiera sud", structure[1], new Vector3(HalfX, FloorH, -2.5f), new Vector3(HalfX + 2f, FloorH + 1f, -2.4f), matDark);
            Box("Ringhiera nord", structure[1], new Vector3(HalfX, FloorH, 2.4f), new Vector3(HalfX + 2f, FloorH + 1f, 2.5f), matDark);

            // Salti: tutti sopra i 3 m sono "pericolosi" (area Jump): solo con Alt + clic destro.
            int jump = CharacterMotor.DangerousJumpArea;
            Link(house, "Salto dal balcone", house.TransformPoint(new Vector3(HalfX + 1f, FloorH, 0f)),
                 house.TransformPoint(new Vector3(HalfX + 3.6f, 0f, 0f)), 3f, false, jump, 1f);
            Link(house, "Salto dal tetto a terra", house.TransformPoint(new Vector3(HalfX - 0.7f, 3 * FloorH, 3.5f)),
                 house.TransformPoint(new Vector3(HalfX + 2.5f, 0f, 3.5f)), 1.5f, false, jump, 0f);
            Link(house, "Salto dal tetto al balcone", house.TransformPoint(new Vector3(HalfX - 0.7f, 3 * FloorH, 0f)),
                 house.TransformPoint(new Vector3(HalfX + 1f, FloorH, 0f)), 1.5f, false, jump, 0f);

            Label(house, "Ingresso", house.TransformPoint(new Vector3(0f, 0.05f, -HalfZ - 1.5f)));
            Label(house, "Alt + clic destro: salta dal balcone / tetto", house.TransformPoint(new Vector3(HalfX + 4f, 0.05f, -4f)));
        }

        static void SlabWithHole(Transform parent, string name, float topY, Rect hole, Material mat)
        {
            float y0 = topY - Slab, y1 = topY;
            BoxIf(name + " ovest", parent, new Vector3(-HalfX, y0, -HalfZ), new Vector3(hole.xMin, y1, HalfZ), mat);
            BoxIf(name + " est", parent, new Vector3(hole.xMax, y0, -HalfZ), new Vector3(HalfX, y1, HalfZ), mat);
            BoxIf(name + " sud", parent, new Vector3(hole.xMin, y0, -HalfZ), new Vector3(hole.xMax, y1, hole.yMin), mat);
            BoxIf(name + " nord", parent, new Vector3(hole.xMin, y0, hole.yMax), new Vector3(hole.xMax, y1, HalfZ), mat);
        }

        /// <summary>Muro dritto con aperture (porte: bottom = 0, finestre: bottom > 0). Coordinate locali al parent.</summary>
        static void Wall(Transform parent, string name, bool alongX, float fixedCoord, float from, float to,
                         float y0, float height, List<Opening> openings)
        {
            openings.Sort((p, q) => p.a.CompareTo(q.a));
            int n = 0;

            void Seg(float s, float e, float lo, float hi)
            {
                if (e - s < 0.01f || hi - lo < 0.01f) return;
                Vector3 min = alongX ? new Vector3(s, y0 + lo, fixedCoord - WallT / 2f) : new Vector3(fixedCoord - WallT / 2f, y0 + lo, s);
                Vector3 max = alongX ? new Vector3(e, y0 + hi, fixedCoord + WallT / 2f) : new Vector3(fixedCoord + WallT / 2f, y0 + hi, e);
                Box($"{name} {++n}", parent, min, max, matWall);
            }

            float cursor = from;
            foreach (var o in openings)
            {
                Seg(cursor, o.a, 0f, height);
                Seg(o.a, o.b, 0f, o.bottom);
                Seg(o.a, o.b, o.top, height);
                cursor = o.b;
            }
            Seg(cursor, to, 0f, height);
        }

        /// <summary>Scala piena: ogni gradino è un blocco dal pavimento alla sua altezza. Sale nella direzione + dell'asse.</summary>
        static void Stairs(Transform parent, string name, bool alongX, float start, float baseY, int steps,
                           float rise, float run, float crossMin, float crossMax, Material mat)
        {
            Transform g = Group(name, parent);
            for (int i = 0; i < steps; i++)
            {
                float a = start + i * run, b = start + (i + 1) * run;
                float top = baseY + (i + 1) * rise;
                Vector3 min = alongX ? new Vector3(a, baseY, crossMin) : new Vector3(crossMin, baseY, a);
                Vector3 max = alongX ? new Vector3(b, top, crossMax) : new Vector3(crossMax, top, b);
                Box($"Gradino {i + 1}", g, min, max, mat);
            }
        }

        // ------------------------------------------------------------------ navigazione

        static void BuildNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            go.AddComponent<RuntimeNavMeshBaker>();
        }

        /// <summary>NavMeshLink tra due punti in coordinate mondo, con l'altezza dell'ostacolo per il CharacterMotor.</summary>
        /// <summary>
        /// Un NavMeshLink fa passare un solo agente alla volta: i passaggi larghi vengono quindi
        /// divisi in corsie affiancate, così più personaggi scavalcano contemporaneamente.
        /// </summary>
        const float LaneWidth = 1.2f;

        static void Link(Transform parent, string name, Vector3 worldStart, Vector3 worldEnd, float width,
                         bool bidirectional, int area, float obstacleHeight)
        {
            int lanes = Mathf.Max(1, Mathf.FloorToInt(width / LaneWidth));
            float laneWidth = width / lanes;
            Vector3 dir = worldEnd - worldStart;
            dir.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);

            // Il gruppo "Passaggio ..." dice a TraversalLink quali corsie sono alternative tra loro.
            Transform group = Group($"{TraversalLink.LaneGroupPrefix}: {name}", parent);
            for (int i = 0; i < lanes; i++)
            {
                Vector3 offset = side * ((i - (lanes - 1) * 0.5f) * laneWidth);
                string laneName = $"Corsia {i + 1}";
                LinkLane(group, laneName, worldStart + offset, worldEnd + offset, laneWidth * 0.9f, bidirectional, area, obstacleHeight);
            }
        }

        static void LinkLane(Transform parent, string name, Vector3 worldStart, Vector3 worldEnd, float width,
                             bool bidirectional, int area, float obstacleHeight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (worldStart + worldEnd) * 0.5f;

            var link = go.AddComponent<NavMeshLink>();
            link.startPoint = go.transform.InverseTransformPoint(worldStart);
            link.endPoint = go.transform.InverseTransformPoint(worldEnd);
            link.width = width;
            link.bidirectional = bidirectional;
            link.area = area;
            link.UpdateLink();

            go.AddComponent<TraversalLink>().obstacleHeight = obstacleHeight;
        }

        // ------------------------------------------------------------------ personaggi e sistemi

        static void BuildCharacter(string name, Vector3 position, Material bodyMat, float walk, float jog, float sprint)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 5.5f;
            agent.acceleration = 24f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.05f;
            agent.autoTraverseOffMeshLink = false;
            agent.enabled = false; // lo attiva CharacterMotor.Start dopo la costruzione della NavMesh

            var motor = root.AddComponent<CharacterMotor>();
            motor.walkSpeed = walk;
            motor.jogSpeed = jog;
            motor.sprintSpeed = sprint;
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // il personaggio non deve "bucare" la NavMesh
            var selectable = root.AddComponent<SelectableCharacter>();
            selectable.displayName = name;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            body.GetComponent<Collider>().isTrigger = true;

            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Viso (direzione)";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, 1.55f, 0.3f);
            face.transform.localScale = new Vector3(0.25f, 0.15f, 0.2f);
            face.GetComponent<Renderer>().sharedMaterial = matDark;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Anello selezione";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f);
            ring.GetComponent<Renderer>().sharedMaterial = matSelect;
            ring.SetActive(false);
            selectable.selectionIndicator = ring;
        }

        static void BuildSystems()
        {
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.position = new Vector3(0f, 0f, -3f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();

            var rig = rigGo.AddComponent<RTSCameraRig>();
            rig.cam = cam;
            rig.ApplyTransform();

            var systems = new GameObject("Sistemi di gioco");
            systems.AddComponent<SelectionController>().cam = cam;
            systems.AddComponent<DebugHUD>();
        }

        // ------------------------------------------------------------------ utilità

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

        static void BoxIf(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
        {
            Vector3 s = max - min;
            if (s.x > 0.01f && s.y > 0.01f && s.z > 0.01f) Box(name, parent, min, max, mat);
        }

        static void Label(Transform parent, string text, Vector3 worldPos)
        {
            var go = new GameObject("Etichetta: " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // stesa a terra, leggibile dall'alto

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;

            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        static void CreateMaterials()
        {
            matGround = Mat("Terreno", new Color(0.42f, 0.52f, 0.30f));
            matStone = Mat("Pietra", new Color(0.55f, 0.55f, 0.52f));
            matWood = Mat("Legno", new Color(0.55f, 0.38f, 0.22f));
            matWall = Mat("Muro", new Color(0.86f, 0.80f, 0.68f));
            matRoof = Mat("Tetto", new Color(0.60f, 0.25f, 0.18f));
            matObstacle = Mat("Ostacolo", new Color(0.65f, 0.62f, 0.58f));
            matDark = Mat("Ferro", new Color(0.15f, 0.15f, 0.17f), 0.4f);
            matSelect = Mat("Selezione", new Color(0.3f, 1f, 0.4f));
            matBodyA = Mat("Personaggio A", new Color(0.25f, 0.45f, 0.85f));
            matBodyB = Mat("Personaggio B", new Color(0.85f, 0.55f, 0.20f));
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color color, float smoothness = 0.1f)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
