using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FantaLandia.Characters
{
    public enum TraversalKind { None, Vault, Climb, ClimbHigh, ClimbUp, Drop }

    /// <summary>Fasi di un attraversamento (lette dall'animatore procedurale).</summary>
    public enum TraversalPhase { None, Approach, Vault, Jump, Scramble, PullUp, Mantle, Fall }

    /// <summary>Geometria e avanzamento dell'attraversamento in corso.</summary>
    public class TraversalState
    {
        public TraversalKind kind;
        public TraversalPhase phase;
        /// <summary>Avanzamento 0..1 della fase corrente.</summary>
        public float phaseT;
        public Vector3 start, end;
        /// <summary>Direzione orizzontale dell'attraversamento e il suo lato destro.</summary>
        public Vector3 dir, right;
        /// <summary>Punti a terra sulla faccia del muro dal lato di partenza e di arrivo (o il bordo del salto).</summary>
        public Vector3 nearFace, farFace;
        /// <summary>Altezza del bordo superiore dell'ostacolo.</summary>
        public float topY;
        public Vector3 Edge => new Vector3(nearFace.x, topY, nearFace.z);
    }

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

        /// <summary>Stato dell'attraversamento in corso: lo legge l'animatore per mettere mani e piedi al posto giusto.</summary>
        public TraversalState Traversal { get; } = new TraversalState();

        [Header("Tempi dell'arrampicata alta (s)")]
        public float climbJumpTime = 0.22f;
        public float climbScrambleTime = 0.55f;
        public float climbPullUpTime = 0.45f;
        public float climbMantleTime = 0.5f;

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
            Measure(kind, start, end, obstacle, dy);
            TraversalStarted?.Invoke(kind);

            // Durante l'attraversamento la rotazione la decidiamo noi: sempre rivolti verso l'ostacolo.
            agent.updateRotation = false;
            FaceTraversal();

            switch (kind)
            {
                case TraversalKind.Vault:
                    yield return StartCoroutine(VaultOver(false));
                    break;
                case TraversalKind.Climb:
                    yield return StartCoroutine(VaultOver(true));
                    break;
                case TraversalKind.ClimbHigh:
                    yield return StartCoroutine(ClimbWall(false));
                    break;
                case TraversalKind.ClimbUp:
                    if (dy > 1.3f) yield return StartCoroutine(ClimbWall(true));
                    else yield return StartCoroutine(VaultOver(false));
                    break;
                case TraversalKind.Drop:
                    yield return StartCoroutine(DropDown(obstacle));
                    break;
            }

            transform.position = end;
            FaceTraversal();
            agent.CompleteOffMeshLink();
            agent.updateRotation = true;
            if (lane != null && lane.Occupant == this) lane.Occupant = null;
            CurrentTraversal = TraversalKind.None;
            Traversal.phase = TraversalPhase.None;

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

        /// <summary>Misura l'ostacolo reale con dei raggi: facce del muro, altezza del bordo, punto di salto.</summary>
        void Measure(TraversalKind kind, Vector3 start, Vector3 end, float obstacle, float dy)
        {
            var t = Traversal;
            t.kind = kind;
            t.phase = TraversalPhase.None;
            t.phaseT = 0f;
            t.start = start;
            t.end = end;

            Vector3 flat = end - start;
            flat.y = 0f;
            float dist = flat.magnitude;
            t.dir = dist > 0.001f ? flat / dist : transform.forward;
            t.right = Vector3.Cross(Vector3.up, t.dir);
            t.topY = dy > 0.5f ? end.y : (dy < -0.5f ? start.y + obstacle : start.y + obstacle);

            Vector3 mid = Vector3.Lerp(start, end, 0.5f);
            if (kind == TraversalKind.Drop)
            {
                // Il bordo: primo punto lungo il percorso dove il pavimento sparisce sotto i piedi.
                t.nearFace = mid;
                t.nearFace.y = start.y;
                for (float s = 0.05f; s < dist; s += 0.05f)
                {
                    Vector3 p = start + t.dir * s + Vector3.up * 0.4f;
                    if (!Physics.Raycast(p, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        t.nearFace = start + t.dir * s;
                        break;
                    }
                }
                t.farFace = t.nearFace;
                return;
            }

            float baseY = Mathf.Min(start.y, end.y);
            float probeY = baseY + Mathf.Clamp((t.topY - baseY) * 0.5f, 0.15f, 1.0f);
            Vector3 o = new Vector3(start.x, probeY, start.z);
            t.nearFace = Physics.Raycast(o, t.dir, out RaycastHit hit, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore)
                ? new Vector3(hit.point.x, start.y, hit.point.z)
                : new Vector3(mid.x, start.y, mid.z);
            Vector3 o2 = new Vector3(end.x, probeY, end.z);
            t.farFace = dy <= 0.5f && Physics.Raycast(o2, -t.dir, out hit, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore)
                ? new Vector3(hit.point.x, end.y, hit.point.z)
                : t.nearFace;
        }

        /// <summary>Muretto basso (balzo con una mano) o muro medio (volteggio laterale stile parkour).</summary>
        IEnumerator VaultOver(bool parkour)
        {
            var t = Traversal;
            float takeoffDist = parkour ? 0.7f : 0.45f;
            Vector3 takeoff = transform.position;
            if (FlatDistance(takeoff, t.nearFace) > takeoffDist + 0.05f)
            {
                takeoff = t.nearFace - t.dir * takeoffDist;
                takeoff.y = t.start.y;
            }
            float approachSpeed = Mathf.Max(3f, agent.speed);
            yield return StartCoroutine(MovePhase(transform.position, takeoff, FlatDistance(transform.position, takeoff) / approachSpeed,
                                                  TraversalPhase.Approach, Ease.None));

            // Radice (= piedi a gambe tese): le gambe raccolte passano sopra il bordo.
            float peak = Mathf.Max(t.topY - (parkour ? 0.3f : 0.25f), Mathf.Max(t.start.y, t.end.y) + 0.12f);
            float duration = parkour ? 0.6f : 0.5f;
            Vector3 a = takeoff, b = t.end;
            float h = peak - (a.y + b.y) * 0.5f;
            t.phase = TraversalPhase.Vault;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float u = Mathf.Clamp01(time / duration);
                t.phaseT = u;
                FaceTraversal();
                Vector3 p = Vector3.Lerp(a, b, u);
                p.y = Mathf.Lerp(a.y, b.y, u) + 4f * h * u * (1f - u);
                transform.position = p;
                yield return null;
            }
        }

        /// <summary>Muro alto: salto, mani sul bordo, piedi che spingono sul muro, trazione, scavalco.</summary>
        IEnumerator ClimbWall(bool endsOnTop)
        {
            var t = Traversal;
            Vector3 wallBase = t.nearFace - t.dir * 0.32f;
            wallBase.y = t.start.y;
            float approachSpeed = Mathf.Max(3f, agent.speed);
            yield return StartCoroutine(MovePhase(transform.position, wallBase, FlatDistance(transform.position, wallBase) / approachSpeed,
                                                  TraversalPhase.Approach, Ease.None));

            Vector3 hang = wallBase;
            hang.y = Mathf.Max(t.start.y, t.topY - 2.0f);
            yield return StartCoroutine(MovePhase(wallBase, hang, climbJumpTime, TraversalPhase.Jump, Ease.Out));

            Vector3 scramble = wallBase;
            scramble.y = Mathf.Max(hang.y, t.topY - 1.45f);
            yield return StartCoroutine(MovePhase(hang, scramble, climbScrambleTime, TraversalPhase.Scramble, Ease.None));

            Vector3 pull = t.nearFace - t.dir * 0.18f;
            pull.y = t.topY - 0.95f;
            yield return StartCoroutine(MovePhase(scramble, pull, climbPullUpTime, TraversalPhase.PullUp, Ease.InOut));

            Vector3 onTop = endsOnTop ? t.nearFace + t.dir * 0.45f : Vector3.Lerp(t.nearFace, t.farFace, 0.5f);
            onTop.y = t.topY;
            yield return StartCoroutine(MovePhase(pull, onTop, climbMantleTime, TraversalPhase.Mantle, Ease.InOut));

            if (endsOnTop)
            {
                yield return StartCoroutine(MovePhase(onTop, t.end, Mathf.Max(0.1f, FlatDistance(onTop, t.end) / 2.5f),
                                                      TraversalPhase.Approach, Ease.None));
            }
            else
            {
                // Giù dall'altra parte: piccolo slancio, poi caduta.
                Vector3 edge = t.farFace + t.dir * 0.1f;
                edge.y = t.topY + 0.15f;
                yield return StartCoroutine(MovePhase(onTop, edge, 0.15f, TraversalPhase.Jump, Ease.Out));
                yield return StartCoroutine(FallPhase(edge, t.end));
            }
        }

        /// <summary>Salto giù da un bordo (o oltre una ringhiera), poi caduta libera.</summary>
        IEnumerator DropDown(float railingHeight)
        {
            var t = Traversal;
            Vector3 edgeApproach = t.nearFace - t.dir * 0.2f;
            edgeApproach.y = t.start.y;
            if (Vector3.Dot(edgeApproach - transform.position, t.dir) > 0.05f)
            {
                yield return StartCoroutine(MovePhase(transform.position, edgeApproach,
                    FlatDistance(transform.position, edgeApproach) / Mathf.Max(3f, agent.speed), TraversalPhase.Approach, Ease.None));
            }
            float peakY = t.start.y + Mathf.Max(railingHeight + 0.2f, 0.35f);
            Vector3 over = t.nearFace + t.dir * 0.25f;
            over.y = peakY;
            yield return StartCoroutine(MovePhase(transform.position, over, Mathf.Max(0.12f, (peakY - t.start.y) / vaultUpSpeed),
                                                  TraversalPhase.Jump, Ease.Out));
            yield return StartCoroutine(FallPhase(over, t.end));
        }

        enum Ease { None, Out, InOut }

        void FaceTraversal()
        {
            if (Traversal.dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(Traversal.dir);
        }

        IEnumerator MovePhase(Vector3 from, Vector3 to, float duration, TraversalPhase phase, Ease ease)
        {
            Traversal.phase = phase;
            Traversal.phaseT = 0f;
            if (duration <= 0.0001f) { transform.position = to; Traversal.phaseT = 1f; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                Traversal.phaseT = u;
                FaceTraversal();
                float e = ease == Ease.Out ? 1f - (1f - u) * (1f - u) : ease == Ease.InOut ? u * u * (3f - 2f * u) : u;
                transform.position = Vector3.Lerp(from, to, e);
                yield return null;
            }
        }

        /// <summary>Orizzontale lineare, verticale accelerato (gravità).</summary>
        IEnumerator FallPhase(Vector3 from, Vector3 to)
        {
            Traversal.phase = TraversalPhase.Fall;
            float duration = Mathf.Sqrt(2f * Mathf.Max(0.05f, from.y - to.y) / gravity);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                Traversal.phaseT = u;
                FaceTraversal();
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
