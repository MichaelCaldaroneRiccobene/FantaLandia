using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FantaLandia.Characters
{
    public enum TraversalKind { None, Vault, Climb, ClimbHigh, ClimbUp, Drop }

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

        [Header("Velocità (m/s)")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 5.5f;

        [Header("Scavalcare / arrampicarsi")]
        public float vaultUpSpeed = 6f;
        public float climbUpSpeed = 2.8f;
        public float highClimbUpSpeed = 1.4f;
        public float gravity = 20f;
        [Tooltip("Fino a questa altezza l'ostacolo si scavalca con un balzo")]
        public float lowObstacleMax = 1.0f;
        [Tooltip("Fino a questa altezza ci si arrampica velocemente; oltre è un'arrampicata alta")]
        public float mediumObstacleMax = 1.7f;

        [Header("Atterraggio")]
        [Tooltip("Da questa altezza in su l'atterraggio è duro: il personaggio resta stordito")]
        public float hardLandingHeight = 3.5f;
        public float hardLandingStun = 0.9f;

        public bool IsRunning { get; private set; } = true;
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
            if (!agent.enabled) return;
            if (!IsTraversing && agent.isOnOffMeshLink)
                StartCoroutine(TraverseLink());
        }

        public void MoveTo(Vector3 destination, bool allowDangerousJumps)
        {
            if (!agent.enabled) return;
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

        public void SetRunning(bool run)
        {
            IsRunning = run;
            ApplySpeed();
        }

        void ApplySpeed()
        {
            if (agent != null) agent.speed = IsRunning ? runSpeed : walkSpeed;
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
            if (data.owner is Component owner && owner.TryGetComponent(out TraversalLink info))
                obstacle = info.obstacleHeight;
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
