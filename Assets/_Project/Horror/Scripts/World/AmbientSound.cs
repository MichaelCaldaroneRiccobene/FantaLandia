using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    public enum AmbientClip { Night, TvStatic, Cartoon }

    /// <summary>Suono d'ambiente in loop generato dal codice (grilli e vento della notte, ecc.).</summary>
    public class AmbientSound : MonoBehaviour
    {
        public AmbientClip clip = AmbientClip.Night;
        [Range(0f, 1f)] public float volume = 0.35f;
        public bool spatial;

        void Start()
        {
            AudioClip c = clip == AmbientClip.Night ? ProceduralAudio.NightAmbience()
                        : clip == AmbientClip.TvStatic ? ProceduralAudio.TvStatic() : ProceduralAudio.CartoonMusic();
            HorrorAudio.CreateLoop(transform, c, volume * FantaLandia.Horror.UI.GameSettings.Effects, spatial).Play();
        }
    }
}
