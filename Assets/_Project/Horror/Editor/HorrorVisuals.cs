using System.Collections.Generic;
using FantaLandia.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// La resa visiva dell'horror:
    ///  - pipeline URP: HDR con color grading, ombre più definite (anche delle lampade), SSAO più marcato;
    ///  - post-processing a volumi: uno globale per la notte (tonemapping ACES, bloom, grana, vignetta, toni freddi),
    ///    uno caldo dentro casa e uno cupo e sgranato in cantina, che si mescolano passando da una zona all'altra;
    ///  - camera con post-processing e antialiasing SMAA;
    ///  - coni di luce finti sotto lampioni e lampade (nella nebbia sembrano volumetrici);
    ///  - polvere sospesa che brilla solo dove arriva la luce (nel fascio della torcia, sotto le lampade);
    ///  - "cookie" della torcia: fascio con il centro caldo e un alone irregolare come una torcia vera.
    /// </summary>
    public static class HorrorVisuals
    {
        const string Folder = "Assets/_Project/Horror/Generated/Grafica";

        /// <summary>Coni di luce creati in questa costruzione (il blackout li spegne).</summary>
        public static readonly List<GameObject> Cones = new List<GameObject>();

        // ------------------------------------------------------------------ pipeline

        [MenuItem("FantaLandia Horror/Migliora la grafica (pipeline)")]
        public static void ConfigurePipeline()
        {
            RenderPipelineAsset rp = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (rp == null) { Debug.LogWarning("[Grafica] Nessuna pipeline URP trovata"); return; }
            var so = new SerializedObject(rp);
            SetInt(so, "m_SupportsHDR", 1);
            SetInt(so, "m_ColorGradingMode", 1);                  // HDR: tonemapping vero
            SetInt(so, "m_ColorGradingLutSize", 32);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetInt(so, "m_AdditionalLightShadowsSupported", 1);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096);
            SetInt(so, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetInt(so, "m_SoftShadowsSupported", 1);
            SetInt(so, "m_SoftShadowQuality", 3);
            SetFloat(so, "m_ShadowDistance", 60f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetInt(so, "m_RequireDepthTexture", 1);                // per i coni di luce morbidi
            so.ApplyModifiedPropertiesWithoutUndo();

            // SSAO del renderer: ombre di contatto più presenti (angoli, sotto i mobili, tra i cuscini)
            var list = so.FindProperty("m_RendererDataList");
            if (list != null)
                for (int i = 0; i < list.arraySize; i++)
                {
                    var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                    {
                        if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                        var fso = new SerializedObject(f);
                        SetFloat(fso, "m_Settings.Intensity", 1.1f);
                        SetFloat(fso, "m_Settings.Radius", 0.45f);
                        SetFloat(fso, "m_Settings.DirectLightingStrength", 0.45f);
                        SetFloat(fso, "m_Settings.Falloff", 60f);
                        SetInt(fso, "m_Settings.Samples", 0);          // qualità alta
                        SetInt(fso, "m_Settings.Downsample", 0);
                        fso.ApplyModifiedPropertiesWithoutUndo();
                        f.SetActive(true);
                        EditorUtility.SetDirty(f);
                    }
                    EditorUtility.SetDirty(data);
                }
            EditorUtility.SetDirty(rp);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Grafica] Pipeline '{rp.name}' aggiornata: HDR, ombre 4K, SSAO più marcato");
        }

        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) { if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v != 0; else p.intValue = v; } }
        static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }

        // ------------------------------------------------------------------ camera

        public static void SetupCamera(Camera cam)
        {
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.stopNaN = true;
            data.dithering = true;  // niente bande nei gradienti scuri
            // i volumi di post-processing stanno sul layer Ignore Raycast (2): la camera deve vederli
            data.volumeLayerMask = (1 << 0) | (1 << 2);
            data.volumeTrigger = cam.transform;
        }

        // ------------------------------------------------------------------ volumi di post-processing

        /// <summary>Il volume globale della notte, quello caldo dentro casa e quello cupo della cantina.</summary>
        public static void BuildVolumes(Transform parent, Bounds house, Bounds basement)
        {
            EditorAssetUtil.EnsureFolder(Folder);

            // notte: freddo, contrastato, bagliori morbidi, grana da videocassetta anni '90
            var night = NewProfile("Notte");
            Add<Tonemapping>(night).mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>(night);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));
            bloom.highQualityFiltering.Override(true);
            var ca = Add<ColorAdjustments>(night);
            ca.postExposure.Override(0.55f);
            ca.contrast.Override(14f);
            ca.saturation.Override(-12f);
            var wb = Add<WhiteBalance>(night);
            wb.temperature.Override(-12f);
            wb.tint.Override(4f);
            var smh = Add<ShadowsMidtonesHighlights>(night);
            smh.shadows.Override(new Vector4(0.92f, 0.97f, 1.12f, -0.02f));   // ombre bluastre
            smh.highlights.Override(new Vector4(1.08f, 1.0f, 0.9f, 0f));     // luci calde
            var vig = Add<Vignette>(night);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.01f, 0.01f, 0.02f));
            var grain = Add<FilmGrain>(night);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.75f);
            Add<ChromaticAberration>(night).intensity.Override(0.06f);
            Save(night);
            MakeVolume("Post-processing: notte", parent, night, true, default, 0, 0f);

            // dentro casa: più caldo e luminoso, meno freddo (le lampade a incandescenza)
            var inside = NewProfile("Dentro casa");
            var ca2 = Add<ColorAdjustments>(inside);
            ca2.postExposure.Override(0.35f);
            ca2.saturation.Override(-4f);
            var wb2 = Add<WhiteBalance>(inside);
            wb2.temperature.Override(8f);
            Add<Vignette>(inside).intensity.Override(0.28f);
            Save(inside);
            MakeVolume("Post-processing: dentro casa", parent, inside, false, house, 1, 1.2f);

            // cantina: cupa, desaturata, grana più forte, vignetta stretta
            var cellar = NewProfile("Cantina");
            var ca3 = Add<ColorAdjustments>(cellar);
            ca3.postExposure.Override(0.2f);
            ca3.saturation.Override(-30f);
            ca3.contrast.Override(22f);
            var wb3 = Add<WhiteBalance>(cellar);
            wb3.temperature.Override(-6f);
            wb3.tint.Override(8f);       // un filo di verde malato
            var v3 = Add<Vignette>(cellar);
            v3.intensity.Override(0.45f);
            v3.smoothness.Override(0.5f);
            var g3 = Add<FilmGrain>(cellar);
            g3.type.Override(FilmGrainLookup.Thin2);
            g3.intensity.Override(0.45f);
            Save(cellar);
            MakeVolume("Post-processing: cantina", parent, cellar, false, basement, 2, 0.8f);
        }

        static VolumeProfile NewProfile(string name)
        {
            string path = $"{Folder}/Volume {name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = name;
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            T c = p.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void Save(VolumeProfile p)
        {
            foreach (var c in p.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
        }

        static void MakeVolume(string name, Transform parent, VolumeProfile profile, bool global, Bounds bounds, int priority, float blend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = 2; // Ignore Raycast: i trigger non intralciano le interazioni né la NavMesh
            if (!global)
            {
                go.transform.position = bounds.center;
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = bounds.size;
            }
            var v = go.AddComponent<Volume>();
            v.isGlobal = global;
            v.sharedProfile = profile;
            v.priority = priority;
            v.blendDistance = blend;
        }

        // ------------------------------------------------------------------ coni di luce

        static Mesh coneMesh;
        static readonly Dictionary<string, Material> coneMats = new Dictionary<string, Material>();

        /// <summary>Cono di luce sotto una lampada: punta in 'localPos', alto 'height', largo 'radius' alla base.</summary>
        public static GameObject LightCone(Transform parent, Vector3 localPos, float height, float radius, Color color, float intensity)
        {
            var go = new GameObject("Cono di luce");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ConeMaterial(color, intensity);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            Cones.Add(go);
            return go;
        }

        /// <summary>Un materiale per ogni colore/intensità (salvati come asset: i property block non restano nella scena).</summary>
        static Material ConeMaterial(Color color, float intensity)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + "_" + Mathf.RoundToInt(intensity * 1000f);
            if (coneMats.TryGetValue(key, out Material cached) && cached != null) return cached;
            string path = $"{Folder}/Cono di luce {key}.mat";
            EditorAssetUtil.EnsureFolder(Folder);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("FantaLandia/LightCone");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.SetColor("_Color", color);
            m.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(m);
            coneMats[key] = m;
            return m;
        }

        /// <summary>Cono aperto alto 1 e largo 1 (raggio) con la punta nell'origine, verso il basso; uv.y = 0 in punta, 1 alla base.</summary>
        static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            string path = $"{Folder}/Cono di luce.asset";
            EditorAssetUtil.EnsureFolder(Folder);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var m = existing != null ? existing : new Mesh();
            m.Clear();
            m.name = "Cono di luce";
            const int seg = 28, rings = 6;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(dir * t + Vector3.down * t);
                    norms.Add((dir + Vector3.up).normalized);
                    uvs.Add(new Vector2(s / (float)seg, t));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int i0 = r * (seg + 1) + s, i1 = i0 + seg + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i0 + 1);
                    tris.Add(i0 + 1); tris.Add(i1); tris.Add(i1 + 1);
                }
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            coneMesh = m;
            return m;
        }

        // ------------------------------------------------------------------ pozzanghere

        /// <summary>
        /// Una pozzanghera dalla forma irregolare (un "blob" a bordi morbidi), piatta sul pavimento: dentro l'acqua scura e
        /// lucida, intorno un alone un po' più grande di cemento bagnato. Senza collider.
        /// </summary>
        public static void Puddle(Transform parent, Vector3 center, float radius, int seed, Material water, Material wet)
        {
            var go = new GameObject("Pozza");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, seed * 37f, 0f);
            Add(go.transform, "Alone bagnato", BlobMesh(seed, radius * 1.25f, 0.35f, "Pozza alone " + seed), wet, -0.0015f);
            Add(go.transform, "Acqua", BlobMesh(seed, radius, 0.3f, "Pozza " + seed), water, 0f);

            static void Add(Transform p, string n, Mesh m, Material mat, float y)
            {
                var g = new GameObject(n);
                g.transform.SetParent(p, false);
                g.transform.localPosition = new Vector3(0f, y, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>Disco irregolare (raggio che varia con alcune onde), salvato come asset.</summary>
        static Mesh BlobMesh(int seed, float radius, float wobble, string name)
        {
            string path = $"{Folder}/{name}.asset";
            EditorAssetUtil.EnsureFolder(Folder);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var m = existing != null ? existing : new Mesh();
            m.Clear();
            m.name = name;
            const int seg = 40;
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6f, p2 = (float)rng.NextDouble() * 6f, p3 = (float)rng.NextDouble() * 6f;
            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float r = radius * (1f + wobble * (0.5f * Mathf.Sin(a * 2f + p1) + 0.3f * Mathf.Sin(a * 3f + p2) + 0.2f * Mathf.Sin(a * 5f + p3)));
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++) { tris.Add(0); tris.Add(1 + (i + 1) % seg); tris.Add(1 + i); }
            var norms = new List<Vector3>();
            foreach (var _ in verts) norms.Add(Vector3.up);
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ polvere

        static Material dustMat;

        /// <summary>Polvere sospesa in una stanza (box in coordinate mondo): puntini che fluttuano piano.</summary>
        public static void Dust(Transform parent, string name, Bounds room, int count)
        {
            if (dustMat == null)
            {
                string path = $"{Folder}/Polvere.mat";
                EditorAssetUtil.EnsureFolder(Folder);
                dustMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Shader sh = Shader.Find("FantaLandia/Dust");
                if (dustMat == null) { dustMat = new Material(sh); AssetDatabase.CreateAsset(dustMat, path); }
                else dustMat.shader = sh;
            }
            var go = new GameObject("Polvere: " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = room.center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.9f, 0.8f), new Color(1f, 0.95f, 0.85f, 0.4f));
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.0005f;
            var em = ps.emission;
            em.rateOverTime = count / 14f;
            var sh2 = ps.shape;
            sh2.shapeType = ParticleSystemShapeType.Box;
            sh2.scale = room.size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ torcia

        /// <summary>Cookie della torcia: centro caldo, anello più luminoso, alone irregolare e qualche macchia (lente sporca).</summary>
        public static Texture2D FlashlightCookie()
        {
            string path = $"{Folder}/Cookie torcia.asset";
            EditorAssetUtil.EnsureFolder(Folder);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool create = tex == null;
            const int size = 256;
            if (create) tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Cookie torcia" };
            tex.wrapMode = TextureWrapMode.Clamp;
            var rng = new System.Random(5);
            var blots = new List<Vector3>();
            for (int i = 0; i < 9; i++) blots.Add(new Vector3((float)rng.NextDouble() * 1.2f - 0.6f, (float)rng.NextDouble() * 1.2f - 0.6f, 0.05f + (float)rng.NextDouble() * 0.12f));
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float core = Mathf.Exp(-r * r * 9f);                               // centro
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.42f) / 0.06f, 2f)) * 0.35f; // anello del riflettore
                    float halo = Mathf.Clamp01(1f - r) * 0.35f;                         // alone
                    float ang = Mathf.Atan2(v, u);
                    halo *= 0.8f + 0.2f * Mathf.Sin(ang * 7f + r * 12f);                 // alone irregolare
                    float val = core + ring + halo;
                    foreach (var b in blots)
                    {
                        float d = Mathf.Sqrt((u - b.x) * (u - b.x) + (v - b.y) * (v - b.y));
                        val *= 1f - 0.18f * Mathf.Exp(-(d * d) / (b.z * b.z));         // sporco sulla lente
                    }
                    val *= Mathf.Clamp01((1f - r) * 6f);                                // bordo del fascio
                    val = Mathf.Clamp01(val);
                    px[y * size + x] = new Color(val, val, val, val);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            if (create) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }
    }
}
