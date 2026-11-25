using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FantaLandia.Characters
{
    public enum TraversalKind { None, Vault, Climb, ClimbHigh, ClimbUp, Drop }

    /// <summary>Andatura. Lo scatto in città/insediamenti sarà sospetto per gli NPC (calo di reputazione).</summary>
    public enum Gait { Walk, Jog, Sprint }

    /// <summary>
    /// Movimento di un personaggio: cammina/corre sulla NavMesh e attraversa i NavMeshLink
    /// (scavalcare ostacoli, arrampicarsi, buttarsi giù) con movimenti procedurali.
    /// Quando arriveranno i modelli animati, qui si agganceranno le animazioni (evento TraversalStarted).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CharacterMotor : MonoBehaviour
    {
        /// <summary>Area NavMesh predefinita "Jump": la usiamo per i salti pericolosi, consentiti solo su ordine esplicito.</summary>
        public const int DangerousJumpArea = 2;

        [Header("Velocità (m/s) — in futuro calcolate dalle statistiche del personaggio")]
        public float walkSpeed = 2.0f;
        public float jogSpeed = 4.0f;
        public float sprintSpeed = 6.5f;

        [Header("Scavalcare / arrampicarsi")]
        public float vaultUpSpeed = 6f;
        public float climbUpSpeed = 2.8f;
        public float highClimbUpSpeed = 1.4f;
        public float gravity = 20f;
        [Tooltip("Fino a questa altezza l'ostacolo si scavalca con un balzo")]
        public float lowObstacleMax = 1.0f;
        [Tooltip("Fino a questa altezza ci si arrampica velocemente; oltre è un'arrampicata alta")]
        public float mediumObstacleMax = 1.7f;

        [Header("Scelta corsia")]
        [Tooltip("Dopo quanti secondi fermo davanti a una corsia occupata ne cerca una libera")]
        public float laneWaitBeforeSwitch = 0.15f;
        [Tooltip("Distanza massima da un ingresso occupato per considerarsi in coda")]
        public float laneSearchRadius = 2.5f;

        [Header("Atterraggio")]
        [Tooltip("Da questa altezza in su l'atterraggio è duro: il personaggio resta stordito")]
        public float hardLandingHeight = 3.5f;
        public float hardLandingStun = 0.9f;

        public Gait CurrentGait { get; private set; } = Gait.Jog;
        /// <summary>Andatura che attira l'attenzione (scatto): usata più avanti dal sistema di sospetto/reputazione.</summary>
        public bool IsConspicuous => CurrentGait == Gait.Sprint && CurrentSpeed > 0.5f;
        /// <summary>Limite di velocità imposto dal movimento di gruppo (0 = nessun limite).</summary>
        public float SpeedCap { get; private set; }
        public event Action<Gait> GaitChanged;
        public bool IsTraversing { get; private set; }
        public bool IsStunned { get; private set; }
        public TraversalKind CurrentTraversal { get; private set; }
        public float LastFallHeight { get; private set; }
        public float LastLandingTime { get; private set; } = -100f;
        public NavMeshAgent Agent => agent;
        public float CurrentSpeed => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;

        /// <summary>Inizio di un attraversamento: punto d'aggancio per animazioni e suoni.</summary>
        public event Action<TraversalKind> TraversalStarted;
        /// <summary>Atterraggio dopo un salto, con l'altezza della caduta in metri (servirà per i danni agli arti).</summary>
        public event Action<float> Landed;

        NavMeshAgent agent;
        bool hasPending;
        Vector3 pendingDestination;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.autoTraverseOffMeshLink = false;
        }

        void Start()
        {
            // L'agente nasce disattivato: la NavMesh viene costruita a runtime (RuntimeNavMeshBaker.Awake).
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                transform.position = hit.position;
            agent.enabled = true;
            ApplySpeed();
            SetDangerousJumps(false);
        }

        void Update()
        {
            if (!agent.enabled || IsTraversing) return;
            if (agent.isOnOffMeshLink)
            {
                StartCoroutine(TraverseLink());
                return;
            }
            UpdateLaneChoice();
        }

        public void MoveTo(Vector3 destination, bool allowDangerousJumps)
        {
            if (!agent.enabled) return;
            if (detouring)
            {
                // Sta già andando verso una corsia libera: aggiorna solo la meta finale.
                detourFinal = destination;
                SetDangerousJumps(allowDangerousJumps);
                return;
            }
            if (IsTraversing)
            {
                // Finisce prima il salto/arrampicata in corso, poi esegue l'ordine.
                pendingDestination = destination;
                pendingDangerous = allowDangerousJumps;
                hasPending = true;
                return;
            }
            SetDangerousJumps(allowDangerousJumps);
            agent.SetDestination(destination);
        }

        bool pendingDangerous;

        // ------------------------------------------------------------------ scelta della corsia libera

        float waitTimer;
        bool detouring;
        Vector3 detourFinal;
        Vector3 detourTarget;

        /// <summary>
        /// Unity fa aspettare chi trova un link occupato. Se il personaggio è fermo in coda davanti
        /// a una corsia occupata, va verso l'ingresso della corsia libera più vicina dello stesso passaggio.
        /// </summary>
        void UpdateLaneChoice()
        {
            if (detouring)
            {
                if (!agent.pathPending && FlatDistance(transform.position, detourTarget) <= 0.3f)
                {
                    detouring = false;
                    agent.SetDestination(detourFinal);
                }
                return;
            }

            bool waiting = agent.hasPath && !agent.pathPending && !agent.isStopped &&
                           agent.velocity.sqrMagnitude < 0.04f &&
                           FlatDistance(transform.position, agent.destination) > 0.5f;
            waitTimer = waiting ? waitTimer + Time.deltaTime : 0f;
            if (waitTimer < laneWaitBeforeSwitch) return;
            waitTimer = 0f;

            // La corsia occupata più vicina a noi: è lì che siamo in coda.
            TraversalLink blocked = null;
            int side = 0;
            float best = laneSearchRadius;
            foreach (var lane in TraversalLink.All)
            {
                if (lane.IsFree || lane.Occupant == this) continue;
                int s = lane.NearestEntrySide(transform.position, out float d);
                if (d < best) { best = d; blocked = lane; side = s; }
            }
            if (blocked == null) return;

            TraversalLink free = null;
            float bestFree = float.MaxValue;
            foreach (var lane in blocked.Lanes)
            {
                if (!lane.IsFree) continue;
                float d = FlatDistance(transform.position, lane.Entry(side));
                if (d < bestFree) { bestFree = d; free = lane; }
            }
            if (free == null) return; // tutte occupate: si aspetta

            detouring = true;
            detourFinal = agent.destination;
            detourTarget = free.Entry(side);
            agent.SetDestination(detourTarget);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public void SetGait(Gait gait)
        {
            if (CurrentGait == gait) return;
            CurrentGait = gait;
            ApplySpeed();
            GaitChanged?.Invoke(gait);
        }

        /// <summary>Velocità massima di questo personaggio con l'andatura data.</summary>
        public float SpeedFor(Gait gait)
        {
            switch (gait)
            {
                case Gait.Walk: return walkSpeed;
                case Gait.Sprint: return sprintSpeed;
                default: return jogSpeed;
            }
        }

        /// <summary>Movimento di gruppo: nessuno supera questa velocità. 0 toglie il limite.</summary>
        public void SetSpeedCap(float cap)
        {
            SpeedCap = cap;
            ApplySpeed();
        }

        void ApplySpeed()
        {
            if (agent == null) return;
            float speed = SpeedFor(CurrentGait);
            if (SpeedCap > 0f) speed = Mathf.Min(speed, SpeedCap);
            agent.speed = speed;
        }

        void SetDangerousJumps(bool allow)
        {
            int mask = NavMesh.AllAreas;
            if (!allow) mask &= ~(1 << DangerousJumpArea);
            agent.areaMask = mask;
        }

        // ------------------------------------------------------------------ attraversamenti

        IEnumerator TraverseLink()
        {
            IsTraversing = true;

            OffMeshLinkData data = agent.currentOffMeshLinkData;
            Vector3 start = transform.position;
            Vector3 end = data.endPos + Vector3.up * agent.baseOffset;
            float dy = end.y - start.y;

            float obstacle = 0f;
            TraversalLink lane = null;
            if (data.owner is Component owner && owner.TryGetComponent(out lane))
            {
                obstacle = lane.obstacleHeight;
                lane.Occupant = this;
            }
            if (obstacle <= 0f && Mathf.Abs(dy) < 0.5f)
                obstacle = ProbeObstacleHeight(start, end);

            TraversalKind kind = Classify(dy, obstacle);
            CurrentTraversal = kind;
            TraversalStarted?.Invoke(kind);

            Vector3 flat = end - start;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);

            switch (kind)
            {
                case TraversalKind.Vault:
                    yield return StartCoroutine(OverObstacle(start, end, start.y + obstacle + 0.25f, vaultUpSpeed, 0.35f, 0f));
                    break;
                case TraversalKind.Climb:
                    yield return StartCoroutine(OverObstacle(start, end, start.y + obstacle + 0.15f, climbUpSpeed, 0.4f, 0.1f));
                    break;
                case TraversalKind.ClimbHigh:
                    yield return StartCoroutine(OverObstacle(start, end, start.y + obstacle + 0.1f, highClimbUpSpeed, 0.45f, 0.25f));
                    break;
                case TraversalKind.ClimbUp:
                    yield return StartCoroutine(ClimbUp(start, end, dy));
                    break;
                case TraversalKind.Drop:
                    yield return StartCoroutine(Drop(start, end, start.y + Mathf.Max(obstacle + 0.2f, 0.35f)));
                    break;
            }

            transform.position = end;
            agent.CompleteOffMeshLink();
            if (lane != null && lane.Occupant == this) lane.Occupant = null;
            CurrentTraversal = TraversalKind.None;

            if (kind == TraversalKind.Drop)
            {
                LastFallHeight = start.y - end.y;
                LastLandingTime = Time.time;
                Landed?.Invoke(LastFallHeight);

                if (LastFallHeight >= hardLandingHeight)
                {
                    IsStunned = true;
                    agent.isStopped = true;
                    yield return new WaitForSeconds(hardLandingStun);
                    agent.isStopped = false;
                    IsStunned = false;
                }
            }

            IsTraversing = false;
            if (hasPending)
            {
                hasPending = false;
                SetDangerousJumps(pendingDangerous);
                agent.SetDestination(pendingDestination);
            }
        }

        TraversalKind Classify(float dy, float obstacle)
        {
            if (dy > 0.5f) return TraversalKind.ClimbUp;
            if (dy < -0.5f) return TraversalKind.Drop;
            if (obstacle <= lowObstacleMax) return TraversalKind.Vault;
            if (obstacle <= mediumObstacleMax) return TraversalKind.Climb;
            return TraversalKind.ClimbHigh;
        }

        /// <summary>Su, sopra l'ostacolo, poi giù con la gravità dall'altra parte.</summary>
        IEnumerator OverObstacle(Vector3 start, Vector3 end, float peakY, float upSpeed, float upFraction, float hangTime)
        {
            Vector3 top = Vector3.Lerp(start, end, upFraction);
            top.y = peakY;
            float upTime = Mathf.Max(0.1f, (peakY - start.y) / upSpeed);
            yield return StartCoroutine(MoveLinear(start, top, upTime, true));
            if (hangTime > 0f) yield return new WaitForSeconds(hangTime);
            float fallTime = Mathf.Sqrt(2f * Mathf.Max(0.05f, peakY - end.y) / gravity);
            yield return StartCoroutine(Fall(top, end, fallTime));
        }

        /// <summary>Mani sul bordo, tirarsi su, poi un passo avanti.</summary>
        IEnumerator ClimbUp(Vector3 start, Vector3 end, float dy)
        {
            Vector3 ledge = Vector3.Lerp(start, end, 0.15f);
            ledge.y = end.y + 0.3f;
            float speed = dy > mediumObstacleMax ? highClimbUpSpeed : climbUpSpeed;
            yield return StartCoroutine(MoveLinear(start, ledge, Mathf.Max(0.2f, (ledge.y - start.y) / speed), false));
            yield return StartCoroutine(MoveLinear(ledge, end, 0.35f, true));
        }

        /// <summary>Piccolo balzo oltre il bordo (o la ringhiera), poi caduta libera.</summary>
        IEnumerator Drop(Vector3 start, Vector3 end, float peakY)
        {
            Vector3 edge = Vector3.Lerp(start, end, 0.3f);
            edge.y = peakY;
            yield return StartCoroutine(MoveLinear(start, edge, Mathf.Max(0.12f, (peakY - start.y) / vaultUpSpeed), true));
            float fallTime = Mathf.Sqrt(2f * Mathf.Max(0.05f, peakY - end.y) / gravity);
            yield return StartCoroutine(Fall(edge, end, fallTime));
        }

        IEnumerator MoveLinear(Vector3 from, Vector3 to, float duration, bool easeOut)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                if (easeOut) u = 1f - (1f - u) * (1f - u);
                transform.position = Vector3.Lerp(from, to, u);
                yield return null;
            }
        }

        /// <summary>Orizzontale lineare, verticale accelerato (gravità).</summary>
        IEnumerator Fall(Vector3 from, Vector3 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                Vector3 p = Vector3.Lerp(from, to, u);
                p.y = from.y + (to.y - from.y) * u * u;
                transform.position = p;
                yield return null;
            }
        }

        /// <summary>Se il link non dice quanto è alto l'ostacolo, lo misura con dei raggi verso il basso.</summary>
        static float ProbeObstacleHeight(Vector3 start, Vector3 end)
        {
            float baseY = Mathf.Min(start.y, end.y);
            float best = 0f;
            for (int i = 1; i < 8; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, i / 8f);
                p.y = baseY + 2.8f;
                if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 2.8f, ~0, QueryTriggerInteraction.Ignore))
                    best = Mathf.Max(best, hit.point.y - baseY);
            }
            return best;
        }
    }
}
