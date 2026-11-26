using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>
    /// Un posto a sedere (poltrona, un posto del divano). La posizione di questo oggetto è il punto a terra
    /// sotto il sedile, la rotazione è la direzione in cui si guarda da seduti. Ci si può sedere sia il giocatore
    /// (E: "Siediti") sia i personaggi; un posto occupato non lo prende nessun altro.
    /// </summary>
    public class Seat : Interactable
    {
        [Tooltip("Dove ci si mette in piedi prima di sedersi e dopo essersi alzati")]
        public Transform front;
        public float height = 0.5f;
        public string label = "Siediti";
        [Tooltip("Posti dello stesso gruppo (es. il salotto): se questo è occupato si cerca tra questi")]
        public string group = "salotto";

        /// <summary>Chi è seduto qui (un personaggio, il giocatore) o null.</summary>
        public Object Occupant { get; private set; }
        public bool IsFree => Occupant == null;
        public Vector3 FrontPosition => front != null ? front.position : transform.position + transform.forward * 0.75f;

        public static readonly List<Seat> All = new List<Seat>();
        public static event System.Action<Seat> Freed;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Claim(Object who)
        {
            if (Occupant != null && Occupant != who) return false;
            Occupant = who;
            return true;
        }

        public void Release(Object who)
        {
            if (Occupant != who) return;
            Occupant = null;
            Freed?.Invoke(this);
        }

        public override string Prompt(Interactor who) => label;

        public override bool CanInteract(Interactor who)
        {
            if (!enabled || Occupant != null) return false;
            var fpc = who.GetComponent<Player.FirstPersonController>();
            return fpc != null && !fpc.IsSeated;
        }

        public override void Interact(Interactor who)
        {
            var fpc = who.GetComponent<Player.FirstPersonController>();
            if (fpc != null) fpc.SitOn(this);
        }

        /// <summary>Il posto libero dello stesso gruppo più vicino a 'near' (null se sono tutti occupati).</summary>
        public static Seat FreeNear(string group, Vector3 near, Seat except = null)
        {
            Seat best = null;
            float bestD = float.MaxValue;
            foreach (var s in All)
            {
                if (s == except || !s.IsFree || s.group != group) continue;
                float d = (s.transform.position - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }
    }
}
