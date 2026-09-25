using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace E7
{
    /// <summary>
    /// Permet de brancher un autre « cerveau » (par exemple un agent entraîné par renforcement, dossier ML)
    /// à la place des règles écrites à la main. Voir Scripts/ML.
    /// </summary>
    public interface ISuspectBrain
    {
        /// <summary>Renvoie true si le cerveau a pris la main pour cette image.</summary>
        bool Decide(SuspectAI me, float dt);
    }

    /// <summary>
    /// Suspect armé. États : patrouille → recherche → combat ⇄ couverture / contournement,
    /// sonné (grenade flash), rendu, menotté, mort.
    /// Améliorations par rapport à la version web : NavMesh, vraies positions de couverture, tir en se relevant
    /// derrière un abri, contournement coordonné, réaction aux balles qui passent près (« suppression »),
    /// jetons de tir (pas plus de N tireurs à la fois), cibles multiples (joueur et coéquipiers).
    /// </summary>
    public class SuspectAI : Actor
    {
        public enum S { Patrol, Search, Combat, Cover, Flank, Stunned, Surrender, Arrested, Dead }
        public S state = S.Patrol;
        public int idx;
        public string wtype;
        public EnemyWeapon ew;
        public NavMeshAgent agent;
        public ISuspectBrain brain;

        public float yaw;
        public Vector3 lastKnown;
        public Threat target;
        public bool sees;
        float lostT, searchT, coverT, stunT, senseT, reaction, cool, shotT, wait, strafeT, strafe, suppression, peekT, stepPhase, relocateT;
        int burst, peeks;
        public int ammo;
        float reloadT;
        CoverPoint cover;
        bool flankRole;
        public string lastVoice;

        public override bool Alive => state != S.Dead;
        public bool Hostile => state != S.Dead && state != S.Surrender && state != S.Arrested;
        Difficulty D => Squad.I.diff;

        static readonly string[] Hoodies = { "#2d3440", "#4a4f3a", "#5b2c2c", "#1f1f22", "#3c4a5a", "#6b6b6b", "#3a2f45", "#2c4a3a" };
        static readonly string[] Pants = { "#2c3a55", "#1b1d22", "#5a5140", "#3a3f4a", "#24324a" };
        static readonly string[] Skins = { "#c59a7a", "#8d5f45", "#5a3a2a", "#e0b596", "#a77a5a" };
        static readonly string[] Shoes = { "#dedede", "#1a1a1a", "#7a2020", "#2a3a5a" };

        public static SuspectAI Create(Transform parent, Vector3 spawn, int idx, string weapon)
        {
            var go = new GameObject("Suspect_" + idx);
            go.transform.SetParent(parent, false);
            go.transform.position = spawn;
            var s = go.AddComponent<SuspectAI>();
            s.idx = idx; s.wtype = weapon; s.ew = EnemyWeapon.Get(weapon); s.ammo = s.ew.ammo;
            // tenue : chaque suspect a ses couleurs (matériaux propres)
            string style = idx % 3 == 1 ? "vest" : "hood";
            bool cap = idx % 2 == 0;
            var mats = new Dictionary<string, Material>();
            Material M(string k)
            {
                if (mats.TryGetValue(k, out var m)) return m;
                var baseM = E7Assets.Mat("human", k);
                m = new Material(baseM);
                if (k == "top") m.SetColor("_Color", U.Hex(Hoodies[idx % Hoodies.Length]));
                else if (k == "pants") m.SetColor("_Color", U.Hex(U.Pick(Pants)));
                else if (k == "skin") m.SetColor("_Color", U.Hex(U.Pick(Skins)));
                else if (k == "shoe") m.SetColor("_Color", U.Hex(U.Pick(Shoes)));
                else if (k == "glove") m = Random.value < 0.5f ? M("mask") : M("skin");
                else m = baseM;
                mats[k] = m; return m;
            }
            bool Keep(string tag) => tag == "base" || tag == "style:" + style || (cap && tag == "headgear:cap") || (!cap && tag == "headgear:none");
            s.body = Humanoid.Create(go.transform, "h_suspect", Keep, M, s);
            s.body.scale = Random.Range(0.93f, 1.03f);
            s.body.transform.localScale = Vector3.one * s.body.scale;
            s.body.AttachGun(HeldGun.World(weapon, s.body.transform));
            s.yaw = Random.Range(0, 360f);
            s.wait = Random.Range(0.5f, 3f);
            s.senseT = Random.value * 0.2f;
            s.cool = Random.Range(0.3f, 1f);
            s.flankRole = idx % 3 == 2;
            s.agent = go.AddComponent<NavMeshAgent>();
            s.agent.agentTypeID = Level.I.NavAgentType;
            s.agent.radius = 0.3f; s.agent.height = 1.75f; s.agent.speed = 1.15f; s.agent.acceleration = 12f; s.agent.angularSpeed = 0;
            s.agent.updateRotation = false; s.agent.stoppingDistance = 0.15f; s.agent.autoBraking = true;
            s.agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if (NavMesh.SamplePosition(spawn, out var hit, 2f, NavMesh.AllAreas)) s.agent.Warp(hit.position);
            if (Save.Data.settings.brain == "reseau" && MLHooks.MakeBrain != null) s.brain = MLHooks.MakeBrain(s);
            return s;
        }

        // ---------------- perception ----------------
        bool CanSee(Threat t)
        {
            var e = EyePos; var to = t.Eye - e; float dist = to.magnitude;
            float range = (t.LampOn ? 34 : 16) * (t.Crouch > 0.5f ? 0.8f : 1f);
            if (state == S.Combat || state == S.Cover || state == S.Flank) range = 45;
            if (dist > range) return false;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var flat = U.Flat(to).normalized;
            if (state != S.Combat && state != S.Cover && state != S.Flank && Vector3.Dot(fwd, flat) < 0.34f && dist > 2.2f) return false;
            return !Physics.Linecast(e, t.Eye, L.SightMask, QueryTriggerInteraction.Ignore) || !Physics.Linecast(e, t.Eye + Vector3.down * 0.45f, L.SightMask, QueryTriggerInteraction.Ignore);
        }

        void Sense()
        {
            bool was = sees;
            Threat best = null; float bd = 1e9f;
            foreach (var t in Squad.I.Threats())
            {
                if (!CanSee(t)) continue;
                float d = Vector3.Distance(t.Pos, Pos) * (t.player != null ? 0.85f : 1f); // léger penchant pour le joueur
                if (d < bd) { bd = d; best = t; }
            }
            sees = best != null;
            if (sees)
            {
                target = best; lastKnown = best.Pos; lostT = 0;
                if (state == S.Patrol || state == S.Search) Engage(false);
                else if (!was && state == S.Combat) reaction = Mathf.Max(reaction, D.react * 0.5f);
            }
        }

        public void Hear(Vector3 pos, bool loud)
        {
            if (!Hostile || state == S.Stunned) return;
            lastKnown = pos;
            if (state == S.Combat || state == S.Cover || state == S.Flank) return;
            if (state == S.Patrol && Random.value < 0.5f) Say(U.Pick(new[] { "p1", "p2", "p3" }), false);
            state = S.Search; searchT = loud ? 6 : 4; GoTo(pos);
        }

        public void Engage(bool instant)
        {
            if (!Hostile || state == S.Stunned) return;
            if (state != S.Combat)
            {
                state = S.Combat; lostT = 0; burst = 0;
                reaction = instant ? 0.3f : D.react * Random.Range(0.8f, 1.35f);
                if (Random.value < 0.7f) Say(U.Pick(new[] { "s1", "s2", "s3", "s4" }), false);
                Squad.Alert(this, target != null ? target.Pos : lastKnown);
            }
        }

        /// <summary>Balles qui passent près : le suspect se protège.</summary>
        public void Suppress(float amount, Vector3 shooter)
        {
            if (!Hostile || state == S.Stunned) return;
            suppression = Mathf.Min(3, suppression + amount);
            lastKnown = shooter;
            if (state == S.Patrol || state == S.Search) Engage(true);
            if (suppression > 1.4f && state == S.Combat && Random.value < 0.5f) GoCover();
        }

        public void Stun(float sec)
        {
            if (!Hostile) return;
            state = S.Stunned; stunT = sec; burst = 0; Stop();
            Squad.I.ReleaseToken(this);
            Say(U.Pick(new[] { "b1", "b2", "b3" }), true);
        }

        public void OnShout(Vector3 from)
        {
            float p;
            switch (state)
            {
                case S.Patrol: p = 0.5f; break;
                case S.Search: p = 0.35f; break;
                case S.Combat: p = 0.12f; break;
                case S.Cover: p = 0.2f; break;
                case S.Stunned: p = 0.75f; break;
                default: p = 0.2f; break;
            }
            p += (100 - hp) / 100f * 0.5f + D.surrender;
            if (ammo <= 0 || reloadT > 0) p += 0.15f;             // à court de munitions : plus enclin à se rendre
            if (Squad.I.mates.Exists(m => m.Alive && Vector3.Distance(m.Pos, Pos) < 10)) p += 0.1f; // encerclé
            if (Random.value < p) Surrender();
            else { lastKnown = from; if (state == S.Patrol || state == S.Search) Engage(false); }
        }

        public void Surrender()
        {
            state = S.Surrender; Stop();
            Squad.I.ReleaseToken(this);
            body.DropGun();
            body.surrendered = true;
            Say(U.Pick(new[] { "u1", "u2", "u3" }), true);
            Game.I.CheckEnd();
        }

        public void Arrest(bool byPlayer)
        {
            state = S.Arrested; body.arrested = true;
            AudioSys.I.Sfx3D("cuffs", Pos, 0.7f, false);
        }

        void Say(string id, bool force)
        {
            if (!AudioSys.SuspectLines.TryGetValue(id, out var text)) return;
            if (!Hud.CanEnemyTalk(force, Pos)) return;
            lastVoice = id;
            Hud.Subtitle("SUSPECT", text, "enemy");
            AudioSys.I.SuspectVoice(idx, id, EyePos);
        }

        // ---------------- dégâts ----------------
        public override DamageResult Damage(string part, Vector3 point, Vector3 dir, WeaponDef w, bool byPlayer)
        {
            if (!Alive) return default;
            bool unjust = state == S.Surrender || state == S.Arrested;
            float dmg = part == "head" ? w.dmgHead : part == "torso" ? w.dmgTorso : w.dmgLimb;
            hp -= dmg;
            OnHurt?.Invoke(dmg);
            FX.I.Blood(point, dir, part == "head");
            AudioSys.I.Sfx3D("impact_flesh", point, 0.7f, false);
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            body.flinch = 0.35f; body.flinchSide = Mathf.Sign(Vector3.Dot(dir, right)); if (body.flinchSide == 0) body.flinchSide = 1;
            if (hp <= 0) { Kill(dir); return new DamageResult { kill = true, head = part == "head", unjust = unjust && byPlayer }; }
            if (!unjust && state != S.Stunned)
            {
                if (Random.value < 0.5f) Say(U.Pick(new[] { "h1", "h2", "h3" }), false);
                lastKnown = byPlayer && Player.I ? Player.I.transform.position : point - dir * 10;
                if (hp < 60 && Random.value < 0.6f) GoCover(); else Engage(true);
                // blessé, à court de munitions et acculé : il peut se rendre de lui-même
                if (hp < 35 && Random.value < 0.25f + D.surrender) Surrender();
            }
            return new DamageResult { unjust = unjust && byPlayer };
        }

        void Kill(Vector3 dir)
        {
            state = S.Dead; Stop();
            Squad.I.ReleaseToken(this);
            if (agent) agent.enabled = false;
            body.DropGun();
            body.StartDeath(dir, yaw);
            AudioSys.I.Sfx3D("body", Pos, 0.6f);
            Invoke(nameof(BloodPool), 1.4f);
        }

        void BloodPool() { if (body && body.chest) FX.I.Pool(body.chest.position); }

        // ---------------- déplacements ----------------
        void GoTo(Vector3 p) { if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(p); } }
        void Stop() { if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } }
        bool Arrived => agent == null || !agent.enabled || !agent.isOnNavMesh || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f);

        void FaceTo(Vector3 p, float k)
        {
            float want = Mathf.Atan2(p.x - Pos.x, p.z - Pos.z) * Mathf.Rad2Deg;
            yaw += Mathf.DeltaAngle(yaw, want) * k;
        }

        /// <summary>Cherche un abri caché de la cible, pas trop loin, pas trop près d'elle.</summary>
        CoverPoint FindCover(Vector3 threatEye, float maxDist = 12)
        {
            CoverPoint best = null; float bestScore = 1e9f;
            foreach (var c in Level.I.Covers)
            {
                if (c.owner != null && c.owner != (object)this) continue;
                float d = Vector3.Distance(c.pos, Pos);
                if (d > maxDist || Vector3.Distance(c.pos, threatEye) < 4) continue;
                float h = c.low ? 0.8f : 1.2f;
                if (!Physics.Linecast(threatEye, c.pos + Vector3.up * h, L.SightMask, QueryTriggerInteraction.Ignore)) continue;
                // l'abri doit se trouver entre le suspect et la menace (sinon on s'expose en y allant)
                float score = d + (Vector3.Dot(c.towards, (threatEye - c.pos).normalized) > 0.2f ? 0 : 4);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        void GoCover()
        {
            var eye = target != null && target.Alive ? target.Eye : lastKnown + Vector3.up * 1.5f;
            var c = FindCover(eye);
            if (c != null)
            {
                if (cover != null) cover.owner = null;
                cover = c; c.owner = this;
                state = S.Cover; GoTo(c.pos); coverT = Random.Range(1.5f, 3f); peeks = 0;
                Squad.I.ReleaseToken(this);
            }
            else Engage(true);
        }

        /// <summary>Contournement : un point d'où l'on voit la dernière position connue, par un autre côté.</summary>
        bool TryFlank()
        {
            var eye = lastKnown + Vector3.up * 1.5f;
            var cur = U.Flat(Pos - lastKnown).normalized;
            Vector3? best = null; float bs = 1e9f;
            foreach (var t in Level.I.WalkTiles)
            {
                var p = Level.TileCenter(t.x, t.y);
                float d = Vector3.Distance(p, lastKnown);
                if (d < 5 || d > 12) continue;
                var dir = U.Flat(p - lastKnown).normalized;
                if (Vector3.Dot(dir, cur) > 0.3f) continue; // même côté : pas un contournement
                if (Physics.Linecast(p + Vector3.up * 1.5f, eye, L.SightMask, QueryTriggerInteraction.Ignore)) continue;
                float s = Vector3.Distance(p, Pos);
                if (s < bs) { bs = s; best = p; }
            }
            if (best == null) return false;
            state = S.Flank; GoTo(best.Value); relocateT = 8f;
            Squad.I.ReleaseToken(this);
            return true;
        }

        // ---------------- tir ----------------
        void Shoot()
        {
            if (body.gun == null || target == null) return;
            var from = body.gun.Muzzle.position;
            Squad.I.MuzzleFlash(from, wtype);
            var eye = target.Eye;
            float dist = Vector3.Distance(from, eye);
            bool occ = Player.I && Physics.Linecast(from, Player.I.Eye, L.SightMask, QueryTriggerInteraction.Ignore);
            AudioSys.I.EnemyShot(from, ew.snd, occ);
            Squad.Hear(from, 12, true);
            ammo--;
            float chance = D.acc * ew.acc * Mathf.Clamp(1.25f - dist / 28f, 0.25f, 1.15f);
            float dmgK = ew.dmg;
            if (ew.range > 0 && dist > ew.range) { chance *= 0.45f; dmgK *= 0.4f; }
            if (target.Moving) chance *= target.Sprinting ? 0.55f : 0.75f;
            if (target.Crouch > 0.5f) chance *= 0.8f;
            if (agent && agent.velocity.magnitude > 0.5f) chance *= 0.7f;
            chance *= 1f - 0.3f * Mathf.Min(1f, suppression);
            if (Random.value < chance && !Physics.Linecast(from, eye, L.SightMask, QueryTriggerInteraction.Ignore))
            {
                float dealt = Random.Range(D.dmgMin, D.dmgMax) * dmgK * (target.player != null ? 1f : 0.7f);
                target.Hurt(dealt, Pos);
                OnDealt?.Invoke(dealt, !target.Alive);
            }
            else
            {
                var aim = eye + new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(-0.7f, 0.6f), Random.Range(-0.9f, 0.9f));
                var dir = (aim - from).normalized;
                if (Physics.Raycast(from, dir, out var h, 60, L.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    var surf = h.collider.GetComponent<Surface>();
                    FX.I.Impact(h.point, h.normal, surf ? surf.kind : "concrete");
                }
                if (Player.I && target.player != null && dist < 25) AudioSys.I.Play2D(AudioSys.I.Bank("crack"), 0.6f * Save.Data.settings.vSfx, Random.Range(0.9f, 1.1f), Mathf.Clamp(Player.I.RelAngle(from), -1, 1));
            }
        }

        void Reload()
        {
            if (reloadT > 0 || ammo >= ew.ammo) return;
            reloadT = 2.6f;
            if (Random.value < 0.6f) Say("r1", false);
            AudioSys.I.Sfx3D("enemyReload", Pos, 0.45f);
        }

        // ---------------- boucle ----------------
        void Update()
        {
            if (Game.I == null || Game.I.State != GameState.Playing && Game.I.State != GameState.Dead) return;
            float dt = Time.deltaTime, k = 1 - Mathf.Exp(-dt * 9);
            suppression = Mathf.Max(0, suppression - dt * 0.6f);
            bool aiming = false;
            if (state == S.Dead) { body.Pose(dt, 0, Vector3.zero, false); return; }
            if (state == S.Surrender || state == S.Arrested)
            {
                body.kneel = Mathf.Lerp(body.kneel, 1, 1 - Mathf.Exp(-dt * 4));
                if (Player.I) FaceTo(Player.I.transform.position, 1 - Mathf.Exp(-dt * 2));
                Finish(dt, false);
                return;
            }
            if (state == S.Stunned)
            {
                stunT -= dt;
                yaw += Mathf.Sin(Time.time / 0.18f + idx) * dt * 1.5f * Mathf.Rad2Deg;
                body.stunned = true;
                Finish(dt, false);
                if (stunT <= 0) { body.stunned = false; state = S.Combat; reaction = 0.8f; lostT = 0; }
                return;
            }
            senseT -= dt;
            if (senseT <= 0) { senseT = 0.12f; Sense(); }
            if (target != null && !target.Alive) { target = null; sees = false; }
            if (reloadT > 0) { reloadT -= dt; if (reloadT <= 0) ammo = ew.ammo; }
            body.reloadT = reloadT;
            if (brain != null && brain.Decide(this, dt)) { Finish(dt, sees); return; }
            switch (state)
            {
                case S.Patrol:
                    agent.speed = 1.15f;
                    body.lookAround = false;
                    if (Arrived)
                    {
                        wait -= dt;
                        yaw += Mathf.Sin(Time.time / 1.4f + idx) * dt * 0.4f * Mathf.Rad2Deg;
                        if (wait <= 0)
                        {
                            var t = Level.I.WalkTiles[Random.Range(0, Level.I.WalkTiles.Count)];
                            var p = Level.TileCenter(t.x, t.y);
                            if (Vector3.Distance(p, Pos) < 14) GoTo(p);
                            wait = Random.Range(2f, 5f);
                        }
                    }
                    break;
                case S.Search:
                    agent.speed = 2.6f;
                    body.lookAround = true;
                    if (Arrived) { yaw += dt * 1.3f * Mathf.Rad2Deg; searchT -= dt; if (searchT <= 0) { state = S.Patrol; wait = 1; } }
                    break;
                case S.Cover:
                    agent.speed = 3.3f;
                    if (Arrived)
                    {
                        FaceTo(lastKnown, 1 - Mathf.Exp(-dt * 4));
                        if (ammo < ew.ammo && (ammo < ew.ammo / 2 || peeks > 1)) Reload();
                        coverT -= dt;
                        bool low = cover != null && cover.low;
                        if (reloadT <= 0 && coverT <= 0)
                        {
                            // se relever (abri bas) ou sortir de l'abri (abri haut) pour tirer, puis revenir
                            peekT += dt;
                            body.crouch = Mathf.Lerp(body.crouch, low ? 0 : 0.2f, k);
                            if (!low && cover != null && peekT < 0.1f) agent.Move(Quaternion.Euler(0, yaw, 0) * Vector3.right * (idx % 2 == 0 ? 0.6f : -0.6f));
                            if (sees && target != null) { aiming = true; FaceTo(target.Pos, k); CombatFire(dt); }
                            if (peekT > (sees ? 2.2f : 1.2f))
                            {
                                peekT = 0; peeks++; coverT = Random.Range(1.2f, 2.5f);
                                if (!low && cover != null) GoTo(cover.pos);
                                if (peeks > 3 || suppression < 0.2f && hp > 60) { if (!flankRole || !TryFlank()) { state = S.Combat; reaction = 0.35f; lostT = 0; } }
                            }
                        }
                        else body.crouch = Mathf.Lerp(body.crouch, 1, k);
                    }
                    break;
                case S.Flank:
                    agent.speed = 3.0f;
                    relocateT -= dt;
                    if (sees && target != null) { state = S.Combat; reaction = 0.2f; }
                    else if (Arrived || relocateT <= 0) { state = S.Search; searchT = 3; GoTo(lastKnown); }
                    break;
                case S.Combat:
                    agent.speed = 2.4f;
                    if (sees && target != null)
                    {
                        aiming = true;
                        FaceTo(target.Pos, k);
                        reaction -= dt;
                        // se déplacer un peu de côté de temps en temps (plus dur à toucher)
                        strafeT -= dt;
                        if (strafeT <= 0) { strafe = U.Pick(new[] { -1f, 0f, 0f, 1f }); strafeT = Random.Range(0.6f, 1.6f); }
                        if (strafe != 0 && agent.isOnNavMesh) agent.Move(Quaternion.Euler(0, yaw, 0) * Vector3.right * strafe * 1.3f * dt);
                        if (ammo <= 0) GoCover();
                        else if (!Squad.I.RequestToken(this))
                        {
                            // pas le droit de tirer maintenant : se mettre à couvert ou contourner
                            if (flankRole) { if (!TryFlank()) GoCover(); } else if (Random.value < dt * 0.8f) GoCover();
                        }
                        else CombatFire(dt);
                        if (suppression > 1.8f) GoCover();
                    }
                    else
                    {
                        lostT += dt;
                        Squad.I.ReleaseToken(this);
                        if (lostT > 0.9f)
                        {
                            if (flankRole && Squad.I.suspects.Exists(o => o != this && o.state == S.Combat && o.sees)) { if (!TryFlank()) { state = S.Search; searchT = 4; GoTo(lastKnown); } }
                            else { state = S.Search; searchT = 4; GoTo(lastKnown); }
                        }
                    }
                    break;
            }
            if (state != S.Cover) body.crouch = Mathf.Lerp(body.crouch, 0, 1 - Mathf.Exp(-dt * 6));
            if (state != S.Cover && cover != null) { cover.owner = null; cover = null; }
            if (state == S.Cover && agent.hasPath) body.crouch = Mathf.Lerp(body.crouch, 0.3f, k);
            Finish(dt, aiming || (state == S.Cover && Arrived && reloadT <= 0));
        }

        void CombatFire(float dt)
        {
            if (reaction > 0 || ammo <= 0) return;
            shotT -= dt;
            if (burst > 0) { if (shotT <= 0) { Shoot(); burst--; shotT = ew.gap; } }
            else
            {
                cool -= dt;
                if (cool <= 0) { burst = Mathf.Min(ammo, Random.Range(ew.burstMin, ew.burstMax + 1)); cool = D.cool * Random.Range(0.7f, 1.5f); }
            }
        }

        void Finish(float dt, bool aiming)
        {
            // direction du corps : vers le mouvement si on ne vise pas
            if (!aiming && agent && agent.enabled && agent.velocity.sqrMagnitude > 0.05f && state != S.Surrender && state != S.Arrested)
                FaceTo(Pos + agent.velocity, 1 - Mathf.Exp(-dt * 7));
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            float sp = agent && agent.enabled ? U.Flat(agent.velocity).magnitude : 0;
            body.speed = sp;
            var aimPt = target != null ? target.Eye : lastKnown + Vector3.up * 1.5f;
            body.Pose(dt, aiming ? 1 : 0, aimPt, sp > 0.1f);
            float prev = stepPhase; stepPhase += dt * sp * 3.1f;
            if (Mathf.FloorToInt(stepPhase / Mathf.PI) != Mathf.FloorToInt(prev / Mathf.PI) && sp > 0.2f) AudioSys.I.EnemyStep(Pos, sp > 2);
        }

        void OnDestroy() { if (cover != null) cover.owner = null; }

        // =====================================================================
        // Commandes pour un cerveau externe (IA par renforcement, dossier ML)
        // =====================================================================
        /// <summary>Appelé quand le suspect perd des points de vie (valeur = dégâts).</summary>
        public System.Action<float> OnHurt;
        /// <summary>Appelé quand le suspect touche sa cible (dégâts, cible tuée ?).</summary>
        public System.Action<float, bool> OnDealt;
        public float Suppression => suppression;
        public bool IsReloading => reloadT > 0;
        public float Crouch => body.crouch;

        /// <summary>Se déplacer dans une direction (repère du monde), vitesse en m/s.</summary>
        public void BrainMove(Vector3 worldDir, float speed)
        {
            if (!agent || !agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.Move(U.Flat(worldDir).normalized * Mathf.Clamp(speed, 0, 3.3f) * Time.deltaTime * Mathf.Min(1f, worldDir.magnitude));
        }

        /// <summary>Tourner (degrés par seconde, signe = sens).</summary>
        public void BrainTurn(float degPerSec) { yaw += degPerSec * Time.deltaTime; }

        public void BrainCrouch(bool on) { body.crouch = Mathf.Lerp(body.crouch, on ? 1 : 0, 1 - Mathf.Exp(-Time.deltaTime * 6)); }

        public void BrainReload() { Reload(); }

        /// <summary>Tire une rafale si c'est possible (cible visible, munitions, pas en rechargement). Renvoie vrai si un tir part.</summary>
        public bool BrainShoot(float dt)
        {
            if (!sees || target == null || reloadT > 0 || ammo <= 0) return false;
            // l'arme ne tire que si le corps est à peu près tourné vers la cible
            float want = Mathf.Atan2(target.Pos.x - Pos.x, target.Pos.z - Pos.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, want)) > 25) return false;
            shotT -= dt;
            if (shotT > 0) return false;
            Shoot(); shotT = ew.gap * 1.5f;
            return true;
        }

        /// <summary>Abri libre le plus proche (pour les observations).</summary>
        public CoverPoint NearestCover()
        {
            CoverPoint best = null; float bd = 1e9f;
            foreach (var c in Level.I.Covers) { float d = Vector3.Distance(c.pos, Pos); if (d < bd) { bd = d; best = c; } }
            return best;
        }
    }
}
