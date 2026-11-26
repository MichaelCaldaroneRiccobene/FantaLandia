using FantaLandia.Horror.Audio;
using FantaLandia.Horror.UI;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// I suoni di una casa vera: il frigo che ronza (si spegne con il blackout: il silenzio pesa di più),
    /// l'orologio della cucina che ticchetta e il legno che ogni tanto scricchiola (più spesso al buio).
    /// </summary>
    public class HouseAmbience : MonoBehaviour
    {
        public Transform fridge;
        public Transform clock;
        public HousePower power;
        [Tooltip("Dove possono venire gli scricchiolii (la casa)")]
        public Bounds house = new Bounds(new Vector3(0f, 3f, 5.5f), new Vector3(14f, 6f, 11f));

        AudioSource hum, tick;
        float nextCreak;

        void Start()
        {
            if (fridge != null) { hum = HorrorAudio.CreateLoop(fridge, ProceduralAudio.FridgeHum(), 0.12f, true, 6f); hum.Play(); }
            if (clock != null) { tick = HorrorAudio.CreateLoop(clock, ProceduralAudio.ClockTick(), 0.1f, true, 7f); tick.Play(); }
            nextCreak = Time.time + Random.Range(15f, 30f);
        }

        void Update()
        {
            bool on = power == null || power.powered;
            if (hum != null)
            {
                hum.volume = Mathf.MoveTowards(hum.volume, on ? 0.12f * GameSettings.Effects : 0f, Time.deltaTime * 0.2f);
                if (hum.volume <= 0.001f && hum.isPlaying) hum.Stop();
                if (hum.volume > 0.001f && !hum.isPlaying) hum.Play();
            }
            if (tick != null) tick.volume = 0.1f * GameSettings.Effects;

            if (Time.time < nextCreak) return;
            nextCreak = Time.time + (on ? Random.Range(20f, 45f) : Random.Range(8f, 18f));
            Vector3 p = new Vector3(Random.Range(house.min.x, house.max.x), Random.Range(house.min.y, house.max.y), Random.Range(house.min.z, house.max.z));
            HorrorAudio.PlayAt(ProceduralAudio.WoodCreak(Random.Range(0, 4)), p, on ? 0.25f : 0.4f, Random.Range(0.85f, 1.1f), 14f);
        }
    }
}
