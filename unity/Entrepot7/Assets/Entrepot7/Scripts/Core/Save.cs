using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace E7
{
    [Serializable]
    public class Settings
    {
        public string diff = "normal";      // facile, normal, realiste
        public string quality = "moyen";    // bas, moyen, haut
        public string camFx = "fort";       // off, leger, fort
        public string keyboard = "azerty";  // azerty ou qwerty
        public float sens = 1f, adsSens = 0.7f, fov = 74f, bright = 1f;
        public bool bob = true;
        public float vMaster = 0.8f, vSfx = 1f, vAmb = 0.8f, vVoice = 0.9f, vMusic = 0.55f;
        public bool voices = true, subs = true, invertY = false, dot = true, fps = false;
        public int teammates = 2;           // coéquipiers IA (0 à 3)
    }

    [Serializable]
    public class Loadout
    {
        public string primary = "carbine";
        public List<Attach> att = new List<Attach>
        {
            new Attach { weapon = "carbine", optic = "reddot", muzzle = "std", rail = "light" },
            new Attach { weapon = "smg", optic = "holo", muzzle = "std", rail = "light" },
            new Attach { weapon = "shotgun", optic = "irons", muzzle = "std", rail = "light" },
            new Attach { weapon = "pistol", optic = "irons", muzzle = "std", rail = "light" },
        };
        public Attach For(string w) { foreach (var a in att) if (a.weapon == w) return a; var n = new Attach { weapon = w }; att.Add(n); return n; }
    }

    [Serializable]
    public class Attach { public string weapon, optic = "irons", muzzle = "std", rail = "light"; }

    [Serializable]
    public class Character
    {
        public string name = "MARTIN", unit = "4471", uniform = "police", gloves = "noir", head = "casquette";
    }

    [Serializable]
    public class MissionRecord { public long date; public bool won; public string grade; public int score, time, arrests, kills; public string weapon; }

    [Serializable]
    public class Career
    {
        public int missions, wins, arrests, kills, heads, shots, hits, unjust, time, bestScore, streak, bestStreak;
        public string bestGrade = "";
        public int trainHits, trainHeads, trainAcc;
        public List<MissionRecord> history = new List<MissionRecord>();
    }

    [Serializable]
    public class SaveFile
    {
        public int version = 1;
        public Settings settings = new Settings();
        public Loadout loadout = new Loadout();
        public Character character = new Character();
        public Career career = new Career();
    }

    /// <summary>
    /// Sauvegarde automatique dans un fichier JSON (dossier de données de l'utilisateur).
    /// Sous Windows : %USERPROFILE%\AppData\LocalLow\&lt;société&gt;\&lt;jeu&gt;\entrepot7_save.json
    /// </summary>
    public static class Save
    {
        public static SaveFile Data { get; private set; }
        public static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "entrepot7_save.json");
        static readonly Dictionary<string, int> GradeRank = new Dictionary<string, int> { { "S", 5 }, { "A", 4 }, { "B", 3 }, { "C", 2 }, { "D", 1 }, { "F", 0 } };

        public static void Load()
        {
            try
            {
                if (File.Exists(PathFile)) Data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(PathFile));
            }
            catch (Exception e) { Debug.LogWarning("[E7] Sauvegarde illisible : " + e.Message); }
            if (Data == null) Data = new SaveFile();
            if (Data.settings == null) Data.settings = new Settings();
            if (Data.loadout == null) Data.loadout = new Loadout();
            if (Data.character == null) Data.character = new Character();
            if (Data.career == null) Data.career = new Career();
            if (Data.career.history == null) Data.career.history = new List<MissionRecord>();
        }

        public static void Write()
        {
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning("[E7] Impossible d'écrire la sauvegarde : " + e.Message); }
        }

        public static void RecordMission(bool won, int score, string grade, MissionStats s, string weapon)
        {
            var c = Data.career;
            c.missions++; if (won) c.wins++;
            c.arrests += s.arrests; c.kills += s.kills; c.heads += s.heads; c.shots += s.shots; c.hits += s.hits; c.unjust += s.unjust;
            c.time += Mathf.RoundToInt(s.time);
            c.bestScore = Mathf.Max(c.bestScore, score);
            if (!GradeRank.TryGetValue(c.bestGrade ?? "", out int old)) old = -1;
            if (GradeRank.TryGetValue(grade, out int nw) && nw > old) c.bestGrade = grade;
            c.streak = won ? c.streak + 1 : 0;
            c.bestStreak = Mathf.Max(c.bestStreak, c.streak);
            c.history.Insert(0, new MissionRecord { date = DateTime.Now.Ticks, won = won, grade = grade, score = score, time = Mathf.RoundToInt(s.time), arrests = s.arrests, kills = s.kills, weapon = weapon });
            if (c.history.Count > 30) c.history.RemoveRange(30, c.history.Count - 30);
            Write();
        }

        public static void RecordTraining(int hits, int heads, int shots)
        {
            if (hits <= 0) return;
            int acc = shots > 0 ? Mathf.RoundToInt(Mathf.Min(hits, shots) * 100f / shots) : 0;
            var c = Data.career;
            if (hits > c.trainHits || (hits == c.trainHits && acc > c.trainAcc)) { c.trainHits = hits; c.trainHeads = heads; c.trainAcc = acc; Write(); }
        }

        public static void WipeCareer() { Data.career = new Career(); Write(); }
    }

    public class MissionStats
    {
        public int shots, hits, heads, kills, arrests, unjust;
        public float dmg, time;
    }
}
