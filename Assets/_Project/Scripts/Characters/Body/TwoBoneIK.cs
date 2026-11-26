using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>
    /// IK analitica a due ossa (coscia-stinco o braccio-avambraccio) per segmenti che puntano lungo il -Y locale.
    /// Il "pole" indica dove deve puntare il ginocchio / gomito.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <param name="a">giunto radice (anca / spalla)</param>
        /// <param name="b">giunto intermedio (ginocchio / gomito)</param>
        /// <param name="c">giunto finale (caviglia / polso)</param>
        /// <param name="target">dove portare il giunto finale (mondo)</param>
        /// <param name="pole">direzione verso cui piegare il giunto intermedio (mondo)</param>
        /// <param name="flipFront">true per le braccia: la parte frontale dell'osso è opposta alla direzione del gomito</param>
        public static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight, bool flipFront)
        {
            if (weight <= 0.001f || a == null || b == null || c == null) return;

            Vector3 pa = a.position;
            float l1 = Vector3.Distance(pa, b.position);
            float l2 = Vector3.Distance(b.position, c.position);
            if (l1 < 1e-4f || l2 < 1e-4f) return;

            Vector3 toTarget = target - pa;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-4f);
            Vector3 dirT = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.down;

            Vector3 perp = pole - dirT * Vector3.Dot(pole, dirT);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dirT, Vector3.right);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dirT, Vector3.forward);
            perp.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 mid = pa + dirT * (l1 * cosA) + perp * (l1 * sinA);
            Vector3 end = pa + dirT * d;
            Vector3 n = Vector3.Cross(dirT, perp);

            a.rotation = Quaternion.Slerp(a.rotation, BoneRotation(mid - pa, n, flipFront), weight);
            b.rotation = Quaternion.Slerp(b.rotation, BoneRotation(end - b.position, n, flipFront), weight);
        }

        static Quaternion BoneRotation(Vector3 boneDir, Vector3 n, bool flipFront)
        {
            Vector3 down = boneDir.sqrMagnitude > 1e-8f ? boneDir.normalized : Vector3.down;
            Vector3 front = Vector3.Cross(n, down);
            if (flipFront) front = -front;
            if (front.sqrMagnitude < 1e-6f) front = Vector3.forward;
            return Quaternion.LookRotation(front, -down);
        }
    }
}
