using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using UnityEngine;

namespace FantaLandia.Horror.Player
{
    /// <summary>Torcia: si accende con F una volta raccolta. Segue lo sguardo con un leggero ritardo e a volte tremola.</summary>
    public class PlayerFlashlight : MonoBehaviour
    {
        public Light spot;
        public bool hasFlashlight;
        public bool isOn;
        [Tooltip("Ritardo con cui il fascio segue lo sguardo")]
        public float follow = 14f;

        Camera cam;
        float flickerUntil;
        float baseIntensity;

        void Start()
        {
            cam = GetComponentInChildren<Camera>();
            if (spot != null) baseIntensity = spot.intensity;
            Apply();
        }

        public void Give()
        {
            hasFlashlight = true;
            isOn = true;
            HorrorAudio.Play2D(ProceduralAudio.Click(), 0.6f);
            Apply();
        }

        void Update()
        {
            if (hasFlashlight && GameInput.FlashlightToggle && !DialogueSystem.IsBlocking)
            {
                isOn = !isOn;
                HorrorAudio.Play2D(ProceduralAudio.Click(), 0.6f);
                Apply();
            }
            if (spot == null || cam == null) return;

            // il fascio insegue lo sguardo (sensazione di peso)
            spot.transform.position = cam.transform.position + cam.transform.right * 0.18f - cam.transform.up * 0.15f;
            spot.transform.rotation = Quaternion.Slerp(spot.transform.rotation, cam.transform.rotation, 1f - Mathf.Exp(-follow * Time.deltaTime));

            // tremolio occasionale
            if (isOn)
            {
                if (Time.time > flickerUntil + 6f && Random.value < 0.002f) flickerUntil = Time.time + Random.Range(0.1f, 0.4f);
                spot.intensity = Time.time < flickerUntil ? baseIntensity * Random.Range(0.2f, 1f) : baseIntensity;
            }
        }

        void Apply()
        {
            if (spot != null) spot.enabled = hasFlashlight && isOn;
        }
    }
}
