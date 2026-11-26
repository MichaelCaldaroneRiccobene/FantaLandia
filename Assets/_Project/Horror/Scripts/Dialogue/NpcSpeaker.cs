using System.Collections.Generic;
using FantaLandia.Characters.Body;
using UnityEngine;

namespace FantaLandia.Horror.Dialogue
{
    /// <summary>
    /// Collega un personaggio ai dialoghi: quando parla la bocca segue le sillabe e le sopracciglia
    /// reagiscono a domande ed esclamazioni; quando parla qualcun altro lì vicino si gira a guardarlo.
    /// </summary>
    public class NpcSpeaker : MonoBehaviour
    {
        [Tooltip("Id del personaggio nei dialoghi (tom, jef, roy, vicino, vicina...)")]
        public string speakerId = "tom";
        [Tooltip("Distanza entro cui si gira verso chi parla")]
        public float listenDistance = 8f;

        static readonly List<NpcSpeaker> all = new List<NpcSpeaker>();
        FaceAnimator face;
        float releaseAt = -1f;

        public FaceAnimator Face => face;
        public Vector3 EyePoint => face != null ? face.EyePoint : transform.position + Vector3.up * 1.6f;

        /// <summary>Un oggetto vuoto che segue gli occhi del personaggio (bersaglio per gli sguardi altrui).</summary>
        public Transform LookPoint { get; private set; }

        public static NpcSpeaker Find(string id)
        {
            foreach (var n in all) if (n != null && string.Equals(n.speakerId, id, System.StringComparison.OrdinalIgnoreCase)) return n;
            return null;
        }

        void Awake()
        {
            face = GetComponentInChildren<FaceAnimator>();
            LookPoint = new GameObject("Punto sguardo").transform;
            LookPoint.SetParent(transform, false);
        }

        void OnEnable()
        {
            all.Add(this);
            DialogueSystem.SyllableSpoken += OnSyllable;
            DialogueSystem.LineStarted += OnLine;
            DialogueSystem.ConversationEnded += OnEnded;
        }

        void OnDisable()
        {
            all.Remove(this);
            DialogueSystem.SyllableSpoken -= OnSyllable;
            DialogueSystem.LineStarted -= OnLine;
            DialogueSystem.ConversationEnded -= OnEnded;
        }

        void LateUpdate()
        {
            if (LookPoint != null) LookPoint.position = EyePoint;
            if (releaseAt > 0f && Time.time > releaseAt && !DialogueSystem.IsBlocking)
            {
                releaseAt = -1f;
                if (face != null) { face.lookTarget = null; face.SetExpression(0f); }
            }
        }

        void OnSyllable(Speaker s)
        {
            if (face != null && s != null && s.id == speakerId) face.Pulse();
        }

        void OnLine(Speaker s, string text)
        {
            if (face == null || s == null) return;
            Transform player = Camera.main != null ? Camera.main.transform : null;

            if (s.id == speakerId)
            {
                // parlo io: guardo il giocatore; sopracciglia secondo il tono della frase
                face.lookTarget = player;
                face.SetExpression(text.Contains("?") ? 0.7f : text.Contains("!") ? 1f : text.Contains("...") ? -0.5f : 0.15f);
            }
            else
            {
                NpcSpeaker other = Find(s.id);
                Transform target = other != null ? other.LookPoint : player;
                if (target != null && Vector3.Distance(target.position, transform.position) <= listenDistance)
                {
                    face.lookTarget = target;
                    face.SetExpression(0f);
                }
            }
            releaseAt = Time.time + 4f;
        }

        void OnEnded() => releaseAt = Time.time + 1.5f;
    }
}
