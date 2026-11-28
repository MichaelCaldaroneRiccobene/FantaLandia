using System.Collections;
using System.Collections.Generic;
using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using FantaLandia.Horror.UI;
using FantaLandia.Horror.World;
using UnityEngine;

namespace FantaLandia.Horror.Story
{
    /// <summary>
    /// La regia del capitolo 2, "Non aprite a nessuno". Un'ora dopo il notiziario, la cantina di Tom è diventata un rifugio.
    ///
    /// rifugio e radio da sintonizzare (tre stazioni) → i ruggiti fanno tremare la casa, poi silenzio → bussano alla porta:
    /// "AIUTO!! APRITE!!" → Muark va su (timer nascosto di due minuti) → il piano di sopra è stato strappato via →
    /// alla porta il fattorino della pizza fa in tempo a dire grazie, poi un enorme frammento di roccia lo travolge
    /// (rallentatore, sangue ovunque, Muark sbalzato via e stordito) → di nuovo giù, sotto shock → Tom sintonizza la radio:
    /// i militari dicono di abbandonare l'area, poi il segnale salta → il generatore muore, buio → Tom prende il comando
    /// e la torcia, si sale: la porta è bloccata, si esce dal retro → in giardino, urla lontane, una creatura alta come
    /// un palazzo si alza all'orizzonte e ruggisce → "ATTENTI!" → un macigno dal cielo. Nero.
    /// → epilogo: la TV crepata di Tom tra le macerie, il notiziario del mattino.
    ///
    /// I punti di salvataggio sono 100 + la scena (StoryDirector usa 0..6 per il capitolo 1).
    /// </summary>
    public class Chapter2Director : MonoBehaviour
    {
        public enum StartAt { Rifugio, Bussano, Ritorno, Fuga, Epilogo }

        public const int CheckpointBase = 100;
        public const string SceneName = "Casa_Tom_Capitolo2";

        [Header("Prova: da dove parte il capitolo")]
        public StartAt startAt = StartAt.Rifugio;

        [Header("Porte")]
        public HorrorDoor frontDoor;
        public HorrorDoor backDoor;
        public HorrorDoor cellarDoor;
        public HorrorDoor[] blockedDoors;   // laterale, garage: bloccate dalle macerie

        [Header("Personaggi")]
        public CharacterMotor tom;
        public CharacterMotor roy;
        public CharacterMotor jef;
        public CharacterMotor pizzaBoy;
        public Transform pizzaBox;

        [Header("Rifugio in cantina")]
        public Seat seatTom;        // la poltrona di Tom, portata giù
        public Seat seatRoy;        // il divano
        public Seat seatPlayer;
        public Seat seatJef;        // una cassa, in disparte
        public RadioTuner radio;
        public GeneratorStarter generator;
        public Light[] cellarBulbs;
        public Transform[] bulbPivots;
        public Transform tomTorch;  // la torcia sulla cassa (Tom la prende quando va via la luce)
        public Transform[] shelfPoints; // dove tintinnano i barattoli durante le scosse
        public float basementFloorY = -2.5f;
        public float ceilingY = 0.05f;
        public Bounds basementBounds = new Bounds(new Vector3(0f, -1.3f, 5.5f), new Vector3(13f, 2.6f, 10f));
        public Bounds houseBounds = new Bounds(new Vector3(0f, 1.5f, 5.5f), new Vector3(14f, 10f, 11f));

        [Header("Punti")]
        public Transform stairsBottom;
        public Transform stairsTop;
        public Transform frontInside;   // davanti alla porta d'ingresso, dentro
        public Transform backInside;    // davanti alla porta sul retro, dentro
        public Transform gardenTom, gardenRoy, gardenJef, gardenPlayer;
        public Transform playerBasementStart;
        public Transform screamsPoint;  // le urla lontane (a destra, guardando il fondo del giardino)
        public Transform tvView;        // dove sta la camera nell'epilogo

        [Header("Il frammento di roccia")]
        public Transform rockFront;     // finisce davanti alla porta e la blocca
        public Transform rockFinal;     // quello che cade sul giardino alla fine
        public Collider frontBlocker;   // il varco della porta chiuso dopo l'impatto
        public GameObject[] porchRoof;  // il tetto del portico (viene giù con il masso)

        [Header("Mondo")]
        public GiantCreature creature;
        public CrtTv tv;
        public SmokeColumn smoke;
        public NeighborhoodPower neighborhood;
        public Material bloodMaterial;
        public Material dustMaterial;
        public Material rockMaterial;
        public Material fireMaterial;

        // ------------------------------------------------------------------ stato

        Transform player;
        FirstPersonController fpc;
        Interactor interactor;
        StunEffect stun;
        float fade = 1f;
        string title;
        float titleAlpha;
        float shakeTime, shakeAmount;
        AudioSource sirens, drone;
        float sirenTarget, droneTarget;
        bool sawTheBoy;          // Muark ha aperto la porta (e ha visto tutto)
        bool bunkerChatter;      // Tom e Roy litigano a bassa voce
        bool aftershocks;        // scosse e ruggiti continuano in sottofondo
        bool bulbsFlicker = true;
        float[] bulbBase;
        Light tomLight;
        readonly Dictionary<Seat, CharacterMotor> seated = new Dictionary<Seat, CharacterMotor>();
        Transform jefCeiling;

        bool PlayerInside => player != null && houseBounds.Contains(player.position);
        bool PlayerInBasement => player != null && player.position.y < basementFloorY + 0.8f;
        bool PlayerOnGroundFloor => PlayerInside && !PlayerInBasement;

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        void Start()
        {
            int pending = StoryDirector.PendingStart;
            if (pending >= CheckpointBase) { startAt = (StartAt)Mathf.Clamp(pending - CheckpointBase, 0, 4); StoryDirector.PendingStart = -1; }
            else if (pending >= 0) StoryDirector.PendingStart = -1;
            StoryDirector.Objective = "";
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                player = fpc.transform;
                interactor = fpc.GetComponent<Interactor>();
                if (fpc.cam != null)
                {
                    stun = fpc.cam.GetComponent<StunEffect>();
                    if (stun == null) stun = fpc.cam.gameObject.AddComponent<StunEffect>();
                }
            }
            sirens = HorrorAudio.CreateLoop(transform, ProceduralAudio.Siren(), 0f, false);
            drone = HorrorAudio.CreateLoop(transform, ProceduralAudio.Drone(), 0f, false);
            bulbBase = new float[cellarBulbs.Length];
            for (int i = 0; i < cellarBulbs.Length; i++) if (cellarBulbs[i] != null) { bulbBase[i] = cellarBulbs[i].intensity; cellarBulbs[i].enabled = true; }
            if (smoke != null) smoke.Ignite();
            if (neighborhood != null) neighborhood.SetPower(false);
            if (generator != null) { generator.armed = false; generator.StartRunning(); }
            if (radio != null) radio.usable = false;
            if (creature != null) creature.gameObject.SetActive(false);
            if (rockFront != null) rockFront.gameObject.SetActive(false);
            if (rockFinal != null) rockFinal.gameObject.SetActive(false);
            if (frontBlocker != null) frontBlocker.enabled = false;
            if (blockedDoors != null) foreach (var d in blockedDoors) if (d != null) { d.locked = true; d.lockedText = "Bloccata. Dall'altra parte è crollato tutto."; }
            if (backDoor != null) { backDoor.locked = true; backDoor.lockedText = "Non si apre. È incastrata dalle macerie."; }
            if (frontDoor != null) { frontDoor.locked = true; frontDoor.knockable = false; frontDoor.lockedText = "Meglio non uscire..."; }
            // dalla cantina non si sale finché non bussano alla porta
            if (cellarDoor != null) { if (cellarDoor.IsOpen) cellarDoor.Close(); cellarDoor.locked = true; cellarDoor.lockedText = "Meglio restare qui sotto con gli altri."; }
            if (tv != null) { tv.powered = false; tv.cracked = true; tv.SetChannel(TvChannel.Off); }
            if (pizzaBoy != null) pizzaBoy.gameObject.SetActive(false);
            NpcActor.SharedLookTarget = null;
            // Jef fissa il soffitto
            jefCeiling = new GameObject("Jef guarda il soffitto").transform;
            if (jef != null) jefCeiling.position = jef.transform.position + jef.transform.forward * 0.6f + Vector3.up * 3.2f;
            StartCoroutine(Main());
            StartCoroutine(BunkerChatter());
            StartCoroutine(Aftershocks());
            StartCoroutine(DistantRoars());
        }

        bool distantRoars;

