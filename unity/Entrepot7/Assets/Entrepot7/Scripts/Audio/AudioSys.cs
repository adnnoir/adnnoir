using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>
    /// Tout le son du jeu : vrais coups de feu (enregistrés), voix (Piper), bruitages synthétisés,
    /// sons 3D étouffés derrière les murs, radio (effet talkie-walkie), ambiance et musique.
    /// </summary>
    public class AudioSys : MonoBehaviour
    {
        public static AudioSys I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        Dictionary<string, AudioClip> bank;
        readonly List<AudioSource> pool2D = new List<AudioSource>();
        readonly List<AudioSource> pool3D = new List<AudioSource>();
        readonly List<AudioSource> radioPool = new List<AudioSource>();
        AudioSource ambRain, ambRoom, ambHum, music, tinnitus, uiSrc;
        AudioLowPassFilter muffle;
        float muffleT, muffleDur = 1f, muffleFrom = 18000f;
        int next2D, next3D, nextRadio;
        float ambTimer = 2f;

        static readonly Dictionary<string, string[]> Shots = new Dictionary<string, string[]>
        {
            { "rifle", new[] { "carbine_1", "carbine_2" } }, { "smg", new[] { "smg_1", "smg_2" } },
            { "pistol", new[] { "pistol_1", "pistol_2" } }, { "shotgun", new[] { "shotgun_1", "shotgun_2" } },
            { "e_rifle", new[] { "e_rifle_1", "e_rifle_2", "e_rifle_3" } }, { "e_pistol", new[] { "e_pistol_1", "e_pistol_2" } },
            { "e_shotgun", new[] { "e_shotgun_1", "e_shotgun_2" } },
        };

        // répliques des suspects (texte → identifiant du fichier vo/sX_id)
        public static readonly Dictionary<string, string> SuspectLines = new Dictionary<string, string>
        {
            { "p1", "T’as entendu ?" }, { "p2", "C’est quoi ce bruit ?" }, { "p3", "Y’a quelqu’un ?" },
            { "s1", "Il est là !" }, { "s2", "Les flics !" }, { "s3", "Là-bas !" }, { "s4", "Contact !" },
            { "b1", "Aaah ! Mes yeux !" }, { "b2", "J’y vois plus rien !" }, { "b3", "Ça brûle !" },
            { "h1", "Je suis touché !" }, { "h2", "Aïe ! Putain…" }, { "h3", "Il m’a eu !" },
            { "u1", "D’accord, d’accord ! Je me rends !" }, { "u2", "Tirez pas ! Tirez pas !" }, { "u3", "C’est bon, je lâche mon arme !" },
            { "r1", "Je recharge !" },
        };

        public static AudioSys Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioSys>();
        }

        void Awake()
        {
            I = this;
            bank = SoundSynth.BuildBank();
            for (int i = 0; i < 16; i++) pool2D.Add(MakeSource("2D", false));
            for (int i = 0; i < 28; i++) pool3D.Add(MakeSource("3D", true));
            for (int i = 0; i < 12; i++)
            {
                var s = MakeSource("radio", false);
                var hp = s.gameObject.AddComponent<AudioHighPassFilter>(); hp.cutoffFrequency = 380;
                var lp = s.gameObject.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 2900;
                var ds = s.gameObject.AddComponent<AudioDistortionFilter>(); ds.distortionLevel = 0.55f;
                radioPool.Add(s);
            }
            ambRain = Loop("rain", 0.035f); ambRoom = Loop("room", 0.05f); ambHum = Loop("hum", 0f);
            music = Loop("music", 0f); music.ignoreListenerPause = true;
            uiSrc = MakeSource("ui", false); uiSrc.ignoreListenerPause = true;
            tinnitus = MakeSource("tinnitus", false); tinnitus.clip = Bank("tinnitus"); tinnitus.loop = true; tinnitus.volume = 0;
        }

        AudioSource MakeSource(string n, bool spatial)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 2.5f; s.maxDistance = 90f;
            s.dopplerLevel = 0f;
            if (spatial) { var lp = go.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 22000; lp.enabled = false; }
            return s;
        }

        AudioSource Loop(string clip, float vol)
        {
            var s = MakeSource("loop_" + clip, false);
            s.clip = Bank(clip); s.loop = true; s.volume = vol; s.Play();
            return s;
        }

        static Settings St => Save.Data.settings;

        public AudioClip Clip(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>("E7/Audio/" + path);
            clips[path] = c;
            return c;
        }

        public AudioClip Bank(string name) => bank != null && bank.TryGetValue(name, out var c) ? c : null;

        /// <summary>L'écouteur (la caméra) : on y met le filtre « oreilles bouchées ».</summary>
        public void AttachListener(AudioListener l)
        {
            muffle = l.GetComponent<AudioLowPassFilter>();
            if (!muffle) muffle = l.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 22000;
        }

        void Update()
        {
            AudioListener.volume = St.vMaster;
            float amb = St.vAmb;
            ambRain.volume = 0.18f * amb; ambRoom.volume = 0.12f * amb;
            music.volume = Mathf.MoveTowards(music.volume, musicTarget * 0.55f * St.vMusic, Time.unscaledDeltaTime * 0.3f);
            if (muffle)
            {
                if (muffleT < muffleDur) { muffleT += Time.deltaTime; muffle.cutoffFrequency = Mathf.Lerp(muffleFrom, 22000f, Mathf.Pow(Mathf.Clamp01(muffleT / muffleDur), 2f)); }
                else muffle.cutoffFrequency = 22000f;
            }
            tinnitus.volume = Mathf.MoveTowards(tinnitus.volume, 0, Time.deltaTime * 0.03f);
            if (tinnitus.volume <= 0 && tinnitus.isPlaying) tinnitus.Stop();
            // bruits d'ambiance aléatoires pendant la partie
            if (Game.I != null && Game.I.State == GameState.Playing)
            {
                ambTimer -= Time.deltaTime;
                if (ambTimer <= 0)
                {
                    ambTimer = Random.Range(0.9f, 4f);
                    float r = Random.value;
                    if (r < 0.12f) Play2D(Bank("thunder"), 0.5f * amb, 1f, Random.Range(-0.5f, 0.5f));
                    else if (r < 0.2f) Play2D(Bank("siren"), 0.05f * amb, 1f, Random.Range(-0.8f, 0.8f));
                    else if (Level.I != null) Play3D(Bank("drip"), Level.I.RandomFloorPoint() + Vector3.up * 0.1f, 0.4f * amb, 1f, false);
                }
            }
        }

        float musicTarget;
        public void MusicLevel(float v) { musicTarget = v; if (!music.isPlaying) music.Play(); }
        public void Hum(float v) { ambHum.volume = Mathf.Clamp(v, 0, 0.03f) * St.vAmb * 4f; }

        // ---------------- lecture ----------------
        public AudioSource Play2D(AudioClip c, float vol = 1f, float pitch = 1f, float pan = 0f)
        {
            if (!c) return null;
            var s = pool2D[next2D]; next2D = (next2D + 1) % pool2D.Count;
            s.clip = c; s.volume = vol; s.pitch = pitch; s.panStereo = pan; s.Play();
            return s;
        }

        public AudioSource Play3D(AudioClip c, Vector3 pos, float vol = 1f, float pitch = 1f, bool? occluded = null, float delay = 0f)
        {
            if (!c) return null;
            var s = pool3D[next3D]; next3D = (next3D + 1) % pool3D.Count;
            s.transform.position = pos;
            s.clip = c; s.volume = vol; s.pitch = pitch;
            bool occ = occluded ?? IsOccluded(pos);
            var lp = s.GetComponent<AudioLowPassFilter>();
            lp.enabled = occ; lp.cutoffFrequency = 700;
            if (delay > 0) s.PlayDelayed(delay); else s.Play();
            return s;
        }

        public bool IsOccluded(Vector3 pos)
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 from = cam.transform.position, to = pos;
            to.y = Mathf.Max(0.3f, to.y);
            return Physics.Linecast(from, to, L.SightMask, QueryTriggerInteraction.Ignore);
        }

        public void Sfx(string name, float vol = 1f, float pitch = 1f) => Play2D(Bank(name), vol * St.vSfx, pitch);
        public void Sfx3D(string name, Vector3 pos, float vol = 1f, bool? occ = null) => Play3D(Bank(name), pos, vol * St.vSfx, Random.Range(0.95f, 1.05f), occ);
        public void Ui(bool hi = false) { uiSrc.PlayOneShot(Bank(hi ? "uiHi" : "ui"), 0.7f); }

        public void Step(bool loud, bool crouch) => Play2D(Bank((loud ? "stepLoud" : "step") + Random.Range(0, 4)), (crouch ? 0.12f : loud ? 0.45f : 0.26f) * St.vSfx, Random.Range(0.92f, 1.08f), Random.Range(-0.1f, 0.1f));
        public void EnemyStep(Vector3 pos, bool loud) { if (Camera.main && Vector3.Distance(pos, Camera.main.transform.position) < 26) Play3D(Bank("step" + Random.Range(0, 4)), pos, (loud ? 0.7f : 0.45f) * St.vSfx, Random.Range(0.9f, 1.1f)); }

        /// <summary>Coup de feu du joueur (vrai enregistrement).</summary>
        public void Shot(string kind, bool suppressed)
        {
            if (!Shots.TryGetValue(kind, out var list)) list = Shots["rifle"];
            var c = Clip("sfx/" + list[Random.Range(0, list.Length)]);
            if (!c) return;
            var s = Play2D(c, (suppressed ? 0.38f : 0.95f) * St.vSfx, suppressed ? Random.Range(1.03f, 1.1f) : Random.Range(0.97f, 1.03f));
            if (suppressed) Play2D(Bank("click"), 0.25f * St.vSfx, 0.8f);
        }

        /// <summary>Coup de feu d'un suspect : arrive avec le retard du son (343 m/s), étouffé derrière un mur.</summary>
        public void EnemyShot(Vector3 pos, string kind, bool? occluded = null)
        {
            string k = kind == "shotgun" ? "e_shotgun" : kind == "pistol" ? "e_pistol" : "e_rifle";
            var list = Shots[k];
            float d = Camera.main ? Vector3.Distance(Camera.main.transform.position, pos) : 0;
            Play3D(Clip("sfx/" + list[Random.Range(0, list.Length)]), pos, 1.0f * St.vSfx, Random.Range(0.95f, 1.05f), occluded, d / 343f);
        }

        public void Muffle(float cutoff, float seconds, float ringing = 0f)
        {
            muffleFrom = cutoff; muffleDur = seconds; muffleT = 0;
            if (ringing > 0) { tinnitus.volume = ringing * St.vSfx; if (!tinnitus.isPlaying) tinnitus.Play(); }
        }

        // ---------------- voix ----------------
        public bool SuspectVoice(int voice, string lineId, Vector3 pos)
        {
            if (!St.voices) return false;
            var c = Clip("vo/s" + (voice % 4) + "_" + lineId);
            if (!c) return false;
            Play3D(c, pos, 1.3f * St.vVoice, 1f);
            return true;
        }

        public bool PlayerShout(int i)
        {
            if (!St.voices) return false;
            var c = Clip("vo/p_sh" + (i + 1));
            if (!c) return false;
            Play2D(c, 1f * St.vVoice);
            return true;
        }

        public void Instructor() { if (St.voices) Play2D(Clip("vo/i_train"), 0.9f * St.vVoice, 1f, 0.2f); }

        /// <summary>
        /// Message radio en plusieurs morceaux. "#" = numéro d'unité, prononcé chiffre par chiffre.
        /// voice : "c" (centrale) ou "p" (joueur). Renvoie la durée totale.
        /// </summary>
        public float Radio(string[] parts, string voice, string unit)
        {
            Sfx("squelch", 1f);
            if (!St.voices) return 0;
            var seq = new List<AudioClip>();
            foreach (var p in parts)
            {
                if (p == "#") { foreach (char ch in unit) if (char.IsDigit(ch)) seq.Add(Clip("vo/" + voice + "_d" + ch)); }
                else seq.Add(Clip("vo/" + p));
            }
            if (seq.Exists(x => x == null)) return 0;
            double t = AudioSettings.dspTime + 0.16;
            foreach (var c in seq)
            {
                var s = radioPool[nextRadio]; nextRadio = (nextRadio + 1) % radioPool.Count;
                s.clip = c; s.volume = 0.9f * St.vVoice; s.PlayScheduled(t);
                t += c.length + (c.name.Contains("_d") ? 0.015 : 0.07);
            }
            float total = (float)(t - AudioSettings.dspTime);
            Invoke(nameof(SquelchLater), total);
            return total;
        }

        void SquelchLater() => Sfx("squelch", 1f);
    }
}
