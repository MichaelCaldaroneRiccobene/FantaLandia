using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>
    /// Fisica semplice dei capelli che pendono (lunghi, coda, mullet): un pendolo con molla e smorzamento.
    /// Ondeggiano quando il personaggio accelera, si gira o si ferma, e tendono a pendere verso il basso
    /// quando la testa si inclina. Gira dopo l'animazione del corpo e del viso.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class HairPhysics : MonoBehaviour
    {
        [Tooltip("Il pezzo di capelli mobile (perno in alto, i capelli pendono verso il basso)")]
        public Transform hair;
        public float stiffness = 55f;
        public float damping = 6f;
        [Tooltip("Quanto i capelli restano indietro quando la testa accelera")]
        public float inertia = 0.9f;
        [Tooltip("Quanto i capelli seguono la gravità quando la testa si inclina (0..1)")]
        public float gravity = 0.6f;
        public float maxAngle = 40f;

        Quaternion rest;
        Vector3 lastPos, lastVel;
        Vector2 angle, angVel;
        bool started;

        void Awake()
        {
            if (hair != null) rest = hair.localRotation;
        }

        void LateUpdate()
        {
            if (hair == null || hair.parent == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = hair.position;
            if (!started) { lastPos = pos; lastVel = Vector3.zero; started = true; }
            Vector3 vel = (pos - lastPos) / dt;
            Vector3 acc = Vector3.ClampMagnitude((vel - lastVel) / dt, 40f);
            lastPos = pos;
            lastVel = vel;

            Transform parent = hair.parent;
            Vector3 a = parent.InverseTransformDirection(acc);
            Vector3 g = parent.InverseTransformDirection(Vector3.down);

            // inerzia: la punta resta indietro rispetto all'accelerazione
            float tx = a.z * inertia, tz = -a.x * inertia;
            // gravità: la punta tende verso il basso del mondo
            tx += -Mathf.Asin(Mathf.Clamp(g.z, -1f, 1f)) * Mathf.Rad2Deg * gravity;
            tz += Mathf.Asin(Mathf.Clamp(g.x, -1f, 1f)) * Mathf.Rad2Deg * gravity;
            Vector2 target = new Vector2(Mathf.Clamp(tx, -maxAngle, maxAngle), Mathf.Clamp(tz, -maxAngle, maxAngle));

            angVel += (stiffness * (target - angle) - damping * angVel) * dt;
            angle += angVel * dt;
            angle.x = Mathf.Clamp(angle.x, -maxAngle, maxAngle);
            angle.y = Mathf.Clamp(angle.y, -maxAngle, maxAngle);
            // i capelli dietro non entrano nella testa: verso l'avanti si piegano poco
            if (angle.x < -8f) { angle.x = -8f; angVel.x = Mathf.Max(0f, angVel.x); }

            hair.localRotation = rest * Quaternion.Euler(angle.x, 0f, angle.y);
        }
    }
}
