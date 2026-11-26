using FantaLandia.Characters;
using UnityEngine;

namespace FantaLandia.World
{
    /// <summary>
    /// Porta a una o due ante: si apre quando un personaggio si avvicina, sempre dal lato opposto
    /// a chi arriva (si apre in entrambi i sensi), e si richiude da sola dolcemente.
    /// Il +Z locale della porta è il verso "dentro": le ante ruotano attorno ai cardini (figli "leaves").
    /// Le ante stanno sul layer "Ignore Raycast": i clic le attraversano e arrivano al pavimento.
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        [Tooltip("Cardini delle ante (ruotano sull'asse Y)")]
        public Transform[] leaves;
        [Tooltip("Verso di rotazione di ogni anta: -1 cardine a sinistra, +1 cardine a destra")]
        public float[] leafSign;

        [Tooltip("Zona di rilevamento (locale): larghezza, altezza, profondità per lato")]
        public Vector3 zoneSize = new Vector3(2.6f, 3f, 3.4f);
        public float openAngle = 95f;
        public float openTime = 0.22f;
        public float closeTime = 0.9f;
        public float closeDelay = 0.6f;

        float angle, velocity;
        float lastSeen = -10f;
        int openSide; // +1 = si apre verso +Z, -1 = verso -Z, 0 = chiusa

        void Update()
        {
            // Chi è vicino alla porta? Il lato si decide solo quando la porta parte da chiusa.
            float bestDist = float.MaxValue;
            int side = 0;
            foreach (CharacterMotor m in CharacterMotor.All)
            {
                Vector3 local = transform.InverseTransformPoint(m.transform.position);
                if (Mathf.Abs(local.x) > zoneSize.x * 0.5f || local.y < -0.5f || local.y > zoneSize.y) continue;
                if (Mathf.Abs(local.z) > zoneSize.z * 0.5f) continue;
                float d = Mathf.Abs(local.z);
                if (d < bestDist) { bestDist = d; side = local.z < 0f ? 1 : -1; }
            }

            if (side != 0)
            {
                lastSeen = Time.time;
                if (openSide == 0) openSide = side;
            }

            bool open = Time.time - lastSeen < closeDelay;
            float target = open ? openAngle * openSide : 0f;
            angle = Mathf.SmoothDamp(angle, target, ref velocity, open ? openTime : closeTime);
            if (!open && Mathf.Abs(angle) < 0.5f) { angle = 0f; openSide = 0; }

            for (int i = 0; i < leaves.Length; i++)
                if (leaves[i] != null) leaves[i].localRotation = Quaternion.Euler(0f, angle * leafSign[i], 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, zoneSize.y * 0.5f, 0f), zoneSize);
        }
    }
}
