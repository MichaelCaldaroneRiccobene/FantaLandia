using System.Collections;
using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using FantaLandia.Horror.World;
using UnityEngine;

namespace FantaLandia.Horror.Story
{
    /// <summary>
    /// La regia della prima serata a casa di Tom (fase 4), dall'arrivo al notiziario:
    /// arrivo e birra → scherzo di Roy ("Buu!") → si aspetta la pizza → esplosione → tutti in giardino (fumo, sirene, vicini)
    /// → arriva Jef → di nuovo davanti alla TV → blackout → teorie e sasso-carta-forbice truccato → cantina e generatore
    /// → notiziario. I personaggi vanno verso la meta da soli; la scena parte quando il giocatore li raggiunge.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public enum StartAt { Inizio, ScherzoDiRoy, Salotto, Esplosione, Blackout, Cantina, Notiziario }

        [Header("Prova: da dove parte la storia")]
        public StartAt startAt = StartAt.Inizio;

        [Header("Porte")]
        public HorrorDoor frontDoor;
        public HorrorDoor backDoor;
        public HorrorDoor bathDoor;
        public HorrorDoor cellarDoor;

        [Header("Personaggi")]
        public CharacterMotor tom;
        public CharacterMotor roy;
        public CharacterMotor jef;
        public NpcActor[] neighbors; // Frank, Martha, Gary, Linda

        [Header("Punti")]
        public Transform tomAtDoor;
        public Transform tomBesideDoor;
        public Transform tomCloseDoor;
        public Transform chairFront, chairSeat;
        public float chairHeight = 0.5f;
        public Transform royDoorstep;
        public Transform sofaFrontRoy, sofaSeatRoy, sofaFrontJef, sofaSeatJef;
        public float sofaHeight = 0.5f;
        public Transform deckTom, deckRoy, deckJef;
        public Transform livingRoom;
        public float livingRoomRadius = 3.2f;
        public Transform playerKitchen;      // per le prove: davanti all'isola
        public Transform playerCellarTop;    // per le prove: in cima alla scala della cantina
        public Transform cellarScratch;      // dove si sentono i graffi nel muro
        public float basementFloorY = -2.5f;
        public Bounds houseBounds = new Bounds(new Vector3(0f, 1.5f, 5.5f), new Vector3(14f, 10f, 11f));

        [Header("Oggetti")]
        public PlaceSpot island;
        public CrtTv tv;
        public HousePower house;
        public NeighborhoodPower neighborhood;
        public SmokeColumn smoke;
        public GeneratorStarter generator;
        public Light[] cellarBulbs;
        public AmbientSound nightAmbience;

        // ------------------------------------------------------------------ stato

        Transform player;
        FirstPersonController fpc;
        Interactor interactor;
        PlayerFlashlight flashlight;
        readonly Dictionary<CharacterMotor, Vector3> seatFront = new Dictionary<CharacterMotor, Vector3>();
        string objective = "";
        float fade;           // 0 = niente, 1 = nero
        string endTitle;
        float shakeTime, shakeAmount;
        AudioSource sirens, drone;
        float sirenTarget, droneTarget;

