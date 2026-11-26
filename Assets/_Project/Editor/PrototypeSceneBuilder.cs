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

        static Material matGround, matStone, matWood, matWall, matRoof, matObstacle, matDark, matSelect;

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
            BuildGroundWithMoat(env);
            BuildObstacleCourse(env);
            BuildPlatform(env);
            BuildHouse(env, new Vector3(0f, 0f, 14f));
            BuildMoat(env);
            BuildNavMesh();

            // Modelli a blocchi generati da BlockyCharacterBuilder (rigenerati ogni volta).
            GameObject male = BlockyCharacterBuilder.BuildMale();
            GameObject female = BlockyCharacterBuilder.BuildFemale();

            // Velocità diverse per provare il movimento di gruppo (G): la Compagna è più lenta.
            BuildCharacter("Viandante", new Vector3(-1f, 0f, -3f), male, 2.0f, 4.0f, 6.5f);
            BuildCharacter("Compagna", new Vector3(1f, 0f, -3f), female, 1.8f, 3.5f, 5.5f);
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

        // ------------------------------------------------------------------ fossato con due ponti rotti

        // Fossato lungo l'asse Z: x da MoatX0 a MoatX1, z da -MoatHalfLen a +MoatHalfLen, profondo MoatDepth.
        const float MoatX0 = 20f, MoatX1 = 24f, MoatHalfLen = 20f, MoatDepth = 2.5f;

        /// <summary>Terreno spesso con uno scavo per il fossato (il resto della scena è a quota 0).</summary>
        static void BuildGroundWithMoat(Transform env)
        {
            Transform g = Group("Terreno", env);
            const float E = 60f, bottom = -MoatDepth - 0.5f;
            Box("Terreno ovest", g, new Vector3(-E, bottom, -E), new Vector3(MoatX0, 0f, E), matGround);
            Box("Terreno est", g, new Vector3(MoatX1, bottom, -E), new Vector3(E, 0f, E), matGround);
            Box("Terreno sud", g, new Vector3(MoatX0, bottom, -E), new Vector3(MoatX1, 0f, -MoatHalfLen), matGround);
            Box("Terreno nord", g, new Vector3(MoatX0, bottom, MoatHalfLen), new Vector3(MoatX1, 0f, E), matGround);
            Box("Fondo del fossato", g, new Vector3(MoatX0, bottom, -MoatHalfLen), new Vector3(MoatX1, -MoatDepth, MoatHalfLen), matStone);
        }

        static void BuildMoat(Transform env)
        {
            Transform root = Group("Fossato", env);

            // Scale per uscire dal fossato alle due estremità (gradini da 0.25 m)
            const int steps = 10;
            const float rise = MoatDepth / steps, run = 0.35f;
            Stairs(root, "Scala fossato nord", false, MoatHalfLen - steps * run, -MoatDepth, steps, rise, run, MoatX0, MoatX1, matStone);
            Stairs(root, "Scala fossato sud", false, -MoatHalfLen + steps * run, -MoatDepth, steps, rise, -run, MoatX0, MoatX1, matStone);

            BuildBrokenBridge(root, "Ponte con varco piccolo", -8f, 1.2f);
            BuildBrokenBridge(root, "Ponte con varco grande", 8f, 3.0f);

            Label(root, "Fossato: Alt + clic sinistro sull'altra sponda per saltare", new Vector3((MoatX0 + MoatX1) * 0.5f, 0.05f, -14f));
            Label(root, "Varco 1.2 m (si salta)", new Vector3(MoatX0 - 3f, 0.05f, -8f));
            Label(root, "Varco 3 m (si cade)", new Vector3(MoatX0 - 3f, 0.05f, 8f));
        }

        /// <summary>Ponte di legno sul fossato con un buco in mezzo largo 'gap' metri.</summary>
        static void BuildBrokenBridge(Transform parent, string name, float z, float gap)
        {
            Transform b = Group(name, parent);
            const float halfW = 1.2f, deckT = 0.3f, overhang = 1.5f;
            float x0 = MoatX0 - overhang, x1 = MoatX1 + overhang;
            float mid = (MoatX0 + MoatX1) * 0.5f;
            float g0 = mid - gap * 0.5f, g1 = mid + gap * 0.5f;

            const float top = 0.05f; // appena sopra il terreno, per non sovrapporsi alla sua superficie
            Box("Assi ovest", b, new Vector3(x0, -deckT, z - halfW), new Vector3(g0, top, z + halfW), matWood);
            Box("Assi est", b, new Vector3(g1, -deckT, z - halfW), new Vector3(x1, top, z + halfW), matWood);
            for (int side = -1; side <= 1; side += 2)
            {
                float zr = z + side * (halfW - 0.05f);
                Box("Parapetto ovest", b, new Vector3(x0, top, zr - 0.05f), new Vector3(g0 - 0.1f, top + 1f, zr + 0.05f), matWood);
                Box("Parapetto est", b, new Vector3(g1 + 0.1f, top, zr - 0.05f), new Vector3(x1, top + 1f, zr + 0.05f), matWood);
            }
            // pilastri contro le pareti del fossato
            Box("Pilastro ovest", b, new Vector3(MoatX0, -MoatDepth, z - 0.25f), new Vector3(MoatX0 + 0.4f, -deckT, z + 0.25f), matStone);
            Box("Pilastro est", b, new Vector3(MoatX1 - 0.4f, -MoatDepth, z - 0.25f), new Vector3(MoatX1, -deckT, z + 0.25f), matStone);

            // Salto del varco (solo con Alt): nei due sensi
            // Estremi ben dentro la zona calpestabile (la NavMesh è erosa di 0.5 m dai bordi), così il link si aggancia sempre.
            Vector3 west = new Vector3(g0 - 1.0f, top, z), east = new Vector3(g1 + 1.0f, top, z);
            Link(b, "Salto del varco", west, east, 1.2f, true, CharacterMotor.DangerousJumpArea, 0f, true);
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

            // Porte a due ante: ingresso (piano terra, muro sud) e balcone (primo piano, muro est)
            BuildDoor(walls[0], "Porta d'ingresso", new Vector3(0f, 0f, -HalfZ + WallT / 2f), 0f);
            BuildDoor(walls[1], "Porta del balcone", new Vector3(HalfX - WallT / 2f, FloorH, 0f), 90f);

            // Balcone sul lato est del primo piano
            Box("Balcone", structure[1], new Vector3(HalfX, FloorH - Slab, -2.5f), new Vector3(HalfX + 2f, FloorH, 2.5f), matWood);
            Box("Ringhiera", structure[1], new Vector3(HalfX + 1.9f, FloorH, -2.5f), new Vector3(HalfX + 2f, FloorH + 1f, 2.5f), matDark);
            Box("Ringhiera sud", structure[1], new Vector3(HalfX, FloorH, -2.5f), new Vector3(HalfX + 2f, FloorH + 1f, -2.4f), matDark);
            Box("Ringhiera nord", structure[1], new Vector3(HalfX, FloorH, 2.4f), new Vector3(HalfX + 2f, FloorH + 1f, 2.5f), matDark);

            // Salti: tutti sopra i 3 m sono "pericolosi" (area Jump): solo con Alt + clic sinistro.
            int jump = CharacterMotor.DangerousJumpArea;
            Link(house, "Salto dal balcone", house.TransformPoint(new Vector3(HalfX + 1f, FloorH, 0f)),
                 house.TransformPoint(new Vector3(HalfX + 3.6f, 0f, 0f)), 3f, false, jump, 1f);
            Link(house, "Salto dal tetto a terra", house.TransformPoint(new Vector3(HalfX - 0.7f, 3 * FloorH, 3.5f)),
                 house.TransformPoint(new Vector3(HalfX + 2.5f, 0f, 3.5f)), 1.5f, false, jump, 0f);
            Link(house, "Salto dal tetto al balcone", house.TransformPoint(new Vector3(HalfX - 0.7f, 3 * FloorH, 0f)),
                 house.TransformPoint(new Vector3(HalfX + 1f, FloorH, 0f)), 1.5f, false, jump, 0f);

            Label(house, "Ingresso", house.TransformPoint(new Vector3(0f, 0.05f, -HalfZ - 1.5f)));
            Label(house, "Alt + clic sinistro: salta dal balcone / tetto", house.TransformPoint(new Vector3(HalfX + 4f, 0.05f, -4f)));
        }

        /// <summary>
        /// Porta a due ante larga 2 m nel vano di una parete. Non entra nella NavMesh (altrimenti chiuderebbe il passaggio)
        /// e sta sul layer "Ignore Raycast" così i clic la attraversano.
        /// </summary>
        static void BuildDoor(Transform parent, string name, Vector3 localPos, float yaw)
        {
            const float width = 2f, height = 2.35f, thick = 0.06f;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            var leaves = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;                    // cardine a sinistra / destra
                var hinge = new GameObject(side < 0 ? "Anta sinistra" : "Anta destra").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(side * width * 0.5f, 0f, 0f);
                leaves[i] = hinge;

                float cx = -side * width * 0.25f;              // centro dell'anta rispetto al cardine
                float w = width * 0.5f - 0.02f;
                Box("Assi", hinge, new Vector3(cx - w * 0.5f, 0f, -thick * 0.5f), new Vector3(cx + w * 0.5f, height, thick * 0.5f), matWood);
                for (int k = 0; k < 2; k++)
                {
                    float y = k == 0 ? 0.45f : height - 0.55f;
                    Box("Traversa", hinge, new Vector3(cx - w * 0.45f, y, -thick * 0.5f - 0.02f), new Vector3(cx + w * 0.45f, y + 0.12f, thick * 0.5f + 0.02f), matWood);
                }
                float hx = cx - side * (w * 0.5f - 0.1f);        // maniglia vicino al bordo libero
                Box("Maniglia", hinge, new Vector3(hx - 0.02f, 1.0f, -thick * 0.5f - 0.05f), new Vector3(hx + 0.02f, 1.15f, thick * 0.5f + 0.05f), matDark);
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2; // Ignore Raycast

            var door = root.AddComponent<AutoDoor>();
            door.leaves = leaves;
            door.leafSign = new[] { -1f, 1f };
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

        /// <summary>Scala piena: ogni gradino è un blocco dal pavimento alla sua altezza. Sale nel verso di 'run' (anche negativo).</summary>
        static void Stairs(Transform parent, string name, bool alongX, float start, float baseY, int steps,
                           float rise, float run, float crossMin, float crossMax, Material mat)
        {
            Transform g = Group(name, parent);
            for (int i = 0; i < steps; i++)
            {
                float a0 = start + i * run, b0 = start + (i + 1) * run;
                float a = Mathf.Min(a0, b0), b = Mathf.Max(a0, b0);
                float top = baseY + (i + 1) * rise;
                Vector3 min = alongX ? new Vector3(a, baseY, crossMin) : new Vector3(crossMin, baseY, a);
                Vector3 max = alongX ? new Vector3(b, top, crossMax) : new Vector3(crossMax, top, b);
                Box($"Gradino {i + 1}", g, min, max, mat);
            }

            // Bordi della scala più "costosi" per la navigazione: i personaggi la prendono al centro.
            float s0 = Mathf.Min(start, start + steps * run) - 0.6f, s1 = Mathf.Max(start, start + steps * run) + 0.6f;
            float c = (crossMin + crossMax) * 0.5f, band = 0.3f;
            float y0 = baseY - 0.5f, y1 = baseY + steps * rise + 1f;
            StairEdgeVolume(g, alongX, s0, s1, y0, y1, crossMin - 0.3f, c - band);
            StairEdgeVolume(g, alongX, s0, s1, y0, y1, c + band, crossMax + 0.3f);
        }

        /// <summary>Area NavMesh "Bordo scala" (indice 3, costo 5 in Project Settings > Navigation > Areas).</summary>
        public const int StairEdgeArea = 3;

        static void StairEdgeVolume(Transform parent, bool alongX, float a0, float a1, float y0, float y1, float c0, float c1)
        {
            if (c1 - c0 < 0.05f) return;
            var go = new GameObject("Bordo scala (navigazione)");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<NavMeshModifierVolume>();
            Vector3 min = alongX ? new Vector3(a0, y0, c0) : new Vector3(c0, y0, a0);
            Vector3 max = alongX ? new Vector3(a1, y1, c1) : new Vector3(c1, y1, a1);
            vol.center = (min + max) * 0.5f;
            vol.size = max - min;
            vol.area = StairEdgeArea;
        }

        // ------------------------------------------------------------------ navigazione

        static void BuildNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Il layer "Ignore Raycast" (porte) non entra nella NavMesh: le porte non devono chiudere i passaggi.
            surface.layerMask = ~(1 << 2);
            go.AddComponent<RuntimeNavMeshBaker>();
        }

        /// <summary>NavMeshLink tra due punti in coordinate mondo, con l'altezza dell'ostacolo per il CharacterMotor.</summary>
        /// <summary>
        /// Un NavMeshLink fa passare un solo agente alla volta: i passaggi larghi vengono quindi
        /// divisi in corsie affiancate, così più personaggi scavalcano contemporaneamente.
        /// </summary>
        const float LaneWidth = 1.2f;

        static void Link(Transform parent, string name, Vector3 worldStart, Vector3 worldEnd, float width,
                         bool bidirectional, int area, float obstacleHeight, bool gap = false)
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
                LinkLane(group, laneName, worldStart + offset, worldEnd + offset, laneWidth * 0.9f, bidirectional, area, obstacleHeight, gap);
            }
        }

        static void LinkLane(Transform parent, string name, Vector3 worldStart, Vector3 worldEnd, float width,
                             bool bidirectional, int area, float obstacleHeight, bool gap)
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

            var info = go.AddComponent<TraversalLink>();
            info.obstacleHeight = obstacleHeight;
            info.isGap = gap;
        }

        // ------------------------------------------------------------------ personaggi e sistemi

        static void BuildCharacter(string name, Vector3 position, GameObject modelPrefab, float walk, float jog, float sprint)
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

            // Collider per i clic di selezione (trigger: non blocca nulla)
            var clickCollider = root.AddComponent<CapsuleCollider>();
            clickCollider.isTrigger = true;
            clickCollider.radius = 0.3f;
            clickCollider.height = 1.8f;
            clickCollider.center = new Vector3(0f, 0.9f, 0f);

            // Modello a segmenti con animazione procedurale
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Modello";
            model.transform.SetParent(root.transform, false);

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
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color color, float smoothness = 0.1f) => EditorAssetUtil.Mat(name, color, smoothness);

        static void EnsureFolder(string path) => EditorAssetUtil.EnsureFolder(path);
    }
}
