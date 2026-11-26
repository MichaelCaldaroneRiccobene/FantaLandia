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
    /// La regia della prima serata a casa di Tom (capitolo 1), dall'arrivo al notiziario:
    /// arrivo e birra → scherzo di Roy ("Buu!") → si aspetta la pizza → esplosione → tutti in giardino (fumo, sirene, vicini)
    /// → arriva Jef → di nuovo davanti alla TV (solo neve) → blackout → teorie e sasso-carta-forbice truccato
    /// → cantina e generatore → notiziario.
    ///
    /// Niente scritte "Obiettivo": se il giocatore non sa cosa fare sono gli amici a ricordarglielo (o Muark tra sé).
    /// I dialoghi si fanno camminando; gli amici, nei momenti tranquilli, si alzano, vanno al frigo, guardano fuori.
    /// I posti a sedere si contendono: se Muark è seduto al posto di qualcuno, quello si siede altrove o aspetta in piedi.
    /// In cantina gli spaventi li sceglie un piccolo "regista della paura" in base a dove si trova il giocatore.
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
        public HorrorDoor sideDoor;

        [Header("Personaggi")]
        public CharacterMotor tom;
        public CharacterMotor roy;
        public CharacterMotor jef;
        public NpcActor[] neighbors; // Frank, Martha, Gary, Linda

        [Header("Posti a sedere del salotto (il preferito di ciascuno)")]
        public Seat seatTom;
        public Seat seatRoy;
        public Seat seatJef;
        [Tooltip("Dove aspettano in piedi se i posti sono tutti occupati")]
        public Transform[] standSpots;

        [Header("Punti")]
        public Transform tomAtDoor;
        public Transform tomBesideDoor;
        public Transform tomCloseDoor;
        public Transform royDoorstep;
        public Transform deckTom, deckRoy, deckJef;
        public Transform livingRoom;
        public float livingRoomRadius = 3.2f;
        [Tooltip("Mete delle faccende di sottofondo: frigo, finestra del soggiorno, finestra sul retro")]
        public Transform fridgeSpot, frontWindowSpot, backWindowSpot;
        public Transform playerKitchen;      // per le prove: davanti all'isola
        public Transform playerCellarTop;    // per le prove: in cima alla scala della cantina
        public float basementFloorY = -2.5f;
        public float upstairsFloorY = 3.3f;
        public Bounds houseBounds = new Bounds(new Vector3(0f, 1.5f, 5.5f), new Vector3(14f, 10f, 11f));
        [Tooltip("La cantina (per scegliere da dove arrivano i rumori)")]
        public Bounds basementBounds = new Bounds(new Vector3(0f, -1.3f, 5.5f), new Vector3(13f, 2.6f, 10f));

        [Header("Oggetti")]
        public PlaceSpot island;
        public CrtTv tv;
        public HousePower house;
        public NeighborhoodPower neighborhood;
        public SmokeColumn smoke;
        public GeneratorStarter generator;
        [Tooltip("La vecchia radio per terra in cantina (uno degli spaventi: si accende da sola)")]
        public Transform cellarRadio;
        public Light[] cellarBulbs;
        public AmbientSound nightAmbience;

        // ------------------------------------------------------------------ stato

        Transform player;
        FirstPersonController fpc;
        Interactor interactor;
        PlayerFlashlight flashlight;
        float fade;           // 0 = niente, 1 = nero
        string endTitle;
        float shakeTime, shakeAmount;
        AudioSource sirens, drone;
        float sirenTarget, droneTarget;
        bool calm;            // momenti tranquilli: gli amici fanno le loro faccende
        bool jefOut;

        bool PlayerInside => player != null && houseBounds.Contains(player.position);
        bool PlayerInBasement => player != null && player.position.y < basementFloorY + 0.8f;
        bool PlayerUpstairs => player != null && player.position.y > upstairsFloorY - 0.4f;
        bool PlayerOnGroundFloor => PlayerInside && !PlayerInBasement && !PlayerUpstairs;
        bool PlayerInLivingRoom => player != null && livingRoom != null &&
                                   ((Flat(player.position, livingRoom.position) < livingRoomRadius && Mathf.Abs(player.position.y - livingRoom.position.y) < 1.2f)
                                    || (fpc != null && fpc.IsSeated && fpc.CurrentSeat != null && fpc.CurrentSeat.group == seatTom.group));

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
            var tvVoice = NpcSpeaker.Find("tv");
            if (tvVoice != null) NpcActor.SharedLookTarget = tvVoice.LookPoint;
            preferred[tom] = seatTom;
            preferred[roy] = seatRoy;
            preferred[jef] = seatJef;
            StartCoroutine(Main());
            StartCoroutine(IdleLife());
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
            Goal((null, Speakers.Muark, "Dovrei bussare alla porta di Tom."),
                 (null, Speakers.Muark, "Allora, busso o resto qui al freddo?"));
            frontDoor.locked = true;
            frontDoor.knockable = true;
            bool knocked = false;
            System.Action<HorrorDoor> onKnock = d => knocked = true;
            frontDoor.PlayerKnocked += onKnock;
            while (!knocked) yield return null;
            frontDoor.PlayerKnocked -= onKnock;
            ClearGoal();

            yield return new WaitForSeconds(0.6f);
            DialogueSystem.Bark(Speakers.Tom, "Arrivo, arrivo!", tom.transform.position + Vector3.up * 1.6f);
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
                }, 10f, () => DialogueSystem.Insert((Speakers.Tom, "Muark? Tutto bene? Sembri congelato."),
                                                    (Speakers.Tom, "Dai, entra. La birra mettila sull'isola in cucina.")))
                .Say(Speakers.Tom, "Jef è in bagno. Da mezz'ora. Non chiedere."), tom);

            Goal((tom, Speakers.Tom, "Muark, la birra! In cucina, sull'isola."),
                 (null, Speakers.Muark, "Prima mollo la birra in cucina."));
            Job(tom, TomHostsThenSits());
            while (island != null && island.Placed == null) yield return null;
            ClearGoal();
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
            yield return SitOn(tom);
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
            DialogueSystem.Bark(Speakers.Tom, "Muark, vai a vedere tu? Io qui sto troppo comodo.", Eye(tom));

            bool opened = false;
            System.Action<HorrorDoor> onOpen = d => opened = true;
            frontDoor.Opened += onOpen;
            frontDoor.requireFront = true; // si apre solo stando davanti: così Roy compare proprio di fronte
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
                    DialogueSystem.Bark(Speakers.Tom, reminders == 1 ? "Muark! La porta!" : "Stanno ancora bussando...", Eye(tom));
                }
                yield return null;
            }
            frontDoor.Opened -= onOpen;
            frontDoor.requireFront = false;

            // --- scena: non c'è nessuno
            if (fpc.IsSeated) fpc.StandUp();
            Lock(true);
            yield return new WaitForSeconds(0.5f);
            Vector3 outside = royDoorstep.position + Vector3.up * 1.6f - royDoorstep.forward * 2f;
            yield return LookAt(outside, 0.6f);
            yield return new WaitForSeconds(1.1f);
            DialogueSystem.Bark(Speakers.Muark, "...C'è qualcuno?");
            yield return new WaitForSeconds(1.6f);
            yield return LookAt(Eye(tom), 0.9f);
            yield return Talk(DialogueSystem.Conversation().Blocking()
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

            yield return Talk(DialogueSystem.Conversation().Blocking()
                .Say(Speakers.Roy, "AH AH AH! Dovevi vedere la tua faccia, Muark!")
                .Choice(new[] { "Roy! Mi hai fatto prendere un colpo!", "Sei un idiota. Entra, dai." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Roy, "Lo so, lo so. Sono un genio."));
                    else DialogueSystem.Insert((Speakers.Roy, "Ti voglio bene anch'io, amico."));
                }, 8f)
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
            yield return SitOn(roy);
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
            // si comincia quando Roy si è seduto e Muark è davanti al divano (o seduto in salotto)
            for (float w = 0f; w < 25f && !roy.IsSeated; w += Time.deltaTime) yield return null;
            calm = true;
            Goal((tom, Speakers.Tom, "Muark! Vieni in salotto, dai!"),
                 (roy, Speakers.Roy, "Muark, dove sei finito? Vieni qua!"));
            Vector3 sofaFront = seatRoy.FrontPosition;
            while (!(fpc.IsSeated || (Flat(player.position, sofaFront) < 2f && Mathf.Abs(player.position.y - sofaFront.y) < 1f)) || !PlayerInLivingRoom)
                yield return null;
            ClearGoal();
            calm = false;
            yield return Gather();
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Eccoci qua. Beh, quasi tutti: Jef è ancora in bagno.")
                .Say(Speakers.Roy, "Ancora? È lì dentro da quando sono nato.")
                .Say(Speakers.Tom, "Ho ordinato due pizze. Ora bisogna solo aspettare la pizza.")
                .Choice(new[] { "Che pizze hai preso?", "Speriamo che arrivi presto, ho una fame..." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Una salame piccante e una con l'ananas. Quella è per Jef."),
                                                      (Speakers.Roy, "Che schifo."));
                    else DialogueSystem.Insert((Speakers.Roy, "Anch'io. Mi mangerei il divano."));
                }, 8f), tom);

            // un po' di cartoni; il botto arriva quando Muark è al piano terra (o dopo un po' comunque)
            yield return new WaitForSeconds(6f);
            for (float w = 0f; w < 40f && !PlayerOnGroundFloor; w += Time.deltaTime) yield return null;
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
            Goal((tom, Speakers.Tom, "Muark! Qui fuori, in fondo al giardino!"),
                 (roy, Speakers.Roy, "Muark, vieni a vedere! Presto!"));
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
            if (!afterExplosion || neighbors == null || neighbors.Length < 4) return;
            neighbors[0].ambientLines = new[] { "martha|Oh santo cielo, Frank, guarda quel fumo...", "frank|Torna dentro, Martha! Potrebbe esplodere ancora!",
                                                "martha|E se è la fabbrica? Ci abita la figlia dei Parker là vicino!", "frank|Chiamo lo sceriffo. Dov'è il telefono?" };
            neighbors[2].ambientLines = new[] { "gary|Linda! Prendi la videocamera!", "linda|Gary, non è il momento!",
                                                "gary|Ehi Tom! Avete sentito anche voi?", "linda|Sento le sirene... stanno arrivando." };
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
            ClearGoal();
            KeepPlayerOutside(true);
            for (float w = 0f; w < 10f && (Flat(tom.transform.position, deckTom.position) > 0.8f || Flat(roy.transform.position, deckRoy.position) > 0.8f); w += Time.deltaTime)
                yield return null;
            Vector3 smokeTop = smoke != null ? smoke.transform.position + Vector3.up * 22f : deckTom.position + Vector3.forward * 40f;
            StartCoroutine(Actor(tom).FaceTowards(smokeTop, 0.6f));
            StartCoroutine(Actor(roy).FaceTowards(smokeTop, 0.6f));
            Lock(true);
            yield return LookAt(smokeTop, 1.2f);
            yield return new WaitForSeconds(1.5f);
            Lock(false);

            yield return Talk(DialogueSystem.Conversation().Blocking() // con i vicini la camera va su chi parla
                .Say(Speakers.Tom, "Guardate là... quanto fumo!")
                .Say(Speakers.Roy, "Sarà saltata una conduttura del gas. O un distributore.")
                .Choice(new[] { "Sembra vicino... a qualche isolato da qui.", "Speriamo che non ci sia nessun ferito." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Verso la vecchia centrale, forse. Dietro Elm Street."));
                    else DialogueSystem.Insert((Speakers.Roy, "Già... senti quante sirene."));
                }, 8f)
                .Do(() => Job(jef, JefRunsOut()))   // intanto Jef esce dal bagno e arriva di corsa
                .Say(Speakers.Gary, "Ehi, Tom! L'avete sentita anche voi?!")
                .Say(Speakers.Tom, "Sì, Gary! Sai cos'è successo?")
                .Say(Speakers.Gary, "Non ne ho idea! Ha tremato tutta la casa!")
                .Say(Speakers.Martha, "Frank dice che è il gas. Frank dice sempre che è il gas."), tom, 9f);
            yield return new WaitForSeconds(2f);
        }

        GameObject outsideBarrier;
        float nextBarrierLine;

        /// <summary>
        /// Finché gli altri guardano l'incendio Muark resta in giardino con loro: una barriera invisibile sulla porta
        /// sul retro (solo per il giocatore, i personaggi camminano sulla NavMesh) e la porta laterale chiusa.
        /// </summary>
        void KeepPlayerOutside(bool keep)
        {
            if (sideDoor != null)
            {
                if (keep) { if (sideDoor.IsOpen) sideDoor.Close(); sideDoor.locked = true; sideDoor.lockedText = "Meglio restare con gli altri, adesso."; }
                else sideDoor.locked = false;
            }
            if (!keep) { if (outsideBarrier != null) Destroy(outsideBarrier); outsideBarrier = null; return; }
            if (outsideBarrier != null || backDoor == null) return;
            outsideBarrier = new GameObject("Barriera: resta in giardino");
            outsideBarrier.transform.SetPositionAndRotation(backDoor.transform.TransformPoint(new Vector3(0.45f, 1.1f, -0.45f)), backDoor.transform.rotation);
            var col = outsideBarrier.AddComponent<BoxCollider>();
            col.size = new Vector3(1.3f, 2.4f, 0.3f);
            outsideBarrier.layer = 2; // invisibile anche al raggio delle interazioni
        }

        void BarrierLine()
        {
            if (outsideBarrier == null || Time.time < nextBarrierLine || DialogueSystem.IsTalking) return;
            if (Vector3.Distance(player.position, outsideBarrier.transform.position) > 1.4f) return;
            nextBarrierLine = Time.time + 12f;
            DialogueSystem.Bark(Speakers.Muark, "Prima vediamo cosa sta succedendo.");
        }

        // ================================================================== 6. Jef esce dal bagno

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
                }, 8f)
                .Say(Speakers.Tom, "Torniamo dentro. Magari in TV dicono qualcosa."), jef, 9f);
        }

        // ================================================================== 7. di nuovo davanti alla TV

        IEnumerator BackToTheTv()
        {
            Goal((tom, Speakers.Tom, "Muark, rientra! Dai."),
                 (roy, Speakers.Roy, "Muark, non restare lì fuori da solo!"));
            Job(tom, SitOn(tom));
            Job(roy, SitOn(roy));
            Job(jef, SitOn(jef));
            KeepPlayerOutside(false); // gli altri rientrano: ora si può tornare dentro
            while (!PlayerInside) yield return null;
            sirenTarget = 0.18f;
            while (!PlayerInLivingRoom) yield return null;
            ClearGoal();
            if (backDoor.IsOpen) backDoor.Close();
            if (tv != null) tv.SetChannel(TvChannel.Static);
            yield return Gather();
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Ma che... c'è solo la neve.")
                .Do(() => StartCoroutine(ChannelSurf(3)))
                .Say(Speakers.Roy, "Cambia canale, dai!")
                .Say(Speakers.Tom, "Ci sto provando! Canale 4... canale 7...")
                .Do(() => StartCoroutine(ChannelSurf(4)))
                .Say(Speakers.Tom, "Niente. Sono tutti così. Nessun segnale.")
                .Say(Speakers.Jef, "..."), tom);
            yield return new WaitForSeconds(6f);
            for (float w = 0f; w < 30f && !PlayerOnGroundFloor; w += Time.deltaTime) yield return null;
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

            // chi era in giro torna (al buio) dagli altri; Muark viene chiamato se è lontano
            Goal((tom, Speakers.Tom, "Muark?! Dove sei? Vieni qui, in salotto!"),
                 (roy, Speakers.Roy, "Muark! Non si vede niente, torna qui!"));
            yield return Gather();
            while (!PlayerInLivingRoom) yield return null;
            ClearGoal();

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Roy, "Ehi! Chi ha spento la luce?")
                .Say(Speakers.Tom, "È saltata la corrente... Guardate fuori: è buio tutto il quartiere.")
                .Say(Speakers.Tom, "Ve lo dico io: sono i terroristi. Prima l'esplosione, adesso questo.")
                .Say(Speakers.Roy, "Terroristi? A Maple Grove? Sono gli alieni, amico. Fidati.")
                .Choice(new[] { "Smettetela, mi state mettendo ansia.", "Jef, tu che ne pensi?" }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Roy, "Ansia? Io? Ma figurati."));
                    else DialogueSystem.Insert((Speakers.Jef, "..."), (Speakers.Tom, "Jef?"), (Speakers.Jef, "..."));
                }, 8f)
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
                }, 8f, () => DialogueSystem.Insert((Speakers.Tom, "Troppo lento! Hai perso a tavolino."),
                                                   (Speakers.Roy, "Muark va in cantina! Muark va in cantina!")))
                .Choice(new[] { "Ma dai! Al meglio di tre?", "Va bene, va bene... ci vado io." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Niente rivincite. Le regole sono regole."));
                    else DialogueSystem.Insert((Speakers.Tom, "Bravo. Così si fa."));
                }, 8f)
                .Say(Speakers.Tom, "C'è una torcia sul bancone della cucina. La porta della cantina è quella accanto all'isola.")
                .Say(Speakers.Jef, "Stai attento, laggiù."), tom);
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

        bool generatorRunning;

        IEnumerator Basement()
        {
            calm = true; // di sopra, al buio, gli amici si muovono un po'
            if (flashlight != null && !flashlight.HasFlashlight)
            {
                Goal((tom, Speakers.Tom, "La torcia è sul bancone in cucina, Muark."),
                     (roy, Speakers.Roy, "Che c'è, Muark? Hai paura del buio?"));
                while (!flashlight.HasFlashlight) yield return null;
            }
            Goal((tom, Speakers.Tom, "Muark? La cantina è da quella parte, accanto all'isola."),
                 (roy, Speakers.Roy, "Ehi, il generatore non si accende da solo!"));
            while (!PlayerInBasement) yield return null;
            Goal((null, Speakers.Muark, "Il generatore... cerca quella lucina rossa."),
                 (null, Speakers.Muark, "Tira la corda e torna su, Muark. Facile."));
            droneTarget = 0.22f;

            // gli spaventi li sceglie il "regista della paura" finché il generatore non parte
            StartCoroutine(ScareDirector());

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
            generatorRunning = true;
            ClearGoal();

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
            Goal((roy, Speakers.Roy, "Muark! Sali, presto!"),
                 (null, Speakers.Muark, "Devo tornare di sopra."));

            while (PlayerInBasement || player.position.y < livingRoom.position.y - 0.5f) yield return null;
            droneTarget = 0f;
            DialogueSystem.Bark(Speakers.Roy, "Muark! Vieni, presto! C'è il notiziario!", Eye(roy));
        }

        /// <summary>
        /// Il regista della paura della cantina: dopo un po' di calma sceglie uno spavento non ancora usato e lo fa
        /// arrivare da dove il giocatore non sta guardando (alle spalle, di lato, sopra la testa).
        /// </summary>
        IEnumerator ScareDirector()
        {
            var pool = new List<int> { 0, 1, 2, 3, 4 };
            if (cellarRadio != null) pool.Add(5);
            float calmUntil = Time.time + Random.Range(3f, 6f);
            while (!generatorRunning && pool.Count > 0)
            {
                if (Time.time < calmUntil || !PlayerInBasement || DialogueSystem.IsTalking) { yield return null; continue; }
                int k = pool[Random.Range(0, pool.Count)];
                // lo spavento più forte (il tonfo) solo quando si è già vicini al generatore
                if (k == 2 && generator != null && Flat(player.position, generator.transform.position) > 3.5f && pool.Count > 1) { yield return null; continue; }
                pool.Remove(k);
                yield return Scare(k);
                calmUntil = Time.time + Random.Range(8f, 15f);
            }
        }

        Vector3 BehindPlayer(float distance)
        {
            Vector3 p = player.position - player.forward * distance;
            Vector3 min = basementBounds.min + new Vector3(0.3f, 0f, 0.3f), max = basementBounds.max - new Vector3(0.3f, 0f, 0.3f);
            return new Vector3(Mathf.Clamp(p.x, min.x, max.x), player.position.y, Mathf.Clamp(p.z, min.z, max.z));
        }

        IEnumerator Scare(int k)
        {
            switch (k)
            {
                case 0: // passi lenti di sopra, proprio sopra la testa
                {
                    Vector3 above = player.position + Vector3.up * 3.1f;
                    Vector3 walk = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    for (int i = 0; i < 6; i++)
                    {
                        HorrorAudio.PlayAt(ProceduralAudio.Footstep(Surface.Wood, i % 4), above + walk * (i * 0.55f), 0.7f, 0.8f, 14f);
                        yield return new WaitForSeconds(0.85f);
                    }
                    DialogueSystem.Bark(Speakers.Muark, "...Ragazzi? Siete voi?");
                    break;
                }
                case 1: // graffi dentro il muro alle spalle
                {
                    Vector3 b = BehindPlayer(6f);
                    HorrorAudio.PlayAt(ProceduralAudio.Scratch(), b + Vector3.up * 1.1f, 0.8f, 0.9f, 16f);
                    yield return new WaitForSeconds(2.8f);
                    DialogueSystem.Bark(Speakers.Muark, "Topi. Sono solo topi. Grossi topi.");
                    break;
                }
                case 2: // qualcosa cade alle spalle
                {
                    HorrorAudio.PlayAt(ProceduralAudio.Thud(), BehindPlayer(3f) + Vector3.up * 0.4f, 1f, 1f, 18f);
                    HorrorAudio.Play2D(ProceduralAudio.Sting(), 0.35f, 0.8f);
                    Shake(0.4f, 0.3f);
                    yield return new WaitForSeconds(0.7f);
                    DialogueSystem.Bark(Speakers.Muark, "Chi c'è?!");
                    break;
                }
                case 3: // un sussurro all'orecchio
                    HorrorAudio.PlayAt(ProceduralAudio.Whisper(), player.position + player.right * (Random.value < 0.5f ? -1.2f : 1.2f) - player.forward * 0.6f + Vector3.up * 1.6f, 0.45f, 0.85f, 5f);
                    yield return new WaitForSeconds(2.6f);
                    break;
                case 5: // la vecchia radio si accende da sola: neve e una voce che non si capisce
                {
                    Vector3 rp = cellarRadio.position + Vector3.up * 0.15f;
                    HorrorAudio.PlayAt(ProceduralAudio.Click(), rp, 0.7f, 0.8f, 12f);
                    HorrorAudio.PlayAt(ProceduralAudio.TvStatic(), rp, 0.55f, 0.9f, 14f);
                    yield return new WaitForSeconds(0.6f);
                    HorrorAudio.PlayAt(ProceduralAudio.Whisper(), rp, 0.6f, 0.75f, 10f);
                    yield return new WaitForSeconds(1.6f);
                    HorrorAudio.PlayAt(ProceduralAudio.Click(), rp, 0.7f, 0.7f, 12f);
                    yield return new WaitForSeconds(0.8f);
                    DialogueSystem.Bark(Speakers.Muark, "...La radio? Ma è staccata.");
                    break;
                }
                case 4: // la torcia sfarfalla
                    if (flashlight != null && flashlight.isOn)
                    {
                        flashlight.Flicker(1.6f);
                        yield return new WaitForSeconds(1.8f);
                        DialogueSystem.Bark(Speakers.Muark, "No, no, no... non adesso.");
                    }
                    break;
            }
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
            calm = false;
            Goal((roy, Speakers.Roy, "Muark! Il notiziario! Sbrigati!"),
                 (tom, Speakers.Tom, "Muark, vieni qui! Subito!"));
            StartCoroutine(Gather());
            while (!PlayerInLivingRoom) yield return null;
            ClearGoal();
            yield return Gather();
            // Muark non dice niente: si ferma e guarda la TV insieme agli altri
            Lock(true);
            if (tv != null) yield return LookAt(tv.transform.position + Vector3.up * 0.3f, 1.2f);
            yield return Talk(DialogueSystem.Conversation().Blocking()
                .Say(Speakers.Tom, "Shh! Guarda...")
                .Say(Speakers.Tv, "Interrompiamo le trasmissioni per un'edizione straordinaria.")
                .Say(Speakers.Tv, "Questa sera, poco dopo le nove, una forte esplosione ha colpito la zona est di Maple Grove.")
                .Say(Speakers.Tv, "Le cause non sono ancora note. Le linee elettriche e telefoniche di tutta la contea sono fuori uso.")
                .Say(Speakers.Tv, "Le autorità raccomandano ai residenti di restare in casa, di chiudere porte e finestre e di non uscire per nessun motivo fino a domattina.")
                .Say(Speakers.Tv, "Ripetiamo: non uscite di casa. Non aprite a nessuno.")
                .Say(Speakers.Roy, "Ok... ok. Adesso ho paura sul serio.")
                .Say(Speakers.Tom, "Non aprite a nessuno?! Che vuol dire \"non aprite a nessuno\"?!")
                .Say(Speakers.Roy, "E adesso che facciamo?!")
                .Say(Speakers.Tom, "Chiudiamo tutto. Porte, finestre, tutto. E aspettiamo domattina.")
                .Say(Speakers.Jef, "Non è stata un'esplosione.")
                .Say(Speakers.Tom, "Cosa? Jef... che stai dicendo?")
                .Say(Speakers.Jef, "..."), tom);

            // la TV perde il segnale, poi il buio
            if (tv != null) tv.SetChannel(TvChannel.Static);
            HorrorAudio.Play2D(ProceduralAudio.Whisper(), 0.25f, 0.7f);
            yield return new WaitForSeconds(2.5f);
            Lock(true);
            endTitle = "Fine del capitolo 1\n\n<size=22>Continua...</size>";
            for (float t = 0f; t < 1f; t += Time.deltaTime / 3f) { fade = t; yield return null; }
            fade = 1f;
        }

        // ================================================================== suggerimenti a voce (al posto dell'obiettivo a schermo)

        readonly List<(CharacterMotor who, Speaker sp, string text)> hints = new List<(CharacterMotor, Speaker, string)>();
        float hintClock;
        int hintIndex;

        /// <summary>
        /// Cosa deve fare il giocatore ora. Non compare scritto: se dopo un po' non lo fa, un amico glielo ricorda
        /// (o Muark ci pensa tra sé), a turno, ogni mezzo minuto circa.
        /// </summary>
        void Goal(params (CharacterMotor who, Speaker sp, string text)[] h)
        {
            hints.Clear();
            hints.AddRange(h);
            hintIndex = 0;
            hintClock = Time.time + 25f;
        }

        void ClearGoal() => hints.Clear();

        void UpdateHints()
        {
            if (hints.Count == 0 || Time.time < hintClock || DialogueSystem.IsTalking || fade > 0.01f) return;
            hintClock = Time.time + 30f;
            for (int n = 0; n < hints.Count; n++)
            {
                var h = hints[hintIndex++ % hints.Count];
                if (h.who != null && !h.who.gameObject.activeInHierarchy) continue;
                DialogueSystem.Bark(h.sp, h.text, h.who != null ? Eye(h.who) : (Vector3?)null);
                return;
            }
        }

        // ================================================================== vita di sottofondo

        bool saidUpstairs;

        /// <summary>
        /// Nei momenti tranquilli ogni tanto uno degli amici si alza e fa qualcosa (Tom va al frigo, Roy guarda fuori
        /// dalla finestra, Jef resta a fissare il buio dal retro), poi torna a sedersi se il posto è libero.
        /// </summary>
        IEnumerator IdleLife()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(14f, 26f));
                if (!calm || DialogueSystem.IsTalking || fade > 0.01f) continue;
                var candidates = new List<CharacterMotor>();
                foreach (var m in new[] { tom, roy, jef })
                    if (m != null && m.gameObject.activeInHierarchy && m.IsSeated && !running.Contains(m) && (m != jef || jefOut)
                        && (m != tom || (house != null && !house.powered))) // Tom resta in poltrona: va al frigo solo al buio, mentre Muark è in cantina
                        candidates.Add(m);
                if (candidates.Count == 0) continue;
                CharacterMotor who = candidates[Random.Range(0, candidates.Count)];
                Job(who, Errand(who));
            }
        }

        IEnumerator Errand(CharacterMotor m)
        {
            bool dark = house != null && !house.powered;
            Transform spot = m == tom ? fridgeSpot : m == roy ? frontWindowSpot : backWindowSpot;
            if (spot == null) yield break;
            Stand(m);
            yield return new WaitForSeconds(0.6f);
            if (m == tom)
                DialogueSystem.Bark(Speakers.Tom, dark ? "Il frigo si sta scaldando... meglio finire le birre." : "Prendo un'altra birra. Qualcuno vuole qualcosa?", Eye(tom));
            yield return Actor(m).WalkTo(spot.position);
            yield return Actor(m).FaceTowards(spot.position + spot.forward * 2f + Vector3.up * 1.5f, 0.6f);
            if (m == roy)
                DialogueSystem.Bark(Speakers.Roy, dark ? "Non si vede niente là fuori. Niente di niente." : "Niente pizza all'orizzonte...", Eye(roy), true);
            yield return new WaitForSeconds(m == jef ? Random.Range(7f, 11f) : Random.Range(2.5f, 4.5f)); // Jef resta lì a fissare il buio
            if (m == tom) HorrorAudio.PlayAt(ProceduralAudio.CansClink(), spot.position + spot.forward * 0.6f + Vector3.up, 0.5f);
            yield return SitOn(m);
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
            SitNow(tom);
            tomSeated = true;

            if (s >= (int)StartAt.Salotto)
            {
                Warp(roy, seatRoy.FrontPosition, 0f);
                roy.gameObject.SetActive(true);
                yield return null;
                SitNow(roy);
                TeleportPlayer(livingRoom);
            }
            if (s >= (int)StartAt.Blackout)
            {
                if (smoke != null) smoke.Ignite();
                bathDoor.Unlock();
                jefOut = true;
                Warp(jef, seatJef.FrontPosition, 0f);
                yield return null;
                SitNow(jef);
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

        // ================================================================== attori e posti a sedere

        NpcActor Actor(CharacterMotor m) => m.GetComponent<NpcActor>();

        static Vector3 Eye(CharacterMotor m)
        {
            var sp = m.GetComponent<NpcSpeaker>();
            return sp != null ? sp.EyePoint : m.transform.position + Vector3.up * 1.6f;
        }

        Speaker SpeakerOf(CharacterMotor m) => m == tom ? Speakers.Tom : m == roy ? Speakers.Roy : m == jef ? Speakers.Jef : null;

        readonly Dictionary<CharacterMotor, Coroutine> jobs = new Dictionary<CharacterMotor, Coroutine>();
        readonly HashSet<CharacterMotor> running = new HashSet<CharacterMotor>();
        readonly Dictionary<CharacterMotor, Seat> preferred = new Dictionary<CharacterMotor, Seat>();
        readonly Dictionary<CharacterMotor, Seat> seatOf = new Dictionary<CharacterMotor, Seat>();

        /// <summary>Un ordine per personaggio alla volta: quello nuovo annulla il precedente (es. andare a sedersi).</summary>
        void Job(CharacterMotor m, IEnumerator routine)
        {
            if (jobs.TryGetValue(m, out Coroutine c) && c != null) StopCoroutine(c);
            // un posto prenotato ma non ancora usato torna libero
            if (seatOf.TryGetValue(m, out Seat st) && !m.IsSitting && !m.IsSeated) { st.Release(m); seatOf.Remove(m); }
            jobs[m] = StartCoroutine(RunJob(m, routine));
        }

        IEnumerator RunJob(CharacterMotor m, IEnumerator routine)
        {
            running.Add(m);
            yield return routine;
            running.Remove(m);
        }

        /// <summary>
        /// Va a sedersi al suo posto preferito. Se l'ha preso Muark ne cerca un altro libero nel salotto;
        /// se sono tutti occupati aspetta in piedi lì vicino e, appena un posto si libera, ci dà un'occhiata e si siede.
        /// </summary>
        IEnumerator SitOn(CharacterMotor m)
        {
            if (m.IsSeated) yield break;
            Seat pref = preferred.TryGetValue(m, out Seat ps) ? ps : null;
            if (pref == null) yield break;
            bool complained = false;
            while (true)
            {
                Seat target = pref.IsFree ? pref : Seat.FreeNear(pref.group, pref.transform.position);
                if (!pref.IsFree && !complained && pref.Occupant is FirstPersonController && Flat(m.transform.position, pref.transform.position) < 6f)
                {
                    complained = true;
                    if (m == tom) DialogueSystem.Bark(Speakers.Tom, "Ehi, quella è la mia poltrona! ...Vabbè, per stasera.", Eye(m));
                    else if (m == roy) DialogueSystem.Bark(Speakers.Roy, "Mi hai rubato il posto, eh?", Eye(m));
                }
                if (target == null)
                {
                    // nessun posto: in piedi lì vicino, finché uno non si libera
                    yield return Actor(m).WalkTo(StandSpotFor(m));
                    if (tv != null) yield return Actor(m).FaceTowards(tv.transform.position, 0.6f);
                    while (Seat.FreeNear(pref.group, pref.transform.position) == null) yield return new WaitForSeconds(0.5f);
                    yield return new WaitForSeconds(Random.Range(0.8f, 1.8f)); // si guarda intorno, controlla il posto
                    continue;
                }
                yield return Actor(m).WalkTo(target.FrontPosition);
                if (!target.Claim(m)) continue; // nel frattempo l'ha preso qualcun altro
                seatOf[m] = target;
                m.SitDown(target.transform.position, target.transform.rotation, target.height);
                yield return new WaitForSeconds(2.4f);
                yield break;
            }
        }

        Vector3 StandSpotFor(CharacterMotor m)
        {
            if (standSpots == null || standSpots.Length == 0) return preferred[m].FrontPosition;
            int i = m == tom ? 0 : m == roy ? 1 : 2;
            return standSpots[i % standSpots.Length].position;
        }

        /// <summary>Per le prove: subito seduto al suo posto (o a uno libero).</summary>
        void SitNow(CharacterMotor m)
        {
            Seat pref = preferred[m];
            Seat target = pref.IsFree ? pref : Seat.FreeNear(pref.group, pref.transform.position);
            if (target == null || !target.Claim(m)) return;
            seatOf[m] = target;
            Warp(m, target.FrontPosition, target.transform.eulerAngles.y);
            m.SitDown(target.transform.position, target.transform.rotation, target.height);
        }

        void Stand(CharacterMotor m)
        {
            if (!m.IsSeated && !m.IsSitting) return;
            Seat s = seatOf.TryGetValue(m, out Seat st) ? st : null;
            m.StandUp(s != null ? s.FrontPosition : m.transform.position + m.transform.forward * 0.7f);
            if (s != null) s.Release(m);
            seatOf.Remove(m);
        }

        /// <summary>Riunisce gli amici in salotto (ognuno torna a sedersi) prima di una scena; aspetta al massimo 12 s.</summary>
        IEnumerator Gather()
        {
            foreach (var m in new[] { tom, roy, jef })
                if (m != null && m.gameObject.activeInHierarchy && !m.IsSeated && (m != jef || jefOut)) Job(m, SitOn(m));
            for (float t = 0f; t < 12f; t += Time.deltaTime)
            {
                bool all = true;
                foreach (var m in new[] { tom, roy, jef })
                    if (m != null && m.gameObject.activeInHierarchy && (m != jef || jefOut) && running.Contains(m)) all = false;
                if (all) yield break;
                yield return null;
            }
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

        /// <summary>Una conversazione "camminando": se Muark si allontana da 'near' il discorso si ferma e lo richiamano.</summary>
        IEnumerator Talk(DialogueSystem.Builder conversation, CharacterMotor near = null, float radius = 7f)
        {
            while (DialogueSystem.IsTalking) yield return null;
            if (near != null) conversation.Near(near.transform, SpeakerOf(near), radius);
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
            UpdateHints();
            BarrierLine();

            // reazioni: Muark sale di sopra mentre si aspetta la pizza
            if (calm && !saidUpstairs && PlayerUpstairs && tom.IsSeated && (house == null || house.powered))
            {
                saidUpstairs = true;
                DialogueSystem.Bark(Speakers.Tom, "Muark? Che ci fai di sopra? La festa è qui sotto!", Eye(tom));
            }

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

        // ================================================================== interfaccia (solo dissolvenze e titolo finale)

        GUIStyle titleStyle;

        void OnGUI()
        {
            if (fade <= 0.001f) return;
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
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
