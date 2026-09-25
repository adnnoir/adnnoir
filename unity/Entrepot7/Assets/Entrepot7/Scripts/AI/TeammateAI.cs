using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace E7
{
    /// <summary>
    /// Coéquipier (policier IA) : suit le joueur en formation, tient une position sur ordre,
    /// repère et engage les suspects armés, crie « Police ! », menotte ceux qui se rendent.
    /// Touches : T = avec moi / tenez la position · Y = allez là où je vise.
    /// </summary>
    public class TeammateAI : Actor
    {
        public enum Order { Follow, Hold }
        public enum S { Move, Combat, Arrest, Down }
        public Order order = Order.Follow;
        public S state = S.Move;
        public int slot;
        public string callsign;
        NavMeshAgent agent;
        Vector3 holdPos;
        float yaw, senseT, shotT, cool, reaction, shoutCd, arrestT, stepPhase, speakCd;
        int burst, ammo = 30;
        float reloadT;
        SuspectAI target, arrestTarget;
        readonly HashSet<SuspectAI> warned = new HashSet<SuspectAI>();

        public override bool Alive => state != S.Down;
        public bool IsMoving => agent && agent.enabled && agent.velocity.magnitude > 0.4f;

        static readonly string[] Names = { "BRAVO", "CHARLIE", "DELTA" };

        public static TeammateAI Create(Transform parent, Vector3 spawn, int slot)
        {
            var go = new GameObject("Coequipier_" + slot);
            go.transform.SetParent(parent, false);
            go.transform.position = spawn;
            var m = go.AddComponent<TeammateAI>();
            m.slot = slot; m.callsign = Names[slot % Names.Length];
            var ch = Save.Data.character;
            var uniforms = new Dictionary<string, (string top, string vest)> { { "police", ("#1d2736", "#161b24") }, { "bri", ("#151617", "#1b1c1d") }, { "olive", ("#3d4232", "#4a4f3a") }, { "urbain", ("#4a4e55", "#2b2e33") } };
            var u = uniforms.TryGetValue(ch.uniform ?? "", out var uu) ? uu : uniforms["police"];
            var mats = new Dictionary<string, Material>();
            Material M(string k)
            {
                if (mats.TryGetValue(k, out var mm)) return mm;
                var b = E7Assets.Mat("human", k);
                if (k == "top" || k == "pants") { mm = new Material(b); mm.SetColor("_Color", U.Hex(u.top)); }
                else if (k == "vest") { mm = new Material(b); mm.SetColor("_Color", U.Hex(u.vest)); }
                else if (k == "skin") { mm = new Material(b); mm.SetColor("_Color", U.Hex(new[] { "#c59a7a", "#8d5f45", "#e0b596" }[slot % 3])); }
                else mm = b;
                mats[k] = mm; return mm;
            }
            string head = slot % 2 == 0 ? "helmet" : "cap";
            m.body = Humanoid.Create(go.transform, "h_officer", t => t == "base" || t == "headgear:" + head, M, m);
            m.body.AttachGun(HeldGun.Detailed("carbine", m.body.transform));
            m.agent = go.AddComponent<NavMeshAgent>();
            m.agent.agentTypeID = Level.I.NavAgentType;
            m.agent.radius = 0.3f; m.agent.height = 1.75f; m.agent.speed = 3.2f; m.agent.acceleration = 14f; m.agent.angularSpeed = 0;
            m.agent.updateRotation = false; m.agent.stoppingDistance = 0.3f;
            m.agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            m.agent.avoidancePriority = 40 + slot;
            if (NavMesh.SamplePosition(spawn, out var hit, 2f, NavMesh.AllAreas)) m.agent.Warp(hit.position);
            m.yaw = Player.I ? Player.I.Yaw : 0;
            return m;
        }

        public void Follow() { order = Order.Follow; if (state == S.Combat && target == null) state = S.Move; }
        public void Hold(Vector3 p) { order = Order.Hold; holdPos = p; }

        void Speak(string text, float cd = 3f)
        {
            if (speakCd > 0) return;
            speakCd = cd;
            Hud.Subtitle(callsign, text, "mate");
            AudioSys.I.Sfx("squelch", 0.35f);
        }

        public void Hurt(float dmg, Vector3 from)
        {
            if (!Alive) return;
            hp -= dmg;
            if (body) { body.flinch = 0.3f; body.flinchSide = Random.value < 0.5f ? -1 : 1; }
            if (hp <= 0) Down(Pos - from);
            else if (hp < 50) Speak("Je suis touché ! Je tiens le coup !", 6);
        }

        public override DamageResult Damage(string part, Vector3 point, Vector3 dir, WeaponDef w, bool byPlayer)
        {
            // pas de tir ami : on prévient le joueur
            if (byPlayer) { Hud.Note("Attention : tir sur un coéquipier !"); Speak("Hé ! Attention, c'est moi !", 2); return default; }
            Hurt(part == "head" ? w.dmgHead * 0.5f : w.dmgTorso * 0.5f, point - dir);
            return default;
        }

        void Down(Vector3 dir)
        {
            state = S.Down;
            if (agent) agent.enabled = false;
            body.StartDeath(dir, yaw);
            Hud.Subtitle("CENTRALE", "Un agent est à terre ! Envoyez les secours !", "");
            AudioSys.I.Sfx("squelch");
        }

        // ---------------- perception ----------------
        bool CanSee(SuspectAI s)
        {
            if (!s.Alive) return false;
            float d = Vector3.Distance(s.Pos, Pos);
            if (d > 30) return false;
            return !Physics.Linecast(EyePos, s.EyePos, L.SightMask, QueryTriggerInteraction.Ignore) || !Physics.Linecast(EyePos, s.EyePos + Vector3.down * 0.6f, L.SightMask, QueryTriggerInteraction.Ignore);
        }

        void Sense()
        {
            SuspectAI best = null; float bd = 1e9f;
            foreach (var s in Squad.I.suspects)
            {
                if (!s.Hostile || !CanSee(s)) continue;
                float d = Vector3.Distance(s.Pos, Pos);
                if (d < bd) { bd = d; best = s; }
            }
            if (best != null && target != best)
            {
                // suspect qui ne nous a pas encore vus : sommation d'abord
                if (!warned.Contains(best) && best.state != SuspectAI.S.Combat && bd < 14 && shoutCd <= 0)
                {
                    warned.Add(best); shoutCd = 3;
                    Hud.Subtitle(callsign, U.Pick(new[] { "Police ! Lâchez votre arme !", "Police ! Mains en l’air !", "Police ! Pas un geste !" }), "mate");
                    Squad.Shout(Pos, EyePos);
                    reaction = 0.9f;
                }
                else { reaction = Random.Range(0.25f, 0.5f); Speak("Contact ! Suspect armé !"); }
            }
            target = best;
            if (target != null) state = S.Combat;
        }

        void GoTo(Vector3 p) { if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(p); } }
        bool Arrived => agent == null || !agent.enabled || !agent.isOnNavMesh || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f);

        void FaceTo(Vector3 p, float k)
        {
            float want = Mathf.Atan2(p.x - Pos.x, p.z - Pos.z) * Mathf.Rad2Deg;
            yaw += Mathf.DeltaAngle(yaw, want) * k;
        }

        void Shoot()
        {
            var from = body.gun.Muzzle.position;
            Squad.I.MuzzleFlash(from, "rifle");
            AudioSys.I.Play3D(AudioSys.I.Clip("sfx/carbine_" + Random.Range(1, 3)), from, 0.9f * Save.Data.settings.vSfx, Random.Range(0.97f, 1.03f));
            Squad.Hear(from, 30, true);
            ammo--;
            if (target == null) return;
            float dist = Vector3.Distance(from, target.EyePos);
            float chance = 0.5f * Mathf.Clamp(1.3f - dist / 30f, 0.3f, 1.1f) * (target.agent && target.agent.velocity.magnitude > 1 ? 0.7f : 1f);
            var aim = target.body.chest ? target.body.chest.position + Vector3.up * 0.2f : target.EyePos;
            var dir = (aim - from).normalized;
            if (Random.value > chance) dir = (dir + Random.insideUnitSphere * 0.06f).normalized;
            if (Physics.Raycast(from, dir, out var h, 60, L.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var hb = h.collider.GetComponent<HitBox>();
                if (hb != null && hb.actor is SuspectAI s)
                {
                    if (s.Hostile)
                    {
                        var res = s.Damage(hb.part, h.point, dir, WeaponDef.All["carbine"], false);
                        if (res.kill) { Game.I.TeamKills++; Speak("Suspect neutralisé !"); Game.I.CheckEnd(); }
                    }
                }
                else if (hb == null)
                {
                    var surf = h.collider.GetComponent<Surface>();
                    FX.I.Impact(h.point, h.normal, surf ? surf.kind : "concrete");
                    Squad.BulletNear(h.point, from);
                }
            }
        }

        void Update()
        {
            if (Game.I == null || (Game.I.State != GameState.Playing && Game.I.State != GameState.Dead)) return;
            float dt = Time.deltaTime, k = 1 - Mathf.Exp(-dt * 9);
            speakCd -= dt; shoutCd -= dt;
            if (state == S.Down) { body.Pose(dt, 0, Vector3.zero, false); return; }
            if (reloadT > 0) { reloadT -= dt; if (reloadT <= 0) ammo = 30; }
            body.reloadT = reloadT;
            senseT -= dt;
            if (senseT <= 0) { senseT = 0.15f; Sense(); }
            if (target != null && !target.Hostile) { target = null; if (state == S.Combat) state = S.Move; }
            bool aiming = false;
            var player = Player.I;
            // formation : derrière le joueur, à gauche et à droite
            Vector3 anchor = order == Order.Hold || player == null ? holdPos : player.transform.position + Quaternion.Euler(0, player.Yaw, 0) * new Vector3(slot % 2 == 0 ? -1.3f : 1.3f, 0, -1.6f - (slot / 2) * 1.2f);
            switch (state)
            {
                case S.Move:
                    agent.speed = player && Vector3.Distance(Pos, anchor) > 5 ? 4.6f : 3.0f;
                    if (Vector3.Distance(Pos, anchor) > (order == Order.Hold ? 0.5f : 1.4f)) GoTo(anchor);
                    // personne à menotter ?
                    if (arrestTarget == null)
                    {
                        var sur = Squad.NearestSurrendered(Pos, 12f);
                        if (sur != null && (player == null || Vector3.Distance(player.transform.position, sur.Pos) > 2.5f) && !Squad.I.mates.Exists(o => o != this && o.arrestTarget == sur))
                        { arrestTarget = sur; state = S.Arrest; arrestT = 0; Speak("Je m'occupe de lui !"); }
                    }
                    if (Arrived && player) yaw = Mathf.LerpAngle(yaw, player.Yaw + (slot % 2 == 0 ? -35 : 35), 1 - Mathf.Exp(-dt * 3));
                    break;
                case S.Combat:
                    if (target == null) { state = S.Move; break; }
                    aiming = true;
                    FaceTo(target.Pos, k);
                    // s'arrêter pour tirer, s'accroupir si un abri bas est tout près
                    if (agent.isOnNavMesh) agent.isStopped = true;
                    body.crouch = Mathf.Lerp(body.crouch, Vector3.Distance(Pos, target.Pos) > 8 ? 0.6f : 0f, k);
                    reaction -= dt;
                    if (ammo <= 0) { if (reloadT <= 0) { reloadT = 2.4f; Speak("Je recharge, couvrez-moi !", 4); } }
                    else if (reaction <= 0 && target.state != SuspectAI.S.Surrender)
                    {
                        shotT -= dt;
                        if (burst > 0) { if (shotT <= 0) { Shoot(); burst--; shotT = 0.11f; } }
                        else { cool -= dt; if (cool <= 0) { burst = Random.Range(2, 4); cool = Random.Range(0.5f, 1.1f); } }
                    }
                    break;
                case S.Arrest:
                    if (arrestTarget == null || arrestTarget.state != SuspectAI.S.Surrender) { arrestTarget = null; state = S.Move; break; }
                    GoTo(arrestTarget.Pos);
                    if (Vector3.Distance(Pos, arrestTarget.Pos) < 1.3f)
                    {
                        if (agent.isOnNavMesh) agent.isStopped = true;
                        FaceTo(arrestTarget.Pos, k);
                        body.crouch = Mathf.Lerp(body.crouch, 0.7f, k);
                        arrestT += dt;
                        if (arrestT > 1.3f)
                        {
                            arrestTarget.Arrest(false);
                            Game.I.Stats.arrests++;
                            Speak("Suspect menotté !", 1);
                            Game.I.CheckEnd();
                            arrestTarget = null; state = S.Move;
                        }
                    }
                    break;
            }
            if (state != S.Combat && state != S.Arrest) body.crouch = Mathf.Lerp(body.crouch, 0, 1 - Mathf.Exp(-dt * 6));
            if (!aiming && agent.enabled && agent.velocity.sqrMagnitude > 0.05f) FaceTo(Pos + agent.velocity, 1 - Mathf.Exp(-dt * 7));
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            float sp = agent.enabled ? U.Flat(agent.velocity).magnitude : 0;
            body.speed = sp;
            body.Pose(dt, aiming ? 1 : 0.15f, target != null ? target.EyePos : EyePos + transform.forward * 5, sp > 0.1f);
            float prev = stepPhase; stepPhase += dt * sp * 3.1f;
            if (Mathf.FloorToInt(stepPhase / Mathf.PI) != Mathf.FloorToInt(prev / Mathf.PI) && sp > 0.2f) AudioSys.I.EnemyStep(Pos, sp > 2);
        }
    }
}
