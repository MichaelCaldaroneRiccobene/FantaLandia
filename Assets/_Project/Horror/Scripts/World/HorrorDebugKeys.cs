using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Tasti di prova della scena di test: B bussano alla porta, T cambia canale, N blackout on/off.
    /// Servono a provare i sistemi che userà la storia.
    /// </summary>
    public class HorrorDebugKeys : MonoBehaviour
    {
        public HorrorDoor frontDoor;
        public CrtTv tv;
        public LightSwitch[] switches;
        [Tooltip("Se presente, N usa il contatore di tutta la casa")]
        public HousePower power;

        bool blackout;

        void Update()
        {
            if (Pressed(0) && frontDoor != null) frontDoor.Knock();
            if (Pressed(1) && tv != null) tv.SetChannel((TvChannel)(((int)tv.channel + 1) % 4));
            if (Pressed(2))
            {
                blackout = !blackout;
                if (power != null) power.SetPower(!blackout);
                else
                {
                    foreach (var s in switches) if (s != null) s.SetPowered(!blackout);
                    if (tv != null) tv.powered = !blackout;
                }
                DialogueSystem.Bark(Speakers.Muark, blackout ? "È saltata la corrente..." : "È tornata la luce.");
            }
        }

        static bool Pressed(int which)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            return which == 0 ? k.bKey.wasPressedThisFrame : which == 1 ? k.tKey.wasPressedThisFrame : k.nKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(which == 0 ? KeyCode.B : which == 1 ? KeyCode.T : KeyCode.N);
#endif
        }

        void OnGUI()
        {
            GUI.depth = -50; // aiuto per le prove (sempre sopra all'immagine dell'effetto retrò, che sta a 100)
            if (DialogueSystem.IsBlocking) return;
            GUI.Label(new Rect(10, 10, 600, 120),
                "<b>Prova prima persona</b>\nWASD muovi · Shift corri · C abbassati · E interagisci · F torcia\n" +
                "B bussano alla porta · T canale TV · N blackout · F2 effetto retro",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
        }
    }
}
