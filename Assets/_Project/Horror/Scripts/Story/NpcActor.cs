using System.Collections;
using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Horror.Dialogue;
using UnityEngine;

namespace FantaLandia.Horror.Story
{
    /// <summary>
    /// Azioni "da regia" per un personaggio della storia: camminare fino a un punto, girarsi, guardare qualcuno,
    /// sedersi/alzarsi, appoggiarsi a una ringhiera. Le sequenze le usano con yield return.
    /// Può anche dire ogni tanto delle frasi di sottofondo quando il giocatore è vicino.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class NpcActor : MonoBehaviour
    {
        [Header("Appoggiato a una ringhiera all'avvio")]
        public bool leanOnStart;
        [Tooltip("Punto sul corrimano davanti al personaggio")]
        public Vector3 leanPoint;

        [Header("Chiacchiere di sottofondo")]
        [Tooltip("Frasi dette ogni tanto quando il giocatore è vicino (formato: id|testo, es. martha|Che freddo stasera)")]
        public string[] ambientLines;
        public float ambientDistance = 12f;
        public Vector2 ambientInterval = new Vector2(8f, 15f);

        public CharacterMotor Motor { get; private set; }
        public FaceAnimator Face { get; private set; }
        public bool IsLeaning { get; private set; }

        ProceduralBodyAnimator anim;
        Vector3 railPoint;
        float lean;
        float nextLine;
        int lineIndex;

        void Awake()
        {
            Motor = GetComponent<CharacterMotor>();
            anim = GetComponentInChildren<ProceduralBodyAnimator>();
            Face = GetComponentInChildren<FaceAnimator>();
            nextLine = Time.time + Random.Range(ambientInterval.x, ambientInterval.y);
        }

        IEnumerator Start()
        {
            yield return null; // dopo che il motore ha attivato l'agente
            if (leanOnStart) StartLeaning(leanPoint);
        }

        // ------------------------------------------------------------------ azioni

        public IEnumerator WalkTo(Vector3 point, Gait gait = Gait.Walk, float timeout = 20f)
        {
            StopLeaning();
            Motor.SetGait(gait);
            Motor.MoveTo(point, false);
            float t = 0f;
            while (t < timeout)
            {
                Vector3 d = transform.position - point;
                d.y = 0f;
                if (d.magnitude < 0.35f && Motor.CurrentSpeed < 0.2f) yield break;
                var ag = Motor.Agent;
                if (t > 0.5f && ag != null && ag.enabled && !ag.pathPending && ag.remainingDistance < 0.3f && Motor.CurrentSpeed < 0.2f) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator FaceTowards(Vector3 point, float duration = 0.5f)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;
            Quaternion from = transform.rotation, to = Quaternion.LookRotation(dir);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
                transform.rotation = Quaternion.Slerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
        }

        public IEnumerator SitOn(Transform seat, Vector3 frontPoint, float seatHeight = 0.5f)
        {
            yield return WalkTo(frontPoint);
            Motor.SitDown(seat.position, seat.rotation, seatHeight);
            yield return new WaitForSeconds(2.4f);
        }

        public void StandUp(Vector3 standPoint) => Motor.StandUp(standPoint);

        /// <summary>Guarda qualcuno (null = sguardo libero).</summary>
        public void LookAt(Transform target)
        {
            if (Face != null) Face.lookTarget = target;
        }

        /// <summary>Si appoggia con le mani al corrimano davanti a sé (punto sul corrimano).</summary>
        public void StartLeaning(Vector3 pointOnRail)
        {
            railPoint = pointOnRail;
            IsLeaning = true;
            Vector3 dir = railPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        public void StopLeaning() => IsLeaning = false;

        void Update()
        {
            lean = Mathf.MoveTowards(lean, IsLeaning ? 1f : 0f, Time.deltaTime * 2f);
            if (anim != null)
            {
                anim.externalLean = 22f * lean;
                if (lean > 0.01f)
                {
                    Vector3 right = transform.right;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 hand = railPoint + right * (side * 0.24f) + Vector3.up * 0.04f - transform.forward * 0.04f;
                        Vector3 pole = right * side * 0.8f - transform.forward * 0.4f + Vector3.down * 0.3f;
                        anim.SetExternalHandIK(side, hand, pole, lean, ProceduralBodyAnimator.PalmDown(side, transform.forward));
                    }
                }
            }
            AmbientChatter();
        }

        void AmbientChatter()
        {
            if (ambientLines == null || ambientLines.Length == 0 || Time.time < nextLine || DialogueSystem.IsBlocking) return;
            nextLine = Time.time + Random.Range(ambientInterval.x, ambientInterval.y);
            Camera cam = Camera.main;
            if (cam == null || Vector3.Distance(cam.transform.position, transform.position) > ambientDistance) return;
            string line = ambientLines[lineIndex++ % ambientLines.Length];
            int bar = line.IndexOf('|');
            if (bar <= 0) return;
            DialogueSystem.Bark(Speakers.Find(line.Substring(0, bar)), line.Substring(bar + 1), transform.position + Vector3.up * 1.6f);
        }
    }
}
