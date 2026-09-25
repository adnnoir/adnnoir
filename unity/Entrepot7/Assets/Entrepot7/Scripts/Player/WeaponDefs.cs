using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>Caractéristiques d'une arme (mêmes valeurs que la version web).</summary>
    public class WeaponDef
    {
        public string key, slot, name, shortName, cal, desc, sound;
        public int mag, reserve, pellets = 1;
        public float interval, pelletSpread, spreadHip, spreadAds, kick, kickYaw, hear, flash;
        public bool auto, pump;
        public float dmgHead, dmgTorso, dmgLimb;
        public string[] optics, muzzles, rails;
        public KeyValuePair<string, float>[] stats;

        public static readonly Dictionary<string, WeaponDef> All = new Dictionary<string, WeaponDef>
        {
            ["carbine"] = new WeaponDef
            {
                key = "carbine", slot = "primary", name = "Carabine CT-4", shortName = "CT-4", cal = "5,56 × 45 mm · 30 coups",
                desc = "Carabine de dotation. Précise à toutes les distances, recul modéré, tir en rafale ou coup par coup.",
                mag = 30, reserve = 120, interval = 0.08f, auto = true, dmgHead = 150, dmgTorso = 38, dmgLimb = 22,
                spreadHip = 0.016f, spreadAds = 0.0015f, kick = 0.013f, kickYaw = 0.006f, hear = 32, sound = "rifle", flash = 0.17f,
                optics = new[] { "reddot", "holo", "irons" }, muzzles = new[] { "std", "suppressor" }, rails = new[] { "light", "laser" },
                stats = S(("Dégâts", 0.72f), ("Cadence", 0.72f), ("Précision", 0.88f), ("Contrôle", 0.55f), ("Maniabilité", 0.58f), ("Capacité", 0.7f)),
            },
            ["smg"] = new WeaponDef
            {
                key = "smg", slot = "primary", name = "Pistolet-mitrailleur MP9", shortName = "MP9", cal = "9 × 19 mm · 30 coups",
                desc = "Compact et très maniable. Idéal en intérieur, moins efficace de loin.",
                mag = 30, reserve = 150, interval = 0.066f, auto = true, dmgHead = 120, dmgTorso = 27, dmgLimb = 17,
                spreadHip = 0.013f, spreadAds = 0.0028f, kick = 0.0075f, kickYaw = 0.005f, hear = 26, sound = "smg", flash = 0.12f,
                optics = new[] { "reddot", "holo", "irons" }, muzzles = new[] { "std", "suppressor" }, rails = new[] { "light", "laser" },
                stats = S(("Dégâts", 0.5f), ("Cadence", 0.92f), ("Précision", 0.66f), ("Contrôle", 0.82f), ("Maniabilité", 0.9f), ("Capacité", 0.7f)),
            },
            ["shotgun"] = new WeaponDef
            {
                key = "shotgun", slot = "primary", name = "Fusil à pompe SG-12", shortName = "SG-12", cal = "Calibre 12 · 7 cartouches",
                desc = "Dévastateur à courte portée. Il faut réarmer à la pompe entre chaque tir et recharger cartouche par cartouche.",
                mag = 7, reserve = 28, interval = 0.12f, auto = false, pump = true, pellets = 9, pelletSpread = 0.045f, dmgHead = 45, dmgTorso = 24, dmgLimb = 14,
                spreadHip = 0.006f, spreadAds = 0.002f, kick = 0.055f, kickYaw = 0.012f, hear = 42, sound = "shotgun", flash = 0.26f,
                optics = new[] { "irons", "reddot", "holo" }, muzzles = new[] { "std" }, rails = new[] { "light", "laser" },
                stats = S(("Dégâts", 1f), ("Cadence", 0.15f), ("Précision", 0.35f), ("Contrôle", 0.25f), ("Maniabilité", 0.5f), ("Capacité", 0.25f)),
            },
            ["pistol"] = new WeaponDef
            {
                key = "pistol", slot = "secondary", name = "Pistolet P17", shortName = "P17", cal = "9 × 19 mm · 17 coups",
                desc = "Arme de poing réglementaire. Se dégaine vite, parfaite quand la carabine est vide.",
                mag = 17, reserve = 68, interval = 0.11f, auto = false, dmgHead = 120, dmgTorso = 30, dmgLimb = 18,
                spreadHip = 0.012f, spreadAds = 0.0035f, kick = 0.022f, kickYaw = 0.008f, hear = 28, sound = "pistol", flash = 0.1f,
                optics = new[] { "irons", "reddot" }, muzzles = new[] { "std", "suppressor" }, rails = new[] { "light", "laser" },
                stats = S(("Dégâts", 0.5f), ("Cadence", 0.5f), ("Précision", 0.7f), ("Contrôle", 0.6f), ("Maniabilité", 1f), ("Capacité", 0.4f)),
            },
        };

        static KeyValuePair<string, float>[] S(params (string, float)[] v)
        {
            var r = new KeyValuePair<string, float>[v.Length];
            for (int i = 0; i < v.Length; i++) r[i] = new KeyValuePair<string, float>(v[i].Item1, v[i].Item2);
            return r;
        }

        public static readonly Dictionary<string, string> AttLabel = new Dictionary<string, string>
        {
            { "reddot", "Point rouge" }, { "holo", "Holographique" }, { "irons", "Mire métallique" }, { "std", "Standard" },
            { "suppressor", "Silencieux" }, { "light", "Lampe" }, { "laser", "Lampe + laser" },
        };
    }

    /// <summary>Animation de rechargement : images clés de la main gauche + événements sonores.</summary>
    public class ReloadAnim
    {
        public float dur;
        public (float t, string pose)[] frames;
        public float holdA, holdB, hideA, hideB;
        public (float t, string ev)[] events;
        public float chargeA = -1, chargeB = -1, release = -1;

        public static (ReloadAnim tactical, ReloadAnim empty) Mag(float s)
        {
            (float, string)[] F(params (float, string)[] a) { var r = new (float, string)[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = (a[i].Item1 * s, a[i].Item2); return r; }
            var t = new ReloadAnim
            {
                dur = 2.0f * s, frames = F((0, "guard"), (0.28f, "mag"), (0.55f, "pouch"), (0.85f, "pouch"), (1.22f, "mag"), (1.75f, "guard")),
                holdA = 0.3f * s, holdB = 1.3f * s, hideA = 0.55f * s, hideB = 0.72f * s,
                events = F((0.3f, "magOut"), (0.62f, "pouch"), (1.28f, "magIn")),
            };
            var e = new ReloadAnim
            {
                dur = 2.45f * s, frames = F((0, "guard"), (0.28f, "mag"), (0.55f, "pouch"), (0.85f, "pouch"), (1.22f, "mag"), (1.5f, "charge"), (1.78f, "charge"), (2.2f, "guard")),
                holdA = 0.3f * s, holdB = 1.3f * s, hideA = 0.55f * s, hideB = 0.72f * s,
                events = F((0.3f, "magOut"), (0.62f, "pouch"), (1.28f, "magIn"), (1.74f, "charge")), chargeA = 1.55f * s, chargeB = 1.8f * s,
            };
            return (t, e);
        }

        public static (ReloadAnim tactical, ReloadAnim empty) Pistol()
        {
            var t = new ReloadAnim
            {
                dur = 1.45f, frames = new[] { (0f, "guard"), (0.18f, "mag"), (0.4f, "pouch"), (0.6f, "pouch"), (0.88f, "mag"), (1.2f, "guard") },
                holdA = 0.2f, holdB = 0.95f, hideA = 0.4f, hideB = 0.5f,
                events = new[] { (0.2f, "pMagOut"), (0.45f, "pouch"), (0.92f, "pMagIn") },
            };
            var e = new ReloadAnim
            {
                dur = 1.85f, frames = new[] { (0f, "guard"), (0.18f, "mag"), (0.4f, "pouch"), (0.6f, "pouch"), (0.88f, "mag"), (1.15f, "charge"), (1.38f, "charge"), (1.65f, "guard") },
                holdA = 0.2f, holdB = 0.95f, hideA = 0.4f, hideB = 0.5f,
                events = new[] { (0.2f, "pMagOut"), (0.45f, "pouch"), (0.92f, "pMagIn"), (1.3f, "slideRel") }, release = 1.3f,
            };
            return (t, e);
        }
    }

    /// <summary>Données d'une arme exportées de la version web (positions de visée, mains, bouche…).</summary>
    public class WeaponMeta
    {
        public Vector3 hip, magGrab;
        public Vector3 gripPos; public Quaternion gripRot;
        public readonly Dictionary<string, (Vector3 pos, Quaternion rot)> poses = new Dictionary<string, (Vector3, Quaternion)>();
        public readonly Dictionary<string, (Vector3 sight, float adsDist)> optics = new Dictionary<string, (Vector3, float)>();
        public readonly Dictionary<string, Vector3> muzzles = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> lasers = new Dictionary<string, Vector3>();
        public float handleTravel, pumpTravel, slideTravel, previewDist = 1.2f, previewCenter;

        static Vector3 V(object o) { var l = o as List<object>; return l != null && l.Count == 3 ? new Vector3(MiniJson.F(l[0]), MiniJson.F(l[1]), MiniJson.F(l[2])) : Vector3.zero; }
        static Quaternion Q(object o) { var l = o as List<object>; return l != null && l.Count == 4 ? new Quaternion(MiniJson.F(l[0]), MiniJson.F(l[1]), MiniJson.F(l[2]), MiniJson.F(l[3])) : Quaternion.identity; }

        public static WeaponMeta Load(string key)
        {
            var j = E7Assets.Json("Models/w_" + key) as Dictionary<string, object>;
            var m = new WeaponMeta();
            if (j == null) return m;
            m.hip = V(j["hip"]);
            var grip = MiniJson.Obj(j, "grip"); m.gripPos = V(grip["pos"]); m.gripRot = Q(grip["rot"]);
            foreach (var kv in MiniJson.Obj(j, "poses")) { var p = kv.Value as Dictionary<string, object>; m.poses[kv.Key] = (V(p["pos"]), Q(p["rot"])); }
            m.magGrab = j.ContainsKey("magGrab") ? V(j["magGrab"]) : Vector3.zero;
            m.handleTravel = MiniJson.Num(j, "handleTravel"); m.pumpTravel = MiniJson.Num(j, "pumpTravel"); m.slideTravel = MiniJson.Num(j, "slideTravel");
            var pv = MiniJson.Obj(j, "preview"); if (pv != null) { m.previewDist = MiniJson.Num(pv, "dist", 1.2f); m.previewCenter = MiniJson.Num(pv, "center"); }
            var opt = MiniJson.Obj(j, "options");
            foreach (var kv in MiniJson.Obj(opt, "optic")) { var o = kv.Value as Dictionary<string, object>; m.optics[kv.Key] = (V(o["sight"]), MiniJson.Num(o, "adsDist", 0.2f)); }
            foreach (var kv in MiniJson.Obj(opt, "muzzle")) m.muzzles[kv.Key] = V((kv.Value as Dictionary<string, object>)["muzzle"]);
            foreach (var kv in MiniJson.Obj(opt, "rail")) { var o = kv.Value as Dictionary<string, object>; if (o.TryGetValue("laser", out var lz) && lz != null) m.lasers[kv.Key] = V(lz); }
            return m;
        }
    }
}
