using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Mettilo su un personaggio: l'erba si piega attorno a lui (shader FantaLandia/WindVegetation).
    /// Fino a 8 personaggi alla volta.
    /// </summary>
    public class GrassPusher : MonoBehaviour
    {
        static readonly List<GrassPusher> all = new List<GrassPusher>();
        static readonly Vector4[] buffer = new Vector4[8];
        static int lastFrame = -1;
        static readonly int PushersId = Shader.PropertyToID("_GrassPushers");

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void LateUpdate()
        {
            if (lastFrame == Time.frameCount) return; // una volta per frame, per tutti
            lastFrame = Time.frameCount;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i < all.Count && all[i] != null)
                {
                    Vector3 p = all[i].transform.position;
                    buffer[i] = new Vector4(p.x, p.y, p.z, 1f);
                }
                else buffer[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(PushersId, buffer);
        }
    }
}
