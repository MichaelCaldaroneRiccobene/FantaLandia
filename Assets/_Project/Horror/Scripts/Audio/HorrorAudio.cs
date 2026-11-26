using UnityEngine;

namespace FantaLandia.Horror.Audio
{
    /// <summary>
    /// Riproduzione dei suoni: un piccolo pool di sorgenti 3D per i suoni nel mondo e una sorgente 2D per le voci.
    /// Si crea da solo alla prima chiamata.
    /// </summary>
    public class HorrorAudio : MonoBehaviour
    {
        static HorrorAudio instance;
        AudioSource[] pool;
        AudioSource ui;
        int next;

        static HorrorAudio Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Audio (generato)");
                instance = go.AddComponent<HorrorAudio>();
                instance.Init();
                return instance;
            }
        }

        void Init()
        {
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = new GameObject("Sorgente " + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 1f;
                s.maxDistance = 25f;
                pool[i] = s;
            }
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
        }

        void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>Suono nel mondo, in un punto.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 25f)
        {
            if (clip == null) return;
            var self = Instance;
            AudioSource s = self.pool[self.next];
            self.next = (self.next + 1) % self.pool.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.volume = volume;
            s.maxDistance = maxDistance;
            s.clip = clip;
            s.Play();
        }

        /// <summary>Suono "nella testa" del giocatore (interfaccia, voci dei dialoghi).</summary>
        public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            Instance.ui.pitch = pitch;
            Instance.ui.PlayOneShot(clip, volume);
        }

        /// <summary>Crea una sorgente in loop attaccata a un oggetto (TV, ambiente, generatore...).</summary>
        public static AudioSource CreateLoop(Transform parent, AudioClip clip, float volume, bool spatial = true, float maxDistance = 20f)
        {
            var s = parent.gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = maxDistance;
            s.playOnAwake = false;
            return s;
        }
    }
}
