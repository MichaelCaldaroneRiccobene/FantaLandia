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
        [Tooltip("Chiacchiera solo se il giocatore è fuori casa (i vicini non si sentono attraverso i muri)")]
        public bool onlyWhenPlayerOutside;
        /// <summary>Il giocatore è dentro una casa (lo aggiorna la regia della storia).</summary>
        public static bool PlayerIndoors;

        [Header("Vita di sottofondo")]
        [Tooltip("Da fermo, ogni tanto guarda qualcuno: il giocatore, gli altri, la TV")]
        public bool idleLook = true;
        [Tooltip("Se il giocatore gli sta addosso, fa un passo indietro")]
        public bool keepPersonalSpace = true;
        /// <summary>Qualcosa che tutti guardano volentieri (la TV del salotto).</summary>
        public static Transform SharedLookTarget;

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
            IdleLook();
            PersonalSpace();
        }

        float nextIdleLook;
        bool idleLooking;

        void IdleLook()
        {
            if (!idleLook || Face == null || DialogueSystem.IsTalking || Time.time < nextIdleLook) return;
            if (Face.lookTarget != null && !idleLooking) return; // lo sguardo l'ha deciso qualcun altro (la storia, un dialogo)
            nextIdleLook = Time.time + Random.Range(4f, 9f);
            Camera cam = Camera.main;
            var options = new System.Collections.Generic.List<Transform>();
            // il giocatore lo guardano solo ogni tanto, da vicino e se lo vedono davvero (non attraverso muri e finestre)
            if (cam != null && Random.value < 0.2f && Vector3.Distance(cam.transform.position, transform.position) < 5f
                && Face != null && !Physics.Linecast(Face.EyePoint, cam.transform.position, ~((1 << 2) | (1 << 7)), QueryTriggerInteraction.Ignore))
                options.Add(cam.transform);
            options.Add(null); // spesso lo sguardo vaga
            foreach (var other in FindObjectsByType<NpcSpeaker>(FindObjectsSortMode.None))
                if (other != null && other.transform != transform && other.LookPoint != null && Vector3.Distance(other.transform.position, transform.position) < 6f)
                    options.Add(other.LookPoint);
            if (SharedLookTarget != null && Vector3.Distance(SharedLookTarget.position, transform.position) < 7f) { options.Add(SharedLookTarget); options.Add(SharedLookTarget); }
            options.Add(null); // a volte lo sguardo vaga
            Face.lookTarget = options[Random.Range(0, options.Count)];
            idleLooking = Face.lookTarget != null;
        }

        float crowdedFor;
        float nextComplaint;

        void PersonalSpace()
        {
            if (!keepPersonalSpace || IsLeaning || Motor == null || Motor.IsSeated) return;
            var ag = Motor.Agent;
            if (ag == null || !ag.enabled || ag.pathPending || (ag.hasPath && ag.remainingDistance > 0.15f) || Motor.CurrentSpeed > 0.1f) { crowdedFor = 0f; return; }
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            d.y = 0f;
            crowdedFor = d.magnitude < 0.6f ? crowdedFor + Time.deltaTime : 0f;
            if (crowdedFor < 0.7f) return;
            crowdedFor = 0f;
            Vector3 away = d.sqrMagnitude > 0.001f ? d.normalized : -transform.forward;
            Motor.SetGait(Gait.Walk);
            Motor.MoveTo(transform.position + away * 0.8f, false);
            if (Time.time > nextComplaint)
            {
                nextComplaint = Time.time + 20f;
                var sp = GetComponent<NpcSpeaker>();
                string[] lines = { "Ehi, spazio vitale!", "Mi stai addosso, amico.", "...Sì?" };
                if (sp != null) DialogueSystem.Bark(Speakers.Find(sp.speakerId), lines[Random.Range(0, lines.Length)], sp.EyePoint);
            }
        }

        void AmbientChatter()
        {
            if (ambientLines == null || ambientLines.Length == 0 || Time.time < nextLine || DialogueSystem.IsTalking) return;
            if (onlyWhenPlayerOutside && PlayerIndoors) return;
            nextLine = Time.time + Random.Range(ambientInterval.x, ambientInterval.y);
            Camera cam = Camera.main;
            if (cam == null || Vector3.Distance(cam.transform.position, transform.position) > ambientDistance) return;
            string line = ambientLines[lineIndex++ % ambientLines.Length];
            int bar = line.IndexOf('|');
            if (bar <= 0) return;
            string who = line.Substring(0, bar);
            // la frase compare sopra la testa di chi la dice (Martha e Frank condividono la lista: cerca chi parla)
            NpcSpeaker speaker = NpcSpeaker.Find(who);
            Vector3 at = speaker != null ? speaker.EyePoint : transform.position + Vector3.up * 1.6f;
            DialogueSystem.Bark(Speakers.Find(who), line.Substring(bar + 1), at, true);
        }
    }
}
