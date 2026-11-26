using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FantaLandia.Core
{
    /// <summary>
    /// Punto unico di lettura dell'input. Funziona sia con il nuovo Input System sia con il vecchio Input Manager.
    /// Il gameplay legge l'input solo da qui: per rimappare i tasti si tocca un solo file.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;

        static bool Held(Key k) => Kb != null && Kb[k].isPressed;
        static bool Pressed(Key k) => Kb != null && Kb[k].wasPressedThisFrame;

        public static Vector2 MousePosition => Ms != null ? Ms.position.ReadValue() : Vector2.zero;

        /// <summary>Scatti della rotellina: circa +1/-1 per scatto.</summary>
        public static float Scroll
        {
            get
            {
                if (Ms == null) return 0f;
                float y = Ms.scroll.ReadValue().y;
                return Mathf.Abs(y) > 10f ? y / 120f : y; // Windows riporta 120 per scatto
            }
        }

        public static bool LeftDown => Ms != null && Ms.leftButton.wasPressedThisFrame;
        public static bool LeftUp => Ms != null && Ms.leftButton.wasReleasedThisFrame;
        public static bool LeftHeld => Ms != null && Ms.leftButton.isPressed;
        public static bool RightDown => Ms != null && Ms.rightButton.wasPressedThisFrame;
        public static bool RightHeld => Ms != null && Ms.rightButton.isPressed;
        public static bool RightUp => Ms != null && Ms.rightButton.wasReleasedThisFrame;
        public static bool MiddleHeld => Ms != null && Ms.middleButton.isPressed;

        public static bool Shift => Kb != null && Kb.shiftKey.isPressed;
        public static bool Alt => Kb != null && Kb.altKey.isPressed;
        public static bool Ctrl => Kb != null && Kb.ctrlKey.isPressed;
        public static bool Deselect => Pressed(Key.Escape);

        public static Vector2 PanAxis => new Vector2(
            (Held(Key.D) || Held(Key.RightArrow) ? 1f : 0f) - (Held(Key.A) || Held(Key.LeftArrow) ? 1f : 0f),
            (Held(Key.W) || Held(Key.UpArrow) ? 1f : 0f) - (Held(Key.S) || Held(Key.DownArrow) ? 1f : 0f));

        public static float RotateAxis => (Held(Key.E) ? 1f : 0f) - (Held(Key.Q) ? 1f : 0f);

        public static bool GaitCycle => Pressed(Key.R);
        public static bool GroupPaceToggle => Pressed(Key.G);
        public static bool FollowToggle => Pressed(Key.F);
        public static bool CenterCamera => Pressed(Key.Space);
        public static bool WallModeCycle => Pressed(Key.V);
        public static bool HelpToggle => Pressed(Key.F1);
        public static bool DebugSever => Pressed(Key.K);
        public static bool DebugRestore => Pressed(Key.L);
        public static bool DebugHit => Pressed(Key.J);
        public static bool DebugHeal => Pressed(Key.H);
        public static bool DebugProsthetic => Pressed(Key.P);

        // --- prima persona (horror)
        public static Vector2 MouseDelta => Ms != null ? Ms.delta.ReadValue() : Vector2.zero;
        public static bool Interact => Pressed(Key.E);
        public static bool FlashlightToggle => Pressed(Key.F);
        public static bool CrouchHeld => Held(Key.C) || Held(Key.LeftCtrl);
        public static bool RunHeld => Shift;
        public static bool AdvanceDialogue => Pressed(Key.E) || Pressed(Key.Space) || LeftDown;
        /// <summary>Salta la battuta in corso nei dialoghi "camminando" (E resta per interagire).</summary>
        public static bool SkipLine => Pressed(Key.Space);
        public static bool StandUp => Pressed(Key.W) || Pressed(Key.S) || Pressed(Key.A) || Pressed(Key.D);
        public static bool RetroToggle => Pressed(Key.F2);
        public static bool DropDown => Pressed(Key.Q);
        public static bool DropHeld => Held(Key.Q);

        /// <summary>Restituisce 1-9 se è stato premuto un tasto numerico in questo frame, altrimenti 0.</summary>
        public static int DigitPressed()
        {
            if (Kb == null) return 0;
            for (int i = 1; i <= 9; i++)
                if (Kb[Key.Digit1 + (i - 1)].wasPressedThisFrame) return i;
            return 0;
        }
#else
        static bool Held(KeyCode k) => Input.GetKey(k);
        static bool Pressed(KeyCode k) => Input.GetKeyDown(k);

        public static Vector2 MousePosition => Input.mousePosition;
        public static float Scroll => Input.mouseScrollDelta.y;

        public static bool LeftDown => Input.GetMouseButtonDown(0);
        public static bool LeftUp => Input.GetMouseButtonUp(0);
        public static bool LeftHeld => Input.GetMouseButton(0);
        public static bool RightDown => Input.GetMouseButtonDown(1);
        public static bool RightHeld => Input.GetMouseButton(1);
        public static bool RightUp => Input.GetMouseButtonUp(1);
        public static bool MiddleHeld => Input.GetMouseButton(2);

        public static bool Shift => Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);
        public static bool Alt => Held(KeyCode.LeftAlt) || Held(KeyCode.RightAlt);
        public static bool Ctrl => Held(KeyCode.LeftControl) || Held(KeyCode.RightControl);
        public static bool Deselect => Pressed(KeyCode.Escape);

        public static Vector2 PanAxis => new Vector2(
            (Held(KeyCode.D) || Held(KeyCode.RightArrow) ? 1f : 0f) - (Held(KeyCode.A) || Held(KeyCode.LeftArrow) ? 1f : 0f),
            (Held(KeyCode.W) || Held(KeyCode.UpArrow) ? 1f : 0f) - (Held(KeyCode.S) || Held(KeyCode.DownArrow) ? 1f : 0f));

        public static float RotateAxis => (Held(KeyCode.E) ? 1f : 0f) - (Held(KeyCode.Q) ? 1f : 0f);

        public static bool GaitCycle => Pressed(KeyCode.R);
        public static bool GroupPaceToggle => Pressed(KeyCode.G);
        public static bool FollowToggle => Pressed(KeyCode.F);
        public static bool CenterCamera => Pressed(KeyCode.Space);
        public static bool WallModeCycle => Pressed(KeyCode.V);
        public static bool HelpToggle => Pressed(KeyCode.F1);
        public static bool DebugSever => Pressed(KeyCode.K);
        public static bool DebugRestore => Pressed(KeyCode.L);
        public static bool DebugHit => Pressed(KeyCode.J);
        public static bool DebugHeal => Pressed(KeyCode.H);
        public static bool DebugProsthetic => Pressed(KeyCode.P);

        // --- prima persona (horror)
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        public static bool Interact => Pressed(KeyCode.E);
        public static bool FlashlightToggle => Pressed(KeyCode.F);
        public static bool CrouchHeld => Held(KeyCode.C) || Held(KeyCode.LeftControl);
        public static bool RunHeld => Shift;
        public static bool AdvanceDialogue => Pressed(KeyCode.E) || Pressed(KeyCode.Space) || LeftDown;
        public static bool SkipLine => Pressed(KeyCode.Space);
        public static bool StandUp => Pressed(KeyCode.W) || Pressed(KeyCode.S) || Pressed(KeyCode.A) || Pressed(KeyCode.D);
        public static bool RetroToggle => Pressed(KeyCode.F2);
        public static bool DropDown => Pressed(KeyCode.Q);
        public static bool DropHeld => Held(KeyCode.Q);

        public static int DigitPressed()
        {
            for (int i = 1; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + (i - 1))) return i;
            return 0;
        }
#endif
    }
}
