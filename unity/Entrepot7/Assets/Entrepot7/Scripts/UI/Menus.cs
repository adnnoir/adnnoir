using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace E7
{
    /// <summary>
    /// Tous les écrans : menu principal, briefing, arsenal (aperçu 3D), personnage, paramètres (4 onglets),
    /// dossier (carrière sauvegardée), pause et fin de mission.
    /// </summary>
    public class Menus : MonoBehaviour
    {
        public static Menus I { get; private set; }
        Canvas canvas;
        readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();
        readonly Stack<string> back = new Stack<string>();
        public string Current { get; private set; }
        public string ArsenalView = "carbine";
        public Vector3 OfficerPos => new Vector3(9, 0, 4.6f);
        Humanoid officer;

        Text bestLine, menuLoadout, menuAgent, menuCareer, briefLoadout;
        // arsenal
        Text wName, wCal, wDesc; Transform statsBox, attBox; readonly Dictionary<string, Button> weaponBtns = new Dictionary<string, Button>();
        // fin
        Text gradeLetter, endTitle, endText, endEyebrow; readonly Dictionary<string, Text> endStats = new Dictionary<string, Text>();
        // dossier
        Text dosTitle; readonly Dictionary<string, Text> dosStats = new Dictionary<string, Text>(); Transform histBox; Button wipeBtn; float wipeArm = -10;
        // paramètres
        readonly Dictionary<string, GameObject> tabs = new Dictionary<string, GameObject>(); readonly Dictionary<string, Button> tabBtns = new Dictionary<string, Button>();
        GameObject pauseArsenal;

        public static Menus Create(Transform parent)
        {
            var go = new GameObject("Menus");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Menus>();
            I = m;
            m.Build();
            return m;
        }

        static Settings S => Save.Data.settings;

        // ---------------- navigation ----------------
        public void Show(string id)
        {
            if (Current == "perso" && id != "perso" && officer) officer.gameObject.SetActive(false);
            Current = id;
            foreach (var kv in screens) kv.Value.SetActive(kv.Key == id);
            canvas.gameObject.SetActive(id != null);
            if (id == "arsenal") RenderArsenal();
            if (id == "perso") ShowOfficer();
            if (id == "dossier") RenderDossier();
            if (id == "pause") pauseArsenal.SetActive(Game.I.Mode == GameMode.Training);
            if (id == "arsenal") Player.I.Weapons.SetHandsVisible(false);
            RefreshLabels();
        }

        public void Open(string id) { back.Push(Current ?? "menu"); Show(id); }

        public void Back()
        {
            if (Current == "arsenal") { Player.I.Weapons.EndPreview(); if (Game.I.State == GameState.Menu) Player.I.Weapons.SetHandsVisible(false); }
            string prev = back.Count > 0 ? back.Pop() : (Game.I.State == GameState.Paused ? "pause" : "menu");
            if (Current == "pause" && Game.I.State == GameState.Paused) { Game.I.Resume(); return; }
            if (Current == "menu") return;
            Show(prev);
        }

        void RefreshLabels()
        {
            var d = Save.Data; var c = d.career;
            if (menuLoadout) menuLoadout.text = WeaponDef.All[d.loadout.primary].shortName + " · P17";
            if (menuAgent) menuAgent.text = (d.character.name ?? "AGENT") + " · " + (d.character.unit ?? "0000");
            if (menuCareer) menuCareer.text = c.missions > 0 ? c.missions + " intervention" + (c.missions > 1 ? "s" : "") + (string.IsNullOrEmpty(c.bestGrade) ? "" : " · meilleure note " + c.bestGrade) : "aucune intervention";
            if (bestLine) bestLine.text = (c.bestScore > 0 ? "Meilleur score : <b>" + c.bestScore + "</b>" : "Aucune intervention enregistrée.") + "\nTir réel · pas de soins automatiques · les suspects peuvent se rendre.\n<size=10>Sons de tir : The Free Firearm Sound Library (CC0) · Voix : Piper, MLS et SIWIS (CC BY 4.0)</size>";
            if (briefLoadout)
            {
                var a = d.loadout.For(d.loadout.primary);
                briefLoadout.text = "Équipement : <b>" + WeaponDef.All[d.loadout.primary].name + "</b> (" + WeaponDef.AttLabel[a.optic] + ", " + WeaponDef.AttLabel[a.muzzle] + ", " + WeaponDef.AttLabel[a.rail] + ") · <b>Pistolet P17</b> · 2 grenades flash";
            }
        }

        // ---------------- construction ----------------
        void Build()
        {
            canvas = UIKit.MakeCanvas(transform, "MenuCanvas", 20);
            BuildMain(); BuildBrief(); BuildArsenal(); BuildPerso(); BuildSettings(); BuildDossier(); BuildPause(); BuildEnd();
        }

        GameObject Screen(string id)
        {
            var r = UIKit.Fill(canvas.transform, "ecran_" + id);
            screens[id] = r.gameObject;
            return r.gameObject;
        }

        /// <summary>Carte centrale avec bande de chantier (briefing, dossier, fin…).</summary>
        RectTransform CardScreen(string id, float width, float height, bool dim = true)
        {
            var sc = Screen(id);
            if (dim) UIKit.Panel(sc.transform, "voile", new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one);
            var card = UIKit.Panel(sc.transform, "carte", UIKit.Card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width / 2, -height / 2), new Vector2(width / 2, height / 2));
            var col = UIKit.Column(card.transform, "col", 12, new RectOffset(26, 26, 0, 22));
            var cr = col; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            var tape = UIKit.Tape(col, 10);
            return col;
        }

        Text Eyebrow(Transform p, string t) { var l = UIKit.Label(p, t.ToUpperInvariant(), 12, UIKit.Amber, TextAnchor.UpperLeft, FontStyle.Bold); UIKit.Size(l, 16, 16); return l; }
        Text H2(Transform p, string t, int size = 40) { var l = UIKit.Label(p, t.ToUpperInvariant(), size, UIKit.TextC, TextAnchor.UpperLeft, FontStyle.Bold); UIKit.Size(l, size + 6, size + 6); return l; }
        Text Para(Transform p, string t, int size = 14, Color? c = null) { var l = UIKit.Label(p, t, size, c ?? new Color(0.79f, 0.81f, 0.83f)); l.supportRichText = true; UIKit.Size(l, size + 4); l.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return l; }

        void BuildMain()
        {
            var sc = Screen("menu");
            var side = UIKit.Panel(sc.transform, "cote", new Color(0.016f, 0.02f, 0.024f, 0.9f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(520, 0));
            var col = UIKit.Column(side.transform, "col", 6, new RectOffset(32, 24, 34, 24));
            col.anchorMin = Vector2.zero; col.anchorMax = Vector2.one; col.offsetMin = Vector2.zero; col.offsetMax = Vector2.zero;
            Eyebrow(col, "Canal 2 · 23 h 41");
            var t1 = UIKit.Label(col, "ENTREPÔT", 64, UIKit.TextC, TextAnchor.UpperLeft, FontStyle.Bold); UIKit.Size(t1, 70, 70);
            var t2 = UIKit.Label(col, "7", 64, UIKit.Amber, TextAnchor.UpperLeft, FontStyle.Bold); UIKit.Size(t2, 64, 64);
            var tp = UIKit.Tape(col, 10); UIKit.Size(tp, 10, 10, 220, 0);
            UIKit.Size(UIKit.Label(col, "", 8, UIKit.Muted), 12, 12);
            UIKit.Button(col, "INTERVENTION", () => Open("brief"), UIKit.BtnStyle.Menu, "7 SUSPECTS ARMÉS");
            UIKit.Button(col, "ENTRAÎNEMENT", () => Game.I.StartGame(GameMode.Training), UIKit.BtnStyle.Menu, "STAND DE TIR");
            menuLoadout = SmallOf(UIKit.Button(col, "ARSENAL", () => { ArsenalView = Save.Data.loadout.primary; Open("arsenal"); }, UIKit.BtnStyle.Menu, "CT-4 · P17"));
            menuAgent = SmallOf(UIKit.Button(col, "PERSONNAGE", () => Open("perso"), UIKit.BtnStyle.Menu, "AGENT"));
            menuCareer = SmallOf(UIKit.Button(col, "DOSSIER", () => Open("dossier"), UIKit.BtnStyle.Menu, "aucune intervention"));
            UIKit.Button(col, "PARAMÈTRES", () => Open("settings"), UIKit.BtnStyle.Menu);
            if (MLHooks.Available) UIKit.Button(col, "ARÈNE IA", () => Game.I.StartArena(), UIKit.BtnStyle.Menu, "ML-AGENTS");
            UIKit.Button(col, "QUITTER", () => { Save.Write(); Application.Quit(); }, UIKit.BtnStyle.Menu);
            var spacer = UIKit.Label(col, "", 8, UIKit.Muted); UIKit.Size(spacer, 10).flexibleHeight = 1;
            bestLine = UIKit.Label(col, "", 12, UIKit.Muted); bestLine.supportRichText = true; UIKit.Size(bestLine, 70, 70);
            UIKit.LabelAt(sc.transform, "<color=#ff4a3a>●</color> REC · BODYCAM X7\nUnity · Built-in RP", 13, UIKit.TextC, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-340, -60), new Vector2(-22, -18), TextAnchor.UpperRight, FontStyle.Bold).supportRichText = true;
        }

        // le petit texte gris à côté d'un gros bouton du menu : on le remplace par un Text séparé pour le mettre à jour
        Text SmallOf(Button b)
        {
            var main = b.GetComponentInChildren<Text>();
            string title = main.text; int i = title.IndexOf("  <size"); if (i > 0) main.text = title.Substring(0, i);
            var small = UIKit.Label(b.transform, "", 12, UIKit.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            var r = small.rectTransform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 1); r.offsetMin = new Vector2(20 + main.preferredWidth + 14, 0); r.offsetMax = Vector2.zero;
            return small;
        }

        void BuildBrief()
        {
            var col = CardScreen("brief", 860, 560);
            Eyebrow(col, "Ordre de mission · priorité 1");
            H2(col, "Entrepôt 7, zone nord", 34);
            var row = UIKit.Row(col, "row", 18);
            var left = UIKit.Column(row, "gauche", 8); UIKit.Size(left, -1, -1, 520, 1);
            Para(left, "Un voisin a entendu des coups de feu. Sept individus armés occupent un entrepôt désaffecté. Les renforts sont là : ton équipe entre avec toi.");
            foreach (var o in new[] {
                "▸ Neutraliser ou interpeller les 7 suspects.",
                "▸ Crier « Police ! » (<b>V</b>) pour leur ordonner de se rendre, puis menotter (<b>G</b>).",
                "▸ Une grenade flash (<b>X</b>) aveugle les suspects quelques secondes.",
                "▸ Coéquipiers : <b>T</b> avec moi / tenez la position · <b>Y</b> allez là où je vise.",
                "▸ Ne jamais tirer sur un suspect qui s’est rendu." }) Para(left, o, 14);
            var right = UIKit.Column(row, "droite", 6); UIKit.Size(right, -1, -1, 250, 0);
            var map = new GameObject("plan", typeof(RectTransform)); map.transform.SetParent(right, false);
            var ri = map.AddComponent<RawImage>(); ri.texture = MapTexture(); UIKit.Size(ri, 165, 165, 250);
            Para(right, "Plan fourni par le propriétaire. <color=#ffb020>■</color> ton point d’entrée. Positions des suspects inconnues.", 11, UIKit.Muted);
            var tm = UIKit.Row(col, "equipe", 10);
            UIKit.Size(UIKit.Label(tm, "Coéquipiers IA", 13, UIKit.Muted, TextAnchor.MiddleLeft), 32, 32, 130);
            UIKit.Segmented(tm, new[] { "0", "1", "2", "3" }, new[] { "Aucun", "1", "2", "3" }, () => S.teammates.ToString(), v => { S.teammates = int.Parse(v); Save.Write(); });
            briefLoadout = Para(col, "", 13);
            var btns = UIKit.Row(col, "boutons", 10);
            UIKit.Size(UIKit.Button(btns, "ENTRER DANS L’ENTREPÔT", () => Game.I.StartGame(GameMode.Mission), UIKit.BtnStyle.Primary), 48, 48, 300);
            UIKit.Size(UIKit.Button(btns, "Changer d’équipement", () => { ArsenalView = Save.Data.loadout.primary; Open("arsenal"); }), 44, 44, 220);
            UIKit.Size(UIKit.Button(btns, "Retour", Back), 44, 44, 120);
        }

        Texture2D MapTexture()
        {
            int px = 8; var t = new Texture2D(Level.Cols * px, Level.Rows * px) { filterMode = FilterMode.Point };
            for (int r = 0; r < Level.Rows; r++) for (int c = 0; c < Level.Cols; c++)
            {
                char ch = Level.Tile(c, r);
                Color col = ch == '#' ? new Color(0.35f, 0.36f, 0.38f) : ch == 'S' ? new Color(0.5f, 0.3f, 0.15f) : ch == 'C' || ch == 'B' ? new Color(0.36f, 0.3f, 0.22f) : ch == 'I' ? new Color(0.4f, 0.42f, 0.45f) : ch == 'D' ? new Color(0.2f, 0.22f, 0.25f) : new Color(0.05f, 0.055f, 0.06f);
                if (ch == 'P') col = UIKit.Amber;
                // la texture a son origine en bas : la ligne 0 de la carte est en haut
                for (int y = 0; y < px; y++) for (int x = 0; x < px; x++) t.SetPixel(c * px + x, (Level.Rows - 1 - r) * px + y, (x == 0 || y == 0) && ch != '#' ? col * 1.15f : col);
            }
            t.Apply();
            return t;
        }

        // ---------------- arsenal ----------------
        void BuildArsenal()
        {
            var sc = Screen("arsenal");
            var side = UIKit.Panel(sc.transform, "cote", new Color(0.016f, 0.02f, 0.024f, 0.95f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(480, 0));
            var col = UIKit.Column(side.transform, "col", 10, new RectOffset(28, 28, 28, 24));
            col.anchorMin = Vector2.zero; col.anchorMax = Vector2.one; col.offsetMin = Vector2.zero; col.offsetMax = Vector2.zero;
            Eyebrow(col, "Armurerie du commissariat");
            H2(col, "Arsenal", 48);
            UIKit.Size(UIKit.Label(col, "ARME PRINCIPALE", 12, UIKit.Muted), 16, 16);
            var prim = UIKit.Row(col, "principales", 6, true);
            foreach (var k in new[] { "carbine", "smg", "shotgun" }) weaponBtns[k] = WeaponButton(prim, k);
            UIKit.Size(UIKit.Label(col, "ARME DE POING", 12, UIKit.Muted), 16, 16);
            var sec = UIKit.Row(col, "poing", 6, true);
            weaponBtns["pistol"] = WeaponButton(sec, "pistol");
            UIKit.Size(UIKit.Label(sec, "", 10, UIKit.Muted), 10, 10, 280);
            wName = H2(col, "", 26);
            wCal = UIKit.Label(col, "", 12, UIKit.Muted); UIKit.Size(wCal, 16, 16);
            wDesc = Para(col, "", 13);
            statsBox = UIKit.Column(col, "stats", 4);
            UIKit.Size(UIKit.Label(col, "ACCESSOIRES", 12, UIKit.Muted), 18, 18);
            attBox = UIKit.Column(col, "accessoires", 6);
            var spacer = UIKit.Label(col, "", 8, UIKit.Muted); UIKit.Size(spacer, 4).flexibleHeight = 1;
            UIKit.Size(UIKit.Button(col, "VALIDER", Back, UIKit.BtnStyle.Primary), 46, 46);
        }

        Button WeaponButton(Transform parent, string k)
        {
            var d = WeaponDef.All[k];
            var b = UIKit.Button(parent, d.shortName, () => { ArsenalView = k; if (d.slot == "primary") { Save.Data.loadout.primary = k; Save.Write(); } RenderArsenal(); }, UIKit.BtnStyle.Secondary);
            var t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft; t.fontSize = 18;
            t.supportRichText = true;
            t.text = d.shortName + "\n<size=11><color=#8b9197>" + (k == "carbine" ? "Carabine" : k == "smg" ? "Pistolet-mitr." : k == "shotgun" ? "Fusil à pompe" : "Pistolet") + "</color></size>";
            UIKit.Size(b, 52, 52, 130);
            return b;
        }

        void RenderArsenal()
        {
            var d = WeaponDef.All[ArsenalView];
            foreach (var kv in weaponBtns)
            {
                bool eq = kv.Key == Save.Data.loadout.primary || kv.Key == "pistol";
                var img = kv.Value.GetComponent<Image>();
                img.color = kv.Key == ArsenalView ? new Color(1f, 0.69f, 0.13f, 0.18f) : eq ? new Color(1, 1, 1, 0.09f) : new Color(1, 1, 1, 0.03f);
            }
            wName.text = d.name.ToUpperInvariant(); wCal.text = d.cal; wDesc.text = d.desc;
            foreach (Transform c in statsBox) Destroy(c.gameObject);
            foreach (var st in d.stats)
            {
                var row = UIKit.Row(statsBox, "stat", 10);
                UIKit.Size(UIKit.Label(row, st.Key.ToUpperInvariant(), 11, UIKit.Muted, TextAnchor.MiddleLeft), 14, 14, 110);
                UIKit.Bar(row, st.Value, UIKit.TextC, 6);
            }
            foreach (Transform c in attBox) Destroy(c.gameObject);
            var a = Save.Data.loadout.For(ArsenalView);
            void AttRow(string label, string[] opts, System.Func<string> get, System.Action<string> set)
            {
                if (opts.Length < 2) return;
                var row = UIKit.Row(attBox, "att", 10);
                UIKit.Size(UIKit.Label(row, label.ToUpperInvariant(), 11, UIKit.Muted, TextAnchor.MiddleLeft), 32, 32, 80);
                var labels = new string[opts.Length]; for (int i = 0; i < opts.Length; i++) labels[i] = WeaponDef.AttLabel[opts[i]];
                UIKit.Segmented(row, opts, labels, get, v => { set(v); Save.Write(); Player.I.Weapons.InvalidateViews(); RenderArsenal(); });
            }
            AttRow("Viseur", d.optics, () => a.optic, v => a.optic = v);
            AttRow("Bouche", d.muzzles, () => a.muzzle, v => a.muzzle = v);
            AttRow("Rail", d.rails, () => a.rail, v => a.rail = v);
            RefreshLabels();
        }

        // ---------------- personnage ----------------
        void BuildPerso()
        {
            var sc = Screen("perso");
            var side = UIKit.Panel(sc.transform, "cote", new Color(0.016f, 0.02f, 0.024f, 0.95f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(470, 0));
            var col = UIKit.Column(side.transform, "col", 12, new RectOffset(28, 28, 28, 24));
            col.anchorMin = Vector2.zero; col.anchorMax = Vector2.one; col.offsetMin = Vector2.zero; col.offsetMax = Vector2.zero;
            Eyebrow(col, "Fiche de l’agent");
            H2(col, "Personnage", 44);
            var c = Save.Data.character;
            Field(col, "Nom", UIKit.Input(null, () => c.name, v => { c.name = (v ?? "").ToUpperInvariant(); Save.Write(); RefreshLabels(); }, 14).gameObject);
            Field(col, "Unité", UIKit.Input(null, () => c.unit, v => { var digits = new System.Text.StringBuilder(); foreach (char ch in v ?? "") if (char.IsDigit(ch)) digits.Append(ch); c.unit = digits.Length > 0 ? digits.ToString() : "4471"; Save.Write(); RefreshLabels(); }, 6).gameObject);
            Field(col, "Tenue", UIKit.Segmented(null, new[] { "police", "bri", "olive", "urbain" }, new[] { "Police", "BRI", "Olive", "Urbain" }, () => c.uniform, v => { c.uniform = v; Save.Write(); ShowOfficer(); Player.I.Weapons.ApplyCharacterColors(); }).gameObject);
            Field(col, "Gants", UIKit.Segmented(null, new[] { "noir", "coyote", "olive" }, new[] { "Noirs", "Coyote", "Olive" }, () => c.gloves, v => { c.gloves = v; Save.Write(); ShowOfficer(); Player.I.Weapons.ApplyCharacterColors(); }).gameObject);
            Field(col, "Tête", UIKit.Segmented(null, new[] { "casquette", "casque", "rien" }, new[] { "Casquette", "Casque", "Rien" }, () => c.head, v => { c.head = v; Save.Write(); ShowOfficer(); }).gameObject);
            Para(col, "Le numéro d’unité est prononcé à la radio, chiffre par chiffre.", 12, UIKit.Muted);
            var spacer = UIKit.Label(col, "", 8, UIKit.Muted); UIKit.Size(spacer, 4).flexibleHeight = 1;
            UIKit.Size(UIKit.Button(col, "VALIDER", Back, UIKit.BtnStyle.Primary), 46, 46);
        }

        void Field(Transform col, string label, GameObject control)
        {
            var row = UIKit.Row(col, "champ", 12);
            UIKit.Size(UIKit.Label(row, label.ToUpperInvariant(), 12, UIKit.Muted, TextAnchor.MiddleLeft), 34, 34, 90);
            control.transform.SetParent(row, false);
        }

        void ShowOfficer()
        {
            if (officer) Destroy(officer.gameObject);
            var c = Save.Data.character;
            var uniforms = new Dictionary<string, (string top, string vest)> { { "police", ("#1d2736", "#161b24") }, { "bri", ("#151617", "#1b1c1d") }, { "olive", ("#3d4232", "#4a4f3a") }, { "urbain", ("#4a4e55", "#2b2e33") } };
            var gloves = new Dictionary<string, string> { { "noir", "#17181a" }, { "coyote", "#8a7556" }, { "olive", "#4b5237" } };
            var u = uniforms.TryGetValue(c.uniform ?? "", out var uu) ? uu : uniforms["police"];
            Material M(string k)
            {
                var b = E7Assets.Mat("human", k);
                if (k == "top" || k == "pants") { var m = new Material(b); m.SetColor("_Color", U.Hex(u.top)); return m; }
                if (k == "vest" || k == "cap") { var m = new Material(b); m.SetColor("_Color", U.Hex(u.vest)); return m; }
                if (k == "glove") { var m = new Material(b); m.SetColor("_Color", U.Hex(gloves.TryGetValue(c.gloves ?? "", out var g) ? g : "#17181a")); return m; }
                return b;
            }
            string hg = c.head == "casque" ? "helmet" : c.head == "casquette" ? "cap" : "none";
            officer = Humanoid.Create(transform, "h_officer", t => t == "base" || t == "headgear:" + hg, M, null);
            officer.transform.position = OfficerPos;
            officer.AttachGun(HeldGun.Detailed("carbine", officer.transform));
            officer.gameObject.AddComponent<OfficerIdle>().h = officer;
            officer.gameObject.SetActive(Current == "perso");
        }

        class OfficerIdle : MonoBehaviour { public Humanoid h; void Update() { h.Pose(Time.unscaledDeltaTime, 0.25f, h.transform.position + Vector3.up * 1.4f + Vector3.forward * 5, false); } }

        // ---------------- paramètres ----------------
        void BuildSettings()
        {
            var col = CardScreen("settings", 820, 600);
            Eyebrow(col, "Réglages · enregistrés automatiquement");
            H2(col, "Paramètres", 36);
            var tabRow = UIKit.Row(col, "onglets", 2);
            var body = new GameObject("corps", typeof(RectTransform)); body.transform.SetParent(col, false);
            UIKit.Size(body.GetComponent<RectTransform>(), 360, 360);
            void Tab(string id, string label, System.Action<Transform> fill)
            {
                var b = UIKit.Button(tabRow, label, () => ShowTab(id), UIKit.BtnStyle.Tab);
                UIKit.Size(b, 34, 34, 150);
                tabBtns[id] = b;
                var page = UIKit.Column(body.transform, "page_" + id, 12, new RectOffset(0, 0, 10, 0));
                page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one; page.offsetMin = Vector2.zero; page.offsetMax = Vector2.zero;
                fill(page);
                tabs[id] = page.gameObject;
            }
            Tab("jeu", "JEU", p =>
            {
                Field(p, "Difficulté", UIKit.Segmented(null, new[] { "facile", "normal", "realiste" }, new[] { "Facile", "Normal", "Réaliste" }, () => S.diff, v => { S.diff = v; Save.Write(); }).gameObject);
                Field(p, "Coéquipiers", UIKit.Segmented(null, new[] { "0", "1", "2", "3" }, new[] { "Aucun", "1", "2", "3" }, () => S.teammates.ToString(), v => { S.teammates = int.Parse(v); Save.Write(); }).gameObject);
                Field(p, "Clavier", UIKit.Segmented(null, new[] { "azerty", "qwerty" }, new[] { "AZERTY (ZQSD)", "QWERTY (WASD)" }, () => S.keyboard, v => { S.keyboard = v; Save.Write(); }).gameObject);
                Field(p, "Sous-titres", Toggle(() => S.subs, v => S.subs = v));
                Field(p, "Point de visée", Toggle(() => S.dot, v => S.dot = v));
                Field(p, "Images/s", Toggle(() => S.fps, v => S.fps = v));
                if (MLHooks.Available) Field(p, "Cerveau IA", UIKit.Segmented(null, new[] { "regles", "reseau" }, new[] { "Règles", "Réseau (RL)" }, () => S.brain, v => { S.brain = v; Save.Write(); }).gameObject);
            });
            Tab("image", "IMAGE", p =>
            {
                Field(p, "Qualité", UIKit.Segmented(null, new[] { "bas", "moyen", "haut" }, new[] { "Basse", "Moyenne", "Haute" }, () => S.quality, v => { S.quality = v; Save.Write(); Game.I.ApplyQuality(); }).gameObject);
                Field(p, "Effets caméra", UIKit.Segmented(null, new[] { "off", "leger", "fort" }, new[] { "Aucun", "Léger", "Bodycam" }, () => S.camFx, v => { S.camFx = v; Save.Write(); }).gameObject);
                Field(p, "Luminosité", UIKit.Slider(null, 0.6f, 1.6f, () => S.bright, v => { S.bright = v; Save.Write(); }, v => Mathf.RoundToInt(v * 100) + " %").transform.parent.gameObject);
                Field(p, "Champ de vision", UIKit.Slider(null, 60, 95, () => S.fov, v => { S.fov = Mathf.Round(v); Save.Write(); }, v => Mathf.RoundToInt(v) + "°").transform.parent.gameObject);
                Field(p, "Balancement", Toggle(() => S.bob, v => S.bob = v));
            });
            Tab("son", "SON", p =>
            {
                string Pc(float v) => Mathf.RoundToInt(v * 100) + " %";
                Field(p, "Général", UIKit.Slider(null, 0, 1, () => S.vMaster, v => { S.vMaster = v; Save.Write(); }, Pc).transform.parent.gameObject);
                Field(p, "Effets", UIKit.Slider(null, 0, 1, () => S.vSfx, v => { S.vSfx = v; Save.Write(); }, Pc).transform.parent.gameObject);
                Field(p, "Ambiance", UIKit.Slider(null, 0, 1, () => S.vAmb, v => { S.vAmb = v; Save.Write(); }, Pc).transform.parent.gameObject);
                Field(p, "Voix", UIKit.Slider(null, 0, 1, () => S.vVoice, v => { S.vVoice = v; Save.Write(); }, Pc).transform.parent.gameObject);
                Field(p, "Musique", UIKit.Slider(null, 0, 1, () => S.vMusic, v => { S.vMusic = v; Save.Write(); }, Pc).transform.parent.gameObject);
                Field(p, "Voix actives", Toggle(() => S.voices, v => S.voices = v));
            });
            Tab("controles", "CONTRÔLES", p =>
            {
                Field(p, "Sensibilité", UIKit.Slider(null, 0.2f, 3f, () => S.sens, v => { S.sens = v; Save.Write(); }, v => v.ToString("0.00")).transform.parent.gameObject);
                Field(p, "En visée", UIKit.Slider(null, 0.2f, 1.2f, () => S.adsSens, v => { S.adsSens = v; Save.Write(); }, v => v.ToString("0.00")).transform.parent.gameObject);
                Field(p, "Inverser Y", Toggle(() => S.invertY, v => S.invertY = v));
                Para(p, "<b>ZQSD / WASD</b> se déplacer · <b>Maj</b> courir · <b>C</b> s’accroupir · <b>A E</b> (QWERTY : Q E) se pencher\n<b>Clic gauche</b> tirer · <b>Clic droit</b> viser · <b>R</b> recharger · <b>B</b> mode de tir · <b>1 2</b> / molette changer d’arme\n<b>F</b> lampe · <b>X</b> grenade flash · <b>I</b> inspecter · <b>V</b> crier « Police ! » · <b>G</b> menotter\n<b>T</b> coéquipiers : avec moi / tenez · <b>Y</b> coéquipiers : allez là · <b>Échap</b> pause", 12, UIKit.Muted);
            });
            var btns = UIKit.Row(col, "boutons", 10);
            UIKit.Size(UIKit.Button(btns, "RETOUR", Back, UIKit.BtnStyle.Primary), 46, 46, 180);
            UIKit.Size(UIKit.Button(btns, "Valeurs par défaut", () => { var kb = S.keyboard; Save.Data.settings = new Settings { keyboard = kb }; Save.Write(); Game.I.ApplyQuality(); Show("settings"); }), 42, 42, 200);
            ShowTab("jeu");
        }

        void ShowTab(string id)
        {
            foreach (var kv in tabs) kv.Value.SetActive(kv.Key == id);
            foreach (var kv in tabBtns) UIKit.SetOn(kv.Value, kv.Key == id);
        }

        GameObject Toggle(System.Func<bool> get, System.Action<bool> set)
        {
            return UIKit.Segmented(null, new[] { "1", "0" }, new[] { "Oui", "Non" }, () => get() ? "1" : "0", v => { set(v == "1"); Save.Write(); }).gameObject;
        }

        // ---------------- dossier ----------------
        void BuildDossier()
        {
            var col = CardScreen("dossier", 780, 620);
            Eyebrow(col, "Dossier de l’agent");
            dosTitle = H2(col, "Carrière", 34);
            var grid = new GameObject("grille", typeof(RectTransform)); grid.transform.SetParent(col, false);
            var gl = grid.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(236, 50); gl.spacing = new Vector2(2, 2); gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            UIKit.Size(grid.GetComponent<RectTransform>(), 210, 210);
            foreach (var k in new[] { "Interventions", "Réussies", "Meilleure note", "Meilleur score", "Série de réussites", "Temps en service", "Interpellés", "Neutralisés", "Précision", "Tirs à la tête", "Tirs injustifiés", "Record stand de tir" })
                dosStats[k] = StatCell(grid.transform, k);
            UIKit.Size(UIKit.Label(col, "DERNIÈRES INTERVENTIONS", 11, UIKit.Muted), 14, 14);
            histBox = UIKit.Column(col, "historique", 2);
            var btns = UIKit.Row(col, "boutons", 10);
            UIKit.Size(UIKit.Button(btns, "RETOUR", Back, UIKit.BtnStyle.Primary), 46, 46, 160);
            UIKit.Size(UIKit.Button(btns, "Ouvrir le dossier de sauvegarde", () => Application.OpenURL("file://" + Application.persistentDataPath)), 42, 42, 280);
            wipeBtn = UIKit.Button(btns, "Effacer la progression", () =>
            {
                if (Time.unscaledTime - wipeArm > 4) { wipeArm = Time.unscaledTime; wipeBtn.GetComponentInChildren<Text>().text = "Sûr ? Clique encore"; return; }
                Save.WipeCareer(); wipeArm = -10; wipeBtn.GetComponentInChildren<Text>().text = "Progression effacée"; RenderDossier(); RefreshLabels();
            }, UIKit.BtnStyle.Danger);
            UIKit.Size(wipeBtn, 42, 42, 220);
        }

        Text StatCell(Transform parent, string label)
        {
            var cell = new GameObject("cellule", typeof(RectTransform)); cell.transform.SetParent(parent, false);
            cell.AddComponent<Image>().color = new Color(1, 1, 1, 0.04f);
            UIKit.LabelAt(cell.transform, label.ToUpperInvariant(), 10, UIKit.Muted, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-6, -4), TextAnchor.UpperLeft);
            return UIKit.LabelAt(cell.transform, "0", 18, UIKit.TextC, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-6, -4), TextAnchor.LowerLeft, FontStyle.Bold);
        }

        void RenderDossier()
        {
            var c = Save.Data.career; var ch = Save.Data.character;
            dosTitle.text = ("Agent " + (ch.name ?? "—")).ToUpperInvariant();
            string T(int s) { int h = s / 3600, m = s % 3600 / 60, r = s % 60; return h > 0 ? h + " h " + m.ToString("00") : m + ":" + r.ToString("00"); }
            dosStats["Interventions"].text = c.missions.ToString();
            dosStats["Réussies"].text = c.wins.ToString();
            dosStats["Meilleure note"].text = string.IsNullOrEmpty(c.bestGrade) ? "—" : c.bestGrade;
            dosStats["Meilleur score"].text = c.bestScore.ToString();
            dosStats["Série de réussites"].text = c.streak + (c.bestStreak > c.streak ? " (max " + c.bestStreak + ")" : "");
            dosStats["Temps en service"].text = T(c.time);
            dosStats["Interpellés"].text = c.arrests.ToString();
            dosStats["Neutralisés"].text = c.kills.ToString();
            dosStats["Précision"].text = (c.shots > 0 ? Mathf.RoundToInt(Mathf.Min(c.hits, c.shots) * 100f / c.shots) : 0) + " %";
            dosStats["Tirs à la tête"].text = c.heads.ToString();
            dosStats["Tirs injustifiés"].text = c.unjust.ToString(); dosStats["Tirs injustifiés"].color = c.unjust > 0 ? UIKit.Red : UIKit.TextC;
            dosStats["Record stand de tir"].text = c.trainHits > 0 ? c.trainHits + " · " + c.trainAcc + " %" : "—";
            foreach (Transform t in histBox) Destroy(t.gameObject);
            if (c.history.Count == 0) { Para(histBox, "Aucune intervention pour l’instant. Lance-toi !", 13, UIKit.Muted); return; }
            for (int i = 0; i < Mathf.Min(6, c.history.Count); i++)
            {
                var h = c.history[i];
                var d = new System.DateTime(h.date);
                var row = UIKit.Label(histBox, d.ToString("dd/MM HH:mm") + "   <b><color=" + (h.won ? "#ffb020" : "#ff5a50") + ">" + h.grade + "</color></b>   " + h.score + " pts   " + h.time / 60 + ":" + (h.time % 60).ToString("00") + "   interp. " + h.arrests + "   neutr. " + h.kills + "   " + (WeaponDef.All.TryGetValue(h.weapon ?? "", out var w) ? w.shortName : "CT-4"), 13, new Color(0.79f, 0.81f, 0.83f));
                row.supportRichText = true; UIKit.Size(row, 20, 20);
            }
        }

        // ---------------- pause ----------------
        void BuildPause()
        {
            var col = CardScreen("pause", 420, 380);
            Eyebrow(col, "Enregistrement suspendu");
            H2(col, "Pause", 40);
            UIKit.Button(col, "REPRENDRE", () => Game.I.Resume(), UIKit.BtnStyle.Primary);
            pauseArsenal = UIKit.Button(col, "Arsenal", () => { ArsenalView = Player.I.Weapons.Cur; Open("arsenal"); }).gameObject;
            UIKit.Button(col, "Paramètres", () => Open("settings"));
            UIKit.Button(col, "Menu principal", () => Game.I.LeaveToMenu());
        }

        // ---------------- fin ----------------
        void BuildEnd()
        {
            var col = CardScreen("end", 760, 540);
            endEyebrow = Eyebrow(col, "Fin d’intervention");
            var row = UIKit.Row(col, "note", 20);
            gradeLetter = UIKit.Label(row, "A", 96, UIKit.Amber, TextAnchor.MiddleCenter, FontStyle.Bold); UIKit.Size(gradeLetter, 110, 110, 110);
            var tcol = UIKit.Column(row, "titre", 6); UIKit.Size(tcol, -1, -1, 540, 1);
            endTitle = H2(tcol, "Zone sécurisée", 32);
            endText = Para(tcol, "", 14);
            var grid = new GameObject("grille", typeof(RectTransform)); grid.transform.SetParent(col, false);
            var gl = grid.AddComponent<GridLayoutGroup>(); gl.cellSize = new Vector2(232, 50); gl.spacing = new Vector2(2, 2); gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            UIKit.Size(grid.GetComponent<RectTransform>(), 158, 158);
            foreach (var k in new[] { "Score", "Durée", "Précision", "Interpellés", "Neutralisés", "Tirs à la tête", "Dégâts subis", "Tirs injustifiés", "Meilleur score" }) endStats[k] = StatCell(grid.transform, k);
            var saved = UIKit.Label(col, "<color=#6fd08c>●</color> PROGRESSION SAUVEGARDÉE", 11, UIKit.Muted); saved.supportRichText = true; UIKit.Size(saved, 16, 16);
            var btns = UIKit.Row(col, "boutons", 10);
            UIKit.Size(UIKit.Button(btns, "RECOMMENCER", () => Game.I.StartGame(Game.I.Mode), UIKit.BtnStyle.Primary), 46, 46, 200);
            UIKit.Size(UIKit.Button(btns, "Arsenal", () => { ArsenalView = Save.Data.loadout.primary; Open("arsenal"); }), 42, 42, 140);
            UIKit.Size(UIKit.Button(btns, "Menu principal", () => Game.I.LeaveToMenu()), 42, 42, 180);
        }

        public void ShowEnd(bool won, int score, string grade, MissionStats s)
        {
            back.Clear();
            Show("end");
            endEyebrow.text = (won ? "Fin d’intervention · caméra coupée" : "Transmission interrompue").ToUpperInvariant();
            endTitle.text = (won ? "Zone sécurisée" : "Agent à terre").ToUpperInvariant();
            endText.text = won
                ? (s.unjust > 0 ? "Zone sécurisée, mais l’enquête interne va examiner tes tirs injustifiés." : s.arrests >= 3 ? "Intervention exemplaire : plusieurs suspects interpellés vivants." : "Tous les suspects sont hors d’état de nuire. Le rapport part au commissariat.")
                : "Avance plus lentement, penche-toi aux angles, lance une grenade flash avant d’entrer et crie « Police ! ».";
            gradeLetter.text = grade;
            endStats["Score"].text = score.ToString();
            endStats["Durée"].text = ((int)s.time / 60) + ":" + ((int)s.time % 60).ToString("00");
            endStats["Précision"].text = (s.shots > 0 ? Mathf.RoundToInt(s.hits * 100f / s.shots) : 0) + " %";
            endStats["Interpellés"].text = s.arrests.ToString();
            endStats["Neutralisés"].text = s.kills + (Game.I.TeamKills > 0 ? " (+" + Game.I.TeamKills + " équipe)" : "");
            endStats["Tirs à la tête"].text = s.heads.ToString();
            endStats["Dégâts subis"].text = Mathf.RoundToInt(s.dmg).ToString();
            endStats["Tirs injustifiés"].text = s.unjust.ToString(); endStats["Tirs injustifiés"].color = s.unjust > 0 ? UIKit.Red : UIKit.TextC;
            endStats["Meilleur score"].text = Save.Data.career.bestScore.ToString();
        }
    }
}