        bool PlayerInside => player != null && houseBounds.Contains(player.position);
        bool PlayerInBasement => player != null && player.position.y < basementFloorY + 0.8f;
        bool PlayerInLivingRoom => player != null && livingRoom != null && Flat(player.position, livingRoom.position) < livingRoomRadius
                                   && Mathf.Abs(player.position.y - livingRoom.position.y) < 1.2f;

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        void Start()
        {
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                player = fpc.transform;
                interactor = fpc.GetComponent<Interactor>();
                flashlight = fpc.GetComponent<PlayerFlashlight>();
            }
            sirens = HorrorAudio.CreateLoop(transform, ProceduralAudio.Siren(), 0f, false);
            drone = HorrorAudio.CreateLoop(transform, ProceduralAudio.Drone(), 0f, false);
            if (generator != null) generator.armed = false;
            foreach (var b in cellarBulbs) if (b != null) b.enabled = false;
            StartCoroutine(Main());
        }

        IEnumerator Main()
        {
            yield return null; // gli agenti si attivano al primo frame
            int s = (int)startAt;
            if (s > 0) yield return SkipTo(startAt);

            if (s <= (int)StartAt.Inizio) yield return Arrival();
            if (s <= (int)StartAt.ScherzoDiRoy) yield return RoyPrank();
            if (s <= (int)StartAt.Salotto) yield return WaitingForPizza();
            if (s <= (int)StartAt.Esplosione) { yield return Explosion(); yield return Backyard(); yield return JefComesOut(); yield return BackToTheTv(); }
            if (s <= (int)StartAt.Blackout) yield return Blackout();
            if (s <= (int)StartAt.Cantina) yield return Basement();
            yield return News();
        }

        // ================================================================== 1. arrivo

        bool tomSeated;

        IEnumerator Arrival()
        {
            objective = "Bussa alla porta di Tom";
            frontDoor.locked = true;
            frontDoor.knockable = true;
            bool knocked = false;
            System.Action<HorrorDoor> onKnock = d => knocked = true;
            frontDoor.PlayerKnocked += onKnock;
            while (!knocked) yield return null;
            frontDoor.PlayerKnocked -= onKnock;
            objective = "";

            yield return new WaitForSeconds(0.6f);
            DialogueSystem.Bark(Speakers.Tom, "Arrivo, arrivo!", tom.transform.position);
            yield return new WaitForSeconds(0.6f);
            yield return Actor(tom).WalkTo(tomAtDoor.position);
            yield return Actor(tom).FaceTowards(frontDoor.transform.position, 0.4f);
            frontDoor.Unlock();
            frontDoor.Open(1);
            yield return new WaitForSeconds(0.8f);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Muark! Finalmente, pensavo ti fossi perso.")
                .Choice(new[] { "Ho portato la birra, come promesso.", "Scusa, c'era un traffico assurdo." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Grande! Mettila sull'isola in cucina, va."));
                    else DialogueSystem.Insert((Speakers.Tom, "Traffico? Abiti a due isolati..."),
                                               (Speakers.Tom, "Dai, entra. La birra mettila sull'isola in cucina."));
                })
                .Say(Speakers.Tom, "Jef è in bagno. Da mezz'ora. Non chiedere."));

            objective = "Posa la birra in cucina";
            Job(tom, TomHostsThenSits());
            while (island != null && island.Placed == null) yield return null;
            objective = "";
        }

        /// <summary>Tom aspetta accanto alla porta che Muark entri, la chiude e torna in poltrona.</summary>
        IEnumerator TomHostsThenSits()
        {
            yield return Actor(tom).WalkTo(tomBesideDoor.position);
            yield return Actor(tom).FaceTowards(frontDoor.KnockPointWorld, 0.6f);
            while (!(PlayerInside && player.position.z > 1f && Flat(player.position, frontDoor.transform.position) > 2.5f)) yield return null;
            if (frontDoor.IsOpen)
            {
                yield return Actor(tom).WalkTo(tomCloseDoor.position);
                yield return Actor(tom).FaceTowards(frontDoor.KnockPointWorld, 0.35f);
                frontDoor.Close();
                yield return new WaitForSeconds(0.9f);
            }
            frontDoor.locked = true;
            frontDoor.knockable = false;
            yield return SitOn(tom, chairFront.position, chairSeat, chairHeight);
            tomSeated = true;
        }

        // ================================================================== 2. lo scherzo di Roy

        IEnumerator RoyPrank()
        {
            float wait = 0f;
            while (!tomSeated && wait < 25f) { wait += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(2.5f);

            frontDoor.Knock();
            frontDoor.locked = false;
            frontDoor.knockable = false;
            yield return new WaitForSeconds(1.2f);
            DialogueSystem.Bark(Speakers.Tom, "Muark, vai a vedere tu? Io qui sto troppo comodo.", tom.transform.position + Vector3.up * 1.2f);
            objective = "Vai ad aprire la porta";

            bool opened = false;
            System.Action<HorrorDoor> onOpen = d => opened = true;
            frontDoor.Opened += onOpen;
            float t = 0f;
            int reminders = 0;
            while (!opened)
            {
                t += Time.deltaTime;
                if (t > 14f && reminders < 3)
                {
                    t = 0f;
                    reminders++;
                    frontDoor.Knock();
                    DialogueSystem.Bark(Speakers.Tom, reminders == 1 ? "Muark! La porta!" : "Stanno ancora bussando...", tom.transform.position + Vector3.up * 1.2f);
                }
                yield return null;
            }
            frontDoor.Opened -= onOpen;
            objective = "";

            // --- scena: non c'è nessuno
            Lock(true);
            yield return new WaitForSeconds(0.5f);
            Vector3 outside = royDoorstep.position + Vector3.up * 1.6f - royDoorstep.forward * 2f;
            yield return LookAt(outside, 0.6f);
            yield return new WaitForSeconds(1.1f);
            DialogueSystem.Bark(Speakers.Muark, "...C'è qualcuno?");
            yield return new WaitForSeconds(1.6f);
            yield return LookAt(Eye(tom), 0.9f);
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Muark, "Ragazzi, non c'è nessuno! Ci hanno fatto uno scherzo.")
                .Say(Speakers.Tom, "Saranno i ragazzini dei Kowalski. Chiudi, dai, che entra il freddo."));
            Lock(true);

            // --- BUU!
            Warp(roy, royDoorstep.position, royDoorstep.eulerAngles.y);
            roy.gameObject.SetActive(true);
            yield return null;
            Vector3 royFace = royDoorstep.position + Vector3.up * 1.62f;
            StartCoroutine(LookAt(royFace, 0.22f));
            yield return new WaitForSeconds(0.12f);
            HorrorAudio.Play2D(ProceduralAudio.Sting(), 0.9f);
            DialogueSystem.Bark(Speakers.Roy, "BUUU!!", royFace);
            Shake(1.2f, 0.6f);
            StartCoroutine(RoyClaws(1.4f));
            yield return new WaitForSeconds(1.6f);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Roy, "AH AH AH! Dovevi vedere la tua faccia, Muark!")
                .Choice(new[] { "Roy! Mi hai fatto prendere un colpo!", "Sei un idiota. Entra, dai." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Roy, "Lo so, lo so. Sono un genio."));
                    else DialogueSystem.Insert((Speakers.Roy, "Ti voglio bene anch'io, amico."));
                })
                .Say(Speakers.Roy, "Allora? Ho sentito che c'è la pizza. Dov'è la pizza?"));
            Lock(false);

            // Roy entra, chiude la porta e va sul divano
            Job(roy, RoyGoesIn());
        }

        IEnumerator RoyGoesIn()
        {
            Actor(roy).Motor.SetGait(Gait.Walk);
            roy.MoveTo(tomCloseDoor.position + Vector3.forward * 0.4f, false);
            float t = 0f;
            while (roy.transform.position.z < 1.1f && t < 10f) { t += Time.deltaTime; yield return null; }
            if (frontDoor.IsOpen) frontDoor.Close();
            frontDoor.locked = true;
            frontDoor.knockable = false;
            yield return SitOn(roy, sofaFrontRoy.position, sofaSeatRoy, sofaHeight);
        }

        IEnumerator RoyClaws(float seconds)
        {
            var anim = roy.GetComponentInChildren<ProceduralBodyAnimator>();
            if (anim == null) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float w = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((seconds - t) / 0.4f);
                Transform r = roy.transform;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 hand = r.position + Vector3.up * (1.72f + Mathf.Sin(t * 25f) * 0.03f) + r.forward * 0.42f + r.right * (side * 0.32f);
                    Vector3 pole = r.right * side * 0.8f + Vector3.down * 0.6f;
                    anim.SetExternalHandIK(side, hand, pole, w, ProceduralBodyAnimator.PalmDown(side, r.forward + Vector3.up));
                }
                yield return null;
            }
        }

        // ================================================================== 3. si aspetta la pizza

        IEnumerator WaitingForPizza()
        {
            // l'obiettivo compare quando Roy si è seduto; la scena parte davanti al divano
            while (!roy.IsSeated) yield return null;
            objective = "Vai in salotto";
            Vector3 sofaFront = (sofaFrontRoy.position + sofaFrontJef.position) * 0.5f;
            while (!(Flat(player.position, sofaFront) < 1.7f && Mathf.Abs(player.position.y - sofaFront.y) < 1f)) yield return null;
            objective = "";
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Eccoci qua. Beh, quasi tutti: Jef è ancora in bagno.")
                .Say(Speakers.Roy, "Ancora? È lì dentro da quando sono nato.")
                .Say(Speakers.Tom, "Ho ordinato due pizze. Ora bisogna solo aspettare la pizza.")
                .Choice(new[] { "Che pizze hai preso?", "Speriamo che arrivi presto, ho una fame..." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Una salame piccante e una con l'ananas. Quella è per Jef."),
                                                      (Speakers.Roy, "Che schifo."));
                    else DialogueSystem.Insert((Speakers.Roy, "Anch'io. Mi mangerei il divano."));
                }));
            yield return new WaitForSeconds(7f); // un po' di cartoni
        }

        // ================================================================== 4. l'esplosione

        IEnumerator Explosion()
        {
            if (smoke != null) smoke.Ignite();
            HorrorAudio.Play2D(ProceduralAudio.Explosion(), 1f);
            Shake(1.6f, 1.8f);
            if (neighborhood != null) StartCoroutine(neighborhood.Flicker(1.3f));
            StartCoroutine(FlickerHouse(1.3f));
            HorrorAudio.PlayAt(ProceduralAudio.Thud(), tv != null ? tv.transform.position : player.position, 0.4f, 1.6f); // vibrano i vetri
            StartCoroutine(TvLosesSignal(3.5f));
            yield return new WaitForSeconds(1.8f);

            Stand(tom);
            Stand(roy);
            yield return new WaitForSeconds(0.4f);
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Cos'è stato?!")
                .Say(Speakers.Roy, "Veniva da dietro! Dal giardino!")
                .Say(Speakers.Tom, "Andiamo a vedere, presto!"));

            sirenTarget = 0.35f;
            SetNeighborChatter(true);
            backDoor.Unlock();
            backDoor.Open(-1);
            objective = "Segui gli altri in giardino";
            Job(tom, Actor(tom).WalkTo(deckTom.position, Gait.Jog, 25f));
            Job(roy, Actor(roy).WalkTo(deckRoy.position, Gait.Jog, 25f));
        }

        IEnumerator TvLosesSignal(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tv == null) yield break;
            tv.SetChannel(TvChannel.Off);
            yield return new WaitForSeconds(0.12f);
            tv.SetChannel(TvChannel.Static);
        }

        /// <summary>Tom cambia canale: clic, schermo nero un attimo, e di nuovo neve.</summary>
        IEnumerator ChannelSurf(int times)
        {
            for (int i = 0; i < times && tv != null; i++)
            {
                HorrorAudio.PlayAt(ProceduralAudio.Click(), tv.transform.position, 0.8f, Random.Range(0.9f, 1.1f));
                tv.SetChannel(TvChannel.Off);
                yield return new WaitForSeconds(0.15f);
                tv.SetChannel(TvChannel.Static);
                yield return new WaitForSeconds(0.55f);
            }
        }

        IEnumerator FlickerHouse(float seconds)
        {
            if (house == null) yield break;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                bool on = Random.value > 0.4f;
                foreach (var l in house.lights) if (l != null) l.enabled = on && house.powered;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.15f));
            }
            foreach (var l in house.lights) if (l != null) l.enabled = house.powered;
        }

        void SetNeighborChatter(bool afterExplosion)
        {
            if (neighbors == null || neighbors.Length < 4) return;
            string[] millers = afterExplosion
                ? new[] { "martha|Oh santo cielo, Frank, guarda quel fumo...", "frank|Torna dentro, Martha! Potrebbe esplodere ancora!", "martha|E se è la fabbrica? Ci abita la figlia dei Parker là vicino!", "frank|Chiamo lo sceriffo. Dov'è il telefono?" }
                : null;
            string[] kowalski = afterExplosion
                ? new[] { "gary|Linda! Prendi la videocamera!", "linda|Gary, non è il momento!", "gary|Ehi Tom! Avete sentito anche voi?", "linda|Sento le sirene... stanno arrivando." }
                : null;
            if (millers == null) return;
            neighbors[0].ambientLines = millers;
            neighbors[2].ambientLines = kowalski;
            foreach (int k in new[] { 0, 2 })
            {
                neighbors[k].ambientDistance = 40f;
                neighbors[k].ambientInterval = new Vector2(3.5f, 6f);
            }
            // tutti si girano verso il fumo
            if (smoke != null)
                foreach (var n in neighbors)
                    if (n != null && n.Face != null) n.Face.lookTarget = smoke.transform;
        }

        // ================================================================== 5. in giardino

        bool InBackyard => player != null && Flat(player.position, deckTom.position) < 4.5f && player.position.y < 1.2f;

        IEnumerator Backyard()
        {
            while (!InBackyard) yield return null;
            objective = "";
            for (float w = 0f; w < 10f && (Flat(tom.transform.position, deckTom.position) > 0.8f || Flat(roy.transform.position, deckRoy.position) > 0.8f); w += Time.deltaTime)
                yield return null;
            Vector3 smokeTop = smoke != null ? smoke.transform.position + Vector3.up * 22f : deckTom.position + Vector3.forward * 40f;
            StartCoroutine(Actor(tom).FaceTowards(smokeTop, 0.6f));
            StartCoroutine(Actor(roy).FaceTowards(smokeTop, 0.6f));
            Lock(true);
            yield return LookAt(smokeTop, 1.2f);
            yield return new WaitForSeconds(1.5f);
            Lock(false);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Guardate là... quanto fumo!")
                .Say(Speakers.Roy, "Sarà saltata una conduttura del gas. O un distributore.")
                .Choice(new[] { "Sembra vicino... a qualche isolato da qui.", "Speriamo che non ci sia nessun ferito." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Verso la vecchia centrale, forse. Dietro Elm Street."));
                    else DialogueSystem.Insert((Speakers.Roy, "Già... senti quante sirene."));
                })
                .Do(() => Job(jef, JefRunsOut()))   // intanto Jef esce dal bagno e arriva di corsa
                .Say(Speakers.Gary, "Ehi, Tom! L'avete sentita anche voi?!")
                .Say(Speakers.Tom, "Sì, Gary! Sai cos'è successo?")
                .Say(Speakers.Gary, "Non ne ho idea! Ha tremato tutta la casa!")
                .Say(Speakers.Martha, "Frank dice che è il gas. Frank dice sempre che è il gas."));
            yield return new WaitForSeconds(3f);
        }

        // ================================================================== 6. Jef esce dal bagno

        bool jefOut;

        IEnumerator JefRunsOut()
        {
            if (jefOut) yield break;
            jefOut = true;
            bathDoor.Unlock();
            bathDoor.Open(-1);
            HorrorAudio.PlayAt(ProceduralAudio.DoorCreak(1), bathDoor.transform.position, 0.6f);
            yield return Actor(jef).WalkTo(deckJef.position, Gait.Jog, 40f);
        }

        IEnumerator JefComesOut()
        {
            if (!jefOut) Job(jef, JefRunsOut());
            for (float t = 0f; t < 40f && Flat(jef.transform.position, deckJef.position) > 0.8f; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return Actor(jef).FaceTowards(player.position, 0.5f);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Jef, "Che succede? Ho sentito un botto.")
                .Choice(new[] { "Non lo so. C'è stata un'esplosione.", "Jef! Sei vivo! Pensavamo fossi caduto nel water." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Jef, "...Un'esplosione."));
                    else DialogueSystem.Insert((Speakers.Jef, "Molto divertente."), (Speakers.Roy, "Ti sei perso tutto, amico."));
                })
                .Say(Speakers.Tom, "Torniamo dentro. Magari in TV dicono qualcosa."));
        }

        // ================================================================== 7. di nuovo davanti alla TV

        IEnumerator BackToTheTv()
        {
            objective = "Torna dentro con gli altri";
            Job(tom, SitOn(tom, chairFront.position, chairSeat, chairHeight));
            Job(roy, SitOn(roy, sofaFrontRoy.position, sofaSeatRoy, sofaHeight));
            Job(jef, SitOn(jef, sofaFrontJef.position, sofaSeatJef, sofaHeight));
            while (!PlayerInside) yield return null;
            sirenTarget = 0.18f;
            while (!PlayerInLivingRoom) yield return null;
            objective = "";
            if (backDoor.IsOpen) backDoor.Close();
            if (tv != null) tv.SetChannel(TvChannel.Static);
            yield return new WaitForSeconds(1f);
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Ma che... c'è solo la neve.")
                .Do(() => StartCoroutine(ChannelSurf(3)))
                .Say(Speakers.Roy, "Cambia canale, dai!")
                .Say(Speakers.Tom, "Ci sto provando! Canale 4... canale 7...")
                .Do(() => StartCoroutine(ChannelSurf(4)))
                .Say(Speakers.Tom, "Niente. Sono tutti così. Nessun segnale.")
                .Say(Speakers.Jef, "..."));
            yield return new WaitForSeconds(7f);
        }

        // ================================================================== 8. blackout, teorie e sasso-carta-forbice

        IEnumerator Blackout()
        {
            HorrorAudio.Play2D(ProceduralAudio.PowerDown(), 0.8f);
            if (house != null) house.SetPower(false);
            if (neighborhood != null) neighborhood.SetPower(false);
            sirenTarget = 0f;   // le sirene si spengono... e anche i grilli: silenzio
            StartCoroutine(FadeNight(6f));
            yield return new WaitForSeconds(2.5f);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Roy, "Ehi! Chi ha spento la luce?")
                .Say(Speakers.Tom, "È saltata la corrente... Guardate fuori: è buio tutto il quartiere.")
                .Say(Speakers.Tom, "Ve lo dico io: sono i terroristi. Prima l'esplosione, adesso questo.")
                .Say(Speakers.Roy, "Terroristi? A Maple Grove? Sono gli alieni, amico. Fidati.")
                .Choice(new[] { "Smettetela, mi state mettendo ansia.", "Jef, tu che ne pensi?" }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Roy, "Ansia? Io? Ma figurati."));
                    else DialogueSystem.Insert((Speakers.Jef, "..."), (Speakers.Tom, "Jef?"), (Speakers.Jef, "..."));
                })
                .Say(Speakers.Tom, "Aspettate! In cantina c'è il generatore d'emergenza. Mio padre l'ha comprato per gli uragani.")
                .Say(Speakers.Roy, "Grande! Vai.")
                .Say(Speakers.Tom, "Io? Neanche morto, laggiù non ci scendo. Facciamo così: sasso, carta, forbice. Chi perde va giù.")
                .Say(Speakers.Tom, "Muark, tu contro di me. Pronto? Sasso... carta... forbice!")
                .Choice(new[] { "Sasso!", "Carta!", "Forbice!" }, i =>
                {
                    // Tom vince sempre
                    string[] tomPlays = { "Carta! Carta batte sasso.", "Forbice! Forbice batte carta.", "Sasso! Sasso batte forbice." };
                    DialogueSystem.Insert((Speakers.Tom, tomPlays[i] + " Ho vinto!"),
                                          (Speakers.Roy, "Muark va in cantina! Muark va in cantina!"));
                })
                .Choice(new[] { "Ma dai! Al meglio di tre?", "Va bene, va bene... ci vado io." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Niente rivincite. Le regole sono regole."));
                    else DialogueSystem.Insert((Speakers.Tom, "Bravo. Così si fa."));
                })
                .Say(Speakers.Tom, "C'è una torcia sul bancone della cucina. La porta della cantina è quella accanto all'isola.")
                .Say(Speakers.Jef, "Stai attento, laggiù."));
            if (generator != null) generator.armed = true;
        }

        IEnumerator FadeNight(float seconds)
        {
            AudioSource night = nightAmbience != null ? nightAmbience.GetComponent<AudioSource>() : null;
            if (night == null) yield break;
            float v0 = night.volume;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds) { night.volume = v0 * (1f - t); yield return null; }
            night.volume = 0f;
        }

        // ================================================================== 9. la cantina

        IEnumerator Basement()
        {
            if (flashlight != null && !flashlight.HasFlashlight)
            {
                objective = "Prendi la torcia sul bancone della cucina";
                while (!flashlight.HasFlashlight) yield return null;
            }
            objective = "Scendi in cantina e avvia il generatore";
            while (!PlayerInBasement) yield return null;
            droneTarget = 0.22f;

            // passi lenti di sopra, proprio sopra la testa
            yield return new WaitForSeconds(1.5f);
            Vector3 above = player.position + Vector3.up * 3.1f;
            Vector3 walk = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            for (int i = 0; i < 6; i++)
            {
                HorrorAudio.PlayAt(ProceduralAudio.Footstep(Surface.Wood, i % 4), above + walk * (i * 0.55f), 0.7f, 0.8f, 14f);
                yield return new WaitForSeconds(0.85f);
            }
            DialogueSystem.Bark(Speakers.Muark, "...Ragazzi? Siete voi?");
            yield return new WaitForSeconds(2.5f);

            // graffi nel muro
            Vector3 gen = generator != null ? generator.transform.position : player.position;
            float t = 0f;
            while (Flat(player.position, gen) > 4.5f && t < 14f) { t += Time.deltaTime; yield return null; }
            if (cellarScratch != null) HorrorAudio.PlayAt(ProceduralAudio.Scratch(), cellarScratch.position, 0.8f, 0.9f, 16f);
            yield return new WaitForSeconds(2.6f);

            // vicino al generatore: qualcosa cade alle spalle
            while (Flat(player.position, gen) > 2.6f) yield return null;
            Vector3 behind = player.position - player.forward * 3f + Vector3.up * 0.4f;
            HorrorAudio.PlayAt(ProceduralAudio.Thud(), behind, 1f, 1f, 18f);
            HorrorAudio.Play2D(ProceduralAudio.Sting(), 0.35f, 0.8f);
            Shake(0.4f, 0.3f);
            yield return new WaitForSeconds(0.7f);
            DialogueSystem.Bark(Speakers.Muark, "Chi c'è?!");
            objective = "Avvia il generatore";

            bool started = false;
            System.Action onStart = () => started = true;
            System.Action<int> onPull = n =>
            {
                if (n == 2) HorrorAudio.PlayAt(ProceduralAudio.Whisper(), player.position - player.right * 1.6f + Vector3.up * 1.5f, 0.5f, 0.9f, 6f);
            };
            if (generator != null)
            {
                generator.Started += onStart;
                generator.Pulled += onPull;
                while (!started) yield return null;
                generator.Started -= onStart;
                generator.Pulled -= onPull;
            }
            objective = "";

            // corrente dal generatore: le lampadine della cantina e la TV (le luci di sopra restano spente)
            StartCoroutine(BulbsOn());
            if (tv != null) { tv.powered = true; tv.SetChannel(TvChannel.News); }
            yield return new WaitForSeconds(1f);
            DialogueSystem.Bark(Speakers.Muark, "Sì! Ce l'ho fatta.");
            yield return new WaitForSeconds(2.2f);

            // la porta della cantina sbatte di colpo
            Vector3 doorPos = cellarDoor != null ? cellarDoor.transform.position : player.position + Vector3.up * 3f;
            if (cellarDoor != null && cellarDoor.IsOpen) cellarDoor.Close();
            HorrorAudio.PlayAt(ProceduralAudio.DoorShut(), doorPos, 1f, 0.85f, 30f);
            HorrorAudio.Play2D(ProceduralAudio.Sting(), 0.8f);
            Shake(0.9f, 0.5f);
            yield return new WaitForSeconds(1.4f);
            DialogueSystem.Bark(Speakers.Muark, "...Ragazzi?! Non è divertente!");
            objective = "Torna di sopra dagli altri";

            while (PlayerInBasement || player.position.y < livingRoom.position.y - 0.5f) yield return null;
            droneTarget = 0f;
            DialogueSystem.Bark(Speakers.Roy, "Muark! Vieni, presto! C'è il notiziario!", Eye(roy));
        }

        IEnumerator BulbsOn()
        {
            HorrorAudio.Play2D(ProceduralAudio.Click(), 0.5f);
            for (int k = 0; k < 6; k++)
            {
                bool on = k % 2 == 0;
                foreach (var b in cellarBulbs) if (b != null) b.enabled = on;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
            }
            foreach (var b in cellarBulbs) if (b != null) b.enabled = true;
        }

        // ================================================================== 10. il notiziario

        IEnumerator News()
        {
            objective = "Raggiungi gli altri davanti alla TV";
            while (!PlayerInLivingRoom) yield return null;
            objective = "";
            yield return Talk(DialogueSystem.Conversation()
                .Choice(new[] { "Chi di voi ha sbattuto la porta della cantina?", "Il generatore va. Cosa dicono?" }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Quale porta? Siamo stati qui tutto il tempo."), (Speakers.Roy, "Shh! Ascoltate!"));
                    else DialogueSystem.Insert((Speakers.Tom, "Shh! Ascolta."));
                })
                .Say(Speakers.Tv, "Interrompiamo le trasmissioni per un'edizione straordinaria.")
                .Say(Speakers.Tv, "Questa sera, poco dopo le nove, una forte esplosione ha colpito la zona est di Maple Grove.")
                .Say(Speakers.Tv, "Le cause non sono ancora note. Le linee elettriche e telefoniche di tutta la contea sono fuori uso.")
                .Say(Speakers.Tv, "Le autorità raccomandano ai residenti di restare in casa, di chiudere porte e finestre e di non uscire per nessun motivo fino a domattina.")
                .Say(Speakers.Tv, "Ripetiamo: non uscite di casa. Non aprite a nessuno.")
                .Say(Speakers.Roy, "Ok... ok. Adesso ho paura sul serio.")
                .Say(Speakers.Tom, "Non aprite a nessuno?! Che vuol dire \"non aprite a nessuno\"?!")
                .Choice(new[] { "Calma. Chiudiamo tutto e aspettiamo domattina.", "Dobbiamo andarcene da qui!" }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Sì... sì. Porte, finestre, tutto."));
                    else DialogueSystem.Insert((Speakers.Roy, "E dove vuoi andare, con questo buio?! Hai sentito la TV!"));
                })
                .Say(Speakers.Jef, "Non è stata un'esplosione.")
                .Say(Speakers.Tom, "Cosa? Jef... che stai dicendo?")
                .Say(Speakers.Jef, "..."));

            // la TV perde il segnale, poi il buio
            if (tv != null) tv.SetChannel(TvChannel.Static);
            HorrorAudio.Play2D(ProceduralAudio.Whisper(), 0.25f, 0.7f);
            yield return new WaitForSeconds(2.5f);
            Lock(true);
            endTitle = "Fine del capitolo 1\n\n<size=22>Continua...</size>";
            for (float t = 0f; t < 1f; t += Time.deltaTime / 3f) { fade = t; yield return null; }
            fade = 1f;
        }

        // ================================================================== salti per le prove

        IEnumerator SkipTo(StartAt at)
        {
            int s = (int)at;
            Lock(true);
            fade = 1f;
            // la birra è già sull'isola, Tom in poltrona, la porta chiusa
            if (interactor != null && interactor.Held != null && island != null) island.CompletePlace(interactor);
            frontDoor.locked = true;
            frontDoor.knockable = false;
            TeleportPlayer(playerKitchen);
            SitNow(tom, chairFront.position, chairSeat, chairHeight);
            tomSeated = true;

            if (s >= (int)StartAt.Salotto)
            {
                Warp(roy, sofaFrontRoy.position, 0f);
                roy.gameObject.SetActive(true);
                yield return null;
                SitNow(roy, sofaFrontRoy.position, sofaSeatRoy, sofaHeight);
                TeleportPlayer(livingRoom);
            }
            if (s >= (int)StartAt.Blackout)
            {
                if (smoke != null) smoke.Ignite();
                bathDoor.Unlock();
                Warp(jef, sofaFrontJef.position, 0f);
                yield return null;
                SitNow(jef, sofaFrontJef.position, sofaSeatJef, sofaHeight);
            }
            if (s >= (int)StartAt.Cantina)
            {
                if (house != null) house.SetPower(false);
                if (neighborhood != null) neighborhood.SetPower(false);
                StartCoroutine(FadeNight(0.1f));
                if (generator != null) generator.armed = true;
                TeleportPlayer(playerKitchen);
            }
            if (s >= (int)StartAt.Notiziario)
            {
                foreach (var b in cellarBulbs) if (b != null) b.enabled = true;
                if (tv != null) { tv.powered = true; tv.SetChannel(TvChannel.News); }
                TeleportPlayer(playerCellarTop);
            }
            yield return new WaitForSeconds(0.6f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.8f) { fade = t; yield return null; }
            fade = 0f;
            Lock(false);
        }

        void TeleportPlayer(Transform at)
        {
            if (at == null || player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = at.position;
            if (cc != null) cc.enabled = true;
            fpc.SetLook(at.eulerAngles.y, 0f);
        }

        // ================================================================== attori

        NpcActor Actor(CharacterMotor m) => m.GetComponent<NpcActor>();

        readonly Dictionary<CharacterMotor, Coroutine> jobs = new Dictionary<CharacterMotor, Coroutine>();

        /// <summary>Un ordine per personaggio alla volta: quello nuovo annulla il precedente (es. andare a sedersi).</summary>
        void Job(CharacterMotor m, IEnumerator routine)
        {
            if (jobs.TryGetValue(m, out Coroutine c) && c != null) StopCoroutine(c);
            jobs[m] = StartCoroutine(routine);
        }

        static Vector3 Eye(CharacterMotor m)
        {
            var sp = m.GetComponent<NpcSpeaker>();
            return sp != null ? sp.EyePoint : m.transform.position + Vector3.up * 1.6f;
        }

        IEnumerator SitOn(CharacterMotor m, Vector3 front, Transform seat, float height)
        {
            if (m.IsSeated) yield break;
            seatFront[m] = front;
            yield return Actor(m).WalkTo(front);
            m.SitDown(seat.position, seat.rotation, height);
            yield return new WaitForSeconds(2.4f);
        }

        void SitNow(CharacterMotor m, Vector3 front, Transform seat, float height)
        {
            seatFront[m] = front;
            Warp(m, front, seat.eulerAngles.y);
            m.SitDown(seat.position, seat.rotation, height);
        }

        void Stand(CharacterMotor m)
        {
            if (!m.IsSeated) return;
            m.StandUp(seatFront.TryGetValue(m, out Vector3 p) ? p : m.transform.position + m.transform.forward * 0.7f);
        }

        static void Warp(CharacterMotor m, Vector3 pos, float yaw)
        {
            var ag = m.Agent;
            if (ag != null && ag.enabled && ag.isOnNavMesh) ag.Warp(pos);
            else m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ================================================================== giocatore e camera

        void Lock(bool locked)
        {
            if (fpc != null) fpc.inputLocked = locked;
            if (interactor != null) interactor.interactionEnabled = !locked;
        }

        IEnumerator LookAt(Vector3 point, float duration)
        {
            if (fpc == null || fpc.cam == null) yield break;
            Vector3 dir = point - fpc.cam.transform.position;
            float yaw1 = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch1 = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -70f, 70f);
            float yaw0 = fpc.Yaw, pitch0 = fpc.Pitch;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
                float e = t * t * (3f - 2f * t);
                fpc.SetLook(Mathf.LerpAngle(yaw0, yaw1, e), Mathf.Lerp(pitch0, pitch1, e));
                yield return null;
            }
        }

        IEnumerator Talk(DialogueSystem.Builder conversation)
        {
            while (DialogueSystem.IsBlocking) yield return null;
            bool done = false;
            conversation.OnEnd(() => done = true).Start();
            while (!done) yield return null;
        }

        void Shake(float amount, float seconds)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTime = Mathf.Max(shakeTime, seconds);
        }

        void Update()
        {
            // sirene: più forti fuori, attutite in casa
            if (sirens != null)
            {
                float want = sirenTarget * (PlayerInside ? 0.45f : 1f);
                sirens.volume = Mathf.MoveTowards(sirens.volume, want, Time.deltaTime * (want > sirens.volume ? 0.01f : 0.12f));
                if (sirens.volume > 0.001f && !sirens.isPlaying) sirens.Play();
                if (sirens.volume <= 0.001f && sirens.isPlaying) sirens.Stop();
            }
            if (drone != null)
            {
                drone.volume = Mathf.MoveTowards(drone.volume, droneTarget, Time.deltaTime * 0.08f);
                if (drone.volume > 0.001f && !drone.isPlaying) drone.Play();
                if (drone.volume <= 0.001f && drone.isPlaying) drone.Stop();
            }
        }

        void LateUpdate()
        {
            if (shakeTime <= 0f || fpc == null || fpc.cam == null) return;
            shakeTime -= Time.deltaTime;
            float a = shakeAmount * Mathf.Clamp01(shakeTime * 2f);
            float t = Time.time * 28f;
            fpc.cam.transform.localRotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * a * 2f, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * a * 2f, 0f);
            if (shakeTime <= 0f) shakeAmount = 0f;
        }

        // ================================================================== interfaccia

        GUIStyle objectiveStyle, titleStyle;

        void OnGUI()
        {
            if (objectiveStyle == null)
            {
                objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
            }
            if (!string.IsNullOrEmpty(objective) && !DialogueSystem.IsBlocking && fade < 0.01f)
            {
                var r = new Rect(24f, 20f, 700f, 30f);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "Obiettivo: " + objective, objectiveStyle);
                GUI.color = Color.white;
                GUI.Label(r, "<color=#e8d8a8>Obiettivo:</color> " + objective, objectiveStyle);
            }
            if (fade > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (!string.IsNullOrEmpty(endTitle))
                {
                    GUI.color = new Color(1f, 1f, 1f, fade);
                    GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), endTitle, titleStyle);
                    GUI.color = Color.white;
                }
            }
        }
    }
}
