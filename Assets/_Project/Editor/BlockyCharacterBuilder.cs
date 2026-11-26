using System.Collections.Generic;
using FantaLandia.Characters.Body;
using UnityEditor;
using UnityEngine;
using BodyPart = FantaLandia.Characters.Body.BodyPart;

namespace FantaLandia.EditorTools
{
    /// <summary>
    /// Genera i modelli umani a blocchi (maschio e femmina in intimo) come prefab.
    /// Ogni parte del corpo è una mesh separata attaccata al suo giunto, con strati distinti:
    /// "Pelle" e "Intimo" (poi "Abbigliamento", "Protesi"...). Menu: FantaLandia > Crea modelli personaggi.
    /// </summary>
    public static class BlockyCharacterBuilder
    {
        public const string Folder = "Assets/_Project/Generated/Characters";

        /// <summary>Proporzioni e colori di un modello. Misure in metri.</summary>
        public class Shape
        {
            public string id, displayName;
            public bool female;

            public float ankleH, shinLen, thighLen, hipJointDrop, hipJointX;
            public float thighW, shinW, footW, footLen;
            public float pelvisW, pelvisD, pelvisH;
            public float absW, absD, absH;
            public float chestW, chestD, chestH;
            public float shoulderX, shoulderDrop;
            public float upperArmLen, upperArmW, forearmLen, forearmW, handLen;
            public float neckW, neckH;
            public float headW, headH, headD;

            public Color skin, hair, lips, pupil, underwear, trim;

            public float Height => ankleH + shinLen + thighLen + hipJointDrop + pelvisH * 0.5f + absH + chestH + neckH + headH;
        }

        public static Shape Male() => new Shape
        {
            id = "Umano_Maschio", displayName = "Umano maschio", female = false,
            ankleH = 0.08f, shinLen = 0.41f, thighLen = 0.41f, hipJointDrop = 0.05f, hipJointX = 0.095f,
            thighW = 0.15f, shinW = 0.12f, footW = 0.10f, footLen = 0.25f,
            pelvisW = 0.33f, pelvisD = 0.20f, pelvisH = 0.20f,
            absW = 0.30f, absD = 0.18f, absH = 0.20f,
            chestW = 0.40f, chestD = 0.22f, chestH = 0.28f,
            shoulderX = 0.245f, shoulderDrop = 0.05f,
            upperArmLen = 0.29f, upperArmW = 0.105f, forearmLen = 0.26f, forearmW = 0.09f, handLen = 0.10f,
            neckW = 0.11f, neckH = 0.08f,
            headW = 0.19f, headH = 0.20f, headD = 0.21f,
            skin = new Color(0.80f, 0.62f, 0.47f), hair = new Color(0.20f, 0.13f, 0.08f),
            lips = new Color(0.62f, 0.40f, 0.33f), pupil = new Color(0.25f, 0.17f, 0.10f),
            underwear = new Color(0.16f, 0.22f, 0.40f), trim = new Color(0.85f, 0.85f, 0.85f),
        };

        public static Shape Female() => new Shape
        {
            id = "Umano_Femmina", displayName = "Umana femmina", female = true,
            ankleH = 0.075f, shinLen = 0.38f, thighLen = 0.39f, hipJointDrop = 0.05f, hipJointX = 0.095f,
            thighW = 0.145f, shinW = 0.11f, footW = 0.09f, footLen = 0.22f,
            pelvisW = 0.36f, pelvisD = 0.20f, pelvisH = 0.20f,
            absW = 0.27f, absD = 0.17f, absH = 0.19f,
            chestW = 0.33f, chestD = 0.19f, chestH = 0.25f,
            shoulderX = 0.20f, shoulderDrop = 0.05f,
            upperArmLen = 0.27f, upperArmW = 0.085f, forearmLen = 0.24f, forearmW = 0.075f, handLen = 0.09f,
            neckW = 0.09f, neckH = 0.075f,
            headW = 0.175f, headH = 0.19f, headD = 0.195f,
            skin = new Color(0.93f, 0.76f, 0.64f), hair = new Color(0.55f, 0.25f, 0.10f),
            lips = new Color(0.72f, 0.35f, 0.35f), pupil = new Color(0.20f, 0.35f, 0.25f),
            underwear = new Color(0.55f, 0.12f, 0.18f), trim = new Color(0.25f, 0.05f, 0.08f),
        };

