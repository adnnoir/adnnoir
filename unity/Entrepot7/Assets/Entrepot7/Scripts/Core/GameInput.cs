using UnityEngine;

namespace E7
{
    /// <summary>
    /// Toutes les touches du jeu au même endroit. Clavier AZERTY (par défaut) ou QWERTY.
    /// Utilise l'ancien système d'entrées d'Unity (Input Manager), actif par défaut dans un projet neuf.
    /// </summary>
    public static class GameInput
    {
        static bool Az => Save.Data == null || Save.Data.settings.keyboard != "qwerty";

        public static KeyCode Forward => Az ? KeyCode.Z : KeyCode.W;
        public static KeyCode Left => Az ? KeyCode.Q : KeyCode.A;
        public static KeyCode Back => KeyCode.S;
        public static KeyCode Right => KeyCode.D;
        public static KeyCode LeanLeft => Az ? KeyCode.A : KeyCode.Q;
        public static KeyCode LeanRight => KeyCode.E;

        public static Vector2 Move
        {
            get
            {
                float x = 0, y = 0;
                if (Input.GetKey(Forward) || Input.GetKey(KeyCode.UpArrow)) y += 1;
                if (Input.GetKey(Back) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
                if (Input.GetKey(Right) || Input.GetKey(KeyCode.RightArrow)) x += 1;
                if (Input.GetKey(Left) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1 ? v.normalized : v;
            }
        }

        public static Vector2 Look
        {
            get
            {
                float x = 0, y = 0;
                try { x = Input.GetAxisRaw("Mouse X"); y = Input.GetAxisRaw("Mouse Y"); } catch (System.Exception) { }
                return new Vector2(x, y);
            }
        }

        public static float Scroll { get { try { return Input.GetAxisRaw("Mouse ScrollWheel"); } catch (System.Exception) { return 0; } } }

        public static bool Fire => Input.GetMouseButton(0);
        public static bool FireDown => Input.GetMouseButtonDown(0);
        public static bool Aim => Input.GetMouseButton(1);
        public static bool Sprint => Input.GetKey(KeyCode.LeftShift);
        public static bool CrouchDown => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
        public static bool Jump => Input.GetKeyDown(KeyCode.Space);
        public static float Lean => (Input.GetKey(LeanRight) ? 1f : 0f) - (Input.GetKey(LeanLeft) ? 1f : 0f);
        public static bool Reload => Input.GetKeyDown(KeyCode.R);
        public static bool Lamp => Input.GetKeyDown(KeyCode.F);
        public static bool FireMode => Input.GetKeyDown(KeyCode.B);
        public static bool Primary => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Ampersand);
        public static bool Secondary => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
        public static bool Flashbang => Input.GetKeyDown(KeyCode.X);
        public static bool Inspect => Input.GetKeyDown(KeyCode.I);
        public static bool Shout => Input.GetKeyDown(KeyCode.V);
        public static bool Arrest => Input.GetKeyDown(KeyCode.G);
        public static bool TeamFollow => Input.GetKeyDown(KeyCode.T);   // coéquipiers : suivre / tenir la position
        public static bool TeamMove => Input.GetKeyDown(KeyCode.Y);     // coéquipiers : aller là où je vise
        public static bool Pause => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
