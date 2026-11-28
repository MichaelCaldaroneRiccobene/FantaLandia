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

        /// <summary>
        /// La regia può bloccare lo sguardo (es. tutti fissano la creatura, o guardano Tom che fa da capo):
        /// finché è impostato, parlare o ascoltare non lo cambia e l'espressione resta 'holdBrow'.
        /// </summary>
        [System.NonSerialized] public Transform holdLook;
        [System.NonSerialized] public float holdBrow;

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

        ProceduralBodyAnimator anim;
        FantaLandia.Characters.CharacterMotor motor;
        float gestureStart = -10f, gestureEnd = -10f;
        int gestureSide = 1;

        void Awake()
        {
            anim = GetComponentInChildren<ProceduralBodyAnimator>();
            motor = GetComponent<FantaLandia.Characters.CharacterMotor>();
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

        /// <summary>La mano che gesticola: davanti al petto, palmo in su, si muove un po' mentre parla.</summary>
        void Update()
        {
            if (anim == null || Time.time > gestureEnd + 0.5f) return;
            var actor = GetComponent<FantaLandia.Horror.Story.NpcActor>();
            if (actor != null && actor.IsLeaning) return;
            if (motor != null && motor.IsTraversing) return;
            float w = Mathf.Clamp01((Time.time - gestureStart) / 0.35f) * Mathf.Clamp01((gestureEnd + 0.5f - Time.time) / 0.5f);
            if (w <= 0f) return;
            bool seated = motor != null && motor.IsSeated;
            Transform t = transform;
            float wob = Mathf.Sin((Time.time - gestureStart) * 5f);
            Vector3 hand = t.position + Vector3.up * ((seated ? 0.9f : 1.12f) + wob * 0.04f) + t.forward * (0.32f + wob * 0.03f) + t.right * (gestureSide * 0.2f);
            Vector3 pole = t.right * gestureSide * 0.8f + Vector3.down * 0.5f - t.forward * 0.2f;
            anim.SetExternalHandIK(gestureSide, hand, pole, w * 0.85f, null);
        }

        void LateUpdate()
        {
            if (LookPoint != null) LookPoint.position = EyePoint;
            if (holdLook != null && face != null) { face.lookTarget = holdLook; face.SetExpression(holdBrow); releaseAt = -1f; return; }
            if (releaseAt > 0f && Time.time > releaseAt && !DialogueSystem.IsTalking)
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
            if (face == null || s == null || holdLook != null) return;
            Transform player = Camera.main != null ? Camera.main.transform : null;

            if (s.id == speakerId)
            {
                // a volte, parlando, gesticola con una mano
                if (Random.value < 0.6f && text.Length > 12)
                {
                    gestureStart = Time.time;
                    gestureEnd = Time.time + Mathf.Clamp(text.Length * 0.05f, 1.2f, 3f);
                    gestureSide = Random.value < 0.5f ? -1 : 1;
                }
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
