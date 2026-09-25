using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>
    /// Armes à la première personne : modèles détaillés (exportés de la version web), mains, visée,
    /// balancement, recul, tir, rechargement animé, pompe, culasse, changement d'arme, lancer de grenade, laser.
    /// Repère : l'arme pointe vers +z (avant de la caméra), x à droite, y en haut.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        class View
        {
            public string key, sig; public GameObject go;
            public Transform mag, slide, pump, handle;
            public WeaponMeta meta; public Vector3 sight, muzzle; public float adsDist; public Vector3? laser;
            public ReloadAnim tac, emp;
        }
        class ReloadState { public string kind; public float t; public bool empty, stop, inserted; public ReloadAnim R; public int ev; public string phase; }
        class SwitchState { public float t; public string phase, to; }

        public Player player;
        Camera cam, vmCam;
        Transform gunRoot, gunBody, holder, gloveR, gloveL, sleeveR, sleeveL, leftArm, handShell, handBang;
        GameObject handsModel;
        readonly Dictionary<string, View> cache = new Dictionary<string, View>();
        View W;
        readonly Dictionary<string, WeaponMeta> metas = new Dictionary<string, WeaponMeta>();

        // état
        public readonly Dictionary<string, int> mag = new Dictionary<string, int>(), reserve = new Dictionary<string, int>();
        public readonly Dictionary<string, string> fireMode = new Dictionary<string, string>();
        public string primary = "carbine", curSlot = "primary";
        public string Cur => curSlot == "primary" ? primary : "pistol";
        public WeaponDef Def => WeaponDef.All[Cur];
        float fireT, pumpT, slideT, inspectT, throwT, flashT;
        public float wallT, adsT, sprintT, recoil, recoilYaw, spreadBloom;
        bool slideLocked, triggerFresh, wantAds;
        ReloadState reload; SwitchState sw;
        public int flashbangs = 2;
        public bool Reloading => reload != null;
        public bool Busy => sw != null || throwT > 0;
        public bool LampOn { get; set; } = true;
        Vector2 swayV;
        public bool previewMode;

        // effets
        Transform muzzleQuad; Light vmFlash, worldFlash; LineRenderer laserLine; Transform laserDot;
        Material lensMat, laserMat;

        public void Init(Player p, Camera main, Camera vm)
        {
            player = p; cam = main; vmCam = vm;
            gunRoot = U.Child(vm.transform, "gunRoot").transform;
            gunBody = U.Child(gunRoot, "gunBody").transform;
            holder = U.Child(gunBody, "weaponHolder").transform;
            BuildHands();
            // flamme de bouche (vue) + lumières du tir
            var mq = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(mq.GetComponent<Collider>());
            mq.name = "flamme"; mq.transform.SetParent(gunBody, false);
            mq.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.UnlitMat(new Color(3f, 2.4f, 1.6f, 1f), E7Assets.Tex("flash"), true);
            mq.layer = L.Viewmodel; muzzleQuad = mq.transform; mq.SetActive(false);
            vmFlash = U.Child(gunBody, "vmFlash").AddComponent<Light>();
            vmFlash.type = LightType.Point; vmFlash.color = new Color(1f, 0.69f, 0.38f); vmFlash.range = 2f; vmFlash.intensity = 0; vmFlash.cullingMask = 1 << L.Viewmodel;
            worldFlash = U.Child(main.transform, "worldFlash", new Vector3(0.1f, -0.15f, 0.9f)).AddComponent<Light>();
            worldFlash.type = LightType.Point; worldFlash.color = new Color(1f, 0.69f, 0.38f); worldFlash.range = 14f; worldFlash.intensity = 0; worldFlash.cullingMask = ~(1 << L.Viewmodel);
            // laser
            var lgo = new GameObject("laser"); lgo.transform.SetParent(transform, false);
            laserLine = lgo.AddComponent<LineRenderer>();
            laserMat = E7Assets.UnlitMat(new Color(8f, 0.4f, 0.3f, 0.5f), null, true);
            laserLine.sharedMaterial = laserMat; laserLine.positionCount = 2; laserLine.widthMultiplier = 0.003f; laserLine.useWorldSpace = true;
            laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; laserLine.enabled = false;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform, false); dot.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.UnlitMat(new Color(6f, 0.5f, 0.4f, 1f), E7Assets.Tex("dot"), true);
            laserDot = dot.transform; dot.SetActive(false);
            lensMat = E7Assets.Mat("gun", "lens");
            foreach (var k in WeaponDef.All.Keys) metas[k] = WeaponMeta.Load(k);
            ResetInventory(false);
        }

        void BuildHands()
        {
            var md = E7Assets.Model("vm_hands");
            if (md == null) return;
            handsModel = md.Build("mains", t => true, k => E7Assets.Mat("gun", k), L.Viewmodel, false);
            handsModel.transform.SetParent(gunBody, false);
            gloveR = handsModel.transform.Find("glove_r"); gloveL = handsModel.transform.Find("glove_l");
            sleeveR = handsModel.transform.Find("sleeve_r"); sleeveL = handsModel.transform.Find("sleeve_l");
            leftArm = U.Child(gunBody, "leftArm").transform;
            if (gloveL) { gloveL.SetParent(leftArm, false); gloveL.localPosition = Vector3.zero; gloveL.localRotation = Quaternion.identity; }
            if (sleeveL) { sleeveL.SetParent(leftArm, false); sleeveL.localPosition = new Vector3(-0.01f, 0, -0.01f); }
            if (gloveR) gloveR.SetParent(gunBody, true);
            if (sleeveR) sleeveR.SetParent(gunBody, true);
            // cartouche et grenade tenues dans la main gauche
            handShell = U.Child(gloveL ? gloveL : leftArm, "cartouche", new Vector3(0, 0.03f, 0.07f)).transform;
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(sh.GetComponent<Collider>());
            sh.transform.SetParent(handShell, false); sh.transform.localRotation = Quaternion.Euler(90, 0, 0); sh.transform.localScale = new Vector3(0.021f, 0.025f, 0.021f);
            sh.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.Mat("gun", "shellRed");
            handBang = U.Child(gloveL ? gloveL : leftArm, "grenade", new Vector3(0, 0.03f, 0.05f)).transform;
            var bg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(handBang, false); bg.transform.localScale = new Vector3(0.048f, 0.05f, 0.048f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.Mat("gun", "bang");
            U.SetLayer(gunRoot.gameObject, L.Viewmodel);
            handShell.gameObject.SetActive(false); handBang.gameObject.SetActive(false);
            ApplyCharacterColors();
        }

        public void ApplyCharacterColors()
        {
            var ch = Save.Data.character;
            var gloves = new Dictionary<string, string> { { "noir", "#17181a" }, { "coyote", "#8a7556" }, { "olive", "#4b5237" } };
            var tops = new Dictionary<string, string> { { "police", "#1d2736" }, { "bri", "#151617" }, { "olive", "#3d4232" }, { "urbain", "#4a4e55" } };
            E7Assets.Mat("gun", "glove").SetColor("_Color", U.Hex(gloves.TryGetValue(ch.gloves ?? "", out var g) ? g : "#17181a"));
            E7Assets.Mat("gun", "sleeve").SetColor("_Color", U.Hex(tops.TryGetValue(ch.uniform ?? "", out var t) ? t : "#1d2736"));
        }

        public void SetHandsVisible(bool v) { if (handsModel) { handsModel.SetActive(v); } if (gloveR) gloveR.gameObject.SetActive(v); if (sleeveR) sleeveR.gameObject.SetActive(v); leftArm.gameObject.SetActive(v); }

        // ---------------- vues des armes ----------------
        View ViewFor(string key)
        {
            var a = Save.Data.loadout.For(key);
            string sig = key + "|" + a.optic + "|" + a.muzzle + "|" + a.rail;
            if (cache.TryGetValue(sig, out var v)) return v;
            var md = E7Assets.Model("w_" + key);
            var meta = metas[key];
            v = new View { key = key, sig = sig, meta = meta };
            if (md != null)
            {
                bool Keep(string tag)
                {
                    if (tag == "base" || tag == "sling") return true;
                    int i = tag.IndexOf(':'); if (i < 0) return true;
                    string dim = tag.Substring(0, i), opt = tag.Substring(i + 1);
                    return dim == "optic" ? opt == a.optic : dim == "muzzle" ? opt == a.muzzle : dim == "rail" ? opt == a.rail : true;
                }
                v.go = md.Build("arme_" + key, Keep, k => E7Assets.Mat("gun", k), L.Viewmodel, false);
                v.go.transform.SetParent(holder, false);
                v.mag = v.go.transform.Find("mag"); v.slide = v.go.transform.Find("slide"); v.pump = v.go.transform.Find("pump"); v.handle = v.go.transform.Find("handle");
                v.go.SetActive(false);
            }
            else { v.go = new GameObject("arme_" + key); v.go.transform.SetParent(holder, false); }
            v.sight = meta.optics.TryGetValue(a.optic, out var o) ? o.sight : new Vector3(0, 0.09f, 0);
            v.adsDist = meta.optics.TryGetValue(a.optic, out var o2) ? o2.adsDist : 0.2f;
            v.muzzle = meta.muzzles.TryGetValue(a.muzzle, out var mz) ? mz : new Vector3(0, 0.03f, 0.6f);
            if (a.rail == "laser" && meta.lasers.TryGetValue("laser", out var lz)) v.laser = lz;
            var ra = key == "pistol" ? ReloadAnim.Pistol() : ReloadAnim.Mag(key == "smg" ? 1.05f : 1f);
            v.tac = ra.tactical; v.emp = ra.empty;
            cache[sig] = v;
            return v;
        }

        public void InvalidateViews()
        {
            foreach (var v in cache.Values) if (v.go) Destroy(v.go);
            cache.Clear(); W = null;
            Equip(Cur);
        }

        public void Equip(string key)
        {
            if (W != null && W.go) W.go.SetActive(false);
            W = ViewFor(key);
            W.go.SetActive(true);
            if (W.mag) { W.mag.localPosition = Vector3.zero; W.mag.gameObject.SetActive(true); }
            if (W.handle) W.handle.localPosition = Vector3.zero;
            if (W.pump) W.pump.localPosition = Vector3.zero;
            if (W.slide) W.slide.localPosition = Vector3.zero;
            if (gloveR) { gloveR.localPosition = W.meta.gripPos; gloveR.localRotation = W.meta.gripRot; }
            if (sleeveR) sleeveR.localPosition = W.meta.gripPos + new Vector3(-0.004f, -0.03f, -0.03f);
            muzzleQuad.localPosition = W.muzzle + new Vector3(0, 0, 0.04f);
            vmFlash.transform.localPosition = W.muzzle;
        }

        public void ResetInventory(bool training)
        {
            primary = Save.Data.loadout.primary;
            foreach (var d in WeaponDef.All.Values) { mag[d.key] = d.mag; reserve[d.key] = d.reserve; fireMode[d.key] = d.auto ? "auto" : "semi"; }
            curSlot = "primary";
            reload = null; sw = null; pumpT = slideT = inspectT = throwT = wallT = fireT = 0; slideLocked = false;
            recoil = recoilYaw = spreadBloom = 0;
            flashbangs = training ? 99 : 2;
            LampOn = true;
            Equip(Cur);
        }

        public void RefillReserve() { reserve[primary] += WeaponDef.All[primary].mag * 2; reserve["pistol"] += 34; }

        // ---------------- actions ----------------
        public void ToggleFireMode()
        {
            if (!Def.auto) { Hud.Note("Cette arme tire coup par coup"); return; }
            fireMode[Cur] = fireMode[Cur] == "auto" ? "semi" : "auto";
            AudioSys.I.Sfx("click", 0.6f, 1.3f);
            Hud.Note(fireMode[Cur] == "auto" ? "Mode rafale" : "Mode coup par coup");
        }

        public void StartReload()
        {
            var d = Def;
            if (reload != null || Busy || !player.Alive || mag[Cur] >= d.mag) return;
            if (reserve[Cur] <= 0) { Hud.Note("Plus de munitions pour cette arme"); return; }
            inspectT = 0;
            if (d.pump) { reload = new ReloadState { kind = "shell", phase = "in", empty = mag[Cur] == 0 }; return; }
            bool empty = mag[Cur] == 0;
            reload = new ReloadState { kind = "mag", empty = empty, R = empty ? W.emp : W.tac };
        }

        void FinishMagReload()
        {
            var d = Def; int take = Mathf.Min(d.mag - mag[Cur], reserve[Cur]);
            mag[Cur] += take; reserve[Cur] -= take;
            if (Game.I.Mode == GameMode.Training) reserve[Cur] = d.reserve;
            reload = null;
        }

        public void SwitchTo(string slot)
        {
            if (slot == curSlot || sw != null || !player.Alive || throwT > 0) return;
            if (reload != null) { reload = null; if (W.mag) { W.mag.localPosition = Vector3.zero; W.mag.gameObject.SetActive(true); } handShell.gameObject.SetActive(false); }
            inspectT = 0;
            sw = new SwitchState { phase = "down", to = slot };
            AudioSys.I.Sfx("holster", 0.7f);
        }

        public void Inspect() { if (reload != null || Busy || wantAds || inspectT > 0) return; inspectT = 0.001f; AudioSys.I.Sfx("rattle", 0.5f); }

        public void ThrowBang()
        {
            if (flashbangs <= 0) { Hud.Note("Plus de grenades flash"); return; }
            if (Busy || reload != null || !player.Alive) return;
            if (Game.I.Mode != GameMode.Training) flashbangs--;
            throwT = 0.001f; inspectT = 0;
            AudioSys.I.Sfx("pin", 0.7f);
        }

        // ---------------- boucle ----------------
        public void Tick(float dt, bool fireHeld, bool fireDown, bool aimHeld, Vector2 mouse)
        {
            wantAds = aimHeld;
            if (fireDown) triggerFresh = true;
            var d = Def;
            fireT -= dt;
            spreadBloom *= Mathf.Exp(-dt * 5);
            // changement d'arme
            if (sw != null)
            {
                sw.t += dt;
                if (sw.phase == "down" && sw.t >= 0.28f) { curSlot = sw.to; Equip(Cur); slideLocked = mag[Cur] == 0 && W.slide; sw.phase = "up"; sw.t = 0; AudioSys.I.Sfx("draw", 0.7f); }
                else if (sw.phase == "up" && sw.t >= 0.32f) sw = null;
            }
            if (inspectT > 0) { inspectT += dt; if (inspectT > 2.7f) inspectT = 0; }
            if (throwT > 0)
            {
                float was = throwT; throwT += dt;
                if (was < 0.33f && throwT >= 0.33f) FX.I.ThrowBang(cam.transform, player.Velocity);
                if (throwT > 0.62f) throwT = 0;
            }
            // pompe
            if (pumpT > 0)
            {
                float was = pumpT; pumpT += dt;
                if (was < 0.14f && pumpT >= 0.14f) AudioSys.I.Sfx("pumpBack");
                if (was < 0.26f && pumpT >= 0.26f) FX.I.EjectShell("shotgun", cam.transform, player.Velocity);
                if (was < 0.4f && pumpT >= 0.4f) AudioSys.I.Sfx("pumpFwd");
                if (pumpT > 0.58f) pumpT = 0;
            }
            if (slideT > 0) slideT = Mathf.Max(0, slideT - dt);
            // rechargement
            if (reload != null && reload.kind == "mag")
            {
                var R = reload.R; reload.t += dt;
                while (reload.ev < R.events.Length && reload.t >= R.events[reload.ev].t)
                {
                    string ev = R.events[reload.ev++].ev;
                    if (ev == "slideRel") slideLocked = false;
                    if ((ev == "magOut" || ev == "pMagOut") && reload.empty) FX.I.DropMag(Cur == "pistol", cam.transform, player.Velocity);
                    AudioSys.I.Sfx(ev, 0.9f);
                }
                if (reload.t >= R.dur) { FinishMagReload(); if (Cur == "pistol") slideLocked = false; }
            }
            else if (reload != null && reload.kind == "shell")
            {
                var r = reload; r.t += dt;
                if (r.phase == "in" && r.t >= 0.22f) { r.phase = "shell"; r.t = 0; }
                else if (r.phase == "shell")
                {
                    if (!r.inserted && r.t >= 0.4f)
                    {
                        r.inserted = true; mag[Cur]++; reserve[Cur]--;
                        if (Game.I.Mode == GameMode.Training) reserve[Cur] = d.reserve;
                        AudioSys.I.Sfx("shellIn");
                    }
                    if (r.t >= 0.5f)
                    {
                        r.inserted = false;
                        if (mag[Cur] < d.mag && reserve[Cur] > 0 && !r.stop) r.t = 0;
                        else { r.phase = "out"; r.t = 0; }
                    }
                }
                else if (r.phase == "out" && r.t >= 0.28f) { reload = null; if (r.empty) pumpT = 0.001f; }
            }
            bool canFire = reload == null && sw == null && throwT <= 0 && !player.Sprinting && wallT < 0.5f && player.Alive && !previewMode;
            if (fireHeld && fireT <= 0 && canFire) Fire();
            if (fireHeld && reload != null && reload.kind == "shell" && mag[Cur] > 0) reload.stop = true;
            // arme trop près d'un mur : on la relève
            bool near = false;
            var cf = cam.transform.forward;
            foreach (float dd in new[] { 0.35f, 0.55f, 0.75f }) if (Physics.CheckSphere(cam.transform.position + cf * dd, 0.05f, L.WorldMask, QueryTriggerInteraction.Ignore)) { near = true; break; }
            wallT = U.Damp(wallT, near && !wantAds ? 1 : 0, 10, dt);
            // visée
            bool canAds = wantAds && !player.Sprinting && reload == null && sw == null && throwT <= 0 && wallT < 0.5f;
            adsT = U.Damp(adsT, canAds ? 1 : 0, Cur == "pistol" ? 15 : Cur == "shotgun" ? 10 : 12, dt);
            sprintT = U.Damp(sprintT, player.Sprinting ? 1 : 0, 8, dt);
            recoil *= Mathf.Exp(-dt * 7); recoilYaw *= Mathf.Exp(-dt * 7);
            swayV.x = U.Damp(swayV.x, Mathf.Clamp(mouse.x * 0.0045f, -0.05f, 0.05f), 10, dt);
            swayV.y = U.Damp(swayV.y, Mathf.Clamp(-mouse.y * 0.0045f, -0.05f, 0.05f), 10, dt);
            Animate(dt, Time.time);
            UpdateFlash(dt);
            UpdateLaser();
        }

        // ---------------- tir ----------------
        void Fire()
        {
            var d = Def; string k = Cur; var att = Save.Data.loadout.For(k);
            if (mag[k] <= 0)
            {
                if (triggerFresh) { AudioSys.I.Sfx("dry"); Hud.Note("Vide · R pour recharger" + (curSlot == "primary" ? " ou 2 pour le pistolet" : "")); }
                triggerFresh = false; return;
            }
            if ((!d.auto || fireMode[k] == "semi") && !triggerFresh) return;
            if (d.pump && pumpT > 0) return;
            triggerFresh = false;
            mag[k]--;
            var st = Game.I.Stats; st.shots++;
            if (Game.I.Mode == GameMode.Training) Game.I.TrainShots++;
            fireT = d.interval;
            bool sup = att.muzzle == "suppressor";
            AudioSys.I.Shot(d.sound, sup);
            flashT = sup ? 0.02f : 0.045f;
            muzzleQuad.localScale = Vector3.one * d.flash * Random.Range(0.8f, 1.2f) * (sup ? 0.35f : 1f);
            float kick = d.kick * Random.Range(0.8f, 1.2f) * (adsT > 0.5f ? 0.7f : 1f) * (player.Crouch > 0.5f ? 0.75f : 1f);
            recoil += kick;
            player.AddLook(kick * 0.45f * Mathf.Rad2Deg, Random.Range(-1f, 1f) * d.kickYaw * 0.5f * Mathf.Rad2Deg);
            recoilYaw += Random.Range(-1f, 1f) * d.kickYaw;
            player.Shake(Mathf.Min(0.04f, kick * 0.6f));
            spreadBloom = Mathf.Min(0.045f, spreadBloom + d.kick * 0.7f);
            float laserK = att.rail == "laser" && LampOn ? 0.6f : 1f;
            float baseSpread = (adsT > 0.5f ? d.spreadAds : d.spreadHip * laserK) + spreadBloom * (adsT > 0.5f ? 0.4f : 1f) + (player.Moving ? 0.016f : 0) + (player.Sprinting ? 0.04f : 0);
            bool anyHit = false, killed = false, unjust = false;
            var hitActors = new HashSet<Actor>();
            var ct = cam.transform;
            for (int p = 0; p < d.pellets; p++)
            {
                float ang = (d.pellets > 1 && p > 0 ? d.pelletSpread * Mathf.Sqrt(Random.value) : 0) + baseSpread * Mathf.Sqrt(Random.value);
                float aa = Random.value * Mathf.PI * 2;
                var dir = (ct.forward + (ct.right * Mathf.Cos(aa) + ct.up * Mathf.Sin(aa)) * ang).normalized;
                if (!Physics.Raycast(ct.position, dir, out var h, d.pellets > 1 ? 60 : 120, L.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                var hb = h.collider.GetComponent<HitBox>();
                var tgt = h.collider.GetComponentInParent<TrainingTarget>();
                if (hb != null && hb.actor != null)
                {
                    anyHit = true;
                    var res = hb.actor.Damage(hb.part, h.point, dir, d, true);
                    hitActors.Add(hb.actor);
                    if (res.unjust) unjust = true;
                    if (res.kill) { killed = true; if (!res.unjust) { st.kills++; if (res.head) st.heads++; } }
                }
                else if (tgt != null)
                {
                    anyHit = true;
                    string zone = tgt.Hit(h.point);
                    if (p == 0 || d.pellets == 1) { Game.I.TrainHits++; if (zone == "head") Game.I.TrainHeads++; }
                    Hud.Hitmark(zone == "head");
                }
                else
                {
                    var surf = h.collider.GetComponent<Surface>();
                    FX.I.Impact(h.point, h.normal, surf ? surf.kind : "concrete");
                    Squad.BulletNear(h.point, player.transform.position);
                }
            }
            if (hitActors.Count > 0) st.hits++;
            if (Game.I.Mode == GameMode.Training && anyHit) st.hits++;
            if (unjust) { st.unjust++; Hud.Note("Tir injustifié : le suspect s’était rendu"); }
            if (hitActors.Count > 0) { Hud.Hitmark(killed); if (killed) Game.I.CheckEnd(); }
            Squad.Hear(player.transform.position, d.hear * (sup ? 0.35f : 1f), true);
            if (d.pump) pumpT = 0.001f;
            else { FX.I.EjectShell(k, ct, player.Velocity); if (W.slide) { slideT = 0.07f; if (mag[k] == 0) slideLocked = true; } }
        }

        void UpdateFlash(float dt)
        {
            flashT -= dt;
            bool on = flashT > 0;
            muzzleQuad.gameObject.SetActive(on);
            if (on) { muzzleQuad.rotation = vmCam.transform.rotation * Quaternion.Euler(0, 0, Random.Range(0, 360f)); }
            vmFlash.intensity = on ? 4f : 0f;
            worldFlash.intensity = on ? 6f : 0f;
            lensMat.SetColor("_Color", LampOn ? new Color(3f, 2.9f, 2.6f) : new Color(0.13f, 0.13f, 0.13f));
        }

        void UpdateLaser()
        {
            bool on = W != null && W.laser.HasValue && LampOn && sw == null && player.Alive && !previewMode && Game.I.State == GameState.Playing;
            laserLine.enabled = on; laserDot.gameObject.SetActive(false);
            if (!on) return;
            // point de départ : position du laser sur l'arme (vue) ramenée dans le monde
            var vmP = W.go.transform.TransformPoint(W.laser.Value);
            var local = vmCam.transform.InverseTransformPoint(vmP);
            var start = cam.transform.TransformPoint(local * 0.9f);
            var aimPt = cam.transform.TransformPoint(new Vector3(0, 0, 15));
            var dir = (aimPt - start).normalized;
            Vector3 end = start + dir * 60;
            if (Physics.Raycast(start, dir, out var h, 60, L.ShootMask, QueryTriggerInteraction.Ignore))
            {
                end = h.point;
                laserDot.gameObject.SetActive(true);
                laserDot.position = h.point - dir * 0.01f;
                laserDot.rotation = cam.transform.rotation;
                laserDot.localScale = Vector3.one * (0.035f + h.distance * 0.0025f);
            }
            laserLine.SetPosition(0, start); laserLine.SetPosition(1, end);
        }

        // ---------------- animation de la vue ----------------
        (Vector3 pos, Quaternion rot) Pose(string n) => W.meta.poses.TryGetValue(n, out var p) ? p : (W.meta.poses.TryGetValue("guard", out var g) ? g : (new Vector3(-0.01f, -0.04f, 0.3f), Quaternion.identity));

        (Vector3 pos, Quaternion rot) KeyLerp((float t, string pose)[] frames, float t)
        {
            var a = frames[0]; var b = frames[frames.Length - 1];
            for (int i = 0; i < frames.Length - 1; i++) if (t >= frames[i].t && t <= frames[i + 1].t) { a = frames[i]; b = frames[i + 1]; break; }
            if (t >= b.t) return Pose(b.pose);
            float k = U.Smooth(Mathf.Clamp01((t - a.t) / Mathf.Max(0.001f, b.t - a.t)));
            var A = Pose(a.pose); var B = Pose(b.pose);
            return (Vector3.Lerp(A.pos, B.pos, k), Quaternion.Slerp(A.rot, B.rot, k));
        }

        static Quaternion E(float rx, float ry, float rz) => Quaternion.Euler(-rx * Mathf.Rad2Deg, -ry * Mathf.Rad2Deg, rz * Mathf.Rad2Deg);

        void Animate(float dt, float t)
        {
            if (W == null || previewMode) return;
            string k = Cur;
            float ph = player.StepPhase, bob = player.BobAmount;
            var aim = -W.sight + new Vector3(0, 0, W.adsDist);
            var p = Vector3.Lerp(W.meta.hip, aim, adsT);
            float swayK = 1 - adsT * 0.6f;
            p.x += (Mathf.Cos(ph) * 0.012f * bob - swayV.x) * swayK + sprintT * 0.07f + Mathf.Sin(t * 0.8f) * 0.0012f * adsT;
            p.y += (-Mathf.Abs(Mathf.Sin(ph)) * 0.012f * bob + swayV.y) * swayK - sprintT * 0.06f + Mathf.Sin(t * 1.5f) * 0.0018f;
            p.z -= recoil * (1.2f + adsT * 0.4f) * (k == "shotgun" ? 0.6f : 1f);
            float rx = recoil * (k == "pistol" ? 3.2f : 2f) + swayV.y * 0.6f * swayK - sprintT * 0.35f;
            float ry = swayV.x * 0.8f * swayK + sprintT * 0.75f + (1 - adsT) * 0.04f;
            float rz = -sprintT * 0.3f - player.Lean * 0.05f + (1 - adsT) * 0.03f;
            float by = 0, bz = 0, brx = 0, bry = 0, brz = 0;
            if (sw != null) { float e = sw.phase == "down" ? U.Smooth(sw.t / 0.28f) : 1 - U.Smooth(sw.t / 0.32f); by -= 0.3f * e; brx -= 1.0f * e; brz += 0.3f * e; }
            if (inspectT > 0)
            {
                float q = inspectT;
                float a1 = U.Smooth((q - 0.1f) / 0.5f) * (1 - U.Smooth((q - 1.3f) / 0.4f));
                float a2 = U.Smooth((q - 1.4f) / 0.4f) * (1 - U.Smooth((q - 2.2f) / 0.4f));
                bry += 0.9f * a1; brz += 0.5f * a1; bz += 0.06f * a1; by += 0.02f * a1; brx += 0.6f * a2; by += 0.04f * a2; bz += 0.04f * a2;
            }
            if (wallT > 0) { brx += 0.95f * wallT; bz -= 0.1f * wallT; by += 0.02f * wallT; }
            float throwE = throwT > 0 ? Mathf.Sin(Mathf.Clamp01(throwT / 0.62f) * Mathf.PI) : 0;
            by -= 0.12f * throwE; brz -= 0.3f * throwE;
            // pompe, culasse, levier
            float pumpOff = pumpT > 0 && W.pump ? W.meta.pumpTravel * (pumpT < 0.27f ? U.Smooth((pumpT - 0.08f) / 0.19f) : 1 - U.Smooth((pumpT - 0.27f) / 0.2f)) : 0;
            if (W.pump) W.pump.localPosition = new Vector3(0, 0, -pumpOff);
            if (W.slide) W.slide.localPosition = new Vector3(0, 0, -(slideLocked ? W.meta.slideTravel : slideT > 0 ? W.meta.slideTravel * Mathf.Sin((1 - slideT / 0.07f) * Mathf.PI) : 0));
            // main gauche
            var lh = Pose("guard"); bool hasLh = true;
            handShell.gameObject.SetActive(false); handBang.gameObject.SetActive(false);
            if (reload != null && reload.kind == "mag")
            {
                var R = reload.R; float rt = reload.t;
                float tilt = Mathf.Sin(Mathf.Min(1, rt / R.dur * 1.15f) * Mathf.PI);
                rx += (k == "pistol" ? 0.1f : 0.18f) * tilt; rz += (k == "pistol" ? 0.35f : 0.5f) * tilt; p.y -= 0.03f * tilt;
                lh = KeyLerp(R.frames, rt);
                if (W.mag)
                {
                    bool held = rt > R.holdA && rt < R.holdB;
                    bool hidden = (rt > R.hideA && rt < R.hideB) || (reload.empty && rt > R.holdA + 0.02f && rt < R.hideB);
                    W.mag.gameObject.SetActive(!hidden);
                    W.mag.localPosition = held ? lh.pos - W.meta.magGrab + new Vector3(0.018f, 0, 0) : Vector3.zero;
                }
                if (W.handle && reload.empty && R.chargeA >= 0)
                {
                    float hz = rt > R.chargeA && rt < R.chargeB ? W.meta.handleTravel * Mathf.Sin((rt - R.chargeA) / (R.chargeB - R.chargeA) * Mathf.PI) : 0;
                    W.handle.localPosition = new Vector3(0, 0, -hz);
                    lh.pos.z -= hz;
                }
            }
            else if (reload != null && reload.kind == "shell")
            {
                var r = reload; rz += 0.35f; rx += 0.1f;
                if (r.phase == "in") { float e = U.Smooth(r.t / 0.22f); var A = Pose("guard"); var B = Pose("port"); lh = (Vector3.Lerp(A.pos, B.pos, e), Quaternion.Slerp(A.rot, B.rot, e)); }
                else if (r.phase == "shell") { lh = KeyLerp(new[] { (0f, "port"), (0.16f, "pouch"), (0.34f, "port"), (0.5f, "port") }, r.t); handShell.gameObject.SetActive(r.t > 0.14f && r.t < 0.42f); }
                else { float e = U.Smooth(r.t / 0.28f); var A = Pose("port"); var B = Pose("guard"); lh = (Vector3.Lerp(A.pos, B.pos, e), Quaternion.Slerp(A.rot, B.rot, e)); }
            }
            else if (W.mag) { W.mag.localPosition = Vector3.zero; W.mag.gameObject.SetActive(true); }
            if (reload == null && W.handle) W.handle.localPosition = Vector3.zero;
            if (W.pump && reload == null) lh.pos.z -= pumpOff;
            if (throwE > 0)
            {
                var tp = new Vector3(-0.2f, 0.05f + throwE * 0.08f, 0.28f + throwE * 0.15f);
                var tr = E(-0.6f, 0.2f, 0.8f);
                lh = (Vector3.Lerp(lh.pos, tp, throwE), Quaternion.Slerp(lh.rot, tr, throwE));
                handBang.gameObject.SetActive(throwT < 0.33f);
            }
            if (hasLh) { leftArm.localPosition = lh.pos; leftArm.localRotation = lh.rot; }
            gunBody.localPosition = new Vector3(0, by, bz);
            gunBody.localRotation = E(brx, bry, brz);
            gunRoot.localPosition = p;
            gunRoot.localRotation = E(rx, ry, rz);
            // avant-bras : toujours vers le bas et l'arrière, même quand l'arme tourne
            bool pis = k == "pistol";
            var qBody = gunBody.localRotation;
            var qR = Quaternion.FromToRotation(new Vector3(0, 0, -1), (pis ? new Vector3(0.3f, -0.55f, -0.75f) : new Vector3(0.25f, -0.8f, -0.55f)).normalized);
            if (sleeveR) sleeveR.localRotation = Quaternion.Inverse(qBody) * qR;
            var qL = Quaternion.FromToRotation(new Vector3(0, 0, -1), (pis ? new Vector3(-0.45f, -0.6f, -0.7f) : new Vector3(-0.35f, -0.75f, -0.55f)).normalized);
            if (sleeveL) sleeveL.localRotation = Quaternion.Inverse(qBody * leftArm.localRotation) * qL;
            vmCam.fieldOfView = Mathf.Lerp(50, 38, adsT);
        }

        /// <summary>Aperçu dans l'arsenal : l'arme tourne lentement, vue de profil gauche.</summary>
        public void Preview(string key, float t)
        {
            previewMode = true;
            if (W == null || W.key != key || W.sig != ViewFor(key).sig) Equip(key);
            SetHandsVisible(false);
            float D = W.meta.previewDist * 1.45f;
            bool narrow = Screen.width < Screen.height * 1.1f;
            holder.localPosition = new Vector3(0, 0, -W.meta.previewCenter);
            gunBody.localPosition = Vector3.zero; gunBody.localRotation = Quaternion.identity;
            gunRoot.localPosition = new Vector3(narrow ? 0 : D * 0.15f, narrow ? D * 0.14f : -0.01f, D);
            gunRoot.localRotation = Quaternion.Euler(-0.08f * Mathf.Rad2Deg, -(Mathf.PI / 2 + Mathf.Sin(t * 0.35f) * 0.3f) * Mathf.Rad2Deg, 0);
            if (W.mag) { W.mag.localPosition = Vector3.zero; W.mag.gameObject.SetActive(true); }
            vmCam.fieldOfView = 34;
            muzzleQuad.gameObject.SetActive(false);
        }

        public void EndPreview()
        {
            previewMode = false;
            holder.localPosition = Vector3.zero;
            SetHandsVisible(true);
            Equip(Cur);
        }

        /// <summary>Vrai pendant la visée avec un point rouge ou un holographique (pour le HUD).</summary>
        public bool AimingOptic => adsT > 0.8f && Save.Data.loadout.For(Cur).optic != "irons";
    }
}
