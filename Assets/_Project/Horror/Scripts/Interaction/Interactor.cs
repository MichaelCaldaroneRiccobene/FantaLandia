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
            if (!interactionEnabled || DialogueSystem.IsBlocking || cam == null) return;

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
            if (DialogueSystem.IsBlocking) return;
            if (promptStyle == null)
                promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true };

            // mirino: un puntino, più grande se c'è qualcosa da usare
            float size = Target != null ? 8f : 4f;
            GUI.color = new Color(1f, 1f, 1f, Target != null ? 0.95f : 0.5f);
            GUI.DrawTexture(new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Target == null || !Target.CanInteract(this)) return;
            string text = $"<b>E</b>  {Target.Prompt(this)}";
            Rect r = new Rect(0f, Screen.height * 0.5f + 24f, Screen.width, 30f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), $"E  {Target.Prompt(this)}", promptStyle);
            GUI.color = Color.white;
            GUI.Label(r, text, promptStyle);
        }
    }
}
