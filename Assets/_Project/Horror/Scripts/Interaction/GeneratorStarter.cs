using System;
using System.Collections;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Player;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>
    /// Il generatore d'emergenza in cantina: si avvia tirando la corda, ma non parte al primo colpo
    /// (tossisce, si spegne, bisogna riprovare). Acceso, fa vibrare il pavimento di rumore.
    /// </summary>
    public class GeneratorStarter : Interactable
    {
        [Tooltip("Tirate necessarie per farlo partire")]
        public int pullsToStart = 3;
        [Tooltip("Finché la storia non lo permette non si può avviare")]
        public bool armed;
        [Header("Spia rossa (si vede al buio)")]
        public Light indicator;
        public Renderer indicatorLamp;
        public Material lampOn, lampOff;
        [Range(0f, 1f)] public float engineVolume = 0.28f;

        public bool Running { get; private set; }
        public int Pulls { get; private set; }
        public event Action<int> Pulled;   // numero della tirata (1, 2, ...), anche quella che lo avvia
        public event Action Started;

        bool busy;
        AudioSource engine;

        static readonly string[] FailLines = { "Dai...", "Avanti, parti!", "Ti prego, ti prego..." };

        public override string Prompt(Interactor who) => Pulls == 0 ? "Tira la corda d'avviamento" : "Riprova a tirare la corda";

        public override bool CanInteract(Interactor who) => enabled && armed && !Running && !busy;

        public override void Interact(Interactor who)
        {
            if (!busy) StartCoroutine(Pull(who.GetComponent<FirstPersonController>()));
        }

        IEnumerator Pull(FirstPersonController fpc)
        {
            busy = true;
            if (fpc != null) fpc.inputLocked = true;
            float yaw = fpc != null ? fpc.Yaw : 0f, pitch = fpc != null ? fpc.Pitch : 0f;

            // ci si china, si tira di scatto, si torna su
            yield return Look(fpc, yaw, pitch, yaw, pitch + 14f, 0.25f);
            HorrorAudio.PlayAt(ProceduralAudio.CordPull(), transform.position + Vector3.up * 0.5f, 0.9f, UnityEngine.Random.Range(0.92f, 1.08f));
            yield return Look(fpc, yaw, pitch + 14f, yaw + 3f, pitch - 6f, 0.18f);
            yield return Look(fpc, yaw + 3f, pitch - 6f, yaw, pitch, 0.35f);

            Pulls++;
            if (Pulls >= pullsToStart)
            {
                HorrorAudio.PlayAt(ProceduralAudio.Sputter(1), transform.position, 0.9f);
                yield return new WaitForSeconds(0.7f);
                Running = true;
                engine = HorrorAudio.CreateLoop(transform, ProceduralAudio.EngineLoop(), 0f, true, 14f);
                engine.Play();
                for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f) { engine.volume = t * engineVolume * FantaLandia.Horror.UI.GameSettings.Effects; engine.pitch = 0.7f + 0.3f * t; yield return null; }
                engine.volume = engineVolume * FantaLandia.Horror.UI.GameSettings.Effects;
                engine.pitch = 1f;
                Pulled?.Invoke(Pulls);
                Started?.Invoke();
            }
            else
            {
                HorrorAudio.PlayAt(ProceduralAudio.Sputter(Pulls == 1 ? 0 : 1), transform.position, 0.8f);
                yield return new WaitForSeconds(0.6f);
                DialogueSystem.Bark(Speakers.Muark, FailLines[(Pulls - 1) % FailLines.Length]);
                Pulled?.Invoke(Pulls);
            }

            if (fpc != null) fpc.inputLocked = false;
            busy = false;
        }

        /// <summary>Già acceso all'inizio della scena (capitolo 2: il generatore gira da un'ora).</summary>
        public void StartRunning()
        {
            if (Running) return;
            Running = true;
            Pulls = pullsToStart;
            engine = HorrorAudio.CreateLoop(transform, ProceduralAudio.EngineLoop(), engineVolume * FantaLandia.Horror.UI.GameSettings.Effects, true, 14f);
            engine.Play();
        }

        /// <summary>Il carburante finisce: il motore tossisce, rallenta e si spegne (non si riaccende più).</summary>
        public IEnumerator Die(float seconds = 2.5f)
        {
            if (!Running) yield break;
            armed = false;
            HorrorAudio.PlayAt(ProceduralAudio.Sputter(0), transform.position, 0.9f);
            float v0 = engine != null ? engine.volume : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (engine != null)
                {
                    engine.pitch = Mathf.Lerp(1f, 0.45f, t) * (1f + Mathf.Sin(t * 40f) * 0.08f * (1f - t));
                    engine.volume = v0 * (1f - t * t);
                }
                if (t > 0.5f && t - Time.deltaTime / seconds <= 0.5f) HorrorAudio.PlayAt(ProceduralAudio.Sputter(1), transform.position, 0.7f, 0.8f);
                yield return null;
            }
            if (engine != null) { engine.Stop(); Destroy(engine); engine = null; }
            Running = false;
            enabled = false; // niente più spia né corda: è a secco
            if (indicator != null) indicator.enabled = false;
            if (indicatorLamp != null && lampOff != null) indicatorLamp.sharedMaterial = lampOff;
        }

        /// <summary>La spia lampeggia piano da spento, resta accesa fissa quando il motore gira.</summary>
        void Update()
        {
            bool on = Running || Mathf.Repeat(Time.time, 1.6f) < 0.8f;
            if (indicator != null)
            {
                indicator.enabled = on;
                indicator.intensity = Running ? 0.9f : 0.6f;
            }
            if (indicatorLamp != null && lampOn != null && lampOff != null)
            {
                Material want = on ? lampOn : lampOff;
                if (indicatorLamp.sharedMaterial != want) indicatorLamp.sharedMaterial = want;
            }
        }

        static IEnumerator Look(FirstPersonController fpc, float y0, float p0, float y1, float p1, float duration)
        {
            if (fpc == null) { yield return new WaitForSeconds(duration); yield break; }
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                float e = t * t * (3f - 2f * t);
                fpc.SetLook(Mathf.LerpAngle(y0, y1, e), Mathf.Lerp(p0, p1, e));
                yield return null;
            }
        }
    }
}
