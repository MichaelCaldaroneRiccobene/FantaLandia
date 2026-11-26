using System.Collections.Generic;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.World;
using UnityEditor;
using UnityEngine;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Mattoncini per costruire case: scatole, muri con porte e finestre (con cornici e vetri),
    /// solai con buchi per le scale, scale, tetti a gradoni, staccionate, luci.
    /// </summary>
    public static class HouseKit
    {
        public static Material M(string name, string hex, float smooth = 0.1f)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return EditorAssetUtil.Mat("Casa " + name, c, smooth);
        }

        public static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        public static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
        {
            Vector3 lo = Vector3.Min(min, max), hi = Vector3.Max(min, max);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (lo + hi) * 0.5f;
            go.transform.localScale = hi - lo;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject BoxIf(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
        {
            Vector3 s = max - min;
            if (Mathf.Abs(s.x) < 0.005f || Mathf.Abs(s.y) < 0.005f || Mathf.Abs(s.z) < 0.005f) return null;
            return Box(name, parent, min, max, mat);
        }

        public static GameObject Floor(string name, Transform parent, Vector3 min, Vector3 max, Material mat, Surface surface)
        {
            var go = Box(name, parent, min, max, mat);
            go.AddComponent<FootstepSurface>().surface = surface;
            return go;
        }

        public static GameObject Place(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        public static Light PointLight(Transform parent, string name, Vector3 localPos, float range, float intensity, Color color, bool shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = localPos;
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static readonly Color Warm = new Color(1f, 0.82f, 0.58f);

        // ------------------------------------------------------------------ muri con aperture

        public enum OpeningKind { Door, Window, Arch }

        public struct Opening
        {
            public float a, b, bottom, top;
            public OpeningKind kind;
            public Opening(float a, float b, float bottom, float top, OpeningKind kind) { this.a = a; this.b = b; this.bottom = bottom; this.top = top; this.kind = kind; }
        }

        public static Opening Door(float center, float width = 0.9f, float height = 2.15f) => new Opening(center - width * 0.5f, center + width * 0.5f, 0f, height, OpeningKind.Door);
        public static Opening Arch(float a, float b, float height = 2.4f) => new Opening(a, b, 0f, height, OpeningKind.Arch);
        public static Opening Window(float center, float width, float bottom = 0.85f, float top = 2.15f) => new Opening(center - width * 0.5f, center + width * 0.5f, bottom, top, OpeningKind.Window);

        /// <summary>
        /// Muro dritto da 'from' a 'to' lungo X (alongX) o Z, centrato su 'fixedCoord', con aperture.
        /// Le finestre hanno vetro e cornice; porte e archi solo la cornice (se 'trim' non è null).
        /// </summary>
        public static void Wall(Transform parent, string name, bool alongX, float fixedCoord, float from, float to, float y0, float height,
                                float thick, Material mat, IList<Opening> openings, Material glass = null, Material trim = null)
        {
            var list = new List<Opening>(openings ?? new Opening[0]);
            list.Sort((p, q) => p.a.CompareTo(q.a));
            int n = 0;

            void Seg(float s, float e, float lo, float hi, Material m, float t, string label)
            {
                if (e - s < 0.01f || hi - lo < 0.01f) return;
                Vector3 min = alongX ? new Vector3(s, y0 + lo, fixedCoord - t * 0.5f) : new Vector3(fixedCoord - t * 0.5f, y0 + lo, s);
                Vector3 max = alongX ? new Vector3(e, y0 + hi, fixedCoord + t * 0.5f) : new Vector3(fixedCoord + t * 0.5f, y0 + hi, e);
                Box($"{name} {label} {++n}", parent, min, max, m);
            }

            float cursor = from;
            foreach (var o in list)
            {
                Seg(cursor, o.a, 0f, height, mat, thick, "");
                Seg(o.a, o.b, 0f, o.bottom, mat, thick, "");
                Seg(o.a, o.b, o.top, height, mat, thick, "");
                cursor = o.b;

                if (o.kind == OpeningKind.Window && glass != null)
                    Seg(o.a, o.b, o.bottom, o.top, glass, 0.03f, "vetro");
                if (trim != null)
                {
                    const float w = 0.08f;
                    float tt = thick + 0.12f;
                    Seg(o.a - w, o.a, o.bottom, o.top + w, trim, tt, "cornice");
                    Seg(o.b, o.b + w, o.bottom, o.top + w, trim, tt, "cornice");
                    Seg(o.a, o.b, o.top, o.top + w, trim, tt, "cornice");
                    if (o.kind == OpeningKind.Window)
                    {
                        Seg(o.a - w, o.b + w, o.bottom - w, o.bottom, trim, tt + 0.06f, "davanzale");
                        Seg((o.a + o.b) * 0.5f - 0.03f, (o.a + o.b) * 0.5f + 0.03f, o.bottom, o.top, trim, 0.05f, "traverso");
                        Seg(o.a, o.b, (o.bottom + o.top) * 0.5f - 0.03f, (o.bottom + o.top) * 0.5f + 0.03f, trim, 0.05f, "traverso");
                    }
                }
            }
            Seg(cursor, to, 0f, height, mat, thick, "");
        }

        // ------------------------------------------------------------------ solai, scale, tetti

        /// <summary>Solaio rettangolare con buchi (es. per le scale). I buchi sono Rect(xMin, zMin, larghezza, profondità).</summary>
        public static void Slab(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float y1, Material mat, params Rect[] holes)
        {
            var xs = new List<float> { x0, x1 };
            var zs = new List<float> { z0, z1 };
            foreach (var h in holes) { xs.Add(h.xMin); xs.Add(h.xMax); zs.Add(h.yMin); zs.Add(h.yMax); }
            xs.Sort(); zs.Sort();
            int n = 0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float a = xs[i], b = xs[i + 1];
                if (b - a < 0.01f || a < x0 - 0.001f || b > x1 + 0.001f) continue;
                // unisce in strisce lungo Z le celle piene consecutive
                float runStart = float.NaN;
                for (int j = 0; j < zs.Count - 1; j++)
                {
                    float c = zs[j], d = zs[j + 1];
                    if (d - c < 0.01f || c < z0 - 0.001f || d > z1 + 0.001f) continue;
                    float cx = (a + b) * 0.5f, cz = (c + d) * 0.5f;
                    bool hole = false;
                    foreach (var h in holes) if (h.Contains(new Vector2(cx, cz))) { hole = true; break; }
                    if (!hole && float.IsNaN(runStart)) runStart = c;
                    if (hole && !float.IsNaN(runStart))
                    {
                        Box($"{name} {++n}", parent, new Vector3(a, y0, runStart), new Vector3(b, y1, c), mat);
                        runStart = float.NaN;
                    }
                }
                if (!float.IsNaN(runStart)) Box($"{name} {++n}", parent, new Vector3(a, y0, runStart), new Vector3(b, y1, zs[zs.Count - 1] > z1 ? z1 : zs[zs.Count - 1]), mat);
            }
        }

        /// <summary>
        /// Scala piena. I gradini avanzano lungo X o Z a partire da 'start' nel verso 'dir' (±1);
        /// 'rise' positivo sale, negativo scende. Ogni gradino è un blocco da 'blockBottom' alla sua quota.
        /// </summary>
        public static void Stairs(Transform parent, string name, bool alongX, float start, int dir, float crossMin, float crossMax,
                                  float baseY, int steps, float rise, float run, float blockBottom, Material mat, Surface surface)
        {
            Transform g = Group(name, parent);
            for (int i = 0; i < steps; i++)
            {
                float a = start + dir * i * run, b = start + dir * (i + 1) * run;
                float top = baseY + (i + 1) * rise;
                Vector3 min = alongX ? new Vector3(a, blockBottom, crossMin) : new Vector3(crossMin, blockBottom, a);
                Vector3 max = alongX ? new Vector3(b, top, crossMax) : new Vector3(crossMax, top, b);
                Floor($"Gradino {i + 1}", g, min, max, mat, surface);
            }
        }

        /// <summary>Tetto a due falde fatto a gradoni di cubetti. Colmo lungo X (ridgeAlongX) o lungo Z.</summary>
        public static void SteppedRoof(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, int layers, float layerH,
                                       bool ridgeAlongX, Material mat, Material gable = null)
        {
            Transform g = Group(name, parent);
            float span = ridgeAlongX ? (z1 - z0) : (x1 - x0);
            float step = span * 0.5f / layers;
            for (int k = 0; k < layers; k++)
            {
                float y = y0 + k * layerH;
                if (ridgeAlongX) Box($"Falda {k + 1}", g, new Vector3(x0, y, z0 + k * step), new Vector3(x1, y + layerH, z1 - k * step), mat);
                else Box($"Falda {k + 1}", g, new Vector3(x0 + k * step, y, z0), new Vector3(x1 - k * step, y + layerH, z1), mat);
            }
            if (gable == null) return;
            // timpani (le pareti triangolari) leggermente rientrati
            for (int k = 0; k < layers; k++)
            {
                float y = y0 + k * layerH;
                if (ridgeAlongX)
                {
                    Box($"Timpano A {k + 1}", g, new Vector3(x0 + 0.35f, y, z0 + k * step + 0.35f), new Vector3(x0 + 0.37f, y + layerH, z1 - k * step - 0.35f), gable);
                    Box($"Timpano B {k + 1}", g, new Vector3(x1 - 0.37f, y, z0 + k * step + 0.35f), new Vector3(x1 - 0.35f, y + layerH, z1 - k * step - 0.35f), gable);
                }
            }
        }

        // ------------------------------------------------------------------ staccionate

        /// <summary>Staccionata dritta da a a b (a terra), con pali ogni ~2 m, assi e corrimano.</summary>
        public static void Fence(Transform parent, string name, Vector3 a, Vector3 b, float height, Material wood, Material post, bool privacy)
        {
            Transform g = Group(name, parent);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Vector3 dir = d / len;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.04f;
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = a + dir * (len * i / posts);
                Box("Palo", g, p + new Vector3(-0.06f, 0f, -0.06f), p + new Vector3(0.06f, height + 0.1f, 0.06f), post);
            }
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
            bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.z);
            if (privacy)
            {
                Vector3 min = alongX ? new Vector3(lo.x, 0.05f, a.z - 0.03f) : new Vector3(a.x - 0.03f, 0.05f, lo.z);
                Vector3 max = alongX ? new Vector3(hi.x, height, a.z + 0.03f) : new Vector3(a.x + 0.03f, height, hi.z);
                Box("Assi", g, min, max, wood);
            }
            else
            {
                // staccionata bassa a stecche (picket fence)
                for (float t = 0.1f; t < len - 0.05f; t += 0.22f)
                {
                    Vector3 p = a + dir * t;
                    Box("Stecca", g, p + new Vector3(-0.045f, 0f, -0.02f) + side * 0f, p + new Vector3(0.045f, height, 0.02f), wood);
                }
                Vector3 r0 = alongX ? new Vector3(lo.x, height * 0.6f, a.z - 0.04f) : new Vector3(a.x - 0.04f, height * 0.6f, lo.z);
                Vector3 r1 = alongX ? new Vector3(hi.x, height * 0.6f + 0.06f, a.z + 0.04f) : new Vector3(a.x + 0.04f, height * 0.6f + 0.06f, hi.z);
                Box("Traversa", g, r0, r1, wood);
            }
        }
    }
}
