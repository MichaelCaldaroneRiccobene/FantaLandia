using System.Collections;
using FantaLandia.Characters;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using UnityEngine;

namespace FantaLandia.Horror.Story
{
    /// <summary>
    /// Sequenza di prova dell'arrivo: la porta è chiusa, Muark bussa, Tom risponde, va alla porta,
    /// apre e parte il dialogo. Poi Tom torna in soggiorno.
    /// </summary>
    public class TestIntroSequence : MonoBehaviour
    {
        public HorrorDoor door;
        public CharacterMotor tom;
        [Tooltip("Dove si ferma Tom per aprire la porta (dentro casa)")]
        public Transform tomAtDoor;
        [Tooltip("Dove torna Tom dopo il saluto")]
        public Transform tomInLiving;

        bool started;

        void Start()
        {
            if (door == null) return;
            door.locked = true;
            door.knockable = true;
            door.PlayerKnocked += OnKnocked;
        }

        void OnKnocked(HorrorDoor d)
        {
            if (started) return;
            started = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(0.6f);
            DialogueSystem.Bark(Speakers.Tom, "Arrivo, arrivo!", tom.transform.position);
            yield return new WaitForSeconds(0.6f);

            tom.SetGait(Gait.Walk);
            tom.MoveTo(tomAtDoor.position, false);
            yield return WaitArrive(tom, tomAtDoor.position, 12f);
            yield return Face(tom, door.transform.position, 0.4f);

            door.Unlock();
            door.Open(1); // la porta si apre verso l'interno
            yield return new WaitForSeconds(0.8f);

            DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Muark! Finalmente, pensavo ti fossi perso.")
                .Choice(new[] { "Ho portato la birra, come promesso.", "Scusa, c'era un traffico assurdo." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Grande! Mettila sul bancone in cucina, va."));
                    else DialogueSystem.Insert((Speakers.Tom, "Traffico? Abiti a due isolati..."),
                                               (Speakers.Tom, "Dai, entra. La birra mettila sul bancone in cucina."));
                })
                .Say(Speakers.Tom, "Jef è in bagno. Da mezz'ora. Non chiedere.")
                .OnEnd(() => tom.MoveTo(tomInLiving.position, false))
                .Start();
        }

        static IEnumerator WaitArrive(CharacterMotor m, Vector3 target, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                Vector3 d = m.transform.position - target;
                d.y = 0f;
                if (d.magnitude < 0.35f && m.CurrentSpeed < 0.2f) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator Face(CharacterMotor m, Vector3 point, float duration)
        {
            Vector3 dir = point - m.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;
            Quaternion from = m.transform.rotation, to = Quaternion.LookRotation(dir);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                m.transform.rotation = Quaternion.Slerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
        }
    }
}
