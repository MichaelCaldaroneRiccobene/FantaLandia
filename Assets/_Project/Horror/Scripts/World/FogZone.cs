using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>Nebbia più fitta all'aperto e più leggera dentro casa (cambia dolcemente passando dalla porta).</summary>
    public class FogZone : MonoBehaviour
    {
        [Tooltip("Ingombro della casa (in coordinate mondo): dentro qui la nebbia è leggera")]
        public Bounds indoor = new Bounds(new Vector3(0f, 1.5f, 5.5f), new Vector3(14f, 10f, 11f));
        public float outsideDensity = 0.075f;
        public float insideDensity = 0.02f;
        public float blendSpeed = 0.6f;

        [Header("Cantina: sotto questa quota la luce ambiente quasi sparisce (buio vero)")]
        public float basementBelowY = -0.5f;
        public Color basementAmbient = new Color(0.006f, 0.006f, 0.008f);

        Color normalAmbient;
        float basementT;

        void Start() => normalAmbient = RenderSettings.ambientLight;

        void Update()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            basementT = Mathf.MoveTowards(basementT, cam.transform.position.y < basementBelowY ? 1f : 0f, Time.deltaTime * 0.8f);
            RenderSettings.ambientLight = Color.Lerp(normalAmbient, basementAmbient, basementT);
            float target = indoor.Contains(cam.transform.position) ? insideDensity : outsideDensity;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, target, blendSpeed * 0.1f * Time.deltaTime);
        }
    }
}
