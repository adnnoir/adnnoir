using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>Zone de tir sur un personnage (tête, torse, membre).</summary>
    public class HitBox : MonoBehaviour { public Actor actor; public string part; }

    public struct DamageResult { public bool kill, head, unjust; }

    /// <summary>Tout personnage qu'on peut toucher (suspect ou coéquipier).</summary>
    public abstract class Actor : MonoBehaviour
    {
        public Humanoid body;
        public float hp = 100;
        public abstract bool Alive { get; }
        public abstract DamageResult Damage(string part, Vector3 point, Vector3 dir, WeaponDef w, bool byPlayer);
        public Vector3 Pos => transform.position;
        public Vector3 EyePos => body != null ? body.EyeWorld : transform.position + Vector3.up * 1.6f;
    }

    /// <summary>Arme tenue par un personnage.</summary>
    public class HeldGun
    {
        public GameObject go; public Vector3 muzzle, grip, guard; public string type;
        public Transform Muzzle;

        public static HeldGun World(string type, Transform parent)
        {
            var g = new HeldGun { type = type };
            var md = E7Assets.Model("wg_" + type);
            g.go = md != null ? md.Build("arme_" + type, t => true, k => E7Assets.Mat("worldgun", k), L.Actors) : new GameObject("arme");
            g.go.transform.SetParent(parent, false);
            var j = E7Assets.Json("Models/wg_" + type) as Dictionary<string, object>;
            if (j != null) { g.muzzle = V(j["muzzle"]); g.grip = V(j["grip"]); g.guard = V(j["guard"]); }
            g.Muzzle = U.Child(g.go.transform, "bouche", g.muzzle).transform;
            return g;
        }

        /// <summary>Carabine détaillée (la même que celle du joueur) pour les coéquipiers.</summary>
        public static HeldGun Detailed(string key, Transform parent)
        {
            var g = new HeldGun { type = "police" };
            var md = E7Assets.Model("w_" + key);
            var meta = WeaponMeta.Load(key);
            g.go = md != null ? md.Build("arme_" + key, t => t == "base" || t == "optic:holo" || t == "muzzle:std" || t == "rail:light", k => E7Assets.Mat("gun", k), L.Actors) : new GameObject("arme");
            g.go.transform.SetParent(parent, false);
            g.muzzle = meta.muzzles.TryGetValue("std", out var m) ? m : new Vector3(0, 0.03f, 0.66f);
            g.grip = meta.gripPos;
            g.guard = meta.poses.TryGetValue("guard", out var gd) ? gd.pos : new Vector3(0, -0.02f, 0.3f);
            g.Muzzle = U.Child(g.go.transform, "bouche", g.muzzle).transform;
            return g;
        }

        static Vector3 V(object o) { var l = o as List<object>; return l != null && l.Count == 3 ? new Vector3(MiniJson.F(l[0]), MiniJson.F(l[1]), MiniJson.F(l[2])) : Vector3.zero; }
    }

    /// <summary>
    /// Corps articulé (modèle exporté de la version web) animé par code :
    /// marche, accroupi, à genoux, bras en cinématique inverse (IK) qui tiennent l'arme, mort.
    /// Le personnage regarde vers +z.
    /// </summary>
    public class Humanoid : MonoBehaviour
    {
        public Transform root, pelvis, spine, chest, neck, head;
        public class Arm { public Transform shoulder, upper, fore, hand; public float l1 = 0.29f, l2 = 0.27f; }
        public class Leg { public Transform hip, knee, ankle; }
        public Arm armL, armR; public Leg legL, legR;
        public HeldGun gun;
        public float scale = 1f;
        readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<Collider> hitboxes = new List<Collider>();
        float pelvisBaseY = 0.95f;

        // état d'animation
        public float walk, speed, aimBlend, crouch, kneel, flinch, flinchSide, reloadT, deathT;
        public bool stunned, surrendered, arrested, dead, gunDropped;
        public Vector3 aimTarget;
        float fallDir = 1, fallSide;
        (Vector3 from, Vector3 to, Quaternion q0, Quaternion q1, float t)? drop;

        public Vector3 EyeWorld => head ? head.position + Vector3.up * 0.1f * scale : transform.position + Vector3.up * 1.6f;

        /// <summary>Crée un corps. model : "h_suspect" ou "h_officer". tags : pièces à garder selon les variantes.</summary>
        public static Humanoid Create(Transform parent, string model, System.Func<string, bool> keep, System.Func<string, Material> mat, Actor owner)
        {
            var md = E7Assets.Model(model);
            var go = md != null ? md.Build(model, keep, mat, L.Actors) : new GameObject(model);
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Humanoid>();
            h.root = go.transform;
            Transform F(string n) { foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            h.pelvis = F("pelvis"); h.spine = F("spine"); h.chest = F("chest"); h.neck = F("neck"); h.head = F("head");
            h.armL = new Arm { shoulder = F("shoulder_L"), upper = F("upper_L"), fore = F("fore_L"), hand = F("hand_L") };
            h.armR = new Arm { shoulder = F("shoulder_R"), upper = F("upper_R"), fore = F("fore_R"), hand = F("hand_R") };
            h.legL = new Leg { hip = F("hip_L"), knee = F("knee_L"), ankle = F("ankle_L") };
            h.legR = new Leg { hip = F("hip_R"), knee = F("knee_R"), ankle = F("ankle_R") };
            if (h.pelvis) h.pelvisBaseY = h.pelvis.localPosition.y;
            h.renderers.AddRange(go.GetComponentsInChildren<Renderer>());
            if (owner != null) h.BuildHitboxes(owner);
            return h;
        }

        void BuildHitboxes(Actor owner)
        {
            void Add(Transform t, string part, Vector3 center, float radius, float height, int dir)
            {
                if (!t) return;
                var go = U.Child(t, "hit_" + part, Vector3.zero);
                go.layer = L.Actors;
                var c = go.AddComponent<CapsuleCollider>();
                c.center = center; c.radius = radius; c.height = height; c.direction = dir;
                var hb = go.AddComponent<HitBox>(); hb.actor = owner; hb.part = part;
                hitboxes.Add(c);
            }
            Add(head, "head", new Vector3(0, 0.1f, 0), 0.11f, 0.26f, 1);
            Add(chest, "torso", new Vector3(0, 0.22f, 0), 0.2f, 0.58f, 1);
            Add(pelvis, "torso", new Vector3(0, 0, 0), 0.17f, 0.34f, 0);
            foreach (var a in new[] { armL, armR }) { Add(a.upper, "limb", new Vector3(0, -0.145f, 0), 0.06f, 0.32f, 1); Add(a.fore, "limb", new Vector3(0, -0.14f, 0), 0.05f, 0.3f, 1); }
            foreach (var l in new[] { legL, legR }) { Add(l.hip, "limb", new Vector3(0, -0.22f, 0), 0.08f, 0.46f, 1); Add(l.knee, "limb", new Vector3(0, -0.21f, 0), 0.065f, 0.44f, 1); }
        }

        public void SetHitboxes(bool on) { foreach (var c in hitboxes) c.enabled = on; }

        public void AttachGun(HeldGun g) { gun = g; if (g != null && spine) { g.go.transform.SetParent(spine, false); } }

        // --------- rotations dans le repère Unity (miroir de la version web : x inchangé, y et z inversés) ---------
        static Quaternion XYZ(float x, float y, float z) => Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);
        static void RotX(Transform t, float x) { if (t) t.localRotation = Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right); }
        static float AngX(Transform t) { if (!t) return 0; var e = t.localEulerAngles.x; if (e > 180) e -= 360; return e * Mathf.Deg2Rad; }

        /// <summary>Bras en cinématique inverse : pose l'épaule, le coude et la main vers une cible (repère de la poitrine).</summary>
        public void SolveArm(Arm arm, Vector3 target, Vector3 pole)
        {
            if (arm.upper == null || arm.fore == null) return;
            Vector3 S = arm.shoulder.localPosition;
            float l1 = arm.l1, l2 = arm.l2;
            var d0 = target - S; float d = d0.magnitude;
            var u = d0 / Mathf.Max(d, 1e-4f);
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.01f, (l1 + l2) * 0.995f);
            float A = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1));
            var v = (pole - u * Vector3.Dot(pole, u)).normalized;
            var E = S + u * (Mathf.Cos(A) * l1) + v * (Mathf.Sin(A) * l1);
            var Tc = S + u * d;
            var a1 = (E - S).normalized; var a2 = (Tc - E).normalized;
            var n = Vector3.Cross(u, v).normalized;
            Quaternion Basis(Vector3 x, Vector3 y) { var z = Vector3.Cross(x, y); return Quaternion.LookRotation(z, y); }
            var q1 = Basis(n, -a1); var q2 = Basis(n, -a2);
            // le bras est enfant de l'épaule (sans rotation) : repère de la poitrine = repère local
            arm.upper.localRotation = q1;
            arm.fore.localRotation = Quaternion.Inverse(q1) * q2;
        }

        // positions de l'arme (dans le repère du dos) : canon bas / épaulé
        static readonly (Vector3 p, Quaternion r) LongLow = (new Vector3(0.06f, 0.24f, 0.26f), XYZ(0.75f, 0.3f, 0));
        static readonly (Vector3 p, Quaternion r) LongAim = (new Vector3(0.075f, 0.37f, 0.32f), Quaternion.identity);
        static readonly (Vector3 p, Quaternion r) PistolLow = (new Vector3(0.02f, 0.2f, 0.24f), XYZ(1.0f, 0, 0));
        static readonly (Vector3 p, Quaternion r) PistolAim = (new Vector3(0.03f, 0.37f, 0.44f), Quaternion.identity);

        /// <summary>Met à jour toute la pose. targetYaw : direction du corps (degrés). aimPoint : point visé (monde).</summary>
        public void Pose(float dt, float aimTargetBlend, Vector3 aimPoint, bool moving)
        {
            float k = 1 - Mathf.Exp(-dt * 9);
            if (dead) { AnimateDeath(dt); UpdateDrop(dt); return; }
            UpdateDrop(dt);
            aimBlend = Mathf.Lerp(aimBlend, aimTargetBlend, 1 - Mathf.Exp(-dt * 7));
            walk += dt * speed * 3.1f;
            float amp = moving ? (speed > 2 ? 0.75f : 0.5f) : 0;
            PoseLegs(walk, amp, crouch * (1 - kneel), kneel, k, moving);
            float pelvisYaw = moving ? Mathf.Sin(walk) * 0.12f : 0;
            if (pelvis) pelvis.localRotation = Quaternion.AngleAxis(-pelvisYaw * Mathf.Rad2Deg, Vector3.up);
            flinch = Mathf.Max(0, flinch - dt);
            float cr = crouch * (1 - kneel);
            float tgtPitch = 0;
            if (aimBlend > 0.01f)
            {
                var from = transform.position + Vector3.up * 1.45f * scale * (1 - cr * 0.4f);
                tgtPitch = Mathf.Clamp(Mathf.Atan2(aimPoint.y - from.y, U.FlatDist(aimPoint, from)), -0.6f, 0.6f);
            }
            if (spine)
            {
                float sx = AngXSpine;
                sx = Mathf.Lerp(sx, -tgtPitch * aimBlend + cr * 0.25f - flinch * 1.1f + (stunned ? 0.35f : 0), k);
                AngXSpine = sx;
                spine.localRotation = Quaternion.AngleAxis(sx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(pelvisYaw * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(-flinch * 0.5f * flinchSide * Mathf.Rad2Deg, Vector3.forward);
            }
            if (head)
            {
                headX = Mathf.Lerp(headX, kneel > 0.5f ? 0.25f : stunned ? 0.4f : lookAround ? Mathf.Sin(Time.time / 0.9f) * 0.15f : 0, k);
                float hy = Mathf.Lerp(0, 0.34f, aimBlend) * (1 - kneel);
                head.localRotation = Quaternion.AngleAxis(-hy * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(headX * Mathf.Rad2Deg, Vector3.right);
            }
            if (!gunDropped && gun != null) PoseUpper(aimBlend, -0.38f * (1 - kneel));
            else
            {
                if (chest) chest.localRotation = Quaternion.identity;
                if (arrested) { SolveArm(armR, new Vector3(0.06f, -0.08f, -0.2f), new Vector3(1, 0, 0.4f)); SolveArm(armL, new Vector3(-0.06f, -0.08f, -0.2f), new Vector3(-1, 0, 0.4f)); }
                else { SolveArm(armR, new Vector3(0.09f, 0.62f, 0.03f), new Vector3(1, 0.3f, -0.2f)); SolveArm(armL, new Vector3(-0.09f, 0.62f, 0.03f), new Vector3(-1, 0.3f, -0.2f)); }
            }
        }
        float AngXSpine; float headX;
        public bool lookAround;

        void PoseUpper(float aim, float twist)
        {
            if (!chest || !spine) return;
            chest.localRotation = Quaternion.AngleAxis(-Mathf.Lerp(0, twist, aim) * Mathf.Rad2Deg, Vector3.up);
            bool pistol = gun.type == "pistol";
            var low = pistol ? PistolLow : LongLow; var hi = pistol ? PistolAim : LongAim;
            var gt = gun.go.transform;
            gt.localPosition = Vector3.Lerp(low.p, hi.p, aim);
            gt.localRotation = Quaternion.Slerp(low.r, hi.r, aim);
            if (reloadT > 0) gt.localRotation *= Quaternion.AngleAxis(-0.4f * Mathf.Rad2Deg, Vector3.forward);
            var grip = chest.InverseTransformPoint(gt.TransformPoint(gun.grip)) + new Vector3(0, 0.02f, -0.03f);
            var guard = chest.InverseTransformPoint(gt.TransformPoint(gun.guard)) + new Vector3(0, -0.02f, -0.04f);
            if (reloadT > 0) guard = Vector3.Lerp(guard, new Vector3(-0.02f, 0.05f, 0.25f), Mathf.Sin(reloadT * 3) * 0.5f + 0.5f);
            // compense l'échelle du personnage (les longueurs du bras sont en unités du modèle)
            SolveArm(armR, grip, new Vector3(0.8f, -1, -0.3f));
            if (stunned) SolveArm(armL, new Vector3(-0.03f, 0.62f, 0.13f), new Vector3(-1, -0.4f, 0));
            else SolveArm(armL, guard, new Vector3(-0.6f, -1, 0));
        }

        void PoseLegs(float w, float amp, float cr, float kn, float k, bool moving)
        {
            if (legL.hip == null) return;
            float sw = Mathf.Sin(w) * amp;
            float hipL = Mathf.Lerp(Mathf.Lerp(-sw, -1.15f, cr), 0, kn), hipR = Mathf.Lerp(Mathf.Lerp(sw, -1.15f, cr), 0, kn);
            float kneeL = Mathf.Lerp(Mathf.Lerp(moving ? Mathf.Max(0, Mathf.Sin(w + 0.6f)) * amp * 2 : 0.04f, 1.95f, cr), 1.57f, kn);
            float kneeR = Mathf.Lerp(Mathf.Lerp(moving ? Mathf.Max(0, -Mathf.Sin(w + 0.6f)) * amp * 2 : 0.04f, 1.95f, cr), 1.57f, kn);
            float hl = Mathf.Lerp(AngX(legL.hip), hipL, k), hr = Mathf.Lerp(AngX(legR.hip), hipR, k);
            float kl = Mathf.Lerp(AngX(legL.knee), kneeL, k), kr = Mathf.Lerp(AngX(legR.knee), kneeR, k);
            RotX(legL.hip, hl); RotX(legR.hip, hr); RotX(legL.knee, kl); RotX(legR.knee, kr);
            RotX(legL.ankle, -hl - kl * (kn > 0.5f ? 0 : 1) + (kn > 0.5f ? 0.9f : 0));
            RotX(legR.ankle, -hr - kr * (kn > 0.5f ? 0 : 1) + (kn > 0.5f ? 0.9f : 0));
            float bob = moving ? Mathf.Abs(Mathf.Cos(w)) * 0.035f * Mathf.Min(1, amp * 2) : 0;
            if (pelvis) { var p = pelvis.localPosition; p.y = Mathf.Lerp(pelvisBaseY + bob, 0.58f, cr) * (1 - kn) + 0.52f * kn; pelvis.localPosition = p; }
        }

        public void StartDeath(Vector3 dir, float yawDeg)
        {
            dead = true; deathT = 0;
            var fwd = Quaternion.Euler(0, yawDeg, 0) * Vector3.forward;
            fallDir = Vector3.Dot(fwd, dir) > 0 ? 1 : -1;
            fallSide = Random.Range(-0.5f, 0.5f);
            SetHitboxes(false);
        }

        void AnimateDeath(float dt)
        {
            if (deathT >= 1.4f) return;
            deathT += dt;
            float t = deathT;
            float buckle = U.Smooth(t / 0.35f), fall = Mathf.Pow(U.Smooth((t - 0.18f) / 0.7f), 1.6f);
            RotX(legL.hip, Mathf.Lerp(-0.5f, 0.1f, fall) * buckle); RotX(legR.hip, -0.2f * buckle);
            RotX(legL.knee, Mathf.Lerp(1.2f, 0.3f, fall) * buckle); RotX(legR.knee, Mathf.Lerp(0.8f, 0.5f, fall) * buckle);
            if (pelvis) { var p = pelvis.localPosition; p.y = Mathf.Lerp(pelvisBaseY, 0.7f, buckle) * (1 - fall) + 0.12f * fall; pelvis.localPosition = p; }
            // le corps entier bascule en avant ou en arrière
            root.localRotation = Quaternion.AngleAxis(fallDir * fall * 90f * 0.96f, Vector3.right) * Quaternion.AngleAxis(-fallSide * fall * Mathf.Rad2Deg, Vector3.forward);
            root.localPosition = new Vector3(0, fall * 0.02f, 0);
            if (spine) spine.localRotation = Quaternion.Slerp(spine.localRotation, Quaternion.AngleAxis(-0.3f * fallDir * Mathf.Rad2Deg, Vector3.right), 0.1f);
            if (head) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.AngleAxis(0.4f * fallDir * Mathf.Rad2Deg, Vector3.right), 0.1f);
            if (chest) chest.localRotation = Quaternion.Slerp(chest.localRotation, Quaternion.identity, 0.1f);
            SolveArm(armR, new Vector3(0.35f + fall * 0.2f, 0.2f + fall * 0.3f, 0.1f), new Vector3(1, -0.5f, 0));
            SolveArm(armL, new Vector3(-0.35f - fall * 0.15f, 0.1f + fall * 0.4f, 0.15f), new Vector3(-1, -0.5f, 0));
        }

        /// <summary>Lâche l'arme : elle tombe au sol à côté du personnage.</summary>
        public void DropGun()
        {
            if (gunDropped || gun == null) return;
            gunDropped = true;
            var t = gun.go.transform;
            t.SetParent(transform.parent, true);
            drop = (t.position, new Vector3(t.position.x + Random.Range(-0.4f, 0.4f), 0.03f, t.position.z + Random.Range(-0.4f, 0.4f)), t.rotation, Quaternion.Euler(0, Random.Range(0, 360f), 90), 0f);
            AudioSys.I.Sfx3D("clatter", t.position, 0.5f);
        }

        void UpdateDrop(float dt)
        {
            if (drop == null || drop.Value.t >= 1) return;
            var d = drop.Value; d.t = Mathf.Min(1, d.t + dt * 2.2f); drop = d;
            var t = gun.go.transform;
            t.position = new Vector3(Mathf.Lerp(d.from.x, d.to.x, d.t), Mathf.Lerp(d.from.y, d.to.y, d.t * d.t), Mathf.Lerp(d.from.z, d.to.z, d.t));
            t.rotation = Quaternion.Slerp(d.q0, d.q1, U.Smooth(d.t));
        }

        public void RemoveGun() { if (gun != null && gun.go) Destroy(gun.go); }
    }
}
