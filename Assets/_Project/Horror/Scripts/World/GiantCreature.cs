using System.Collections;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// La creatura gigante del capitolo 2, vista da lontanissimo: una sagoma nera alta una sessantina di metri,
    /// bipede, con braccia troppo lunghe e una testa piccola. All'inizio è accucciata e nascosta dietro i tetti
    /// e la nebbia; Rise() la fa alzare lentamente, Roar() la fa ruggire (testa all'indietro, braccia aperte).
    /// Le parti (anca, busto, testa, mascella, braccia) le costruisce il generatore della scena.
    /// </summary>
    public class GiantCreature : MonoBehaviour
    {
        public Transform hips, torso, head, jaw, armL, armR;
        [Tooltip("Quanto è sprofondata (dietro l'orizzonte) prima di alzarsi")]
        public float sink = 38f;
        [Tooltip("Busto piegato in avanti da accucciata (gradi)")]
        public float crouchPitch = 70f;

        float rise;       // 0 accucciata e nascosta, 1 in piedi
        float roar;       // 0..1 il ruggito
        Vector3 basePos;
        Quaternion hipsRot0, torsoRot0, headRot0, jawRot0, armLRot0, armRRot0;

        public bool Standing => rise > 0.99f;

        void Awake()
        {
            basePos = transform.localPosition;
            if (hips != null) hipsRot0 = hips.localRotation;
            if (torso != null) torsoRot0 = torso.localRotation;
            if (head != null) headRot0 = head.localRotation;
            if (jaw != null) jawRot0 = jaw.localRotation;
            if (armL != null) armLRot0 = armL.localRotation;
            if (armR != null) armRRot0 = armR.localRotation;
            Pose();
        }

        /// <summary>Si alza in 'seconds' secondi (lento, pesante: prima la schiena, poi la testa).</summary>
        public IEnumerator Rise(float seconds)
        {
            gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                rise = t * t * (3f - 2f * t);
                yield return null;
            }
            rise = 1f;
        }

        /// <summary>Il ruggito: testa indietro, bocca spalancata, braccia larghe; dura 'seconds'.</summary>
        public IEnumerator Roar(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((seconds - t) / 1.2f);
                roar = k * k * (3f - 2f * k);
                yield return null;
            }
            roar = 0f;
        }

        void Update() => Pose();

        void Pose()
        {
            float t = Time.time;
            float breathe = Mathf.Sin(t * 0.7f) * 1.2f;
            float sway = Mathf.Sin(t * 0.31f) * 2f;
            float shake = roar > 0f ? Mathf.Sin(t * 31f) * 0.6f * roar : 0f;
            transform.localPosition = basePos + Vector3.down * sink * (1f - rise) * (1f - rise);
            if (hips != null) hips.localRotation = hipsRot0 * Quaternion.Euler(0f, sway * 0.5f, sway * 0.3f);
            if (torso != null)
            {
                // da accucciata il busto è piegato in avanti; si raddrizza per ultimo; ruggendo si inarca all'indietro
                float pitch = Mathf.Lerp(crouchPitch, 8f, Mathf.Clamp01(rise * 1.3f - 0.3f)) + breathe - roar * 14f + shake;
                torso.localRotation = torsoRot0 * Quaternion.Euler(pitch, 0f, sway * 0.4f);
            }
            if (head != null) head.localRotation = headRot0 * Quaternion.Euler(-roar * 38f + Mathf.Sin(t * 0.5f) * 3f, Mathf.Sin(t * 0.23f) * 6f, 0f);
            if (jaw != null) jaw.localRotation = jawRot0 * Quaternion.Euler(roar * 35f, 0f, 0f);
            float hang = 1f - rise;
            if (armL != null) armL.localRotation = armLRot0 * Quaternion.Euler(-hang * 50f + breathe, 0f, -roar * 65f - 6f);
            if (armR != null) armR.localRotation = armRRot0 * Quaternion.Euler(-hang * 50f - breathe, 0f, roar * 65f + 6f);
        }
    }
}
