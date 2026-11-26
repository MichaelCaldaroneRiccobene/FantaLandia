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

        public bool IsOpen => targetOpen;
        public event Action<HorrorDoor> Opened;
        public event Action<HorrorDoor> Closed;

        bool targetOpen;
        int openSide = 1;
        float t; // 0 chiusa .. 1 aperta

        public override string Prompt(Interactor who) => locked ? lockedText : targetOpen ? "Chiudi la porta" : "Apri la porta";

        public override void Interact(Interactor who)
        {
            if (locked)
            {
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

        void Update()
        {
            float before = t;
            t = Mathf.MoveTowards(t, targetOpen ? 1f : 0f, speed * Time.deltaTime);
            float e = t * t * (3f - 2f * t);
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, -hingeSign * openSide * openAngle * e, 0f);
            if (before > 0f && t <= 0f)
            {
                HorrorAudio.PlayAt(ProceduralAudio.DoorShut(), transform.position, 0.8f);
                Closed?.Invoke(this);
            }
        }
    }
}
