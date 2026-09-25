using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>Réglages de difficulté (mêmes valeurs que la version web).</summary>
    public class Difficulty
    {
        public float acc, dmgMin, dmgMax, react, cool, surrender; public bool regen; public int fireTokens;
        public static Difficulty Get(string d)
        {
            switch (d)
            {
                case "facile": return new Difficulty { acc = 0.15f, dmgMin = 6, dmgMax = 10, react = 0.95f, cool = 1.5f, regen = true, surrender = 0.15f, fireTokens = 1 };
                case "realiste": return new Difficulty { acc = 0.4f, dmgMin = 22, dmgMax = 34, react = 0.38f, cool = 0.8f, regen = false, surrender = -0.1f, fireTokens = 3 };
                default: return new Difficulty { acc = 0.26f, dmgMin = 10, dmgMax = 16, react = 0.6f, cool = 1.05f, regen = false, surrender = 0f, fireTokens = 2 };
            }
        }
    }

    /// <summary>Arme d'un suspect.</summary>
    public class EnemyWeapon
    {
        public int burstMin, burstMax, ammo; public float gap, dmg, acc, range; public string snd;
        public static EnemyWeapon Get(string t)
        {
            switch (t)
            {
                case "shotgun": return new EnemyWeapon { burstMin = 1, burstMax = 1, gap = 0.9f, dmg = 1.7f, acc = 1.15f, ammo = 6, snd = "shotgun", range = 12 };
                case "pistol": return new EnemyWeapon { burstMin = 1, burstMax = 3, gap = 0.3f, dmg = 0.75f, acc = 0.9f, ammo = 15, snd = "pistol" };
                default: return new EnemyWeapon { burstMin = 2, burstMax = 4, gap = 0.11f, dmg = 1f, acc = 1f, ammo = 30, snd = "rifle" };
            }
        }
    }

    /// <summary>Cible pour un suspect : le joueur ou un coéquipier.</summary>
    public class Threat
    {
        public Player player; public TeammateAI mate;
        public bool Alive => player != null ? player.Alive : mate != null && mate.Alive;
        public Vector3 Pos => player != null ? player.transform.position : mate.transform.position;
        public Vector3 Eye => player != null ? player.Eye : mate.EyePos;
        public bool Moving => player != null ? player.Moving : mate.IsMoving;
        public bool Sprinting => player != null && player.Sprinting;
        public float Crouch => player != null ? player.Crouch : mate.body.crouch;
        public bool LampOn => player == null || player.Weapons.LampOn;
        public void Hurt(float dmg, Vector3 from) { if (player != null) player.Hurt(dmg, from); else mate.Hurt(dmg, from); }
    }

    /// <summary>
    /// Chef d'orchestre de l'IA : liste des suspects et des coéquipiers, sons entendus, cris, grenades,
    /// jetons de tir (pas plus de N suspects qui tirent en même temps sur l'équipe), flammes de tir des ennemis.
    /// </summary>
    public class Squad : MonoBehaviour
    {
        public static Squad I { get; private set; }
        public readonly List<SuspectAI> suspects = new List<SuspectAI>();
        public readonly List<TeammateAI> mates = new List<TeammateAI>();
        public Difficulty diff = Difficulty.Get("normal");
        readonly HashSet<SuspectAI> tokens = new HashSet<SuspectAI>();
        readonly List<(Light l, Transform q, float t)> flashes = new List<(Light, Transform, float)>();
        int flashI;
        public static readonly string[] BotWeapons = { "rifle", "pistol", "rifle", "shotgun", "rifle", "pistol", "shotgun" };

        public static Squad Create(Transform parent)
        {
            var go = new GameObject("Squad");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Squad>();
        }

        void Awake()
        {
            I = this;
            var flashMat = E7Assets.UnlitMat(new Color(3f, 2.4f, 1.6f, 1f), E7Assets.Tex("flash"), true);
            for (int i = 0; i < 4; i++)
            {
                var l = U.Child(transform, "flammeEnnemi").AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.69f, 0.38f); l.range = 12; l.intensity = 0; l.shadows = LightShadows.None;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(transform, false); q.GetComponent<MeshRenderer>().sharedMaterial = flashMat; q.SetActive(false);
                flashes.Add((l, q.transform, 0));
            }
        }

        public void Spawn(GameMode mode)
        {
            Clear();
            diff = Difficulty.Get(Save.Data.settings.diff);
            if (mode == GameMode.Mission)
            {
                var spawns = Level.I.EnemySpawns;
                for (int i = 0; i < spawns.Count; i++) suspects.Add(SuspectAI.Create(transform, spawns[i], i, BotWeapons[i % BotWeapons.Length]));
                int n = Mathf.Clamp(Save.Data.settings.teammates, 0, 3);
                for (int i = 0; i < n; i++)
                {
                    // la salle de départ s'étend vers +x et +z depuis le point d'entrée
                    var p = Level.I.PlayerSpawn + new Vector3(1.3f * (i + 1), 0, i % 2 == 0 ? 1.0f : -0.6f);
                    mates.Add(TeammateAI.Create(transform, p, i));
                }
            }
        }

        public void Clear()
        {
            foreach (var s in suspects) if (s) Destroy(s.gameObject);
            foreach (var m in mates) if (m) Destroy(m.gameObject);
            suspects.Clear(); mates.Clear(); tokens.Clear();
        }

        void Update()
        {
            for (int i = 0; i < flashes.Count; i++)
            {
                var f = flashes[i];
                if (f.t <= 0) continue;
                f.t -= Time.deltaTime;
                f.l.intensity = f.t > 0 ? f.l.intensity : 0;
                f.q.gameObject.SetActive(f.t > 0);
                if (f.t > 0 && Camera.main) f.q.rotation = Camera.main.transform.rotation * Quaternion.Euler(0, 0, Random.Range(0, 360f));
                flashes[i] = f;
            }
        }

        public void MuzzleFlash(Vector3 pos, string type)
        {
            int i = flashI; flashI = (flashI + 1) % flashes.Count;
            var f = flashes[i];
            f.l.transform.position = pos; f.l.intensity = type == "shotgun" ? 3.5f : 2.5f; f.t = 0.05f;
            f.q.position = pos; f.q.localScale = Vector3.one * (type == "pistol" ? 0.3f : type == "shotgun" ? 0.65f : 0.5f);
            f.q.gameObject.SetActive(true);
            flashes[i] = f;
        }

        // ---------------- jetons de tir ----------------
        public bool RequestToken(SuspectAI s)
        {
            if (tokens.Contains(s)) return true;
            tokens.RemoveWhere(x => x == null || !x.Hostile || x.state != SuspectAI.S.Combat);
            if (tokens.Count >= diff.fireTokens) return false;
            tokens.Add(s); return true;
        }
        public void ReleaseToken(SuspectAI s) { tokens.Remove(s); }

        // ---------------- cibles pour les suspects ----------------
        public IEnumerable<Threat> Threats()
        {
            if (Player.I != null && Player.I.Alive) yield return new Threat { player = Player.I };
            foreach (var m in mates) if (m != null && m.Alive) yield return new Threat { mate = m };
        }

        // ---------------- événements (appelés par le joueur, les armes, les grenades) ----------------
        public static void Hear(Vector3 pos, float radius, bool loud)
        {
            if (I == null) return;
            foreach (var s in I.suspects) if (s.Hostile && Vector3.Distance(s.Pos, pos) < radius) s.Hear(pos, loud);
        }

        public static void BulletNear(Vector3 impact, Vector3 shooter)
        {
            if (I == null) return;
            foreach (var s in I.suspects)
            {
                if (!s.Hostile) continue;
                float d = Vector3.Distance(s.Pos, impact);
                if (d < 3f) { s.Suppress(1.2f - d * 0.3f, shooter); }
            }
        }

        /// <summary>« Police ! » : chaque suspect proche peut se rendre (selon son état et ses blessures).</summary>
        public static void Shout(Vector3 from, Vector3 eye)
        {
            if (I == null) return;
            I.StartCoroutine(I.ShoutRoutine(from, eye));
        }

        System.Collections.IEnumerator ShoutRoutine(Vector3 from, Vector3 eye)
        {
            yield return new WaitForSeconds(0.7f);
            foreach (var s in suspects)
            {
                if (!s.Hostile) continue;
                float d = Vector3.Distance(s.Pos, from);
                if (d > 14) continue;
                if (d >= 8 && Physics.Linecast(s.EyePos, eye, L.SightMask, QueryTriggerInteraction.Ignore)) continue;
                s.OnShout(from);
            }
        }

        public static void Flashbang(Vector3 pos)
        {
            if (I == null) return;
            foreach (var s in I.suspects)
            {
                if (!s.Hostile) continue;
                float bd = Vector3.Distance(s.Pos, pos);
                if (bd < 9.5f && !Physics.Linecast(s.EyePos, new Vector3(pos.x, 0.3f, pos.z), L.SightMask, QueryTriggerInteraction.Ignore)) s.Stun(Mathf.Clamp(6.5f - bd * 0.45f, 2, 6));
                else if (bd < 30) s.Hear(pos, true);
            }
        }

        public static SuspectAI NearestSurrendered(Vector3 p, float maxDist)
        {
            if (I == null) return null;
            SuspectAI best = null; float bd = maxDist;
            foreach (var s in I.suspects) if (s.state == SuspectAI.S.Surrender) { float d = Vector3.Distance(s.Pos, p); if (d < bd) { bd = d; best = s; } }
            return best;
        }

        /// <summary>Un suspect a repéré quelqu'un : il prévient ceux qui sont proches.</summary>
        public static void Alert(SuspectAI from, Vector3 where)
        {
            foreach (var o in I.suspects) if (o != from && o.Hostile && Vector3.Distance(o.Pos, from.Pos) < 15) o.Hear(where, true);
        }

        public static bool AllClear => I != null && I.suspects.TrueForAll(s => !s.Hostile);

        // ---------------- ordres aux coéquipiers ----------------
        public static void TeamToggleFollow()
        {
            if (I == null || I.mates.Count == 0) return;
            bool anyFollow = I.mates.Exists(m => m.Alive && m.order == TeammateAI.Order.Follow);
            foreach (var m in I.mates) if (m.Alive) { if (anyFollow) m.Hold(m.transform.position); else m.Follow(); }
            Hud.Subtitle("TOI", anyFollow ? "Tenez la position !" : "Avec moi !", "me");
            AudioSys.I.Sfx("squelch", 0.6f);
        }

        public static void TeamMoveTo(Vector3 p)
        {
            if (I == null || I.mates.Count == 0) return;
            int i = 0;
            foreach (var m in I.mates) if (m.Alive) { m.Hold(p + new Vector3((i % 2 == 0 ? -0.8f : 0.8f), 0, (i / 2) * 0.8f)); i++; }
            Hud.Subtitle("TOI", "Allez là-bas, couvrez la zone !", "me");
            AudioSys.I.Sfx("squelch", 0.6f);
        }

        public static bool IsTeammate(Actor a) => a is TeammateAI;
    }
}
