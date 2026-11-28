using FantaLandia.Core;
using FantaLandia.Horror.Dialogue;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>
    /// Sul giocatore: guarda cosa c'è al centro dello schermo, mostra mirino e scritta, interagisce con E.
    /// Tiene anche l'oggetto in mano (es. la confezione di birra).
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        public Camera cam;
        public float reach = 2.3f;
        [Tooltip("Punto davanti alla camera dove si tengono gli oggetti in mano")]
        public Transform holdPoint;
        [Tooltip("Oggetto già in mano all'inizio (es. la birra)")]
        public PickupItem startHeld;

        [Tooltip("Il corpo del giocatore: gli oggetti 'a mano' vanno nella mano sinistra")]
        public FantaLandia.Characters.Body.BodyRig body;

        public PickupItem Held { get; private set; }
        public Interactable Target { get; private set; }

        /// <summary>Le interazioni possono essere sospese durante scene e dialoghi.</summary>
        public bool interactionEnabled = true;

        GUIStyle promptStyle;

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (startHeld != null) Hold(startHeld, true);
        }

        void Update()
        {
            Target = null;
            if (!interactionEnabled || DialogueSystem.IsBlocking || cam == null || FantaLandia.Horror.UI.IntroSequence.Active || FantaLandia.Horror.UI.NotebookMenu.IsOpen) return;

            // Il giocatore sta sul layer "Ignore Raycast": il raggio non colpisce il suo corpo.
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it != null && it.CanInteract(this)) Target = it;
            }

            if (Target != null && GameInput.Interact)
            {
                Interactable used = Target;
                Target = null; // lo stato cambia: il bersaglio si ricalcola al prossimo frame
                used.Interact(this);
            }
        }

        public void Hold(PickupItem item, bool instant = false)
        {
            if (Held != null || item == null) return;
            Held = item;
            Transform wrist = body != null ? body.Joint(FantaLandia.Characters.Body.BodyPart.HandL) : null;
            if (item.carryInLeftHand && wrist != null)
            {
                item.AttachToHand(wrist, instant);
                var anim = body.GetComponent<FantaLandia.Characters.Body.ProceduralBodyAnimator>();
                if (anim != null) anim.carryLeft = true;
            }
            else item.AttachTo(holdPoint != null ? holdPoint : cam.transform, instant);
        }

        /// <summary>Toglie l'oggetto dalle mani (lo prende in carico chi lo riceve).</summary>
        public PickupItem Release()
        {
            PickupItem item = Held;
            Held = null;
            if (body != null)
            {
                var anim = body.GetComponent<FantaLandia.Characters.Body.ProceduralBodyAnimator>();
                if (anim != null) anim.carryLeft = false;
            }
            return item;
        }

        void OnGUI()
        {
            GUI.depth = -5; // il mirino e la scritta (sempre sopra all'immagine dell'effetto retrò, che sta a 100)
            if (DialogueSystem.IsBlocking || FantaLandia.Horror.UI.IntroSequence.Active || FantaLandia.Horror.UI.NotebookMenu.IsOpen) return;
            // mirino: un puntino morbido, che si allarga (con un anello) se c'è qualcosa da usare
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (Target != null) FantaLandia.Horror.UI.UiStyle.Dot(center, 16f, new Color(1f, 0.92f, 0.75f, 0.25f));
            FantaLandia.Horror.UI.UiStyle.Dot(center, Target != null ? 7f : 4f, new Color(1f, 1f, 1f, Target != null ? 0.95f : 0.55f));

            if (Target == null || !Target.CanInteract(this)) return;
            // scritta: tasto E disegnato + azione, a mano, con l'ombra
            var st = FantaLandia.Horror.UI.UiStyle.Text(22, TextAnchor.MiddleLeft, false);
            string label = FantaLandia.Horror.UI.Loc.T(Target.Prompt(this));
            float tw = st.CalcSize(new GUIContent(label)).x;
            float total = 30f + 10f + tw;
            float x = center.x - total * 0.5f, y = center.y + 26f;
            FantaLandia.Horror.UI.UiStyle.Panel(new Rect(x - 12f, y - 6f, total + 24f, 40f), new Color(0.03f, 0.035f, 0.06f, 0.45f), 12);
            float kw = FantaLandia.Horror.UI.UiStyle.KeyCap(x, y, "E", 28f);
            FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(x + kw + 10f, y - 2f, tw + 10f, 30f), label, st, new Color(0.98f, 0.96f, 0.9f));
        }
    }
}
