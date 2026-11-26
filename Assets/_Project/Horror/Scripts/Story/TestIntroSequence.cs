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
        [Tooltip("Dove aspetta Tom dopo il saluto (accanto alla porta, per far passare)")]
        public Transform tomBesideDoor;
        [Tooltip("Dove si mette Tom per chiudere la porta")]
        public Transform tomCloseDoor;
        [Tooltip("Davanti alla poltrona (dove arriva camminando)")]
        public Transform chairFront;
        [Tooltip("Il punto a terra sotto il sedile; la sua rotazione è la direzione in cui si guarda da seduti")]
        public Transform chairSeat;
        public float seatHeight = 0.5f;
        [Tooltip("Quando il giocatore è dentro e lontano almeno così dalla porta, Tom la chiude")]
        public float awayDistance = 2.5f;

        enum Phase { WaitingKnock, Greeting, WaitingAtDoor, Leaving, Seated }
        Phase phase = Phase.WaitingKnock;
        bool started;
        Transform player;

        void Start()
        {
            if (door == null) return;
            door.locked = true;
            door.knockable = true;
            door.PlayerKnocked += OnKnocked;
            door.Closed += OnDoorClosed;
            var fpc = FindFirstObjectByType<FantaLandia.Horror.Player.FirstPersonController>();
            if (fpc != null) player = fpc.transform;
        }

        bool PlayerInside => player != null && door.transform.InverseTransformPoint(player.position).z > 0.6f;

        void Update()
        {
            if (phase != Phase.WaitingAtDoor || player == null) return;
            if (!PlayerInside) return;
            if (Vector3.Distance(player.position, door.transform.position) < awayDistance) return;
            if (door.IsOpen) { phase = Phase.Leaving; StartCoroutine(CloseThenSit()); }
        }

        /// <summary>Se la porta la chiude il giocatore (da dentro), Tom va comunque a sedersi.</summary>
        void OnDoorClosed(HorrorDoor d)
        {
            if (phase != Phase.WaitingAtDoor || !PlayerInside) return;
            phase = Phase.Leaving;
            LockForGood();
            StartCoroutine(GoSit());
        }

        void LockForGood()
        {
            door.locked = true;
            door.knockable = false;
        }

        IEnumerator WaitBesideDoor()
        {
            tom.MoveTo(tomBesideDoor.position, false);
            yield return WaitArrive(tom, tomBesideDoor.position, 8f);
            if (phase == Phase.WaitingAtDoor) yield return Face(tom, door.KnockPointWorld, 0.6f);
        }

        IEnumerator CloseThenSit()
        {
            tom.MoveTo(tomCloseDoor.position, false);
            yield return WaitArrive(tom, tomCloseDoor.position, 8f);
            yield return Face(tom, door.KnockPointWorld, 0.35f);
            if (door.IsOpen) door.Close();
            yield return new WaitForSeconds(0.9f);
            LockForGood();
            yield return GoSit();
        }

        IEnumerator GoSit()
        {
            tom.MoveTo(chairFront.position, false);
            yield return WaitArrive(tom, chairFront.position, 15f);
            tom.SitDown(chairSeat.position, chairSeat.rotation, seatHeight);
            phase = Phase.Seated;
        }

        void OnKnocked(HorrorDoor d)
        {
            if (started) return;
            started = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            phase = Phase.Greeting;
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
                .OnEnd(() =>
                {
                    // si sposta di lato per far entrare Muark, e aspetta lì rivolto verso la porta
                    phase = Phase.WaitingAtDoor;
                    StartCoroutine(WaitBesideDoor());
                })
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
                // punto non raggiungibile del tutto: basta essere arrivati il più vicino possibile
                var ag = m.Agent;
                if (t > 0.5f && ag != null && ag.enabled && !ag.pathPending && ag.remainingDistance < 0.3f && m.CurrentSpeed < 0.2f) yield break;
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
