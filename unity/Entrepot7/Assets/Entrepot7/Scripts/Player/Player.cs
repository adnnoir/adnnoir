using UnityEngine;

namespace E7
{
    /// <summary>
    /// L'agent (le joueur) : déplacement, caméra bodycam, lampe, santé, cris « Police ! », menottes, ordres aux coéquipiers.
    /// Hiérarchie : Player (CharacterController, rotation gauche/droite) → tête → caméra principale → caméra des armes.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Player : MonoBehaviour
    {
        public static Player I { get; private set; }
        public const float STAND_EYE = 1.5f, CROUCH_EYE = 0.98f;

        CharacterController cc;
        public Transform Head { get; private set; }
        public Camera Cam { get; private set; }
        public Camera VmCam { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        Light lamp;

        float yaw, pitch, tYaw, tPitch;
        public float Crouch { get; private set; }
        public float Lean { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Moving { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float StepPhase { get; private set; }
        public float BobAmount { get; private set; }
        public float Stamina { get; private set; } = 1f;
        public float Health { get; private set; } = 100f;
        public bool Alive { get; private set; }
        public float HurtFx { get; private set; }
        public float BlindFx { get; private set; }
        public float DeathT { get; private set; }
        public Vector3 LastHitFrom { get; private set; }
        public float DmgDirT { get; private set; }
        float shake, lastHit, breathT, shoutT, heartT, fovNow = 74f, deathPitch;
        int lastStepIdx;
        bool crouchToggle;

        public Vector3 Eye => Cam.transform.position;
        public float Yaw => yaw;

        public static Player Create(Transform parent)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.layer = L.Player;
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.34f; cc.height = 1.75f; cc.center = new Vector3(0, 0.875f, 0); cc.stepOffset = 0.3f; cc.skinWidth = 0.03f; cc.slopeLimit = 45;
            // les IA contournent le joueur au lieu de lui rentrer dedans
            var ob = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            ob.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule; ob.radius = 0.35f; ob.height = 1.75f; ob.center = new Vector3(0, 0.875f, 0); ob.carving = false;
            var p = go.AddComponent<Player>();
            p.cc = cc;
            p.Head = U.Child(go.transform, "Tete", new Vector3(0, STAND_EYE, 0)).transform;
            var camGo = U.Child(p.Head, "Camera");
            camGo.tag = "MainCamera";
            p.Cam = camGo.AddComponent<Camera>();
            p.Cam.nearClipPlane = 0.05f; p.Cam.farClipPlane = 140f; p.Cam.fieldOfView = Save.Data.settings.fov;
            p.Cam.cullingMask = ~((1 << L.Viewmodel) | (1 << L.Preview));
            p.Cam.clearFlags = CameraClearFlags.SolidColor; p.Cam.backgroundColor = new Color(0.008f, 0.008f, 0.012f);
            p.Cam.allowHDR = true; p.Cam.allowMSAA = true;
            var listener = camGo.AddComponent<AudioListener>();
            var vmGo = U.Child(camGo.transform, "CameraArmes");
            p.VmCam = vmGo.AddComponent<Camera>();
            p.VmCam.clearFlags = CameraClearFlags.Depth; p.VmCam.cullingMask = 1 << L.Viewmodel;
            p.VmCam.nearClipPlane = 0.01f; p.VmCam.farClipPlane = 5f; p.VmCam.fieldOfView = 50; p.VmCam.depth = p.Cam.depth + 1;
            p.VmCam.allowHDR = true;
            // lampe tactique (fixée sous l'arme, vise un peu vers le bas)
            p.lamp = U.Child(camGo.transform, "Lampe", new Vector3(0.1f, -0.22f, 0.1f)).AddComponent<Light>();
            p.lamp.type = LightType.Spot; p.lamp.range = 40f; p.lamp.spotAngle = 52f; p.lamp.innerSpotAngle = 20f;
            p.lamp.intensity = 3.2f; p.lamp.color = new Color(1f, 0.95f, 0.86f);
            p.lamp.shadows = LightShadows.Soft; p.lamp.shadowBias = 0.02f; p.lamp.shadowNormalBias = 0.3f;
            p.lamp.cullingMask = ~(1 << L.Viewmodel);
            p.lamp.transform.localRotation = Quaternion.LookRotation(new Vector3(0.02f, -0.1f, 10f) - new Vector3(0.1f, -0.22f, 0.1f));
            // lumières propres à l'arme (pour bien la voir même dans le noir)
            var key = U.Child(vmGo.transform, "vmKey").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 0.55f; key.color = new Color(0.87f, 0.9f, 0.93f); key.cullingMask = 1 << L.Viewmodel; key.shadows = LightShadows.None;
            key.transform.localRotation = Quaternion.LookRotation(-new Vector3(-0.4f, 1f, -0.25f));
            var rim = U.Child(vmGo.transform, "vmRim").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = 0.35f; rim.color = new Color(0.74f, 0.82f, 1f); rim.cullingMask = 1 << L.Viewmodel; rim.shadows = LightShadows.None;
            rim.transform.localRotation = Quaternion.LookRotation(-new Vector3(-0.6f, 0.4f, 1f));
            var fill = U.Child(vmGo.transform, "vmFill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = 0.15f; fill.color = new Color(1f, 0.89f, 0.75f); fill.cullingMask = 1 << L.Viewmodel; fill.shadows = LightShadows.None;
            fill.transform.localRotation = Quaternion.LookRotation(-new Vector3(0.6f, -0.2f, 0.5f));
            p.Weapons = go.AddComponent<WeaponSystem>();
            p.Weapons.Init(p, p.Cam, p.VmCam);
            if (AudioSys.I) AudioSys.I.AttachListener(listener);
            I = p;
            return p;
        }

        public void Spawn(Vector3 pos, float yawDeg, bool training)
        {
            cc.enabled = false; transform.position = pos; cc.enabled = true;
            yaw = tYaw = yawDeg; pitch = tPitch = 0;
            Health = 100; Alive = true; Crouch = 0; Lean = 0; Velocity = Vector3.zero; Stamina = 1;
            HurtFx = BlindFx = shake = 0; DeathT = 0; DmgDirT = 0; crouchToggle = false;
            Head.localPosition = new Vector3(0, STAND_EYE, 0);
            Cam.transform.localPosition = Vector3.zero; Cam.transform.localRotation = Quaternion.identity;
            Weapons.ResetInventory(training);
            Weapons.SetHandsVisible(true);
            lamp.enabled = true;
        }

        // ---------------- effets extérieurs ----------------
        public void AddLook(float dPitchUpDeg, float dYawDeg) { tPitch = Mathf.Clamp(tPitch - dPitchUpDeg, -77f, 77f); tYaw += dYawDeg; }
        public void Shake(float a) { shake = Mathf.Min(0.05f, shake + a); }
        public void Blind(float amount) { BlindFx = Mathf.Max(BlindFx, amount); }
        public void Heal(float hp) { Health = Mathf.Min(100, Health + hp); }

        public void Hurt(float dmg, Vector3 from)
        {
            if (!Alive) return;
            Health = Mathf.Max(0, Health - dmg);
            Game.I.Stats.dmg += dmg;
            lastHit = Time.time;
            HurtFx = Mathf.Min(0.85f, HurtFx + dmg / 45f);
            Shake(0.02f);
            tPitch -= Random.Range(-0.02f, 0.03f) * Mathf.Rad2Deg; tYaw += Random.Range(-0.03f, 0.03f) * Mathf.Rad2Deg;
            bool heavy = dmg > 18;
            AudioSys.I.Sfx("hurt", 0.8f);
            AudioSys.I.Muffle(600, heavy ? 2.5f : 1.2f, heavy ? 0.05f : 0f);
            LastHitFrom = from; DmgDirT = 1.2f;
            if (Health <= 0) Die();
        }

        void Die()
        {
            Alive = false; DeathT = 0; deathPitch = pitch;
            Weapons.SetHandsVisible(false);
            Game.I.OnPlayerDead();
        }

        /// <summary>Angle (radians) de la source d'un tir par rapport à la direction regardée (pour l'indicateur de dégâts).</summary>
        public float RelAngle(Vector3 p)
        {
            var d = p - transform.position; d.y = 0;
            var f = Quaternion.Euler(0, yaw, 0) * Vector3.forward; var r = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            return Mathf.Atan2(Vector3.Dot(d.normalized, r), Vector3.Dot(d.normalized, f));
        }

        // ---------------- boucle ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            var st = Game.I != null ? Game.I.State : GameState.Menu;
            HurtFx = Mathf.Max((1 - Health / 100f) * 0.3f, HurtFx - dt * 0.6f);
            BlindFx = Mathf.Max(0, BlindFx - dt * 0.28f);
            DmgDirT = Mathf.Max(0, DmgDirT - dt);
            if (st == GameState.Playing && Alive) Tick(dt);
            else if (st == GameState.Dead) DeathCam(dt);
        }

        void Tick(float dt)
        {
            var s = Save.Data.settings;
            // regard
            var look = GameInput.Look;
            float sens = s.sens * (Weapons.adsT > 0.5f ? s.adsSens : 1f) * 2f;
            tYaw += look.x * sens;
            tPitch -= look.y * sens * (s.invertY ? -1 : 1);
            tPitch = Mathf.Clamp(tPitch, -77f, 77f);
            float k = 1 - Mathf.Exp(-dt * 24);
            yaw += (tYaw - yaw) * k; pitch += (tPitch - pitch) * k;
            // accroupi (bascule)
            if (GameInput.CrouchDown) crouchToggle = !crouchToggle;
            Crouch = U.Damp(Crouch, crouchToggle ? 1 : 0, 9, dt);
            cc.height = Mathf.Lerp(1.75f, 1.2f, Crouch); cc.center = new Vector3(0, cc.height / 2, 0);
            // déplacement
            var mv = GameInput.Move;
            bool wantSprint = GameInput.Sprint && mv.y > 0 && !GameInput.Aim && Crouch < 0.5f && !(Weapons.Reloading && Weapons.Def.pump);
            Sprinting = wantSprint && Stamina > 0.05f;
            Stamina = Mathf.Clamp01(Stamina + (Sprinting ? -dt / 6f : dt / 9f));
            string cur = Weapons.Cur;
            float heavy = cur == "pistol" ? 1.06f : cur == "smg" ? 1.03f : 1f;
            float speed = (Sprinting ? 5.3f : Crouch > 0.5f ? 1.7f : Weapons.adsT > 0.5f ? 2.1f : 3.1f) * heavy;
            var rot = Quaternion.Euler(0, yaw, 0);
            var wish = rot * new Vector3(mv.x, 0, mv.y);
            if (wish.sqrMagnitude > 0) wish = wish.normalized * speed;
            var v = Velocity; var hv = new Vector3(v.x, 0, v.z);
            hv = Vector3.Lerp(hv, wish, 1 - Mathf.Exp(-dt * 10));
            float vy = cc.isGrounded ? -1f : v.y - 9.8f * dt;
            Velocity = new Vector3(hv.x, vy, hv.z);
            cc.Move(Velocity * dt);
            transform.rotation = rot;
            float sp = hv.magnitude;
            Moving = sp > 0.4f;
            StepPhase += dt * sp * 1.85f;
            int idx = Mathf.FloorToInt(StepPhase / Mathf.PI);
            if (idx != lastStepIdx)
            {
                lastStepIdx = idx;
                if (Moving)
                {
                    AudioSys.I.Step(Sprinting, Crouch > 0.5f);
                    float loud = Sprinting ? 10 : Crouch > 0.5f ? 0 : 4.5f;
                    if (loud > 0) Squad.Hear(transform.position, loud, false);
                }
            }
            breathT -= dt;
            if (Stamina < 0.55f && breathT <= 0) { breathT = 0.9f + Stamina; AudioSys.I.Sfx(Random.value < 0.5f ? "breathIn" : "breathOut", 0.5f); }
            // se pencher (bloqué si un mur est trop près)
            float leanWant = GameInput.Lean;
            if (leanWant != 0)
            {
                float eye = Mathf.Lerp(STAND_EYE, CROUCH_EYE, Crouch);
                var side = rot * Vector3.right * leanWant;
                if (Physics.Raycast(transform.position + Vector3.up * eye, side, 0.6f, L.WorldMask, QueryTriggerInteraction.Ignore)) leanWant = 0;
            }
            Lean = U.Damp(Lean, leanWant, 8, dt);
            // régénération en mode facile
            if (s.diff == "facile" && Time.time - lastHit > 5 && Health < 100) Health = Mathf.Min(100, Health + dt * 6);
            if (Health < 35) { heartT -= dt; if (heartT <= 0) { AudioSys.I.Sfx("heart", 0.9f); heartT = 0.85f; } }
            shoutT -= dt;
            // actions
            if (GameInput.Lamp) { Weapons.LampOn = !Weapons.LampOn; AudioSys.I.Sfx("click", 0.5f, 1.2f); }
            lamp.enabled = Weapons.LampOn;
            if (GameInput.Reload) Weapons.StartReload();
            if (GameInput.FireMode) Weapons.ToggleFireMode();
            if (GameInput.Primary) Weapons.SwitchTo("primary");
            if (GameInput.Secondary) Weapons.SwitchTo("secondary");
            float sc = GameInput.Scroll; if (Mathf.Abs(sc) > 0.01f) Weapons.SwitchTo(Weapons.curSlot == "primary" ? "secondary" : "primary");
            if (GameInput.Flashbang) Weapons.ThrowBang();
            if (GameInput.Inspect) Weapons.Inspect();
            if (GameInput.Shout) Shout();
            if (GameInput.Arrest) TryArrest();
            if (GameInput.TeamFollow) Squad.TeamToggleFollow();
            if (GameInput.TeamMove) { if (Physics.Raycast(Cam.transform.position, Cam.transform.forward, out var h, 40, L.WorldMask, QueryTriggerInteraction.Ignore)) Squad.TeamMoveTo(h.point + h.normal * 0.4f); }
            Weapons.Tick(dt, GameInput.Fire, GameInput.FireDown, GameInput.Aim, look);
            CameraPose(dt);
        }

        void CameraPose(float dt)
        {
            var s = Save.Data.settings;
            float eye = Mathf.Lerp(STAND_EYE, CROUCH_EYE, Crouch);
            float moveAmt = Mathf.Clamp(new Vector2(Velocity.x, Velocity.z).magnitude / 3.1f, 0, 1.8f) * (s.bob ? 1 : 0.25f);
            BobAmount = U.Damp(BobAmount, moveAmt, 6, dt);
            float ph = StepPhase, t = Time.time;
            float bobY = Mathf.Abs(Mathf.Sin(ph)) * 0.045f * BobAmount * (1 - Crouch * 0.4f);
            float bobX = Mathf.Cos(ph) * 0.03f * BobAmount;
            float breath = (Mathf.Sin(t * 1.5f) * 0.005f + Mathf.Sin(t * 0.37f) * 0.003f) * (1 + (1 - Stamina) * 3);
            shake *= Mathf.Exp(-dt * 10);
            Head.localPosition = new Vector3(0, eye, 0);
            Cam.transform.localPosition = new Vector3(Lean * 0.42f + bobX, bobY + breath - 0.06f * Mathf.Abs(Lean), 0);
            float w = Weapons.recoilYaw, r = Weapons.recoil;
            float rx = pitch - (r + Mathf.Cos(ph * 2) * 0.012f * BobAmount + Random.Range(-1f, 1f) * shake) * Mathf.Rad2Deg;
            float ry = (w + Mathf.Sin(t * 0.9f) * 0.004f + Random.Range(-1f, 1f) * shake) * Mathf.Rad2Deg;
            float rz = (-Lean * 0.16f + Mathf.Cos(ph) * 0.012f * BobAmount + Random.Range(-1f, 1f) * shake * 0.5f) * Mathf.Rad2Deg;
            Cam.transform.localRotation = Quaternion.Euler(rx, ry, rz);
            float zoom = Save.Data.loadout.For(Weapons.Cur).optic == "irons" ? 0.8f : 0.72f;
            fovNow = U.Damp(fovNow, Mathf.Lerp(s.fov, s.fov * zoom, Weapons.adsT) + Weapons.sprintT * 5, 10, dt);
            Cam.fieldOfView = fovNow;
        }

        void DeathCam(float dt)
        {
            DeathT += dt;
            Head.localPosition = new Vector3(0, Mathf.Lerp(Head.localPosition.y, 0.25f, 1 - Mathf.Exp(-dt * 5)), 0);
            var e = Cam.transform.localEulerAngles;
            float z = Mathf.LerpAngle(e.z, 69f, 1 - Mathf.Exp(-dt * 3));
            float x = Mathf.LerpAngle(e.x, 11f, 1 - Mathf.Exp(-dt * 3));
            Cam.transform.localRotation = Quaternion.Euler(x, e.y, z);
        }

        /// <summary>Caméra du menu : lent panoramique dans le grand hall.</summary>
        public void MenuCamera(float t, bool perso, Vector3 officerPos)
        {
            cc.enabled = false;
            if (perso)
            {
                transform.position = new Vector3(officerPos.x + Mathf.Sin(t * 0.2f) * 0.35f, 0, officerPos.z + 2.9f);
                transform.rotation = Quaternion.Euler(0, 180f + Mathf.Sin(t * 0.2f) * 6f, 0);
                Head.localPosition = new Vector3(0, 1.35f, 0);
                Cam.transform.localRotation = Quaternion.Euler(4.6f, 0, 0);
                Cam.fieldOfView = 50;
            }
            else
            {
                float a = t * 0.045f;
                transform.position = new Vector3(15.5f * Level.T + Mathf.Sin(a) * 9, 0, 10 * Level.T + Mathf.Sin(a * 1.3f) * 0.5f);
                transform.rotation = Quaternion.Euler(0, 270f + Mathf.Sin(t * 0.06f) * 63f, 0);
                Head.localPosition = new Vector3(0, 1.55f, 0);
                Cam.transform.localRotation = Quaternion.Euler(2.9f, 0, 0);
                Cam.fieldOfView = Save.Data.settings.fov;
            }
            Cam.transform.localPosition = Vector3.zero;
            Alive = false;
        }

        public void EnableController() { cc.enabled = true; }

        // ---------------- cris et menottes ----------------
        void Shout()
        {
            if (shoutT > 0 || Game.I.Mode != GameMode.Mission) return;
            shoutT = 3;
            int li = Random.Range(0, 3);
            string[] lines = { "Police ! Lâchez vos armes !", "Police ! Mains en l’air !", "Police ! Couchez-vous !" };
            Hud.Subtitle("TOI", lines[li], "me");
            AudioSys.I.PlayerShout(li);
            Squad.Shout(transform.position, Eye);
        }

        void TryArrest()
        {
            var b = Squad.NearestSurrendered(transform.position, 1.9f);
            if (b == null) return;
            b.Arrest(true);
            Game.I.Stats.arrests++;
            Hud.Note("Suspect menotté");
            Game.I.RadioLater(0.5f, "Centrale, ici " + Save.Data.character.unit + ". Un individu interpellé.", "me", new[] { "p_m_ici", "#", "p_m_arrest" });
            Game.I.CheckEnd();
        }
    }
}