        class Mats
        {
            public Material skin, shade, hair, lips, eyeWhite, pupil, under, trim;
        }

        [MenuItem("FantaLandia/Crea modelli personaggi")]
        public static void BuildAll()
        {
            BuildMale();
            BuildFemale();
            Debug.Log("[FantaLandia] Modelli creati in " + Folder);
        }

        public static GameObject BuildMale() => Build(Male());
        public static GameObject BuildFemale() => Build(Female());

        // ------------------------------------------------------------------ costruzione

        public static GameObject Build(Shape s)
        {
            EditorAssetUtil.EnsureFolder(Folder);
            var m = new Mats
            {
                skin = EditorAssetUtil.Mat($"{s.id} Pelle", s.skin),
                shade = EditorAssetUtil.Mat($"{s.id} Pelle ombra", s.skin * 0.86f),
                hair = EditorAssetUtil.Mat($"{s.id} Capelli", s.hair, 0.25f),
                lips = EditorAssetUtil.Mat($"{s.id} Labbra", s.lips),
                eyeWhite = EditorAssetUtil.Mat("Occhi bianco", new Color(0.95f, 0.95f, 0.92f), 0.5f),
                pupil = EditorAssetUtil.Mat($"{s.id} Iride", s.pupil, 0.6f),
                under = EditorAssetUtil.Mat($"{s.id} Intimo", s.underwear, 0.2f),
                trim = EditorAssetUtil.Mat($"{s.id} Intimo bordo", s.trim, 0.2f),
            };

            string meshPath = $"{Folder}/{s.id}_Mesh.asset";
            string prefabPath = $"{Folder}/{s.id}.prefab";
            AssetDatabase.DeleteAsset(meshPath);
            var meshes = new List<Mesh>();

            var root = new GameObject(s.displayName);
            var rig = root.AddComponent<BodyRig>();
            rig.joints = new Transform[BodyRig.PartCount];
            rig.height = s.Height;
            rig.female = s.female;
            root.AddComponent<ProceduralBodyAnimator>();

            // --- scheletro
            float hipsY = s.ankleH + s.shinLen + s.thighLen + s.hipJointDrop;
            Transform hips = Joint(rig, BodyPart.Pelvis, root.transform, new Vector3(0f, hipsY, 0f));
            Transform spine = Joint(rig, BodyPart.Abdomen, hips, new Vector3(0f, s.pelvisH * 0.5f, 0f));
            Transform chest = Joint(rig, BodyPart.Chest, spine, new Vector3(0f, s.absH, 0f));
            Transform neck = Joint(rig, BodyPart.Neck, chest, new Vector3(0f, s.chestH, 0f));
            Transform head = Joint(rig, BodyPart.Head, neck, new Vector3(0f, s.neckH, 0f));

            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? -1 : 1; // -1 = sinistra (-X), +1 = destra
                BodyPart upper = side < 0 ? BodyPart.UpperArmL : BodyPart.UpperArmR;
                BodyPart fore = side < 0 ? BodyPart.ForearmL : BodyPart.ForearmR;
                BodyPart hand = side < 0 ? BodyPart.HandL : BodyPart.HandR;
                BodyPart thigh = side < 0 ? BodyPart.ThighL : BodyPart.ThighR;
                BodyPart shin = side < 0 ? BodyPart.ShinL : BodyPart.ShinR;
                BodyPart foot = side < 0 ? BodyPart.FootL : BodyPart.FootR;

                Transform shoulder = Joint(rig, upper, chest, new Vector3(side * s.shoulderX, s.chestH - s.shoulderDrop, 0f));
                Transform elbow = Joint(rig, fore, shoulder, new Vector3(0f, -s.upperArmLen, 0f));
                Transform wrist = Joint(rig, hand, elbow, new Vector3(0f, -s.forearmLen, 0f));
                Transform hip = Joint(rig, thigh, hips, new Vector3(side * s.hipJointX, -s.hipJointDrop, 0f));
                Transform knee = Joint(rig, shin, hip, new Vector3(0f, -s.thighLen, 0f));
                Transform ankle = Joint(rig, foot, knee, new Vector3(0f, -s.shinLen, 0f));

                Layers(shoulder, UpperArm(s, m), null, meshes, $"{s.id} {upper}");
                Layers(elbow, Forearm(s, m), null, meshes, $"{s.id} {fore}");
                Layers(wrist, Hand(s, m), null, meshes, $"{s.id} {hand}");
                Layers(hip, Thigh(s, m), null, meshes, $"{s.id} {thigh}");
                Layers(knee, Shin(s, m), null, meshes, $"{s.id} {shin}");
                Layers(ankle, Foot(s, m, side), null, meshes, $"{s.id} {foot}");
            }

