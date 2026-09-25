using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace E7
{
    public enum GameState { Loading, Menu, Playing, Paused, Dead, End }
    public enum GameMode { Mission, Training, Arena }

    /// <summary>
    /// Point d'entrée : se crée tout seul au lancement (même dans une scène vide) et construit tout le jeu :
    /// niveau, joueur, IA, sons, effets, interface.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public GameState State { get; private set; } = GameState.Loading;
        public GameMode Mode { get; private set; } = GameMode.Mission;
        public MissionStats Stats { get; private set; } = new MissionStats();
        public int TrainHits, TrainHeads, TrainShots, TeamKills;
        public Player Player { get; private set; }
        public Level Level { get; private set; }
        public Squad Squad { get; private set; }
        public Menus Menus { get; private set; }
        public Hud Hud { get; private set; }
        public BodycamFX Fx { get; private set; }
        public float MenuT;
        float endT = -1;
        bool firstContact;
        public int Best => Save.Data.career.bestScore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<Game>() != null) return;
            // on retire la caméra et la lumière par défaut d'une scène vide
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.GetComponentInParent<Game>() == null) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.GetComponentInParent<Game>() == null) l.gameObject.SetActive(false);
            new GameObject("Entrepot7").AddComponent<Game>();
        }

        void Awake()
        {
            I = this;
            Save.Load();
            ApplyQuality();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.038f;
            RenderSettings.fogColor = new Color(0.012f, 0.012f, 0.016f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.6f, 0.66f) * 0.26f;
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.3f) * 0.2f;
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.09f, 0.07f) * 0.26f;
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            AudioSys.Create(transform);
            FX.Create(transform);
            Level = Level.Build(transform);
            Squad = Squad.Create(transform);
            Player = Player.Create(transform);
            Fx = BodycamFX.Create(transform);
            Fx.world = Player.Cam; Fx.weapons = Player.VmCam;
            Hud = Hud.Create(transform);
            Menus = Menus.Create(transform);
            ToMenu();
        }

        public void ApplyQuality()
        {
            var q = Save.Data.settings.quality;
            QualitySettings.pixelLightCount = q == "haut" ? 8 : q == "moyen" ? 6 : 3;
            QualitySettings.shadows = q == "bas" ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = q == "haut" ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowDistance = q == "haut" ? 40 : 28;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 1;
        }

        // ---------------- états ----------------
        public void ToMenu()
        {
            if (Mode == GameMode.Training && State != GameState.Loading) Save.RecordTraining(TrainHits, TrainHeads, TrainShots);
            if (Mode == GameMode.Arena) MLHooks.StopArena?.Invoke();
            State = GameState.Menu;
            Mode = GameMode.Mission;
            Squad.Clear();
            FX.I.ClearAll();
            Level.SetMode(false);
            Player.Weapons.SetHandsVisible(false);
            Player.Weapons.previewMode = false;
            GameInput.LockCursor(false);
            Menus.Show("menu");
            AudioSys.I.MusicLevel(1);
        }

        public void StartGame(GameMode m)
        {
            Mode = m;
            bool training = m == GameMode.Training;
            Stats = new MissionStats();
            TrainHits = TrainHeads = TrainShots = TeamKills = 0;
            endT = -1; firstContact = false;
            FX.I.ClearAll();
            Level.SetMode(training);
            Player.EnableController();
            Player.Weapons.EndPreview();
            Vector3 spawn = training ? Level.TrainingSpawn : Level.PlayerSpawn;
            float yaw;
            if (training) yaw = 90f;
            else { var d = new Vector3(7.5f * Level.T, 0, 3.5f * Level.T) - spawn; yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            Player.Spawn(spawn, yaw, training);
            Squad.Spawn(m);
            State = GameState.Playing;
            Menus.Show(null);
            GameInput.LockCursor(true);
            AudioSys.I.MusicLevel(0);
            if (m == GameMode.Mission) RadioLater(0.7f, "Centrale à " + Save.Data.character.unit + ". Sept individus armés signalés dans l’entrepôt. Soyez prudent.", "CENTRALE", new[] { "c_a", "#", "c_start" });
            else { Hud.Subtitle("INSTRUCTEUR", "Stand de tir. Munitions illimitées. Échap puis Arsenal pour changer d’arme ou d’accessoire.", ""); StartCoroutine(Later(0.5f, () => AudioSys.I.Instructor())); }
            if (m == GameMode.Mission && Save.Data.settings.teammates > 0)
                StartCoroutine(Later(4.5f, () => Hud.Subtitle("BRAVO", "Équipe en place. T : avec moi / tenez la position · Y : allez là où je vise.", "mate")));
        }

        /// <summary>Arène d'entraînement de l'IA (ML-Agents) : le joueur regarde, les suspects-agents apprennent contre les policiers IA.</summary>
        public void StartArena()
        {
            if (!MLHooks.Available || MLHooks.StartArena == null) return;
            Mode = GameMode.Arena;
            Stats = new MissionStats();
            FX.I.ClearAll();
            Level.SetMode(false);
            Squad.Clear();
            Player.Weapons.EndPreview();
            Player.Weapons.SetHandsVisible(false);
            State = GameState.Playing;
            Menus.Show(null);
            GameInput.LockCursor(false);
            AudioSys.I.MusicLevel(0);
            Hud.Subtitle("ARÈNE", "Entraînement de l’IA en cours. Échap pour quitter.", "");
            MLHooks.StartArena();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0;
            GameInput.LockCursor(false);
            Menus.Show("pause");
            AudioSys.I.MusicLevel(0.35f);
            AudioListener.pause = true;
        }

        public void Resume()
        {
            State = GameState.Playing;
            Time.timeScale = 1;
            AudioListener.pause = false;
            Player.Weapons.EndPreview();
            Menus.Show(null);
            GameInput.LockCursor(true);
            AudioSys.I.MusicLevel(0);
        }

        public void LeaveToMenu() { Time.timeScale = 1; AudioListener.pause = false; ToMenu(); }

        public void OnPlayerDead()
        {
            State = GameState.Dead;
            RadioLater(0.9f, "Agent à terre ! Agent à terre ! Envoyez les secours pour le " + Save.Data.character.unit + " !", "CENTRALE", new[] { "c_down", "#" });
            StartCoroutine(Later(3.2f, () => Finish(false)));
        }

        public void CheckEnd()
        {
            if (Mode == GameMode.Mission && Squad.AllClear && endT < 0 && State == GameState.Playing) endT = 2.2f;
        }

        public static int ComputeScore(bool won, MissionStats s)
        {
            float acc = s.shots > 0 ? s.hits / (float)s.shots : 0;
            float sc = s.arrests * 250 + s.kills * 100 + s.heads * 25 - s.unjust * 400 - Mathf.Round(s.dmg * 1.5f);
            if (won) sc += 300 + Mathf.Round(acc * 300) + Mathf.Max(0, Mathf.Round(600 - s.time * 2));
            return Mathf.Max(0, Mathf.RoundToInt(sc));
        }

        public static string Grade(int score, bool won) => !won ? "F" : score >= 2400 ? "S" : score >= 1900 ? "A" : score >= 1400 ? "B" : score >= 900 ? "C" : "D";

        void Finish(bool won)
        {
            if (State == GameState.End) return;
            State = GameState.End;
            GameInput.LockCursor(false);
            int score = ComputeScore(won, Stats);
            string g = Grade(score, won);
            Save.RecordMission(won, score, g, Stats, Save.Data.loadout.primary);
            Menus.ShowEnd(won, score, g, Stats);
            AudioSys.I.MusicLevel(0.7f);
        }

        void Update()
        {
            if (State == GameState.Menu || State == GameState.End)
            {
                MenuT += Time.unscaledDeltaTime;
                bool arsenal = Menus.Current == "arsenal";
                bool perso = Menus.Current == "perso";
                Player.MenuCamera(MenuT, perso, Menus.OfficerPos);
                if (arsenal) Player.Weapons.Preview(Menus.ArsenalView, MenuT);
                else if (Player.Weapons.previewMode) { Player.Weapons.previewMode = false; Player.Weapons.SetHandsVisible(false); }
            }
            if (State == GameState.Paused && Menus.Current == "arsenal") Player.Weapons.Preview(Menus.ArsenalView, Time.unscaledTime);
            if (GameInput.Pause)
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Menus.Back();
                else if (State == GameState.Menu) Menus.Back();
            }
            if (State == GameState.Playing && Mode == GameMode.Arena)
            {
                // spectateur : vue d'ensemble du grand hall
                Player.MenuCamera(Time.time * 0.6f, false, Vector3.zero);
                return;
            }
            if (State == GameState.Playing)
            {
                Stats.time += Time.deltaTime;
                if (Mode == GameMode.Mission && !firstContact && Squad.suspects.Exists(s => s.state == SuspectAI.S.Combat))
                {
                    firstContact = true;
                    RadioLater(0.4f, Save.Data.character.unit + " à Centrale, contact ! Suspects armés !", Save.Data.character.unit, new[] { "#", "p_m_contact" });
                }
                if (endT > 0)
                {
                    endT -= Time.deltaTime;
                    if (endT <= 0)
                    {
                        Radio("Centrale, ici " + Save.Data.character.unit + ". Zone sécurisée. Envoyez une équipe.", Save.Data.character.unit, new[] { "p_m_ici", "#", "p_m_clear" });
                        Finish(true);
                    }
                }
                // si la fenêtre perd le curseur, on se met en pause
                if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused && Input.GetMouseButtonDown(0)) GameInput.LockCursor(true);
            }
        }

        // dans l'éditeur, cliquer sur la Console ou la Scène fait perdre le focus : on ne met en pause que dans le jeu compilé
        void OnApplicationFocus(bool focus)
        {
#if !UNITY_EDITOR
            if (!focus && State == GameState.Playing) Pause();
#endif
        }

        // ---------------- radio ----------------
        public void Radio(string text, string who, string[] parts)
        {
            Hud.Subtitle(who, text, who == "CENTRALE" ? "" : "me");
            AudioSys.I.Radio(parts, who == "CENTRALE" ? "c" : "p", Save.Data.character.unit ?? "4471");
        }

        public void RadioLater(float delay, string text, string who, string[] parts) => StartCoroutine(Later(delay, () => Radio(text, who == "me" ? Save.Data.character.unit : who, parts)));

        public static IEnumerator Later(float t, System.Action a) { yield return new WaitForSecondsRealtime(t); a(); }
    }
}
