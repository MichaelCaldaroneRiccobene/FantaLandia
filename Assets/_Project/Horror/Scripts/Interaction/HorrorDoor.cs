using System;
using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>
    /// Porta che si apre e si chiude con E, sempre dal lato opposto a chi la apre.
    /// Può essere chiusa a chiave e "bussata" dalla storia. Il +Z locale è il lato "dentro".
    /// </summary>
    public class HorrorDoor : Interactable
    {
        [Tooltip("Il cardine: ruota sull'asse Y")]
        public Transform hinge;
        [Tooltip("+1 se il cardine è a sinistra guardando verso +Z, -1 se è a destra")]
        public float hingeSign = 1f;
        public float openAngle = 100f;
        public float speed = 2.2f; // aperture al secondo
        public bool locked;
        public string lockedText = "È chiusa a chiave";
        [Tooltip("Se chiusa, da fuori si può bussare (la storia decide cosa succede)")]
        public bool knockable;
        [Tooltip("Punto dove si bussa, nello spazio della porta (lato esterno = -Z)")]
        public Vector3 knockLocal = new Vector3(0.45f, 1.62f, -0.03f);

        /// <summary>Il giocatore ha bussato (a gesto finito).</summary>
        public event Action<HorrorDoor> PlayerKnocked;

        public Vector3 KnockPointWorld => transform.TransformPoint(knockLocal);
        public Vector3 OutwardWorld => -transform.forward;

        public bool IsOpen => targetOpen;
        public event Action<HorrorDoor> Opened;
        public event Action<HorrorDoor> Closed;

        bool targetOpen;
        int openSide = 1;
        float t; // 0 chiusa .. 1 aperta

        [Tooltip("Si può aprire solo stando davanti alla porta (dentro casa), non di lato")]
        public bool requireFront;

        public override bool CanInteract(Interactor who)
        {
            if (!enabled) return false;
            if (!requireFront) return true;
            Vector3 local = transform.InverseTransformPoint(who.transform.position);
            // l'anta va da x 0 a ~0.9 (cardine in 0): davanti vuol dire in mezzo all'anta e a meno di 2 m, dal lato di dentro
            return Mathf.Abs(local.x - 0.45f) < 0.45f && local.z > 0.3f && local.z < 2f;
        }

        public override string Prompt(Interactor who)
        {
            if (locked && knockable && IsOutside(who)) return "Bussa";
            return locked ? lockedText : targetOpen ? "Chiudi la porta" : "Apri la porta";
        }

        bool IsOutside(Interactor who) => transform.InverseTransformPoint(who.transform.position).z < 0f;

        public void NotifyKnocked() => PlayerKnocked?.Invoke(this);

        public void Unlock() => locked = false;

        public override void Interact(Interactor who)
        {
            if (locked)
            {
                var actions = who.GetComponent<FantaLandia.Horror.Player.PlayerActions>();
                if (knockable && IsOutside(who) && actions != null) { actions.Knock(this); return; }
                HorrorAudio.PlayAt(ProceduralAudio.DoorLocked(), transform.position, 0.8f);
                return;
            }
            if (targetOpen) Close();
            else
            {
                // si apre dalla parte opposta rispetto a chi spinge
                float z = transform.InverseTransformPoint(who.transform.position).z;
                Open(z < 0f ? 1 : -1);
            }
        }

        public void Open(int side = 1)
        {
            if (targetOpen) return;
            openSide = side >= 0 ? 1 : -1;
            // se da quella parte c'è subito un muro o un mobile, si apre dall'altra (se lì c'è più spazio)
            if (freeAngle == null) MeasureFreeAngles();
            if (FreeAngle(openSide) < 60f && FreeAngle(-openSide) > FreeAngle(openSide)) openSide = -openSide;
            targetOpen = true;
            HorrorAudio.PlayAt(ProceduralAudio.DoorCreak(UnityEngine.Random.Range(0, 3)), transform.position, 0.5f, UnityEngine.Random.Range(0.9f, 1.1f));
            Opened?.Invoke(this);
        }

        public void Close()
        {
            if (!targetOpen) return;
            targetOpen = false;
            HorrorAudio.PlayAt(ProceduralAudio.DoorCreak(UnityEngine.Random.Range(0, 3)), transform.position, 0.35f, 1.2f);
        }

        /// <summary>Qualcuno bussa da fuori.</summary>
        public void Knock()
        {
            HorrorAudio.PlayAt(ProceduralAudio.Knock(), transform.position, 1f, UnityEngine.Random.Range(0.95f, 1.05f), 30f);
        }

        float[] freeAngle; // [0] lato +1, [1] lato -1

        float FreeAngle(int side) => freeAngle[side > 0 ? 0 : 1];

        /// <summary>
        /// Quanto si può aprire la porta da ciascun lato senza entrare in muri o mobili: fa ruotare un'anta "virtuale"
        /// a passi di 3° e controlla dei punti lungo il bordo (a tre altezze). Si ferma al primo ostacolo.
        /// </summary>
        void MeasureFreeAngles()
        {
            freeAngle = new[] { openAngle, openAngle };
            if (hinge == null) return;
            var own = new System.Collections.Generic.HashSet<Collider>(GetComponentsInChildren<Collider>(true));
            Quaternion closedHinge = hinge.localRotation;
            // larghezza dell'anta, misurata da chiusa
            float width = 0.8f;
            Vector3 along = hinge.parent.rotation * Vector3.right;
            foreach (var c in own)
            {
                Bounds b = c.bounds;
                width = Mathf.Max(width, Vector3.Dot(b.max - hinge.position, along), Vector3.Dot(b.min - hinge.position, along));
            }
            width = Mathf.Min(width, 1.2f);
            int mask = ~((1 << 2) | (1 << 7)); // né il giocatore né i personaggi
            var hits = new Collider[8];
            float baseY = hinge.position.y;
            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? 1 : -1;
                float free = openAngle;
                for (float a = 6f; a <= openAngle; a += 3f)
                {
                    Vector3 dir = hinge.parent.rotation * (Quaternion.Euler(0f, -hingeSign * side * a, 0f) * closedHinge * Vector3.right);
                    if (Blocked(dir)) { free = a - 6f; break; }
                }
                freeAngle[k] = Mathf.Max(free, 0f);
            }

            bool Blocked(Vector3 dir)
            {
                foreach (float d in new[] { 0.3f, 0.55f, width - 0.06f })
                    foreach (float y in new[] { 0.25f, 1.0f, 1.85f })
                    {
                        Vector3 p = new Vector3(hinge.position.x, baseY + y, hinge.position.z) + dir * d;
                        int n = Physics.OverlapSphereNonAlloc(p, 0.04f, hits, mask, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < n; i++)
                            if (!own.Contains(hits[i])) return true;
                    }
                return false;
            }
        }

        void Update()
        {
            float before = t;
            t = Mathf.MoveTowards(t, targetOpen ? 1f : 0f, speed * Time.deltaTime);
            float e = t * t * (3f - 2f * t);
            float angle = freeAngle != null ? FreeAngle(openSide) : openAngle;
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, -hingeSign * openSide * angle * e, 0f);
            if (before > 0f && t <= 0f)
            {
                HorrorAudio.PlayAt(ProceduralAudio.DoorShut(), transform.position, 0.8f);
                Closed?.Invoke(this);
            }
        }
    }
}