            // --- busto e testa
            PelvisLayers(s, m, out BoxMesh pelvisSkin, out BoxMesh pelvisUnder);
            Layers(hips, pelvisSkin, pelvisUnder, meshes, $"{s.id} Pelvis");
            Layers(spine, Abdomen(s, m), null, meshes, $"{s.id} Abdomen");
            ChestLayers(s, m, out BoxMesh chestSkin, out BoxMesh chestUnder);
            Layers(chest, chestSkin, chestUnder, meshes, $"{s.id} Chest");
            Layers(neck, Neck(s, m), null, meshes, $"{s.id} Neck");
            Layers(head, Head(s, m), null, meshes, $"{s.id} Head");

            // --- salvataggio: tutte le mesh in un unico asset, poi il prefab
            AssetDatabase.CreateAsset(meshes[0], meshPath);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], meshPath);
            AssetDatabase.SaveAssets();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Transform Joint(BodyRig rig, BodyPart part, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(BodyRig.DisplayName(part)).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            rig.joints[(int)part] = t;
            return t;
        }

        static void Layers(Transform joint, BoxMesh skin, BoxMesh under, List<Mesh> meshes, string meshName)
        {
            Visual(joint, "Pelle", skin, meshes, meshName + " Pelle");
            Visual(joint, "Intimo", under, meshes, meshName + " Intimo");
        }

        static void Visual(Transform joint, string layer, BoxMesh b, List<Mesh> meshes, string meshName)
        {
            if (b == null || b.IsEmpty) return;
            var go = new GameObject(layer);
            go.transform.SetParent(joint, false);
            Mesh mesh = b.Build(meshName);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = b.Materials;
        }

        // ------------------------------------------------------------------ parti del corpo (coordinate locali al giunto)

        static void PelvisLayers(Shape s, Mats m, out BoxMesh skin, out BoxMesh under)
        {
            skin = new BoxMesh();
            under = new BoxMesh();
            float W = s.pelvisW, D = s.pelvisD, H = s.pelvisH;

            skin.Add(new Vector3(0f, 0f, 0f), new Vector3(W, H, D), m.skin);
            // glutei
            skin.Add(new Vector3(-W * 0.22f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.42f, H * 0.6f, 0.03f), m.skin);
            skin.Add(new Vector3(W * 0.22f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.42f, H * 0.6f, 0.03f), m.skin);

            // slip: copre il bacino dal basso fino a metà, con elastico in vita
            float briefsH = H * 0.65f, briefsY = -H * 0.5f + briefsH * 0.5f;
            under.Add(new Vector3(0f, briefsY, 0f), new Vector3(W + 0.014f, briefsH, D + 0.014f), m.under);
            under.Add(new Vector3(0f, -0.03f, -D * 0.5f - 0.012f), new Vector3(W * 0.9f + 0.012f, H * 0.6f + 0.01f, 0.042f), m.under);
            under.Add(new Vector3(0f, briefsY + briefsH * 0.5f + 0.011f, 0f), new Vector3(W + 0.02f, s.female ? 0.016f : 0.022f, D + 0.02f), m.trim);
            if (s.female)
                under.Add(new Vector3(0f, briefsY + briefsH * 0.5f, D * 0.5f + 0.014f), new Vector3(0.03f, 0.02f, 0.01f), m.trim); // fiocchetto
            else
                under.Add(new Vector3(0f, -0.065f, D * 0.5f + 0.012f), new Vector3(0.08f, 0.06f, 0.025f), m.under);
        }

        static BoxMesh Abdomen(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float W = s.absW, D = s.absD, H = s.absH;
            b.Add(new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), m.skin);
            b.Add(new Vector3(0f, 0.035f, D * 0.5f + 0.002f), new Vector3(0.012f, 0.014f, 0.006f), m.shade); // ombelico
            if (!s.female)
            {
                for (int r = 0; r < 3; r++)
                    for (int c = -1; c <= 1; c += 2)
                        b.Add(new Vector3(c * 0.032f, 0.075f + r * 0.045f, D * 0.5f + 0.004f), new Vector3(0.05f, 0.038f, 0.01f), m.shade);
            }
            // fianchi
            b.Add(new Vector3(-W * 0.5f - 0.006f, H * 0.35f, 0f), new Vector3(0.012f, H * 0.5f, D * 0.8f), m.skin);
            b.Add(new Vector3(W * 0.5f + 0.006f, H * 0.35f, 0f), new Vector3(0.012f, H * 0.5f, D * 0.8f), m.skin);
            return b;
        }

        static void ChestLayers(Shape s, Mats m, out BoxMesh skin, out BoxMesh under)
        {
            skin = new BoxMesh();
            under = s.female ? new BoxMesh() : null;
            float W = s.chestW, D = s.chestD, H = s.chestH;

            skin.Add(new Vector3(0f, H * 0.2f, 0f), new Vector3(W * 0.85f, H * 0.4f, D * 0.95f), m.skin);  // costole
            skin.Add(new Vector3(0f, H * 0.7f, 0f), new Vector3(W, H * 0.6f, D), m.skin);                // spalle
            skin.Add(new Vector3(0f, H + 0.01f, -0.01f), new Vector3(W * 0.55f, 0.04f, D * 0.6f), m.skin); // trapezio
            skin.Add(new Vector3(0f, H * 0.62f, -D * 0.5f - 0.008f), new Vector3(W * 0.7f, H * 0.5f, 0.016f), m.skin); // scapole

            if (!s.female)
            {
                for (int c = -1; c <= 1; c += 2)
                {
                    skin.Add(new Vector3(c * 0.085f, H * 0.6f, D * 0.5f + 0.008f), new Vector3(0.15f, 0.09f, 0.02f), m.skin);         // pettorali
                    skin.Add(new Vector3(c * 0.085f, H * 0.6f - 0.04f, D * 0.5f + 0.02f), new Vector3(0.014f, 0.014f, 0.006f), m.shade); // capezzoli
                }
                return;
            }

            // seno e reggiseno
            float cupY = H * 0.5f, cupX = 0.068f;
            for (int c = -1; c <= 1; c += 2)
            {
                skin.Add(new Vector3(c * cupX, cupY, D * 0.5f + 0.025f), new Vector3(0.115f, 0.10f, 0.06f), m.skin);
                under.Add(new Vector3(c * cupX, cupY, D * 0.5f + 0.03f), new Vector3(0.125f, 0.11f, 0.066f), m.under);      // coppe
                under.Add(new Vector3(c * 0.07f, (cupY + 0.055f + H) * 0.5f, D * 0.5f + 0.006f),
                          new Vector3(0.018f, H - cupY - 0.055f, 0.01f), m.under);                                              // spalline davanti
                under.Add(new Vector3(c * 0.07f, H * 0.62f, -D * 0.5f - 0.02f), new Vector3(0.018f, H * 0.55f, 0.008f), m.under); // spalline dietro
            }
            float bandY = cupY - 0.055f;
            under.Add(new Vector3(0f, bandY, 0f), new Vector3(W * 0.85f + 0.012f, 0.035f, D * 0.95f + 0.012f), m.under);   // fascia
            under.Add(new Vector3(0f, cupY, D * 0.5f + 0.035f), new Vector3(0.03f, 0.025f, 0.02f), m.trim);              // centro
            under.Add(new Vector3(0f, bandY, -D * 0.475f - 0.01f), new Vector3(0.04f, 0.03f, 0.01f), m.trim);           // gancio
        }

        static BoxMesh Neck(Shape s, Mats m)
        {
            var b = new BoxMesh();
            b.Add(new Vector3(0f, s.neckH * 0.5f, 0.005f), new Vector3(s.neckW, s.neckH + 0.03f, s.neckW * 0.95f), m.skin);
            if (!s.female)
                b.Add(new Vector3(0f, s.neckH * 0.5f, s.neckW * 0.475f + 0.01f), new Vector3(0.02f, 0.02f, 0.01f), m.skin); // pomo d'Adamo
            return b;
        }

        static BoxMesh Head(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float W = s.headW, H = s.headH, D = s.headD;
            float jawW = s.female ? 0.80f : 0.86f;
            float jawFront = 0.012f + D * 0.43f;

            b.Add(new Vector3(0f, H * 0.66f, 0f), new Vector3(W, H * 0.68f, D), m.skin);                       // cranio
            b.Add(new Vector3(0f, H * 0.18f, 0.012f), new Vector3(W * jawW, H * 0.36f, D * 0.86f), m.skin);    // mascella
            b.Add(new Vector3(0f, H * 0.06f, jawFront + 0.003f), new Vector3(W * 0.35f, 0.03f, 0.012f), m.skin); // mento
            b.Add(new Vector3(0f, H * 0.42f, D * 0.5f + 0.012f), new Vector3(0.032f, 0.05f, 0.03f), m.skin);    // naso
            b.Add(new Vector3(0f, H * 0.2f, jawFront + 0.002f), new Vector3(W * 0.3f, 0.014f, 0.01f), m.lips); // bocca

            for (int c = -1; c <= 1; c += 2)
            {
                float ex = c * W * 0.24f, ey = H * 0.56f;
                b.Add(new Vector3(ex, ey, D * 0.5f + 0.002f), new Vector3(0.042f, 0.024f, 0.01f), m.eyeWhite);
                b.Add(new Vector3(ex, ey, D * 0.5f + 0.006f), new Vector3(0.018f, 0.02f, 0.01f), m.pupil);
                b.Add(new Vector3(ex, H * 0.66f, D * 0.5f + 0.004f), new Vector3(0.052f, s.female ? 0.008f : 0.012f, 0.012f), m.hair); // sopracciglia
                b.Add(new Vector3(c * (W * 0.5f + 0.008f), H * 0.5f, -0.01f), new Vector3(0.02f, 0.055f, 0.04f), m.skin);           // orecchie
            }

            // capelli
            b.Add(new Vector3(0f, H + 0.012f, -0.002f), new Vector3(W + 0.02f, 0.05f, D + 0.02f), m.hair);
            if (!s.female)
            {
                b.Add(new Vector3(0f, H * 0.72f, -D * 0.5f - 0.008f), new Vector3(W + 0.02f, H * 0.5f, 0.03f), m.hair);
                for (int c = -1; c <= 1; c += 2)
                    b.Add(new Vector3(c * (W * 0.5f + 0.006f), H * 0.82f, -0.03f), new Vector3(0.014f, H * 0.3f, D * 0.55f), m.hair);
                b.Add(new Vector3(0f, H * 0.93f, D * 0.5f + 0.002f), new Vector3(W + 0.01f, 0.025f, 0.02f), m.hair);
                // barba corta
                b.Add(new Vector3(0f, H * 0.1f, jawFront + 0.004f), new Vector3(W * 0.5f, H * 0.12f, 0.008f), m.hair);
            }
            else
            {
                b.Add(new Vector3(0f, H * 0.88f, D * 0.5f + 0.006f), new Vector3(W * 0.9f, 0.04f, 0.02f), m.hair);       // frangia
                b.Add(new Vector3(0f, H * 0.42f, -D * 0.5f - 0.015f), new Vector3(W + 0.04f, H * 1.4f, 0.05f), m.hair);   // capelli lunghi
                for (int c = -1; c <= 1; c += 2)
                    b.Add(new Vector3(c * (W * 0.5f + 0.012f), H * 0.5f, 0.01f), new Vector3(0.022f, H * 0.9f, 0.06f), m.hair); // ciocche laterali
            }
            return b;
        }

        static BoxMesh UpperArm(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.upperArmLen, w = s.upperArmW;
            b.Add(new Vector3(0f, -L * 0.5f, 0f), new Vector3(w, L, w), m.skin);
            b.Add(new Vector3(0f, -0.035f, 0f), new Vector3(w + 0.035f, 0.09f, w + 0.025f), m.skin);                         // deltoide
            b.Add(new Vector3(0f, -L * 0.5f, w * 0.5f + 0.006f), new Vector3(w * 0.65f, L * 0.4f, s.female ? 0.008f : 0.015f), m.skin); // bicipite
            return b;
        }

        static BoxMesh Forearm(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.forearmLen, w = s.forearmW;
            b.Add(new Vector3(0f, -L * 0.275f, 0f), new Vector3(w, L * 0.55f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.8f, L * 0.5f, w * 0.8f), m.skin);
            b.Add(new Vector3(0f, -0.02f, -w * 0.5f - 0.008f), new Vector3(w * 0.5f, 0.04f, 0.02f), m.skin); // gomito
            return b;
        }

        static BoxMesh Hand(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float P = s.handLen, k = P / 0.1f;
            float palmW = 0.085f * k, palmT = 0.035f * k;

            b.Add(new Vector3(0f, -P * 0.45f, 0f), new Vector3(palmT, P * 0.9f, palmW), m.skin);              // palmo
            b.Add(new Vector3(0f, -P * 0.86f, 0f), new Vector3(palmT + 0.004f, 0.012f, palmW), m.shade);      // nocche

            // quattro dita, dal mignolo (dietro) all'indice (davanti); il pollice è davanti
            float[] lengths = { 0.8f, 0.95f, 1.0f, 0.92f };
            float fw = palmW / 4f - 0.003f, fl = P * 0.75f;
            for (int i = 0; i < 4; i++)
            {
                float len = fl * lengths[i];
                float z = (i - 1.5f) * (palmW / 4f);
                b.Add(new Vector3(0f, -P * 0.9f - len * 0.5f + 0.005f, z), new Vector3(palmT * 0.85f, len, fw), m.skin);
            }
            b.Add(new Vector3(0f, -P * 0.4f, palmW * 0.5f + 0.012f), new Vector3(palmT * 0.85f, P * 0.55f, 0.025f * k), m.skin); // pollice
            return b;
        }

        static BoxMesh Thigh(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.thighLen, w = s.thighW;
            b.Add(new Vector3(0f, -L * 0.3f + 0.015f, 0f), new Vector3(w, L * 0.6f + 0.03f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.75f, 0f), new Vector3(w * 0.85f, L * 0.5f, w * 0.85f), m.skin);
            b.Add(new Vector3(0f, -L + 0.02f, w * 0.425f + 0.008f), new Vector3(w * 0.55f, 0.06f, 0.02f), m.skin); // rotula
            return b;
        }

        static BoxMesh Shin(Shape s, Mats m)
        {
            var b = new BoxMesh();
            float L = s.shinLen, w = s.shinW;
            b.Add(new Vector3(0f, -L * 0.25f, 0f), new Vector3(w, L * 0.5f, w), m.skin);
            b.Add(new Vector3(0f, -L * 0.72f, 0f), new Vector3(w * 0.75f, L * 0.56f, w * 0.75f), m.skin);
            b.Add(new Vector3(0f, -L * 0.3f, -w * 0.5f - 0.009f), new Vector3(w * 0.7f, L * 0.3f, 0.022f), m.skin); // polpaccio
            return b;
        }

        static BoxMesh Foot(Shape s, Mats m, int side)
        {
            var b = new BoxMesh();
            float A = s.ankleH, fw = s.footW, fl = s.footLen;
            float zMin = -0.06f, zMax = zMin + fl * 0.72f;
            float inner = -side; // verso il centro del corpo

            b.Add(new Vector3(0f, -A * 0.2f, 0f), new Vector3(s.shinW * 0.75f, A * 0.5f, s.shinW * 0.75f), m.skin);          // caviglia
            b.Add(new Vector3(0f, -A + A * 0.425f, (zMin + zMax) * 0.5f), new Vector3(fw, A * 0.85f, zMax - zMin), m.skin);   // piede
            float toeL = fl * 0.28f, toeY = -A + A * 0.25f, toeZ = zMax + toeL * 0.5f;
            b.Add(new Vector3(inner * fw * 0.32f, toeY, toeZ), new Vector3(fw * 0.34f, A * 0.5f, toeL), m.skin);              // alluce
            b.Add(new Vector3(-inner * fw * 0.18f, toeY - A * 0.03f, toeZ - toeL * 0.08f), new Vector3(fw * 0.6f, A * 0.44f, toeL * 0.84f), m.skin); // dita
            b.Add(new Vector3(0f, -A + 0.01f, zMin + 0.01f), new Vector3(fw * 0.9f, 0.02f, 0.03f), m.shade);                  // tallone
            return b;
        }

        // ------------------------------------------------------------------ mesh a blocchi

        /// <summary>Accumula scatole (con faccette piatte) raggruppate per materiale in sotto-mesh.</summary>
        class BoxMesh
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<List<int>> triangles = new List<List<int>>();
            readonly List<Material> materials = new List<Material>();

            public bool IsEmpty => vertices.Count == 0;
            public Material[] Materials => materials.ToArray();

            static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            public void Add(Vector3 center, Vector3 size, Material mat)
            {
                int sub = materials.IndexOf(mat);
                if (sub < 0)
                {
                    materials.Add(mat);
                    triangles.Add(new List<int>());
                    sub = materials.Count - 1;
                }
                Vector3 h = size * 0.5f;
                foreach (Vector3 n in FaceNormals)
                {
                    Vector3 v = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                    Vector3 u = Vector3.Cross(n, v); // a destra guardando la faccia: triangoli in senso orario
                    float hn = Extent(n, h), hu = Extent(u, h), hv = Extent(v, h);
                    Vector3 c = center + n * hn;
                    int start = vertices.Count;
                    vertices.Add(c - u * hu - v * hv);
                    vertices.Add(c - u * hu + v * hv);
                    vertices.Add(c + u * hu + v * hv);
                    vertices.Add(c + u * hu - v * hv);
                    for (int i = 0; i < 4; i++) normals.Add(n);
                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                    var t = triangles[sub];
                    t.Add(start); t.Add(start + 1); t.Add(start + 2);
                    t.Add(start); t.Add(start + 2); t.Add(start + 3);
                }
            }

            static float Extent(Vector3 axis, Vector3 h) => Mathf.Abs(axis.x) * h.x + Mathf.Abs(axis.y) * h.y + Mathf.Abs(axis.z) * h.z;

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
