using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.Core;
using UnityEngine;
using UnityEngine.AI;

namespace FantaLandia.Player
{
    /// <summary>
    /// Comandi stile strategico: clic sinistro / riquadro per selezionare, clic destro per muovere.
    /// Alt + clic destro consente i salti pericolosi (tetto, balcone).
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        public static SelectionController Instance { get; private set; }

        public Camera cam;
        public float dragThreshold = 8f;
        public float formationSpacing = 1.3f;

        readonly List<SelectableCharacter> selected = new List<SelectableCharacter>();
        public IReadOnlyList<SelectableCharacter> Selected => selected;

        /// <summary>Il personaggio seguito da camera e visibilità degli edifici.</summary>
        public SelectableCharacter Focus { get; private set; }

        Vector2 dragStart;
        bool pressing, dragging;
        Transform marker;
        Material markerMat;
        float markerTimer;

        void Awake()
        {
            Instance = this;
            if (cam == null) cam = Camera.main;
            CreateMarker();
        }

        void Start()
        {
            SelectableCharacter.All.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            if (SelectableCharacter.All.Count > 0) SetSelection(SelectableCharacter.All[0]);
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            HandleMouseSelection();
            if (GameInput.RightDown) IssueMoveOrder();
            if (GameInput.RunToggle) ToggleRun();

            int digit = GameInput.DigitPressed();
            if (digit > 0 && digit <= SelectableCharacter.All.Count)
            {
                var c = SelectableCharacter.All[digit - 1];
                if (GameInput.Shift) Add(c); else SetSelection(c);
            }

            UpdateMarker();
        }

        // ------------------------------------------------------------------ selezione

        void HandleMouseSelection()
        {
            Vector2 mouse = GameInput.MousePosition;
            if (GameInput.LeftDown)
            {
                pressing = true;
                dragging = false;
                dragStart = mouse;
            }
            if (pressing && !dragging && (mouse - dragStart).sqrMagnitude > dragThreshold * dragThreshold)
                dragging = true;

            if (pressing && GameInput.LeftUp)
            {
                pressing = false;
                if (dragging) BoxSelect(dragStart, mouse, GameInput.Shift);
                else ClickSelect(mouse, GameInput.Shift);
                dragging = false;
            }
        }

        void ClickSelect(Vector2 screen, bool additive)
        {
            SelectableCharacter hit = RaycastCharacter(screen);
            if (hit != null)
            {
                if (!additive) SetSelection(hit);
                else if (selected.Contains(hit)) Remove(hit);
                else Add(hit);
            }
            else if (!additive)
            {
                ClearSelection();
            }
        }

        SelectableCharacter RaycastCharacter(Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            SelectableCharacter best = null;
            float bestDist = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var c = h.collider.GetComponentInParent<SelectableCharacter>();
                if (c != null && h.distance < bestDist) { best = c; bestDist = h.distance; }
            }
            return best;
        }

        void BoxSelect(Vector2 a, Vector2 b, bool additive)
        {
            Rect rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (!additive) ClearSelection();
            foreach (var c in SelectableCharacter.All)
            {
                Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 0.9f);
                if (sp.z > 0f && rect.Contains(sp)) Add(c);
            }
        }

        public void SetSelection(SelectableCharacter c)
        {
            ClearSelection();
            Add(c);
        }

        public void Add(SelectableCharacter c)
        {
            if (!selected.Contains(c)) selected.Add(c);
            c.SetSelected(true);
            Focus = c;
        }

        public void Remove(SelectableCharacter c)
        {
            selected.Remove(c);
            c.SetSelected(false);
            if (Focus == c && selected.Count > 0) Focus = selected[0];
        }

        public void ClearSelection()
        {
            foreach (var c in selected) c.SetSelected(false);
            selected.Clear();
            // Focus resta l'ultimo personaggio: la camera continua a sapere su che piano siamo.
        }

        // ------------------------------------------------------------------ ordini

        void IssueMoveOrder()
        {
            if (selected.Count == 0) return;

            Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
            // I pezzi di edificio nascosti sono sul layer "Ignore Raycast", quindi il clic li attraversa.
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas)) return;

            bool dangerous = GameInput.Alt;
            Vector3 center = navHit.position;
            int n = selected.Count;
            float camYaw = cam.transform.eulerAngles.y;

            for (int i = 0; i < n; i++)
            {
                Vector3 target = center;
                if (i > 0)
                {
                    float angle = camYaw + 180f + (i - 1) * 360f / (n - 1);
                    Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * formationSpacing;
                    if (NavMesh.SamplePosition(candidate, out NavMeshHit h, 1.5f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - center.y) < 1f)
                        target = h.position;
                }
                selected[i].Motor.MoveTo(target, dangerous);
            }

            ShowMarker(center, dangerous);
        }

        void ToggleRun()
        {
            if (selected.Count == 0) return;
            bool run = !selected[0].Motor.IsRunning;
            foreach (var c in selected) c.Motor.SetRunning(run);
        }

        // ------------------------------------------------------------------ feedback visivo

        void CreateMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Segnalino destinazione";
            Destroy(go.GetComponent<Collider>());
            marker = go.transform;
            markerMat = go.GetComponent<Renderer>().material;
            go.SetActive(false);
        }

        void ShowMarker(Vector3 pos, bool dangerous)
        {
            marker.position = pos + Vector3.up * 0.03f;
            markerMat.color = dangerous ? new Color(1f, 0.45f, 0.1f) : new Color(0.3f, 1f, 0.4f);
            markerTimer = 0.8f;
            marker.gameObject.SetActive(true);
        }

        void UpdateMarker()
        {
            if (markerTimer <= 0f) return;
            markerTimer -= Time.deltaTime;
            float s = Mathf.Lerp(0.2f, 0.9f, markerTimer / 0.8f);
            marker.localScale = new Vector3(s, 0.01f, s);
            if (markerTimer <= 0f) marker.gameObject.SetActive(false);
        }

        void OnGUI()
        {
            if (!pressing || !dragging) return;
            Vector2 a = dragStart, b = GameInput.MousePosition;
            Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Screen.height - Mathf.Max(a.y, b.y),
                                     Mathf.Max(a.x, b.x), Screen.height - Mathf.Min(a.y, b.y));
            GUI.color = new Color(0.4f, 0.9f, 0.5f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.9f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, 1, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.yMin, 1, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
