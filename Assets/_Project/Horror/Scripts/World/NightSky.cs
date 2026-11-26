using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Il cielo notturno: una sfera grande che segue la camera (stelle, Via Lattea, luna) e nuvole voxel
    /// che si spostano piano nel vento. La luna del cielo è allineata con la luce direzionale "Luna".
    /// </summary>
    public class NightSky : MonoBehaviour
    {
        public Renderer dome;
        public Light moonLight;
        public Transform clouds;
        public Vector3 cloudWind = new Vector3(0.6f, 0f, 0.25f);
        [Tooltip("Le nuvole che escono da questo raggio rientrano dall'altra parte")]
        public float cloudRadius = 170f;

        static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
        MaterialPropertyBlock block;

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            if (dome != null)
            {
                dome.transform.position = c;
                if (moonLight != null)
                {
                    block ??= new MaterialPropertyBlock();
                    dome.GetPropertyBlock(block);
                    block.SetVector(MoonDirId, -moonLight.transform.forward);
                    dome.SetPropertyBlock(block);
                }
            }
            if (clouds == null) return;
            Vector3 step = cloudWind * Time.deltaTime;
            foreach (Transform cl in clouds)
            {
                Vector3 p = cl.position + step;
                Vector3 rel = p - c;
                rel.y = 0f;
                if (rel.magnitude > cloudRadius) p -= rel.normalized * cloudRadius * 1.9f; // rientra dal lato opposto
                cl.position = p;
            }
        }
    }
}
