using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace E7
{
    /// <summary>
    /// Affichage en jeu : incrustation « REC » de la bodycam, santé, munitions, point de visée, marqueur de touche,
    /// direction des dégâts, sous-titres, messages, objectif, état des coéquipiers.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }
        Canvas canvas;
        Text osd, title, objective, weapon, ammo, reserve, fireLine, hpLabel, note, subsWho, subsText, prompt, fps, team, toast;
        Image hpBar, dot, dmgDir, hurtVignette;
        RectTransform hitmark, subsBox;
        Image[] hitLines = new Image[4];
        float noteT, subsT, hitT, toastT, lastEnemyVoice, fpsT; int fpsN;
        static readonly System.Globalization.CultureInfo FR = new System.Globalization.CultureInfo("fr-FR");

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hud>();
            I = h;
            h.Build();
            return h;
        }

        void Build()
        {
            canvas = UIKit.MakeCanvas(transform, "HUDCanvas", 10);
            var root = canvas.transform;
            var shadow = new Color(0, 0, 0, 0.8f);
            Text T(string s, int size, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, TextAnchor al, FontStyle st = FontStyle.Bold)
            {
                var t = UIKit.LabelAt(root, s, size, c, aMin, aMax, oMin, oMax, al, st);
                var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = shadow; sh.effectDistance = new Vector2(1, -1);
                t.supportRichText = true;
                return t;
            }
            osd = T("", 13, UIKit.TextC, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-460, -80), new Vector2(-20, -16), TextAnchor.UpperRight);
            title = T("", 13, UIKit.TextC, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -44), new Vector2(620, -16), TextAnchor.UpperLeft);
            objective = T("", 13, UIKit.TextC, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -64), new Vector2(620, -40), TextAnchor.UpperLeft);
            team = T("", 12, UIKit.Muted, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -130), new Vector2(620, -66), TextAnchor.UpperLeft, FontStyle.Normal);
            hpLabel = T("SANTÉ 100", 13, UIKit.TextC, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 36), new Vector2(300, 56), TextAnchor.LowerLeft);
            var hb = UIKit.Rect(root, "hpBar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 24), new Vector2(210, 30));
            hb.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            hpBar = UIKit.Panel(hb, "fill", UIKit.TextC, Vector2.zero, Vector2.one);
            weapon = T("", 13, UIKit.TextC, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-460, 70), new Vector2(-20, 90), TextAnchor.LowerRight);
            ammo = T("", 30, UIKit.TextC, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-460, 34), new Vector2(-70, 74), TextAnchor.LowerRight);
            reserve = T("", 14, UIKit.Muted, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-66, 38), new Vector2(-20, 58), TextAnchor.LowerLeft);
            fireLine = T("", 12, UIKit.TextC, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-560, 14), new Vector2(-20, 32), TextAnchor.LowerRight);
            // point de visée
            dot = UIKit.Panel(root, "dot", new Color(1, 1, 1, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2, -2), new Vector2(2, 2));
            // marqueur de touche (croix)
            hitmark = UIKit.Rect(root, "hitmark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(14, 14));
            for (int i = 0; i < 4; i++)
            {
                var holder = UIKit.Rect(hitmark, "rot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                holder.localRotation = Quaternion.Euler(0, 0, 45 + 90 * i);
                hitLines[i] = UIKit.Panel(holder, "l", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1, 5), new Vector2(1, 13));
            }
            hitmark.gameObject.SetActive(false);
            // direction des dégâts
            var dd = UIKit.Rect(root, "dmgDir", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130, -130), new Vector2(130, 130));
            dmgDir = UIKit.Panel(dd, "arc", new Color(1f, 0.2f, 0.15f, 0.8f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-34, -8), new Vector2(34, 0));
            // message, invite, sous-titres, compteur FPS
            note = T("", 16, UIKit.TextC, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-400, 200), new Vector2(400, 230), TextAnchor.MiddleCenter);
            toast = T("", 14, UIKit.Green, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -70), new Vector2(300, -44), TextAnchor.MiddleCenter);
            prompt = T("", 15, UIKit.TextC, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -110), new Vector2(300, -80), TextAnchor.MiddleCenter);
            subsBox = UIKit.Rect(root, "subs", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-330, 64), new Vector2(330, 120));
            var sbg = subsBox.gameObject.AddComponent<Image>(); sbg.color = new Color(0, 0, 0, 0.55f);
            subsText = UIKit.LabelAt(subsBox, "", 15, UIKit.TextC, Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6), TextAnchor.MiddleCenter);
            subsText.supportRichText = true;
            subsBox.gameObject.SetActive(false);
            fps = T("", 12, UIKit.Muted, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -100), new Vector2(-20, -84), TextAnchor.UpperRight, FontStyle.Normal);
            canvas.gameObject.SetActive(false);
        }

        // ---------------- API statique (appelée partout) ----------------
        public static void Note(string msg) { if (I) { I.note.text = msg; I.noteT = 2.2f; } }
        public static void Toast(string msg) { if (I) { I.toast.text = msg; I.toastT = 1.6f; } }

        public static void Subtitle(string who, string text, string kind)
        {
            if (!I || !Save.Data.settings.subs) return;
            string col = kind == "enemy" ? "#ff8a7a" : kind == "me" ? "#ffb020" : kind == "mate" ? "#7fc8ff" : "#e9ecef";
            I.subsText.text = "<color=" + col + "><b>" + who + "</b></color>  " + text;
            I.subsT = 2.4f + text.Length * 0.045f;
            I.subsBox.gameObject.SetActive(true);
        }

        public static void Hitmark(bool kill)
        {
            if (!I) return;
            I.hitT = kill ? 0.3f : 0.12f;
            foreach (var l in I.hitLines) l.color = kill ? UIKit.Red : Color.white;
            I.hitmark.gameObject.SetActive(true);
        }

        /// <summary>Évite que les suspects parlent tous en même temps.</summary>
        public static bool CanEnemyTalk(bool force, Vector3 pos)
        {
            if (!I) return false;
            float t = Time.time;
            if (!force && t - I.lastEnemyVoice < 2.6f) return false;
            if (Player.I && Vector3.Distance(pos, Player.I.transform.position) > 28) return false;
            I.lastEnemyVoice = t;
            return true;
        }

        void Update()
        {
            var g = Game.I;
            bool show = g != null && (g.State == GameState.Playing || g.State == GameState.Dead);
            if (canvas.gameObject.activeSelf != show) canvas.gameObject.SetActive(show);
            if (!show) return;
            float dt = Time.unscaledDeltaTime;
            var p = Player.I; var w = p.Weapons; var def = w.Def; var s = Save.Data.settings; var ch = Save.Data.character;
            bool training = g.Mode == GameMode.Training;
            osd.text = "<color=#ff4a3a>●</color> REC · BODYCAM X7\n" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\nAGENT " + (ch.name ?? "") + " · UNITÉ " + (ch.unit ?? "");
            title.text = training ? "<color=#ffb020>STAND DE TIR</color> · ENTRAÎNEMENT" : "<color=#ffb020>ENTREPÔT 7</color> · INTERVENTION";
            if (training)
            {
                int acc = g.TrainShots > 0 ? Mathf.RoundToInt(Mathf.Min(g.TrainHits, g.TrainShots) * 100f / g.TrainShots) : 0;
                objective.text = "TOUCHÉS " + g.TrainHits + " · TÊTE " + g.TrainHeads + " · PRÉCISION " + acc + " %";
                team.text = "";
            }
            else
            {
                int left = Squad.I.suspects.FindAll(x => x.Hostile).Count, arrested = Squad.I.suspects.FindAll(x => x.state == SuspectAI.S.Arrested || x.state == SuspectAI.S.Surrender).Count;
                objective.text = "SUSPECTS ACTIFS " + left + " / " + Squad.I.suspects.Count + " · INTERPELLÉS " + arrested + " · TEMPS " + ((int)g.Stats.time / 60) + ":" + ((int)g.Stats.time % 60).ToString("00");
                var sb = new System.Text.StringBuilder();
                foreach (var m in Squad.I.mates)
                    sb.Append(m.callsign).Append("  ").Append(m.Alive ? (m.order == TeammateAI.Order.Follow ? "suit" : "tient la position") + (m.state == TeammateAI.S.Combat ? " · <color=#ff8a7a>au contact</color>" : m.state == TeammateAI.S.Arrest ? " · menotte" : "") : "<color=#ff5a50>à terre</color>").Append('\n');
                team.text = sb.ToString();
            }
            hpLabel.text = "SANTÉ " + Mathf.CeilToInt(p.Health);
            UIKit.SetBar(hpBar, p.Health / 100f);
            hpBar.color = p.Health < 35 ? UIKit.Red : UIKit.TextC;
            weapon.text = def.name.ToUpper(FR);
            ammo.text = w.mag[w.Cur].ToString();
            reserve.text = "/ " + (training ? "∞" : w.reserve[w.Cur].ToString());
            string modeTxt = def.pump ? "POMPE" : def.auto ? (w.fireMode[w.Cur] == "auto" ? "RAFALE" : "COUP PAR COUP") : "COUP PAR COUP";
            var att = Save.Data.loadout.For(w.Cur);
            fireLine.text = (w.Reloading ? "RECHARGEMENT…" : modeTxt + (w.LampOn ? (att.rail == "laser" ? " · LAMPE + LASER" : " · LAMPE") : "")) + " · FLASH ×" + (training ? "∞" : w.flashbangs.ToString());
            dot.enabled = s.dot && !w.AimingOptic && w.adsT < 0.5f && p.Alive;
            noteT -= dt; note.enabled = noteT > 0; note.color = new Color(1, 1, 1, Mathf.Clamp01(noteT * 3));
            toastT -= dt; toast.enabled = toastT > 0;
            subsT -= dt; if (subsT <= 0 && subsBox.gameObject.activeSelf) subsBox.gameObject.SetActive(false);
            hitT -= dt; if (hitT <= 0 && hitmark.gameObject.activeSelf) hitmark.gameObject.SetActive(false);
            // direction des dégâts
            dmgDir.enabled = p.DmgDirT > 0;
            if (p.DmgDirT > 0)
            {
                dmgDir.rectTransform.parent.localRotation = Quaternion.Euler(0, 0, -p.RelAngle(p.LastHitFrom) * Mathf.Rad2Deg);
                dmgDir.color = new Color(1f, 0.2f, 0.15f, Mathf.Clamp01(p.DmgDirT) * 0.8f);
            }
            // invite « menotter »
            var sur = g.Mode == GameMode.Mission ? Squad.NearestSurrendered(p.transform.position, 1.9f) : null;
            prompt.text = sur != null ? "<color=#ffb020>[G]</color> Menotter le suspect" : "";
            // compteur d'images par seconde
            if (s.fps) { fpsT += dt; fpsN++; if (fpsT >= 0.5f) { fps.text = Mathf.RoundToInt(fpsN / fpsT) + " IPS"; fpsT = 0; fpsN = 0; } }
            else fps.text = "";
        }
    }
}
