using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>
    /// Espressività del viso:
    ///  - bocca: si apre a ogni sillaba (Pulse) e si richiude da sola
    ///  - occhi: sbattono le palpebre, le iridi guardano il bersaglio (o vagano un po')
    ///  - sopracciglia: si alzano per domande/esclamazioni (SetExpression)
    ///  - testa e corpo: si girano verso il bersaglio dello sguardo (lookTarget)
    /// Gira dopo l'animatore del corpo, così la rotazione della testa si somma alla sua posa.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(FaceRig))]
    public class FaceAnimator : MonoBehaviour
    {
        [Tooltip("Dove guarda (testa, occhi e, se fermo, il corpo). Null = sguardo libero")]
        public Transform lookTarget;
        public bool turnBody = true;
        public float blinkMin = 2.2f, blinkMax = 5.5f;
        [Tooltip("Quanto si apre la bocca (m)")]
        public float mouthOpen = 0.012f;

        FaceRig rig;
        Transform head;
        CharacterMotor motor;
        Vector3 lowerLipRest, irisLRest, irisRRest, browLRest, browRRest;
        Quaternion browLRot, browRRot;
        Renderer mouthRenderer, lidLRenderer, lidRRenderer;

        float mouth, mouthTarget, lastPulse = -10f;
        float blinkStart = -10f, nextBlink;
        float brow, browTarget;
        float lookW;
        Vector2 wander, wanderTarget;
        float nextWander;

        /// <summary>Punto da guardare per parlare con questo personaggio (all'altezza degli occhi).</summary>
        public Vector3 EyePoint => head != null ? head.position + head.up * 0.17f + head.forward * 0.05f : transform.position + Vector3.up * 1.6f;

        void Awake()
        {
            rig = GetComponent<FaceRig>();
            var body = GetComponent<BodyRig>();
            head = body != null ? body.Joint(BodyPart.Head) : null;
            motor = GetComponentInParent<CharacterMotor>();

            if (rig.lowerLip) lowerLipRest = rig.lowerLip.localPosition;
            if (rig.irisL) irisLRest = rig.irisL.localPosition;
            if (rig.irisR) irisRRest = rig.irisR.localPosition;
            if (rig.browL) { browLRest = rig.browL.localPosition; browLRot = rig.browL.localRotation; }
            if (rig.browR) { browRRest = rig.browR.localPosition; browRRot = rig.browR.localRotation; }
            if (rig.mouthInside) mouthRenderer = rig.mouthInside.GetComponent<Renderer>();
            if (rig.lidL) lidLRenderer = rig.lidL.GetComponent<Renderer>();
            if (rig.lidR) lidRRenderer = rig.lidR.GetComponent<Renderer>();
            nextBlink = Time.time + Random.Range(blinkMin, blinkMax);
        }

        /// <summary>Una sillaba: la bocca si apre (forza 0..1).</summary>
        public void Pulse(float strength = 1f)
        {
            mouthTarget = Random.Range(0.45f, 1f) * strength;
            lastPulse = Time.time;
        }

        /// <summary>Sopracciglia: 0 neutro, 1 alzate (sorpresa/domanda), negativo aggrottate.</summary>
        public void SetExpression(float browRaise) => browTarget = Mathf.Clamp(browRaise, -1f, 1f);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // --- bocca
            if (Time.time - lastPulse > 0.09f) mouthTarget = 0f;
            mouth = Mathf.MoveTowards(mouth, mouthTarget, dt * 14f);
            if (rig.lowerLip) rig.lowerLip.localPosition = lowerLipRest + Vector3.down * (mouthOpen * mouth);
            if (rig.mouthInside)
            {
                rig.mouthInside.localScale = new Vector3(1f, Mathf.Max(0.02f, mouth), 1f);
                if (mouthRenderer) mouthRenderer.enabled = mouth > 0.03f;
            }

            // --- palpebre
            if (Time.time > nextBlink)
            {
                blinkStart = Time.time;
                nextBlink = Time.time + Random.Range(blinkMin, blinkMax);
                if (Random.value < 0.15f) nextBlink = Time.time + 0.25f; // a volte doppio
            }
            float bt = (Time.time - blinkStart) / 0.16f;
            float closed = bt < 1f ? 1f - Mathf.Abs(bt * 2f - 1f) : 0f;
            closed = Mathf.Max(closed, -Mathf.Min(0f, brow) * 0.35f); // aggrottato = occhi socchiusi
            SetLid(rig.lidL, lidLRenderer, closed);
            SetLid(rig.lidR, lidRRenderer, closed);

            // --- sopracciglia
            brow = Mathf.MoveTowards(brow, browTarget, dt * 4f);
            float lift = 0.006f * brow;
            if (rig.browL) { rig.browL.localPosition = browLRest + Vector3.up * lift; rig.browL.localRotation = browLRot * Quaternion.Euler(0f, 0f, -6f * Mathf.Min(0f, brow)); }
            if (rig.browR) { rig.browR.localPosition = browRRest + Vector3.up * lift; rig.browR.localRotation = browRRot * Quaternion.Euler(0f, 0f, 6f * Mathf.Min(0f, brow)); }

            // --- occhi
            Vector2 eye;
            if (lookTarget != null && head != null)
            {
                Vector3 local = head.InverseTransformDirection((lookTarget.position - EyePoint).normalized);
                eye = new Vector2(Mathf.Clamp(local.x * 0.012f, -0.006f, 0.006f), Mathf.Clamp(local.y * 0.01f, -0.003f, 0.003f));
            }
            else
            {
                if (Time.time > nextWander)
                {
                    nextWander = Time.time + Random.Range(0.8f, 2.5f);
                    wanderTarget = new Vector2(Random.Range(-0.004f, 0.004f), Random.Range(-0.002f, 0.002f));
                }
                wander = Vector2.Lerp(wander, wanderTarget, 1f - Mathf.Exp(-20f * dt));
                eye = wander;
            }
            if (rig.irisL) rig.irisL.localPosition = irisLRest + new Vector3(eye.x, eye.y, 0f);
            if (rig.irisR) rig.irisR.localPosition = irisRRest + new Vector3(eye.x, eye.y, 0f);

            // --- testa (e corpo, se fermo) verso il bersaglio
            lookW = Mathf.MoveTowards(lookW, lookTarget != null ? 1f : 0f, dt * 2.5f);
            if (head != null && lookW > 0.001f && lookTarget != null)
            {
                Transform root = motor != null ? motor.transform : transform;
                Vector3 dir = lookTarget.position - head.position;
                Vector3 l = root.InverseTransformDirection(dir);
                float yaw = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -70f, 70f);
                float pitch = Mathf.Clamp(Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
                Quaternion want = root.rotation * Quaternion.Euler(-pitch, yaw, 0f);
                head.rotation = Quaternion.Slerp(head.rotation, want, lookW * 0.85f);

                // il corpo si gira se il bersaglio è troppo di lato e il personaggio è fermo
                if (turnBody && motor != null && !motor.IsTraversing && motor.CurrentSpeed < 0.1f)
                {
                    float fullYaw = Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(fullYaw) > 35f)
                    {
                        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                        if (flat.sqrMagnitude > 0.01f)
                            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(flat), 140f * dt);
                    }
                }
            }
        }

        static void SetLid(Transform lid, Renderer r, float closed)
        {
            if (lid == null) return;
            lid.localScale = new Vector3(1f, Mathf.Max(0.02f, closed), 1f);
            if (r) r.enabled = closed > 0.04f;
        }
    }
}