        /// <summary>Di sopra e fuori: ogni tanto un ruggito lontano (niente sirene: la città tace).</summary>
        IEnumerator DistantRoars()
        {
            int n = 0;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(22f, 40f));
                if (!distantRoars || fade > 0.01f || PlayerInBasement || aftershocks && Random.value < 0.5f) continue;
                HorrorAudio.Play2D(ProceduralAudio.Roar(n++ % 3), Random.Range(0.3f, 0.5f), Random.Range(0.78f, 0.92f));
                Shake(0.12f, 1.5f);
            }
        }

        IEnumerator Main()
        {
            yield return null; // gli agenti si attivano al primo frame
            Lock(true);
            fade = 1f;
            while (IntroSequence.Active) yield return null;
            int s = (int)startAt;
            yield return Setup(startAt);

            if (s <= (int)StartAt.Rifugio) { Checkpoint(StartAt.Rifugio); yield return OneHourLater(); yield return Bunker(); yield return Radio(); }
            else yield return FadeIn(1.2f);
            if (s <= (int)StartAt.Bussano) { Checkpoint(StartAt.Bussano); yield return QuakeAndSilence(); yield return Knocking(); yield return Upstairs(); }
            if (s <= (int)StartAt.Ritorno) { Checkpoint(StartAt.Ritorno); yield return BackDownstairs(); yield return TomTunesTheRadio(); yield return GeneratorDies(); }
            if (s <= (int)StartAt.Fuga) { Checkpoint(StartAt.Fuga); yield return LeaveTheHouse(); yield return Garden(); }
            Checkpoint(StartAt.Epilogo);
            yield return Epilogue();
            yield return Credits();
        }

        // ================================================================== preparazione (e salti per le prove)

        IEnumerator Setup(StartAt at)
        {
            int s = (int)at;
            SitNow(tom, seatTom);
            SitNow(roy, seatRoy);
            SitNow(jef, seatJef);
            if (jef != null) Actor(jef).idleLook = false;
            LookOf(jef, jefCeiling);
            TeleportPlayer(playerBasementStart);
            yield return null;
            if (s == 0 && fpc != null && seatPlayer != null) fpc.SitOn(seatPlayer); // Muark sul divano con la radio davanti

            if (s >= (int)StartAt.Ritorno)
            {
                if (cellarDoor != null) cellarDoor.Unlock();
                // il fattorino non c'è più, il macigno blocca la porta
                PlaceFrontRock(true);
                BloodFx.Spray(bloodMaterial, frontDoor.transform.position + new Vector3(0.45f, 1.2f, -1.2f), Vector3.back, 30, 5f, 160f);
                droneTarget = 0.12f;
                aftershocks = true;
            }
            if (s >= (int)StartAt.Fuga)
            {
                if (generator != null) yield return generator.Die(0.1f);
                foreach (var b in cellarBulbs) if (b != null) b.enabled = false;
                bulbsFlicker = false;
                if (fpc != null && seatPlayer != null) fpc.SitOn(seatPlayer);
            }
            if (s >= (int)StartAt.Epilogo) aftershocks = false;
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>Il masso al posto della porta. 'doorGone': l'anta è volata via (Muark l'aveva aperta).</summary>
        void PlaceFrontRock(bool doorGone)
        {
            if (rockFront != null) rockFront.gameObject.SetActive(true);
            if (frontBlocker != null) frontBlocker.enabled = true;
            if (pizzaBoy != null) pizzaBoy.gameObject.SetActive(false);
            if (frontDoor != null)
            {
                if (doorGone) HideDoorLeaf();
                else if (frontDoor.IsOpen) frontDoor.Close();
                frontDoor.locked = true;
                frontDoor.lockedText = "Non si passa. C'è un masso enorme al posto della porta.";
                frontDoor.enabled = false;
            }
            // il tetto del portico è venuto giù con il masso
            if (porchRoof != null) foreach (var go in porchRoof) if (go != null) go.SetActive(false);
        }

        void HideDoorLeaf()
        {
            if (frontDoor != null && frontDoor.hinge != null) frontDoor.hinge.gameObject.SetActive(false);
        }

        // ================================================================== 1. un'ora dopo

        IEnumerator OneHourLater()
        {
            fade = 1f;
            yield return new WaitForSeconds(1f);
            title = Loc.T("Un'ora dopo...");
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f) { titleAlpha = t; yield return null; }
            titleAlpha = 1f;
            yield return new WaitForSeconds(2.6f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 1.2f) { titleAlpha = t; yield return null; }
            titleAlpha = 0f;
            title = null;
            yield return FadeIn(2.5f);
        }

        IEnumerator FadeIn(float seconds)
        {
            for (float t = 1f; t > 0f; t -= Time.deltaTime / seconds) { fade = t; yield return null; }
            fade = 0f;
            Lock(false);
        }

        // ================================================================== 2. il rifugio e la radio

        static readonly string[] Argue =
        {
            "roy|Te l'avevo detto di chiamare qualcuno.", "tom|E chi? I telefoni sono morti, Roy.",
            "roy|Potevamo andare via in macchina. Subito.", "tom|In macchina dove? Hai visto il cielo?",
            "roy|Meglio che stare chiusi qui sotto come topi.", "tom|Abbassa la voce.", "roy|Abbassala tu.",
            "tom|Quando finisce la benzina del generatore restiamo al buio. Lo sai, vero?", "roy|Grazie, Tom. Molto rassicurante.",
        };

        /// <summary>Tom e Roy litigano a bassa voce, sopra la testa; Jef non dice niente e fissa il soffitto.</summary>
        IEnumerator BunkerChatter()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(4.5f, 7f));
                if (!bunkerChatter || DialogueSystem.IsTalking || fade > 0.01f) continue;
                if (radio != null && radio.InUse && i % 2 == 1) continue; // mentre Muark ascolta la radio parlano meno
                string line = Argue[i++ % Argue.Length];
                int bar = line.IndexOf('|');
                CharacterMotor m = line.StartsWith("tom") ? tom : roy;
                DialogueSystem.Bark(Speakers.Find(line.Substring(0, bar)), line.Substring(bar + 1), Eye(m), true);
            }
        }

        IEnumerator Bunker()
        {
            bunkerChatter = true;
            yield return new WaitForSeconds(5f);
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Muark. Quella radio funziona ancora?")
                .Choice(new[] { "Proviamo.", "Se non l'hai rotta tu, sì." }, i =>
                {
                    if (i == 1) DialogueSystem.Insert((Speakers.Tom, "Molto divertente. Accendila."));
                }, 8f)
                .Say(Speakers.Tom, "Trova qualcuno. Chiunque. Dobbiamo sapere cosa succede là fuori."));
            if (radio != null) radio.usable = true;
        }

        IEnumerator Radio()
        {
            StoryDirector.Objective = "Sintonizzare la radio (Gira la manopola, genio)";
            Goal((null, Speakers.Muark, "La radio... devo trovare una stazione."),
                 (tom, Speakers.Tom, "Gira piano, Muark. Le stazioni sono tra una tacca e l'altra."),
                 (roy, Speakers.Roy, "Prova più in alto, verso il cento."));
            if (radio == null) yield break;
            var reactions = new Queue<string>();
            System.Action<string> onHeard = id => reactions.Enqueue(id);
            radio.StationHeard += onHeard;
            // timer nascosto: un minuto dopo che Muark ha preso in mano la radio arrivano comunque i ruggiti
            float deadline = float.MaxValue;
            while (radio.HeardCount < 3 && Time.time < deadline)
            {
                if (radio.InUse && deadline == float.MaxValue) deadline = Time.time + 60f;
                if (reactions.Count > 0 && !DialogueSystem.IsTalking)
                {
                    yield return new WaitForSeconds(0.8f);
                    yield return React(reactions.Dequeue());
                    hintClock = Time.time + 30f;
                    if (radio.HeardCount < 3)
                        Goal((tom, Speakers.Tom, "Continua a cercare. Ci sarà qualcun altro."),
                             (null, Speakers.Muark, "Ci dev'essere qualcun altro, su questa radio."));
                }
                yield return null;
            }
            radio.StationHeard -= onHeard;
            ClearGoal();
            if (radio.HeardCount >= 3)
            {
                while (reactions.Count > 0) { yield return new WaitForSeconds(0.8f); yield return React(reactions.Dequeue()); }
                yield return new WaitForSeconds(2f);
            }
        }

        IEnumerator React(string station)
        {
            switch (station)
            {
                case "locale":
                    DialogueSystem.Bark(Speakers.Roy, "\"Oh mio Dio\"? Cosa ha visto?", Eye(roy));
                    yield return new WaitForSeconds(3f);
                    DialogueSystem.Bark(Speakers.Tom, "Restare in casa. Come se avessimo scelta.", Eye(tom));
                    break;
                case "radioamatore":
                    DialogueSystem.Bark(Speakers.Roy, "Alto come un palazzo?! Ma che cazzo dice quello?", Eye(roy));
                    yield return new WaitForSeconds(3.2f);
                    DialogueSystem.Bark(Speakers.Jef, "Dice la verità.", Eye(jef));
                    break;
                case "militare":
                    DialogueSystem.Bark(Speakers.Roy, "Martello Sette? Che cazzo vuol dire Martello Sette?", Eye(roy));
                    yield return new WaitForSeconds(3.2f);
                    DialogueSystem.Bark(Speakers.Tom, "Evacuare a nord... e noi come ci arriviamo? A piedi?", Eye(tom));
                    break;
            }
            yield return new WaitForSeconds(3f);
        }

        // ================================================================== 3. i ruggiti, poi il silenzio

        IEnumerator QuakeAndSilence()
        {
            if (radio != null) { radio.PutDown(); radio.usable = false; }
            bunkerChatter = false;
            yield return Quake(1f, 0);
            Stand(tom);
            Stand(roy);
            LookOf(jef, null);
            DialogueSystem.Bark(Speakers.Roy, "Che cazzo era quello?!", Eye(roy));
            yield return new WaitForSeconds(2.2f);
            DialogueSystem.Bark(Speakers.Tom, "Zitti! State zitti!", Eye(tom));
            yield return new WaitForSeconds(1.6f);
            yield return Quake(0.7f, 1);
            yield return new WaitForSeconds(2.5f);
            // silenzio: anche la radio tace (solo il generatore)
            if (radio != null) radio.PoweredOn = false;
            droneTarget = 0f;
            yield return new WaitForSeconds(4f);
            DialogueSystem.Bark(Speakers.Jef, "...Se n'è andato.", Eye(jef));
            yield return new WaitForSeconds(8.5f); // 15 secondi di silenzio dopo l'ultima scossa, poi bussano
        }

        /// <summary>
        /// Una scossa: il rombo dall'alto (e il ruggito, se 'roar' >= 0), la camera trema, cade polvere dal soffitto,
        /// le lampadine oscillano e si abbassano, i barattoli sugli scaffali tintinnano.
        /// </summary>
        IEnumerator Quake(float strength, int roar)
        {
            if (roar >= 0) HorrorAudio.Play2D(ProceduralAudio.Roar(roar % 3), 0.9f * strength, 0.92f + roar * 0.04f);
            yield return new WaitForSeconds(roar >= 0 ? 0.5f : 0f);
            HorrorAudio.Play2D(ProceduralAudio.Rumble(), 0.9f * strength);
            Shake(1.3f * strength, 2.6f * strength);
            SwingBulbs(14f * strength);
            StartCoroutine(DimBulbs(1.6f * strength));
            CeilingDust(strength);
            if (shelfPoints != null)
                foreach (var p in shelfPoints)
                    if (p != null) HorrorAudio.PlayAt(ProceduralAudio.CansClink(), p.position, 0.5f * strength, Random.Range(1.1f, 1.4f), 10f);
            yield return new WaitForSeconds(2.2f * strength);
        }

        /// <summary>In sottofondo, finché si è in casa: ogni tanto una scossa lontana, a volte con un ruggito.</summary>
        IEnumerator Aftershocks()
        {
            int n = 0;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(14f, 24f));
                if (!aftershocks || fade > 0.01f) continue;
                float s = PlayerInside ? 0.45f : 0.3f;
                yield return Quake(s, (n++ % 2 == 0) ? n : -1);
            }
        }

        // ================================================================== 4. bussano alla porta

        IEnumerator Knocking()
        {
            Vector3 door = frontDoor.transform.position + new Vector3(0.45f, 1.5f, -0.4f);
            yield return Bang(door, 5);
            DialogueSystem.Bark(Speakers.PizzaBoy, "AIUTO!! APRITE!!", door);
            yield return new WaitForSeconds(2.4f);
            yield return Bang(door, 4);
            DialogueSystem.Bark(Speakers.PizzaBoy, "Vi prego! C'è qualcuno?! Aprite!", door);
            yield return new WaitForSeconds(2.6f);
            if (pizzaBoy != null) pizzaBoy.gameObject.SetActive(true);

            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Roy, "C'è qualcuno alla porta...")
                .Say(Speakers.Tom, "Non aprite a nessuno. L'ha detto la TV.")
                .Say(Speakers.Roy, "È un ragazzo, Tom! È solo un ragazzo!")
                .Say(Speakers.Tom, "E allora vai tu, Roy. Vai su tu.")
                .Say(Speakers.Roy, "Io? Sei matto?")
                .Say(Speakers.Jef, "Io non ci vado.")
                .Choice(new[] { "Vado io.", "Cazzo... vado io." }, i =>
                {
                    if (i == 0) DialogueSystem.Insert((Speakers.Tom, "Muark, no! Sei impazzito?"));
                    else DialogueSystem.Insert((Speakers.Roy, "Muark... stai attento, ok?"));
                }, 10f, () => DialogueSystem.Insert((Speakers.Muark, "...Vado io. Non possiamo lasciarlo lì.")))
                .Say(Speakers.Tom, "Apri, fallo entrare e chiudi subito. Subito, hai capito?"), tom, 9f);
            Job(tom, Actor(tom).WalkTo(stairsBottom.position + new Vector3(-0.6f, 0f, 0.8f)));
            if (cellarDoor != null) { cellarDoor.Unlock(); cellarDoor.Open(1); }
        }

        IEnumerator Bang(Vector3 at, int times)
        {
            for (int i = 0; i < times; i++)
            {
                HorrorAudio.PlayAt(ProceduralAudio.KnockOnce(i % 3), at, 1f, Random.Range(0.75f, 0.9f), 30f);
                yield return new WaitForSeconds(Random.Range(0.18f, 0.32f));
            }
        }

        // ================================================================== 5. di sopra (il timer nascosto di due minuti)

        IEnumerator Upstairs()
        {
            StoryDirector.Objective = "Aprire la porta d'ingresso (Il coraggioso del gruppo)";
            Goal((null, Speakers.Muark, "La porta d'ingresso... devo aprirgli."),
                 (roy, Speakers.Roy, "Muark! Il ragazzo! Sbrigati!"));
            frontDoor.Unlock();
            frontDoor.enabled = true;
            float slow = frontDoor.speed;
            frontDoor.speed = 0.45f; // si apre piano, con paura
            bool opened = false;
            System.Action<HorrorDoor> onOpen = d => opened = true;
            frontDoor.Opened += onOpen;
            if (pizzaBoy != null) StartCoroutine(BoyWaits());

            Vector3 door = frontDoor.transform.position + new Vector3(0.45f, 1.5f, -0.4f);
            float deadline = Time.time + 120f;
            float nextPlea = Time.time + 9f;
            int plea = 0;
            string[] pleas = { "fattorino|Per favore! Mi sentite?!", "fattorino|Aprite! Vi prego, aprite!", "fattorino|Sono il fattorino! Le pizze! Vi prego!", "fattorino|Sta arrivando qualcosa... APRITE!" };
            bool saidUpstairs = false;
            while (!opened && Time.time < deadline)
            {
                if (Time.time > nextPlea)
                {
                    nextPlea = Time.time + Random.Range(9f, 14f);
                    yield return Bang(door, 3 + plea % 3);
                    string line = pleas[plea++ % pleas.Length];
                    DialogueSystem.Bark(Speakers.PizzaBoy, line.Substring(line.IndexOf('|') + 1), door);
                }
                if (!saidUpstairs && PlayerOnGroundFloor)
                {
                    saidUpstairs = true;
                    StartCoroutine(SeeTheDevastation());
                }
                yield return null;
            }
            frontDoor.Opened -= onOpen;
            ClearGoal();
            if (opened)
            {
                sawTheBoy = true;
                yield return AtTheDoor();
            }
            else yield return TooLate();
            frontDoor.speed = slow;
        }

        /// <summary>La prima occhiata al piano terra: il primo piano non c'è più, si vede il cielo.</summary>
        IEnumerator SeeTheDevastation()
        {
            yield return new WaitForSeconds(0.8f);
            DialogueSystem.Bark(Speakers.Muark, "Oh, cazzo... la casa...");
            yield return new WaitForSeconds(3.2f);
            DialogueSystem.Bark(Speakers.Muark, "Il piano di sopra... non c'è più.");
            distantRoars = true; // da qui in poi, ogni tanto, la creatura ruggisce lontano
        }

        /// <summary>Il fattorino aspetta sul portico con la pizza in mano, col fiatone, e si guarda alle spalle.</summary>
        IEnumerator BoyWaits()
        {
            var anim = pizzaBoy.GetComponentInChildren<ProceduralBodyAnimator>();
            Actor(pizzaBoy).idleLook = false;
            while (pizzaBoy != null && pizzaBoy.gameObject.activeInHierarchy)
            {
                if (anim != null)
                {
                    Transform r = pizzaBoy.transform;
                    Vector3 box = r.position + Vector3.up * (1.05f + Mathf.Sin(Time.time * 9f) * 0.01f) + r.forward * 0.36f;
                    if (pizzaBox != null) { pizzaBox.position = box; pizzaBox.rotation = r.rotation; }
                    for (int side = -1; side <= 1; side += 2)
                        anim.SetExternalHandIK(side, box + r.right * side * 0.19f, r.right * side * 0.8f + Vector3.down * 0.6f, 1f,
                                               ProceduralBodyAnimator.PalmDown(side, r.forward));
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ il macigno davanti alla porta (Muark ha aperto)

        IEnumerator AtTheDoor()
        {
            Lock(true);
            NpcSpeaker boySp = pizzaBoy != null ? pizzaBoy.GetComponent<NpcSpeaker>() : null;
            Vector3 boyEye = boySp != null ? boySp.EyePoint : frontDoor.transform.position + new Vector3(0.45f, 1.6f, -1.3f);
            yield return LookAt(boyEye, 0.9f);
            LookOf(pizzaBoy, fpc.cam.transform);
            yield return Talk(DialogueSystem.Conversation().Blocking()
                .Say(Speakers.PizzaBoy, "Grazie! Grazie, cazzo... pensavo che nessuno...")
                .Say(Speakers.PizzaBoy, "Le avete ordinate voi, le pizze? Ho girato tutta la sera...")
                .Say(Speakers.PizzaBoy, "Là fuori c'è qualcosa. Dovete farmi entrare, subito, prima che..."));

            // il fischio dall'alto: il ragazzo alza gli occhi, Muark anche
            HorrorAudio.Play2D(ProceduralAudio.Whistle(), 0.9f);
            Vector3 land = rockFront.position;
            Vector3 from = land + new Vector3(9f, 34f, -22f);
            var sky = new GameObject("Il cielo sopra il portico").transform;
            sky.position = Vector3.Lerp(land, from, 0.35f);
            LookOf(pizzaBoy, sky);
            DialogueSystem.Bark(Speakers.PizzaBoy, "...Cos'è questo rumore?", boyEye);
            yield return new WaitForSeconds(0.7f);
            // la camera resta sulla faccia del ragazzo (che guarda in alto): il masso entra nell'inquadratura da solo
            bool falling = true;
            StartCoroutine(TrackFace(boySp, () => falling));
            yield return RockFalls(rockFront, from, land, 1.7f, true);
            falling = false;
            Destroy(sky.gameObject);

            // l'impatto, con Muark a un metro e mezzo
            Vector3 boyChest = pizzaBoy != null ? pizzaBoy.transform.position + Vector3.up * 1.2f : land + Vector3.up;
            HideDoorLeaf(); // l'anta vola via prima che arrivi il sangue (niente macchie sospese nel vuoto)
            Impact(land, boyChest, true);
            PlaceFrontRock(true);
            if (pizzaBox != null) { pizzaBox.gameObject.SetActive(false); }
            yield return ThrownBack(land);
        }

        IEnumerator TrackFace(NpcSpeaker who, System.Func<bool> keep)
        {
            while (who != null && keep())
            {
                Vector3 dir = who.EyePoint - fpc.cam.transform.position;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -80f, 70f);
                float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
                fpc.SetLook(Mathf.LerpAngle(fpc.Yaw, yaw, k), Mathf.Lerp(fpc.Pitch, pitch, k));
                yield return null;
            }
        }

        /// <summary>Il corpo di Muark (visto da terra sembrerebbe in piedi): nascosto finché non si rialza.</summary>
        void SetBodyVisible(bool visible)
        {
            if (fpc == null || fpc.body == null) return;
            foreach (var r in fpc.body.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>Il frammento scende dal cielo girando su sé stesso; nell'ultimo tratto tutto rallenta.</summary>
        IEnumerator RockFalls(Transform rock, Vector3 from, Vector3 to, float seconds, bool slowMotion)
        {
            rock.gameObject.SetActive(true);
            var cols = rock.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;
            Quaternion r0 = rock.rotation;
            Vector3 spin = Random.onUnitSphere * 220f;
            bool slowed = false;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / seconds);
                float e = t * t; // accelera cadendo
                rock.position = Vector3.Lerp(from, to, e);
                rock.rotation = Quaternion.Euler(spin * t) * r0;
                if (slowMotion && !slowed && t > 0.62f) { slowed = true; SetTimeScale(0.22f); }
                if (slowed && t > 0.96f) SetTimeScale(1f);
                yield return null;
            }
            SetTimeScale(1f);
            rock.position = to;
            foreach (var c in cols) c.enabled = true;
        }

        static void SetTimeScale(float s)
        {
            Time.timeScale = s;
            Time.fixedDeltaTime = 0.02f * s;
        }

        /// <summary>L'impatto: schianto, lampo, sangue su tutto (porta, muri, pavimento, la lente), polvere e schegge.</summary>
        void Impact(Vector3 at, Vector3 bloodFrom, bool nearPlayer)
        {
            HorrorAudio.Play2D(ProceduralAudio.HugeImpact(), 1f);
            HorrorAudio.PlayAt(ProceduralAudio.Explosion(), at, 0.6f, 1.4f, 60f);
            Shake(nearPlayer ? 3.2f : 1.2f, nearPlayer ? 1.6f : 1.2f);
            if (pizzaBoy != null) pizzaBoy.gameObject.SetActive(false);
            // il sangue: verso la casa (e Muark), tutto intorno, e una pozza sotto il masso
            Vector3 toHouse = (player.position + Vector3.up * 1.2f - bloodFrom).normalized;
            BloodFx.Spray(bloodMaterial, bloodFrom, toHouse, 90, 9f, 55f, 1.2f);
            BloodFx.Spray(bloodMaterial, bloodFrom, Vector3.up, 50, 6f, 175f);
            BloodFx.Spray(bloodMaterial, bloodFrom + Vector3.up * 0.4f, Vector3.down, 25, 3f, 80f, 1.4f);
            BloodFx.Pool(bloodMaterial, at + new Vector3(0.6f, 0f, 1.2f), 1.6f);
            if (nearPlayer && stun != null) stun.Hit(1f, 28);
            DustBurst(at + Vector3.up * 0.5f, 1f);
            Debris(at + Vector3.up * 1f, 22);
            if (frontDoor != null && frontDoor.hinge != null) Planks(frontDoor.hinge.position + Vector3.up * 1f, toHouse);
            CeilingDust(0.6f);
        }

        /// <summary>Muark vola all'indietro, cade, resta a terra; si rialza a fatica e barcolla.</summary>
        IEnumerator ThrownBack(Vector3 from)
        {
            Vector3 away = player.position - from;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -player.forward;
            fpc.Push(away * 7.5f + Vector3.up * 3f);
            SetBodyVisible(false);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.55f) { fpc.knockdown = t; fpc.roll = Mathf.Lerp(0f, -12f, t); yield return null; }
            fpc.knockdown = 1f;
            yield return new WaitForSeconds(1.2f);
            // gli occhi si chiudono e si riaprono, due volte
            for (int k = 0; k < 2; k++)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f) { fade = t * 0.92f; yield return null; }
                yield return new WaitForSeconds(k == 0 ? 0.9f : 1.6f);
                for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.8f) { fade = t * 0.92f; yield return null; }
                fade = 0f;
                yield return new WaitForSeconds(0.6f);
            }
            DialogueSystem.Bark(Speakers.Muark, "...Cosa... cos'è successo...");
            yield return new WaitForSeconds(2.6f);
            // si rialza, piano
            fpc.stagger = 1f;
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 2.6f) { fpc.knockdown = t; fpc.roll = Mathf.Lerp(0f, -12f, t); yield return null; }
            fpc.knockdown = 0f;
            fpc.roll = 0f;
            SetBodyVisible(true);
            Lock(false);
            StartCoroutine(StaggerFades(45f));
            DialogueSystem.Bark(Speakers.Muark, "Il ragazzo... dov'è il ragazzo...");
            yield return new WaitForSeconds(3.5f);
            DialogueSystem.Bark(Speakers.Muark, "Devo... devo tornare giù.");
        }

        IEnumerator StaggerFades(float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds) { fpc.stagger = Mathf.Max(0.15f, 1f - t); yield return null; }
            // un filo di sbandamento resta finché non si siede
            while (fpc.stagger > 0f && !fpc.IsSeated) yield return null;
            fpc.stagger = 0f;
        }

        // ------------------------------------------------------------------ troppo tardi (Muark non ha aperto in tempo)

        IEnumerator TooLate()
        {
            Vector3 land = rockFront.position;
            Vector3 boy = pizzaBoy != null ? pizzaBoy.transform.position + Vector3.up * 1.2f : land + Vector3.up;
            bool near = PlayerOnGroundFloor && Vector3.Distance(player.position, frontDoor.transform.position) < 10f;
            DialogueSystem.Bark(Speakers.PizzaBoy, "No... no, no! NO!", boy);
            HorrorAudio.PlayAt(ProceduralAudio.Whistle(), land + Vector3.up * 8f, 1f, 1f, 60f);
            yield return new WaitForSeconds(1.6f);
            yield return RockFalls(rockFront, land + new Vector3(9f, 34f, -22f), land, 0.7f, false);
            // la porta chiusa: il sangue resta fuori (porta, portico, finestre); dentro arriva solo lo spostamento d'aria
            HorrorAudio.Play2D(ProceduralAudio.HugeImpact(), near ? 1f : 0.55f);
            Shake(near ? 2f : 0.9f, 1.4f);
            if (pizzaBoy != null) pizzaBoy.gameObject.SetActive(false);
            BloodFx.Spray(bloodMaterial, boy, Vector3.up, 70, 7f, 175f, 1.1f);
            BloodFx.Pool(bloodMaterial, land + new Vector3(0.6f, 0f, 1.2f), 1.6f);
            DustBurst(land + Vector3.up * 0.5f, 0.8f);
            CeilingDust(0.8f);
            PlaceFrontRock(false);
            if (pizzaBox != null) pizzaBox.gameObject.SetActive(false);
            if (near && stun != null) stun.Hit(0.55f, 0);
            yield return new WaitForSeconds(1.5f);
            if (near)
            {
                DialogueSystem.Bark(Speakers.Muark, "No... no, no, no!");
                yield return new WaitForSeconds(3f);
                DialogueSystem.Bark(Speakers.Muark, "Era lì fuori... e io... devo tornare giù.");
            }
            else if (PlayerInBasement)
            {
                DialogueSystem.Bark(Speakers.Roy, "...L'ha preso.", Eye(roy));
                yield return new WaitForSeconds(2.6f);
                DialogueSystem.Bark(Speakers.Tom, "Non c'era niente da fare. Niente.", Eye(tom));
            }
        }

        // ================================================================== 6. di nuovo giù, sotto shock

        IEnumerator BackDownstairs()
        {
            aftershocks = true;
            droneTarget = 0.12f;
            Vector3 sofa = seatPlayer.FrontPosition;
            bool NearSofa() => PlayerInBasement && Flat(player.position, sofa) < 1.5f;
            if (!NearSofa())
            {
                StoryDirector.Objective = "Tornare in cantina dagli altri (Corri, cazzo)";
                Goal((null, Speakers.Muark, "Devo tornare giù dagli altri."),
                     (tom, Speakers.Tom, "Muark?! MUARK! Rispondi!"));
                while (!NearSofa()) yield return null;
                ClearGoal();
            }
            // gli amici gli vanno incontro e lo fanno sedere
            Job(tom, Actor(tom).WalkTo(seatPlayer.FrontPosition + Vector3.right * 0.8f));
            Job(roy, Actor(roy).WalkTo(seatPlayer.FrontPosition + new Vector3(-0.8f, 0f, 0.4f)));
            yield return new WaitForSeconds(0.6f);
            var c = DialogueSystem.Conversation();
            if (sawTheBoy)
                c.Say(Speakers.Tom, "Muark! Cristo santo... sei coperto di sangue!")
                 .Say(Speakers.Roy, "Di chi è quel sangue? Muark? Di chi è?")
                 .Say(Speakers.Muark, "Il ragazzo... era lì, mi ha detto grazie... e poi... un masso enorme... dal cielo...")
                 .Say(Speakers.Muark, "Non c'è più. Non c'è più niente.");
            else
                c.Say(Speakers.Roy, "Muark! Hai sentito quel botto?")
                 .Say(Speakers.Muark, "Il ragazzo... non ho fatto in tempo...")
                 .Say(Speakers.Jef, "Non c'è più.");
            c.Say(Speakers.Jef, "Siediti, Muark. Siediti.")
             .Do(() => { if (fpc != null && !fpc.IsSeated && seatPlayer != null && seatPlayer.IsFree) fpc.SitOn(seatPlayer); })
             .Say(Speakers.Tom, "Respira. Respira, ok? Sei qui con noi.")
             .Say(Speakers.Roy, "Ok... ok, e adesso? Adesso che facciamo?");
            yield return Talk(c.Blocking());
            fpc.stagger = 0f;
        }

        // ================================================================== 7. Tom sintonizza la radio

        IEnumerator TomTunesTheRadio()
        {
            yield return Talk(DialogueSystem.Conversation().Say(Speakers.Tom, "La radio. Fammi provare."));
            if (radio != null)
            {
                Vector3 front = radio.transform.position + (seatPlayer.transform.position - radio.transform.position).normalized * 0.55f;
                front.y = basementFloorY;
                yield return Actor(tom).WalkTo(front + Vector3.right * 0.5f, Gait.Walk, 8f);
                yield return Actor(tom).FaceTowards(radio.transform.position, 0.4f);
                radio.PoweredOn = true;
                radio.SetMilitaryMessage(
                    (Speakers.Military, "...a tutti i civili ancora presenti nella zona di Maple Grove."),
                    (Speakers.Military, "Abbandonate immediatamente l'area. Ripeto: abbandonate immediatamente l'area."),
                    (Speakers.Military, "Questa zona non è sicura. Non è sic..."));
                yield return radio.TuneTo(103.1f, 2.2f);
                yield return new WaitForSeconds(0.6f);
                yield return radio.TuneTo(105.5f, 1.6f);
                for (float t = 0f; t < 40f && !radio.Finished("militare"); t += Time.deltaTime) yield return null;
                radio.KillSignal();
                Shake(0.2f, 0.3f);
                yield return new WaitForSeconds(1.4f);
                radio.PoweredOn = false;
            }
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "No, no, no! Torna su! Cazzo!")
                .Say(Speakers.Roy, "Abbandonare l'area? E come? Là fuori ci sono quelle cose!")
                .Say(Speakers.Tom, "Restare qui sotto è peggio, Roy!")
                .Say(Speakers.Roy, "E Muark? L'hai visto com'è ridotto?")
                .Say(Speakers.Jef, "Smettetela."));
        }

        // ================================================================== 8. il generatore muore

        IEnumerator GeneratorDies()
        {
            bulbsFlicker = false;
            if (generator != null) StartCoroutine(generator.Die(2.6f));
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                float k = 1f - t / 2.6f;
                for (int i = 0; i < cellarBulbs.Length; i++)
                    if (cellarBulbs[i] != null) { cellarBulbs[i].enabled = Random.value < 0.4f + 0.6f * k; cellarBulbs[i].intensity = bulbBase[i] * k; }
                yield return null;
            }
            foreach (var b in cellarBulbs) if (b != null) b.enabled = false;
            HorrorAudio.Play2D(ProceduralAudio.PowerDown(), 0.5f, 0.8f);
            yield return new WaitForSeconds(1.2f);
            DialogueSystem.Bark(Speakers.Roy, "No, no, no... non adesso!", Eye(roy));
            yield return new WaitForSeconds(2.2f);
            DialogueSystem.Bark(Speakers.Tom, "Merda. Il generatore è a secco.", Eye(tom));
            yield return new WaitForSeconds(2.6f);
        }

        // ================================================================== 9. via di qui: Tom prende il comando

        IEnumerator LeaveTheHouse()
        {
            Stand(tom); Stand(roy); Stand(jef);
            LookOf(jef, null);
            if (fpc != null) fpc.noRun = true; // al buio dietro a Tom si cammina
            foreach (var m in new[] { tom, roy, jef }) { Actor(m).idleLook = false; Actor(m).keepPersonalSpace = false; }
            // Tom prende la torcia dalla cassa e la accende
            if (tomTorch != null)
            {
                yield return Actor(tom).WalkTo(tomTorch.position + (tom.transform.position - tomTorch.position).normalized * 0.6f, Gait.Walk, 6f);
                tomTorch.gameObject.SetActive(false);
            }
            HorrorAudio.PlayAt(ProceduralAudio.Click(), Eye(tom), 0.7f);
            TomFlashlight();
            yield return Talk(DialogueSystem.Conversation()
                .Say(Speakers.Tom, "Ascoltatemi. Ce ne andiamo. Adesso.")
                .Say(Speakers.Tom, "Prendiamo la macchina di mia madre e andiamo a nord, come ha detto la radio.")
                .Say(Speakers.Roy, "...Ok. Ok.")
                .Say(Speakers.Tom, "State dietro di me. Muark, ce la fai a camminare?")
                .Choice(new[] { "Sì... credo.", "Non ho scelta, no?" }, i =>
                {
                    if (i == 1) DialogueSystem.Insert((Speakers.Tom, "No. Nessuno di noi ce l'ha."));
                }, 8f)
                .Do(() => { if (fpc != null && fpc.IsSeated) fpc.StandUp(); }), tom, 9f);

            // Tom è il capo: Roy e Jef guardano lui, non Muark
            HoldLook(roy, Look(tom), 0.4f);
            HoldLook(jef, Look(tom), 0.4f);
            StoryDirector.Objective = "Seguire Tom (Resta vicino alla luce)";
            Goal((tom, Speakers.Tom, "Muark, di qua! Stammi dietro!"),
                 (roy, Speakers.Roy, "Muark, dai! Non restare indietro al buio!"));
            // su per la scala: Tom davanti, Roy e Jef dietro
            Job(tom, Lead(stairsTop.position, 1.4f));
            Job(roy, Follow(roy, tom, 1.3f));
            Job(jef, Follow(jef, roy, 1.2f));
            yield return WaitFor(() => Flat(tom.transform.position, stairsTop.position) < 0.8f, 25f);
            Unstick(stairsTop.position);
            yield return WaitFor(() => !PlayerInBasement, 90f);
            // alla porta d'ingresso: niente da fare
            Job(tom, Lead(frontInside.position, 2f));
            yield return WaitFor(() => Flat(tom.transform.position, frontInside.position) < 0.8f, 25f);
            Unstick(frontInside.position);
            yield return Actor(tom).FaceTowards(frontDoor.transform.position + new Vector3(0.45f, 0f, 0f), 0.4f);
            HorrorAudio.PlayAt(ProceduralAudio.Thud(), frontDoor.transform.position + Vector3.up, 0.8f, 0.9f);
            Shake(0.15f, 0.2f);
            yield return WaitFor(() => Flat(player.position, tom.transform.position) < 5f, 30f);
            var blocked = DialogueSystem.Conversation().Say(Speakers.Tom, "Non si passa... c'è quel masso al posto della porta!");
            if (sawTheBoy) blocked.Say(Speakers.Roy, "Il ragazzo è lì sotto. Vero, Muark? È lì sotto.");
            else blocked.Say(Speakers.Roy, "Cristo... il ragazzo era lì.");
            yield return Talk(blocked.Say(Speakers.Tom, "Dal retro. Usciamo in giardino e giriamo dal lato della casa."), tom, 8f);
            // la porta sul retro: incastrata, Tom la sfonda con la spalla
            Job(tom, Lead(backInside.position, 2f));
            Job(roy, Follow(roy, tom, 1.3f));
            Job(jef, Follow(jef, roy, 1.2f));
            yield return WaitFor(() => Flat(tom.transform.position, backInside.position) < 0.8f, 25f);
            Unstick(backInside.position);
            // Tom resta girato verso la porta (anche se Muark è indietro)
            var doorPoint = new GameObject("Porta sul retro (sguardo)").transform;
            doorPoint.position = backDoor.transform.position + Vector3.up * 1.5f;
            HoldLook(tom, doorPoint, 0.6f);
            yield return Actor(tom).FaceTowards(backDoor.transform.position, 0.4f);
            for (int i = 0; i < 2; i++)
            {
                HorrorAudio.PlayAt(ProceduralAudio.Thud(), backDoor.transform.position + Vector3.up, 1f, 0.85f);
                Shake(0.2f, 0.25f);
                if (i == 0) DialogueSystem.Bark(Speakers.Tom, "Dai... dai, apriti!", Eye(tom));
                yield return new WaitForSeconds(1.3f);
            }
            backDoor.Unlock();
            backDoor.Open(-1);
            HoldLook(tom, null, 0f);
            HorrorAudio.PlayAt(ProceduralAudio.DoorCreak(1), backDoor.transform.position, 0.8f);
            yield return new WaitForSeconds(0.8f);
            StoryDirector.Objective = "Uscire in giardino con gli altri";
            Job(tom, Actor(tom).WalkTo(gardenTom.position, Gait.Walk, 25f));
            Job(roy, Actor(roy).WalkTo(gardenRoy.position, Gait.Walk, 25f));
            Job(jef, Actor(jef).WalkTo(gardenJef.position, Gait.Walk, 25f));
            aftershocks = false;
        }

        /// <summary>Tom fa strada con la torcia: se Muark resta indietro si ferma e lo aspetta.</summary>
        IEnumerator Lead(Vector3 target, float waitDistance)
        {
            var actor = Actor(tom);
            while (Flat(tom.transform.position, target) > 0.6f)
            {
                if (player != null && Vector3.Distance(player.position, tom.transform.position) > 7f)
                {
                    // si ferma e aspetta, ma resta girato dove sta andando
                    tom.MoveTo(tom.transform.position, false);
                    while (Vector3.Distance(player.position, tom.transform.position) > 4.5f) yield return null;
                    continue;
                }
                actor.Motor.SetGait(Gait.Walk);
                tom.MoveTo(target, false);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(waitDistance * 0.1f);
        }

        IEnumerator Follow(CharacterMotor who, CharacterMotor leader, float distance)
        {
            var actor = Actor(who);
            while (true)
            {
                Vector3 behind = leader.transform.position - leader.transform.forward * distance;
                if (Flat(who.transform.position, behind) > 0.6f) { actor.Motor.SetGait(Gait.Walk); who.MoveTo(behind, false); }
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// Se Tom non è arrivato (la NavMesh tra le macerie a volte non trova la strada) lo si sposta lì, con gli altri
        /// dietro: la storia non deve bloccarsi.
        /// </summary>
        void Unstick(Vector3 target)
        {
            if (Flat(tom.transform.position, target) < 1.5f) return;
            Vector3 back = (player != null ? player.position : target) - target;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            Warp(tom, target, tom.transform.eulerAngles.y);
            if (Flat(roy.transform.position, target) > 3f) Warp(roy, target + back * 1.2f, roy.transform.eulerAngles.y);
            if (Flat(jef.transform.position, target) > 3f) Warp(jef, target + back * 2.2f, jef.transform.eulerAngles.y);
        }

        IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            for (float t = 0f; t < timeout && !condition(); t += Time.deltaTime) yield return null;
        }

        /// <summary>La torcia in mano a Tom: un faro che segue lo sguardo del personaggio.</summary>
        static Transform Look(CharacterMotor m)
        {
            var sp = m != null ? m.GetComponent<NpcSpeaker>() : null;
            return sp != null ? sp.LookPoint : null;
        }

        /// <summary>Blocca lo sguardo (e l'espressione) di un personaggio: parlare o ascoltare non lo cambia più.</summary>
        static void HoldLook(CharacterMotor m, Transform target, float brow)
        {
            var sp = m != null ? m.GetComponent<NpcSpeaker>() : null;
            if (sp == null) return;
            sp.holdLook = target;
            sp.holdBrow = brow;
            LookOf(m, target);
        }

        void TomFlashlight()
        {
            if (tomLight != null) return;
            tomLight = new GameObject("Torcia di Tom").AddComponent<Light>();
            tomLight.type = LightType.Spot;
            tomLight.spotAngle = 52f;
            tomLight.innerSpotAngle = 22f;
            tomLight.range = 16f;
            tomLight.intensity = 3.2f;
            tomLight.color = new Color(1f, 0.92f, 0.78f);
            tomLight.shadows = LightShadows.Soft;
        }

        // ================================================================== 10. in giardino: la creatura

        IEnumerator Garden()
        {
            ClearGoal();
            yield return WaitFor(() => !PlayerInside && Flat(player.position, gardenTom.position) < 7f, 120f);
            yield return new WaitForSeconds(1.5f);
            // urla lontane, a destra
            HorrorAudio.PlayAt(ProceduralAudio.DistantScreams(), screamsPoint.position, 1f, 1f, 160f);
            yield return new WaitForSeconds(1.2f);
            DialogueSystem.Bark(Speakers.Roy, "Sentite? Da quella parte...", Eye(roy));
            yield return new WaitForSeconds(2.4f);
            HorrorAudio.PlayAt(ProceduralAudio.DistantScreams(), screamsPoint.position + Vector3.forward * 20f, 0.8f, 0.9f, 160f);
            DialogueSystem.Bark(Speakers.Tom, "Zitti.", Eye(tom));
            yield return new WaitForSeconds(1.5f);

            // la creatura si alza all'orizzonte: nessuno riesce a muoversi
            Lock(true);
            distantRoars = false;
            foreach (var m in new[] { tom, roy, jef }) { StopJob(m); m.MoveTo(m.transform.position, false); }
            var focus = new GameObject("La creatura (sguardi)").transform;
            focus.SetParent(creature.head != null ? creature.head : creature.transform, false);
            // tutti fissano la creatura, terrorizzati (sopracciglia alzate), girati verso di lei
            foreach (var m in new[] { tom, roy, jef }) HoldLook(m, focus, 1f);
            staring = true;
            StartCoroutine(StareAtCreature());
            StartCoroutine(LookAt(creature.transform.position + Vector3.up * 45f, 2.4f));
            HorrorAudio.Play2D(ProceduralAudio.Rumble(), 0.6f, 0.6f);
            Shake(0.4f, 6f);
            dreadOn = true;
            yield return creature.Rise(7f);
            Vector3 head = creature.head != null ? creature.head.position : creature.transform.position + Vector3.up * 90f;
            yield return LookAt(head, 1.4f);
            DialogueSystem.Bark(Speakers.Roy, "Oh... mio... Dio...", Eye(roy));
            yield return new WaitForSeconds(1.6f);
            StartCoroutine(creature.Roar(4.5f));
            yield return new WaitForSeconds(0.3f);
            HorrorAudio.Play2D(ProceduralAudio.Roar(2), 1f, 0.85f);
            Shake(1.1f, 4f);
            yield return new WaitForSeconds(4.5f);
            DialogueSystem.Bark(Speakers.Tom, "Non muovetevi. Non... non respirate.", Eye(tom));
            yield return new WaitForSeconds(3.4f);

            // il fischio dall'alto: Jef grida e indica il cielo, Muark si gira verso di lui...
            HorrorAudio.Play2D(ProceduralAudio.Whistle(), 1f, 0.9f);
            yield return new WaitForSeconds(0.4f);
            staring = false;
            var sky = new GameObject("Il cielo sopra il giardino").transform;
            sky.position = jef.transform.position + Vector3.up * 30f;
            HoldLook(jef, sky, 1f);
            DialogueSystem.Bark(Speakers.Jef, "ATTENTI!", Eye(jef));
            StartCoroutine(JefPointsUp(5f));
            dreadOn = false;
            yield return LookAt(Eye(jef) + Vector3.up * 0.4f, 0.5f);
            yield return new WaitForSeconds(1f);
            // ...e un secondo dopo il macigno in fiamme gli arriva addosso dal cielo
            Vector3 cam = fpc.cam.transform.position;
            Vector3 dir = creature.transform.position - cam;
            dir.y = 0f;
            dir.Normalize();
            Vector3 from = cam + dir * 45f + Vector3.up * 60f;
            rockFinal.gameObject.SetActive(true);
            rockFinal.position = from;
            SetOnFire(rockFinal);
            Quaternion r0 = rockFinal.rotation;
            Vector3 end = cam + Vector3.up * 0.3f;
            float seconds = 2.2f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                rockFinal.position = Vector3.Lerp(from, end, t * t);
                rockFinal.rotation = Quaternion.Euler(t * 160f, t * 90f, 0f) * r0;
                // lo sguardo va sul macigno e lo segue mentre arriva
                Vector3 to = rockFinal.position - fpc.cam.transform.position;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -80f, 70f);
                float k = 1f - Mathf.Exp(-(t < 0.25f ? 5f : 12f) * Time.unscaledDeltaTime);
                fpc.SetLook(Mathf.LerpAngle(fpc.Yaw, yaw, k), Mathf.Lerp(fpc.Pitch, pitch, k));
                if (t > 0.7f && Time.timeScale > 0.5f) SetTimeScale(0.4f);
                if (Vector3.Distance(rockFinal.position, fpc.cam.transform.position) < 2.4f) break;
                yield return null;
            }
            SetTimeScale(1f);
            fade = 1f;
            HorrorAudio.Play2D(ProceduralAudio.HugeImpact(), 1f, 0.8f);
            yield return null;
            AudioListener.volume = 0f;
            if (stun != null) stun.ClearAll();
            yield return new WaitForSeconds(3.5f);
        }

        bool staring, dreadOn;

        /// <summary>
        /// Mentre la creatura si alza: i ragazzi restano girati verso di lei, impietriti. Roy si mette le mani in testa,
        /// Tom allarga un braccio davanti agli altri, Jef stringe i pugni al petto.
        /// </summary>
        IEnumerator StareAtCreature()
        {
            var anims = new Dictionary<CharacterMotor, ProceduralBodyAnimator>();
            foreach (var m in new[] { tom, roy, jef }) anims[m] = m.GetComponentInChildren<ProceduralBodyAnimator>();
            for (float t = 0f; staring; t += Time.deltaTime)
            {
                float w = Mathf.Clamp01(t / 0.8f);
                foreach (var m in new[] { tom, roy, jef })
                {
                    Vector3 d = creature.transform.position - m.transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) m.transform.rotation = Quaternion.Slerp(m.transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 2.5f);
                    var anim = anims[m];
                    if (anim == null) continue;
                    Transform r = m.transform;
                    float shiver = Mathf.Sin(Time.time * 22f + m.GetInstanceID()) * 0.012f;
                    if (m == roy)
                        for (int side = -1; side <= 1; side += 2)
                            anim.SetExternalHandIK(side, r.position + Vector3.up * (1.78f + shiver) + r.right * side * 0.16f + r.forward * 0.02f,
                                                   r.right * side + Vector3.down * 0.3f, w, ProceduralBodyAnimator.PalmDown(side, -r.right * side));
                    else if (m == tom)
                        anim.SetExternalHandIK(-1, r.position + Vector3.up * (1.25f + shiver) - r.right * 0.62f + r.forward * 0.25f,
                                               -r.right + Vector3.down * 0.5f, w, ProceduralBodyAnimator.PalmDown(-1, r.forward));
                    else
                        for (int side = -1; side <= 1; side += 2)
                            anim.SetExternalHandIK(side, r.position + Vector3.up * (1.3f + shiver) + r.forward * 0.22f + r.right * side * 0.07f,
                                                   r.right * side + Vector3.down * 0.5f, w, ProceduralBodyAnimator.PalmDown(side, r.forward));
                }
                yield return null;
            }
        }

        /// <summary>Il macigno finale prende fuoco: fiamme e scintille che lasciano la scia, fumo nero, luce arancione.</summary>
        void SetOnFire(Transform rock)
        {
            var glow = new GameObject("Luce del macigno in fiamme").AddComponent<Light>();
            glow.transform.SetParent(rock, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.15f);
            glow.range = 30f;
            glow.intensity = 6f;
            if (fireMaterial != null)
            {
                var fire = MakeParticles("Fiamme del macigno", rock.position, fireMaterial);
                fire.transform.SetParent(rock, true);
                var main = fire.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f, 0.9f), new Color(1f, 0.4f, 0.1f, 0.9f));
                var em = fire.emission;
                em.rateOverTime = 70f;
                var sh = fire.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 1.4f;
                FadeOverLife(fire);
                fire.Play();
            }
            if (dustMaterial != null)
            {
                var smokeTrail = MakeParticles("Fumo del macigno", rock.position, dustMaterial);
                smokeTrail.transform.SetParent(rock, true);
                var main = smokeTrail.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.08f, 0.07f, 0.06f, 0.8f), new Color(0.15f, 0.12f, 0.1f, 0.8f));
                var em = smokeTrail.emission;
                em.rateOverTime = 35f;
                var sh = smokeTrail.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 1.2f;
                FadeOverLife(smokeTrail);
                smokeTrail.Play();
            }
        }

        IEnumerator JefPointsUp(float seconds)
        {
            var anim = jef.GetComponentInChildren<ProceduralBodyAnimator>();
            if (anim == null) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float w = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((seconds - t) / 0.4f);
                Transform r = jef.transform;
                anim.SetExternalHandIK(1, r.position + Vector3.up * 2.35f + r.forward * 0.35f + r.right * 0.25f, r.right * 0.8f, w,
                                       ProceduralBodyAnimator.PalmDown(1, Vector3.up));
                yield return null;
            }
        }

        // ================================================================== 11. epilogo: il notiziario sulla TV crepata

        IEnumerator Epilogue()
        {
            Lock(true);
            fade = 1f;
            aftershocks = false;
            foreach (var m in new[] { tom, roy, jef }) if (m != null) m.gameObject.SetActive(false);
            if (tomLight != null) tomLight.enabled = false;
            if (creature != null) creature.gameObject.SetActive(false);
            if (rockFinal != null) rockFinal.gameObject.SetActive(false);
            if (fpc != null) { fpc.knockdown = 0f; fpc.stagger = 0f; fpc.roll = 0f; fpc.noRun = false; if (fpc.IsSeated) fpc.StandUp(); }
            if (stun != null) stun.ClearAll();
            yield return new WaitForSeconds(0.5f);
            TeleportPlayer(tvView);
            if (fpc != null) fpc.SetLook(tvView.eulerAngles.y, 12f);
            droneTarget = 0f;
            AudioListener.volume = 1f;
            if (tv != null) { tv.powered = true; tv.newsScene = 5; tv.SetChannel(TvChannel.Static); }
            yield return new WaitForSeconds(1f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 4f) { fade = t; yield return null; }
            fade = 0f;
            yield return new WaitForSeconds(1.2f);
            if (tv != null) { tv.newsScene = 0; tv.SetChannel(TvChannel.News); }
            yield return Talk(DialogueSystem.Conversation().Blocking()
                .Say(Speakers.Tv, "...continua la nostra edizione straordinaria. Sono le sei del mattino.")
                .Do(() => Scene(1))
                .Say(Speakers.Tv, "Questa notte, poco dopo le nove, qualcosa è uscito dal sottosuolo, sotto la vecchia centrale elettrica di Maple Grove.")
                .Say(Speakers.Tv, "Queste immagini sono state riprese da un nostro elicottero, poco prima che perdessimo il contatto.")
                .Say(Speakers.Tv, "La creatura, alta più di sessanta metri, ha attraversato la città da est a ovest, scagliando detriti per chilometri.")
                .Say(Speakers.Tv, "Con lei, dalle fognature, sono uscite centinaia di creature più piccole.")
                .Do(() => Scene(2))
                .Say(Speakers.Tv, "L'esercito ha istituito una zona di esclusione di quindici chilometri.")
                .Say(Speakers.Tv, "Chi si trova ancora all'interno deve dirigersi verso nord, lungo la Route 9, fino a Fort Jackson.")
                .Do(() => Scene(3))
                .Say(Speakers.Tv, "Le operazioni militari sono in corso. Il Pentagono non ha rilasciato dichiarazioni.")
                .Do(() => Scene(4))
                .Say(Speakers.Tv, "Non abbiamo ancora un numero delle vittime. Si parla di migliaia di dispersi.")
                .Do(() => Scene(0))
                .Say(Speakers.Tv, "Se ci state ascoltando... non restate dove siete. Andate a nord.")
                .Say(Speakers.Tv, "Ripetiamo: andate a n...")
                .Do(() => Scene(5)));
            if (tv != null) tv.SetChannel(TvChannel.Static);
            yield return new WaitForSeconds(2.5f);
            title = Loc.T("Fine del capitolo 2\n\n<size=22>Continua...</size>");
            titleAlpha = 1f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 3f) { fade = t; yield return null; }
            fade = 1f;
            if (tv != null) tv.powered = false;
        }

        void Scene(int k)
        {
            if (tv == null) return;
            tv.newsScene = k;
            tv.SetChannel(k == 5 ? TvChannel.Static : TvChannel.News);
            HorrorAudio.PlayAt(ProceduralAudio.Click(), tv.transform.position, 0.25f, 1.3f, 8f);
        }

        IEnumerator Credits()
        {
            yield return new WaitForSeconds(4f);
            string[] cards =
            {
                "<size=26>641</size>\n\n<size=18>presenta</size>",
                "<size=46><b>Guys Night</b></size>\n\n<size=22>Capitolo 2 — Non aprite a nessuno</size>",
                "<size=20>Muark · Tom · Roy · Jef</size>\n<size=16>e il ragazzo delle pizze, che ha fatto in tempo a dire grazie</size>",
                "<size=18>Nel prossimo capitolo...</size>\n\n<size=26>A nord.</size>",
                "<size=22>Grazie per aver giocato</size>",
            };
            foreach (var card in cards)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f) { titleAlpha = 1f - t; yield return null; }
                title = Loc.T(card);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f) { titleAlpha = t; yield return null; }
                titleAlpha = 1f;
                yield return new WaitForSeconds(3.5f);
            }
            // si potrà rigiocare il capitolo dal suo inizio
            GameSettings.Checkpoint = CheckpointBase;
        }

        static void Checkpoint(StartAt at)
        {
            int v = CheckpointBase + (int)at;
            if (v > GameSettings.Checkpoint || GameSettings.Checkpoint < CheckpointBase) GameSettings.Checkpoint = v;
        }

        // ================================================================== suggerimenti a voce

        readonly List<(CharacterMotor who, Speaker sp, string text)> hints = new List<(CharacterMotor, Speaker, string)>();
        float hintClock;
        int hintIndex;

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
            if (radio != null && radio.InUse) { hintClock = Time.time + 10f; return; }
            hintClock = Time.time + 30f;
            for (int n = 0; n < hints.Count; n++)
            {
                var h = hints[hintIndex++ % hints.Count];
                if (h.who != null && !h.who.gameObject.activeInHierarchy) continue;
                DialogueSystem.Bark(h.sp, h.text, h.who != null ? Eye(h.who) : (Vector3?)null);
                return;
            }
        }

        // ================================================================== effetti: scosse, polvere, lampadine, schegge

        void Shake(float amount, float seconds)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTime = Mathf.Max(shakeTime, seconds);
        }

        float swingAmp, swingStart;

        void SwingBulbs(float degrees)
        {
            swingAmp = Mathf.Max(swingAmp * Mathf.Exp(-(Time.time - swingStart) / 3f), degrees);
            swingStart = Time.time;
        }

        IEnumerator DimBulbs(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!bulbsFlicker) yield break;
                for (int i = 0; i < cellarBulbs.Length; i++)
                    if (cellarBulbs[i] != null) cellarBulbs[i].enabled = Random.value > 0.25f;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
            }
            for (int i = 0; i < cellarBulbs.Length; i++) if (cellarBulbs[i] != null && bulbsFlicker) cellarBulbs[i].enabled = true;
        }

        /// <summary>Polvere e calcinacci che scendono dal soffitto della cantina (e dove c'è il giocatore).</summary>
        void CeilingDust(float strength)
        {
            if (dustMaterial == null) return;
            Vector3 c = basementBounds.center;
            c.y = ceilingY - 0.1f;
            var ps = MakeParticles("Polvere dal soffitto", new Vector3(c.x, c.y, c.z));
            var main = ps.main;
            main.loop = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.52f, 0.47f, 0.35f), new Color(0.7f, 0.66f, 0.6f, 0.45f));
            main.gravityModifier = 0.06f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(120 * strength)), new ParticleSystem.Burst(0.6f, (short)(60 * strength)) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(basementBounds.size.x * 0.9f, 0.05f, basementBounds.size.z * 0.9f);
            FadeOverLife(ps);
            ps.Play();
            Destroy(ps.gameObject, 8f);
        }

        void DustBurst(Vector3 at, float strength)
        {
            if (dustMaterial == null) return;
            var ps = MakeParticles("Nuvola di polvere", at);
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 9f * strength);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.42f, 0.38f, 0.75f), new Color(0.6f, 0.56f, 0.5f, 0.85f));
            main.gravityModifier = -0.01f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(90 * strength)) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 1.2f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
            FadeOverLife(ps);
            ps.Play();
            Destroy(ps.gameObject, 12f);
        }

        ParticleSystem MakeParticles(string name, Vector3 at) => MakeParticles(name, at, dustMaterial);

        ParticleSystem MakeParticles(string name, Vector3 at, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void FadeOverLife(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        /// <summary>Schegge di roccia che volano via dall'impatto (cubetti con la fisica).</summary>
        void Debris(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Scheggia";
                float s = Random.Range(0.08f, 0.35f);
                go.transform.localScale = new Vector3(s, s * Random.Range(0.5f, 1f), s * Random.Range(0.6f, 1.2f));
                go.transform.position = at + Random.insideUnitSphere * 0.8f;
                go.transform.rotation = Random.rotation;
                if (rockMaterial != null) go.GetComponent<Renderer>().sharedMaterial = rockMaterial;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = s * 20f;
                Vector3 v = Random.onUnitSphere;
                v.y = Mathf.Abs(v.y) * 0.8f + 0.2f;
                rb.linearVelocity = v * Random.Range(4f, 12f);
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                Destroy(rb, 6f); // poi restano ferme dove sono cadute
            }
        }

        /// <summary>I pezzi della porta che volano dentro casa.</summary>
        void Planks(Vector3 at, Vector3 dir)
        {
            Material wood = null;
            var r0 = frontDoor.GetComponentInChildren<Renderer>(true);
            if (r0 != null) wood = r0.sharedMaterial;
            for (int i = 0; i < 6; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Pezzo della porta";
                go.transform.localScale = new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.4f, 1.2f), 0.05f);
                go.transform.position = at + Random.insideUnitSphere * 0.5f;
                go.transform.rotation = Random.rotation;
                if (wood != null) go.GetComponent<Renderer>().sharedMaterial = wood;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 2f;
                rb.linearVelocity = (dir + Random.insideUnitSphere * 0.4f).normalized * Random.Range(5f, 11f);
                rb.angularVelocity = Random.insideUnitSphere * 10f;
                Destroy(rb, 6f);
            }
        }

        // ================================================================== personaggi

        NpcActor Actor(CharacterMotor m) => m.GetComponent<NpcActor>();

        static Vector3 Eye(CharacterMotor m)
        {
            if (m == null) return Vector3.zero;
            var sp = m.GetComponent<NpcSpeaker>();
            return sp != null ? sp.EyePoint : m.transform.position + Vector3.up * 1.6f;
        }

        static void LookOf(CharacterMotor m, Transform target)
        {
            if (m == null) return;
            var a = m.GetComponent<NpcActor>();
            if (a != null) a.LookAt(target);
        }

        readonly Dictionary<CharacterMotor, Coroutine> jobs = new Dictionary<CharacterMotor, Coroutine>();

        void Job(CharacterMotor m, IEnumerator routine)
        {
            if (m == null) return;
            StopJob(m);
            jobs[m] = StartCoroutine(routine);
        }

        void StopJob(CharacterMotor m)
        {
            if (m != null && jobs.TryGetValue(m, out Coroutine c) && c != null) StopCoroutine(c);
            if (m != null) jobs.Remove(m);
        }

        void SitNow(CharacterMotor m, Seat seat)
        {
            if (m == null || seat == null || !seat.Claim(m)) return;
            seated[seat] = m;
            Warp(m, seat.FrontPosition, seat.transform.eulerAngles.y);
            m.SitDown(seat.transform.position, seat.transform.rotation, seat.height);
        }

        void Stand(CharacterMotor m)
        {
            if (m == null || (!m.IsSeated && !m.IsSitting)) return;
            Seat s = null;
            foreach (var kv in seated) if (kv.Value == m) s = kv.Key;
            m.StandUp(s != null ? s.FrontPosition : m.transform.position + m.transform.forward * 0.7f);
            if (s != null) { s.Release(m); seated.Remove(s); }
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

        void TeleportPlayer(Transform at)
        {
            if (at == null || player == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = at.position;
            if (cc != null) cc.enabled = true;
            if (fpc != null) fpc.SetLook(at.eulerAngles.y, 0f);
        }

        IEnumerator LookAt(Vector3 point, float duration)
        {
            if (fpc == null || fpc.cam == null) yield break;
            Vector3 dir = point - fpc.cam.transform.position;
            float yaw1 = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch1 = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -80f, 70f);
            float yaw0 = fpc.Yaw, pitch0 = fpc.Pitch;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
                float e = t * t * (3f - 2f * t);
                fpc.SetLook(Mathf.LerpAngle(yaw0, yaw1, e), Mathf.Lerp(pitch0, pitch1, e));
                yield return null;
            }
        }

        IEnumerator Talk(DialogueSystem.Builder conversation, CharacterMotor near = null, float radius = 7f)
        {
            while (DialogueSystem.IsTalking) yield return null;
            if (near != null) conversation.Near(near.transform, near == tom ? Speakers.Tom : near == roy ? Speakers.Roy : Speakers.Jef, radius);
            bool done = false;
            conversation.OnEnd(() => done = true).Start();
            while (!done) yield return null;
        }

        void Update()
        {
            NpcActor.PlayerIndoors = PlayerInside;
            UpdateHints();
            // claustrofobia: più si guarda la creatura, più la vista si stringe e il cuore batte
            if (stun != null && creature != null && fpc != null && fpc.cam != null)
            {
                float want = 0f;
                if (dreadOn && creature.gameObject.activeInHierarchy && creature.head != null)
                {
                    Vector3 to = (creature.head.position - fpc.cam.transform.position).normalized;
                    want = Mathf.InverseLerp(0.6f, 0.97f, Vector3.Dot(fpc.cam.transform.forward, to));
                }
                stun.dread = Mathf.MoveTowards(stun.dread, want, Time.deltaTime * (want > stun.dread ? 0.35f : 1.2f));
            }

            // le lampadine del generatore: oscillano sul filo e calano quando il motore perde colpi
            if (bulbPivots != null)
            {
                float a = swingAmp * Mathf.Exp(-(Time.time - swingStart) / 3f);
                for (int i = 0; i < bulbPivots.Length; i++)
                    if (bulbPivots[i] != null)
                        bulbPivots[i].localRotation = Quaternion.Euler(Mathf.Sin((Time.time - swingStart) * 3.9f + i) * a,
                                                                       0f, Mathf.Cos((Time.time - swingStart) * 3.4f + i * 2f) * a * 0.6f);
            }
            if (bulbsFlicker)
                for (int i = 0; i < cellarBulbs.Length; i++)
                {
                    if (cellarBulbs[i] == null) continue;
                    float n = Mathf.PerlinNoise(Time.time * 1.3f, i * 3.1f);
                    float dip = n < 0.22f ? 0.45f : 1f; // ogni tanto il generatore singhiozza
                    cellarBulbs[i].intensity = bulbBase[i] * (0.8f + 0.2f * n) * dip;
                }

            // la torcia di Tom segue la sua testa
            if (tomLight != null && tom != null && tomLight.enabled)
            {
                Vector3 eye = Eye(tom);
                tomLight.transform.position = eye + tom.transform.right * 0.22f - Vector3.up * 0.35f + tom.transform.forward * 0.3f;
                tomLight.transform.rotation = Quaternion.Slerp(tomLight.transform.rotation,
                    Quaternion.LookRotation(tom.transform.forward - Vector3.up * 0.25f), Time.deltaTime * 6f);
            }

            if (sirens != null)
            {
                float want = sirenTarget * (PlayerInBasement ? 0.25f : 1f) * GameSettings.Effects;
                sirens.volume = Mathf.MoveTowards(sirens.volume, want, Time.deltaTime * 0.05f);
                if (sirens.volume > 0.001f && !sirens.isPlaying) sirens.Play();
                if (sirens.volume <= 0.001f && sirens.isPlaying) sirens.Stop();
            }
            if (drone != null)
            {
                drone.volume = Mathf.MoveTowards(drone.volume, droneTarget * GameSettings.Effects, Time.deltaTime * 0.08f);
                if (drone.volume > 0.001f && !drone.isPlaying) drone.Play();
                if (drone.volume <= 0.001f && drone.isPlaying) drone.Stop();
            }
        }

        void LateUpdate()
        {
            if (shakeTime <= 0f || fpc == null || fpc.cam == null) return;
            shakeTime -= Time.unscaledDeltaTime;
            float a = shakeAmount * Mathf.Clamp01(shakeTime * 2f);
            float t = Time.unscaledTime * 28f;
            fpc.cam.transform.localRotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * a * 2f, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * a * 2f,
                                                                (Mathf.PerlinNoise(t, 3.3f) - 0.5f) * a);
            if (shakeTime <= 0f) shakeAmount = 0f;
        }

        void OnDestroy()
        {
            SetTimeScale(1f);
            AudioListener.volume = 1f;
        }

        // ================================================================== interfaccia: nero, titoli

        GUIStyle titleStyle;

        void OnGUI()
        {
            if (fade <= 0.001f && titleAlpha <= 0.001f) return;
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, alignment = TextAnchor.MiddleCenter, richText = true, font = UiStyle.Hand };
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (!string.IsNullOrEmpty(title))
            {
                GUI.color = new Color(1f, 1f, 1f, titleAlpha * fade);
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), title, titleStyle);
            }
            GUI.color = Color.white;
        }
    }
}
