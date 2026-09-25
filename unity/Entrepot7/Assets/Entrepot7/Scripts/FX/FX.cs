using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>
    /// Effets visuels : poussière et étincelles d'impact, trous de balles, sang, douilles, chargeurs vides,
    /// grenades flash (avec rebonds et explosion).
    /// </summary>
    public class FX : MonoBehaviour
    {
        public static FX I { get; private set; }

        ParticleSystem dust, sparks, blood;
        readonly List<Transform> holes = new List<Transform>();
        readonly List<Transform> pools = new List<Transform>();
        int holeI;
        Material holeMat, bloodMat, brassMat, shellRedMat, magMat, pMagMat, bangMat;
        Light bangLight; Transform bangFlash; float bangFxT;

        class Body { public Transform t; public Vector3 v, spin; public bool live; public int bounces; public bool heavy; public float life; public string kind; public float fuse, clank; }
        readonly List<Body> shells = new List<Body>();
        readonly List<Body> mags = new List<Body>();
        readonly List<Body> bangs = new List<Body>();
        int shellI;

        public static FX Create(Transform parent)
        {
            var go = new GameObject("FX");
            go.transform.SetParent(parent, false);
            return go.AddComponent<FX>();
        }

        void Awake()
        {
            I = this;
            var soft = SoftDot();
            dust = MakePS("Poussiere", soft, false, 400);
            sparks = MakePS("Etincelles", soft, true, 300);
            blood = MakePS("Sang", soft, false, 200);
            holeMat = new Material(E7Assets.LitFade); holeMat.SetTexture("_MainTex", E7Assets.Tex("hole")); holeMat.SetFloat("_Roughness", 0.95f);
            bloodMat = new Material(E7Assets.LitFade); bloodMat.SetTexture("_MainTex", E7Assets.Tex("blood")); bloodMat.SetFloat("_Roughness", 0.2f);
            brassMat = E7Assets.Mat("gun", "brass");
            shellRedMat = E7Assets.Mat("gun", "shellRed");
            magMat = E7Assets.Mat("gun", "tan");
            pMagMat = E7Assets.Mat("worldgun", "gunMetal");
            bangMat = E7Assets.Mat("gun", "bang");
            for (int i = 0; i < 100; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "trou"; q.transform.SetParent(transform, false); q.transform.localScale = Vector3.one * 0.09f;
                q.GetComponent<MeshRenderer>().sharedMaterial = holeMat;
                q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                q.SetActive(false); holes.Add(q.transform);
            }
            for (int i = 0; i < 40; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(s.GetComponent<Collider>());
                s.name = "douille"; s.transform.SetParent(transform, false);
                s.SetActive(false);
                shells.Add(new Body { t = s.transform });
            }
            bangLight = U.Child(transform, "FlashLumiere").AddComponent<Light>();
            bangLight.type = LightType.Point; bangLight.range = 16; bangLight.intensity = 0; bangLight.color = new Color(1f, 0.97f, 0.9f);
            var bf = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(bf.GetComponent<Collider>());
            bf.transform.SetParent(transform, false); bf.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.UnlitMat(new Color(4f, 3.6f, 3f, 1f), E7Assets.Tex("flash"), true);
            bangFlash = bf.transform; bf.SetActive(false);
        }

        static Texture2D SoftDot()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)));
            }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        ParticleSystem MakePS(string n, Texture tex, bool additive, int max)
        {
            var go = U.Child(transform, n);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1f; main.startSpeed = 0f; main.startSize = 0.1f;
            main.gravityModifier = additive ? 1f : 0.25f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, additive ? AnimationCurve.Linear(0, 1, 1, 0.2f) : AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
            if (!additive) { var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.dampen = 0.08f; lv.limit = 0.5f; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = E7Assets.UnlitMat(Color.white, tex, additive, !additive, true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, startColor = c, startSize = size, startLifetime = life, applyShapeToPosition = false };
            ps.Emit(ep, 1);
        }

        static readonly Color C_DUST = new Color(0.55f, 0.53f, 0.5f, 0.55f), C_WOOD = new Color(0.45f, 0.34f, 0.22f, 0.6f), C_CARD = new Color(0.62f, 0.5f, 0.34f, 0.6f), C_SPARK = new Color(3f, 2f, 0.9f, 1f), C_BLOOD = new Color(0.35f, 0.02f, 0.02f, 0.9f);

        /// <summary>Impact de balle sur le décor.</summary>
        public void Impact(Vector3 point, Vector3 normal, string surface)
        {
            if (surface != "cardboard") AddHole(point, normal);
            else AddHole(point, normal, 0.6f);
            var c = surface == "wood" ? C_WOOD : surface == "cardboard" ? C_CARD : C_DUST;
            for (int i = 0; i < 9; i++) Emit(dust, point, normal * Random.Range(0.6f, 2.4f) + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.2f, 0.9f), Random.Range(-0.7f, 0.7f)), c, Random.Range(0.05f, 0.14f), Random.Range(0.4f, 1.1f));
            if (surface == "metal" || surface == "concrete")
                for (int i = 0; i < (surface == "metal" ? 8 : 3); i++) Emit(sparks, point, normal * Random.Range(1.5f, 4f) + new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 2f), Random.Range(-2f, 2f)), C_SPARK, Random.Range(0.012f, 0.025f), Random.Range(0.12f, 0.3f));
            AudioSys.I.Sfx3D("impact_" + (surface == "wood" || surface == "cardboard" || surface == "metal" ? surface : "concrete"), point, 0.5f, false);
        }

        public void AddHole(Vector3 p, Vector3 n, float scale = 1f)
        {
            var h = holes[holeI]; holeI = (holeI + 1) % holes.Count;
            h.gameObject.SetActive(true);
            h.position = p + n * 0.004f;
            // un Quad Unity est visible depuis -z : son -z doit regarder dans la direction de la normale
            h.rotation = Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
            h.localScale = Vector3.one * 0.09f * Random.Range(0.8f, 1.3f) * scale;
        }

        public void Blood(Vector3 p, Vector3 dir, bool heavy)
        {
            for (int i = 0; i < (heavy ? 16 : 8); i++) Emit(blood, p, dir * Random.Range(0.5f, 2.5f) + Random.insideUnitSphere * 0.8f, C_BLOOD, Random.Range(0.03f, 0.08f), Random.Range(0.3f, 0.7f));
            // éclaboussure sur le mur derrière
            if (Physics.Raycast(p, dir, out var h, 2.2f, L.WorldMask, QueryTriggerInteraction.Ignore))
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(transform, false);
                q.transform.position = h.point + h.normal * 0.005f;
                q.transform.rotation = Quaternion.LookRotation(-h.normal) * Quaternion.Euler(0, 0, Random.Range(0, 360f));
                q.transform.localScale = Vector3.one * Random.Range(0.3f, 0.6f);
                q.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;
                pools.Add(q.transform);
                if (pools.Count > 40) { Destroy(pools[0].gameObject); pools.RemoveAt(0); }
            }
        }

        /// <summary>Flaque de sang qui s'étale sous un corps.</summary>
        public void Pool(Vector3 p)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.position = new Vector3(p.x, 0.007f + pools.Count * 0.0002f, p.z);
            q.transform.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0);
            q.transform.localScale = Vector3.one * 0.01f;
            q.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;
            q.AddComponent<Grow>().target = Random.Range(0.7f, 1.05f);
            pools.Add(q.transform);
            if (pools.Count > 40) { Destroy(pools[0].gameObject); pools.RemoveAt(0); }
        }

        class Grow : MonoBehaviour { public float target = 1f; void Update() { float s = Mathf.MoveTowards(transform.localScale.x, target, Time.deltaTime * 0.12f); transform.localScale = new Vector3(s, s, s); if (s >= target) enabled = false; } }

        /// <summary>Éjecte une douille à droite de l'arme.</summary>
        public void EjectShell(string weapon, Transform cam, Vector3 playerVel)
        {
            var s = shells[shellI]; shellI = (shellI + 1) % shells.Count;
            bool sg = weapon == "shotgun", small = weapon == "pistol" || weapon == "smg";
            s.t.GetComponent<MeshRenderer>().sharedMaterial = sg ? shellRedMat : brassMat;
            s.t.localScale = sg ? new Vector3(0.022f, 0.03f, 0.022f) : small ? new Vector3(0.011f, 0.011f, 0.011f) : new Vector3(0.01f, 0.0225f, 0.01f);
            s.heavy = sg;
            s.t.position = cam.position + cam.right * 0.12f + cam.forward * 0.35f + Vector3.down * 0.18f;
            s.t.rotation = Random.rotation;
            s.v = cam.right * Random.Range(1.8f, 2.8f) + Vector3.up * Random.Range(1.5f, 2.4f) + cam.forward * Random.Range(-0.3f, 0.3f) + playerVel;
            s.spin = new Vector3(Random.Range(-20f, 20f), Random.Range(-20f, 20f), Random.Range(-20f, 20f)) * Mathf.Rad2Deg;
            s.live = true; s.bounces = 0; s.t.gameObject.SetActive(true);
        }

        public void DropMag(bool pistol, Transform cam, Vector3 playerVel)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(transform, false);
            m.transform.localScale = pistol ? new Vector3(0.03f, 0.11f, 0.035f) : new Vector3(0.028f, 0.19f, 0.07f);
            m.GetComponent<MeshRenderer>().sharedMaterial = pistol ? pMagMat : magMat;
            m.transform.position = cam.position + cam.forward * 0.35f + cam.right * 0.05f + Vector3.down * 0.35f;
            m.transform.rotation = cam.rotation;
            mags.Add(new Body { t = m.transform, v = new Vector3(Random.Range(-0.3f, 0.3f), -0.5f, Random.Range(-0.3f, 0.3f)) + playerVel * 0.5f, spin = new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 3f), Random.Range(-6f, 6f)) * Mathf.Rad2Deg, live = true });
            if (mags.Count > 12) { Destroy(mags[0].t.gameObject); mags.RemoveAt(0); }
        }

        public void ThrowBang(Transform cam, Vector3 playerVel)
        {
            var g = new GameObject("grenade_flash");
            g.transform.SetParent(transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(g.transform, false); body.transform.localScale = new Vector3(0.048f, 0.05f, 0.048f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bangMat;
            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(lever.GetComponent<Collider>());
            lever.transform.SetParent(g.transform, false); lever.transform.localPosition = new Vector3(0.026f, 0.01f, 0); lever.transform.localScale = new Vector3(0.012f, 0.07f, 0.01f);
            lever.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.Mat("gun", "steel");
            g.transform.position = cam.position + cam.forward * 0.4f - cam.right * 0.15f + Vector3.up * 0.05f;
            bangs.Add(new Body { t = g.transform, v = cam.forward * 11f + Vector3.up * 2.6f + playerVel, spin = Random.insideUnitSphere * 12f * Mathf.Rad2Deg, fuse = 1.6f, live = true });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var s in shells)
            {
                if (!s.live) continue;
                s.v.y -= 9.8f * dt;
                var p = s.t.position + s.v * dt;
                if (p.y < 0.01f)
                {
                    p.y = 0.01f;
                    if (Mathf.Abs(s.v.y) > 0.6f && s.bounces < 3) { s.bounces++; AudioSys.I.Play3D(AudioSys.I.Bank(s.heavy ? "tinkHeavy" : "tink"), p, 0.35f / s.bounces, Random.Range(0.9f, 1.2f), false); }
                    s.v.y *= -0.35f; s.v.x *= 0.6f; s.v.z *= 0.6f; s.spin *= 0.6f;
                    if (s.v.sqrMagnitude < 0.05f) { s.live = false; s.t.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0); }
                }
                else if (Physics.Linecast(s.t.position, p, out var h, L.WorldMask, QueryTriggerInteraction.Ignore)) { s.v = Vector3.Reflect(s.v, h.normal) * 0.4f; p = h.point + h.normal * 0.01f; }
                s.t.position = p;
                if (s.live) s.t.Rotate(s.spin * dt, Space.World);
            }
            foreach (var m in mags)
            {
                if (!m.live) continue;
                m.v.y -= 9.8f * dt; m.t.position += m.v * dt; m.t.Rotate(m.spin * dt, Space.World);
                if (m.t.position.y < 0.02f) { m.live = false; var p = m.t.position; p.y = 0.02f; m.t.position = p; m.t.rotation = Quaternion.Euler(90, Random.Range(0, 360f), 0); AudioSys.I.Sfx3D("clatter", p, 0.3f, false); }
            }
            for (int i = bangs.Count - 1; i >= 0; i--)
            {
                var b = bangs[i];
                b.fuse -= dt; b.clank -= dt;
                for (int s = 0; s < 3; s++)
                {
                    float h = dt / 3f;
                    b.v.y -= 9.8f * h;
                    var from = b.t.position; var to = from + b.v * h;
                    if (Physics.Linecast(from, to, out var hit, L.WorldMask, QueryTriggerInteraction.Ignore))
                    {
                        b.v = Vector3.Reflect(b.v, hit.normal) * (hit.normal.y > 0.7f ? 0.35f : 0.45f);
                        if (hit.normal.y > 0.7f) { b.v.x *= 0.7f; b.v.z *= 0.7f; }
                        to = hit.point + hit.normal * 0.03f;
                        if (b.clank <= 0 && b.v.magnitude > 0.8f) { AudioSys.I.Sfx3D("clank", to, 0.5f); b.clank = 0.12f; b.spin *= 0.6f; }
                    }
                    b.t.position = to;
                }
                b.t.Rotate(b.spin * dt, Space.World);
                if (b.fuse <= 0) { Explode(b.t.position); Destroy(b.t.gameObject); bangs.RemoveAt(i); }
            }
            if (bangFxT > 0)
            {
                bangFxT -= dt;
                bangLight.intensity = Mathf.Max(0, bangFxT / 0.3f) * 60f;
                bangFlash.gameObject.SetActive(bangFxT > 0.12f);
                if (Camera.main) bangFlash.rotation = Camera.main.transform.rotation;
            }
            else { bangLight.intensity = 0; bangFlash.gameObject.SetActive(false); }
        }

        void Explode(Vector3 pos)
        {
            bangLight.transform.position = pos + Vector3.up * 0.2f; bangFxT = 0.3f;
            bangFlash.position = pos + Vector3.up * 0.15f; bangFlash.localScale = Vector3.one * 2.4f;
            for (int i = 0; i < 24; i++) Emit(sparks, pos, new Vector3(Random.Range(-4f, 4f), Random.Range(0f, 4f), Random.Range(-4f, 4f)), C_SPARK, Random.Range(0.015f, 0.03f), Random.Range(0.15f, 0.4f));
            for (int i = 0; i < 14; i++) Emit(dust, pos, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.2f, 1.5f), Random.Range(-1.5f, 1.5f)), C_DUST, Random.Range(0.15f, 0.35f), Random.Range(1f, 2.2f));
            var cam = Camera.main;
            bool occ = AudioSys.I.IsOccluded(pos);
            float d = cam ? Vector3.Distance(pos, cam.transform.position) : 99;
            AudioSys.I.Play3D(AudioSys.I.Bank("bang"), pos, 3f * Save.Data.settings.vSfx, 1f, occ, d / 343f);
            if (!occ && d < 16) { float k = 1 - d / 16f; AudioSys.I.Muffle(400, 1 + k * 4, 0.3f * k); }
            Squad.Flashbang(pos);
            if (!occ && d < 14 && cam && Player.I)
            {
                var dir = (pos - cam.transform.position).normalized;
                float facing = Vector3.Dot(cam.transform.forward, dir);
                Player.I.Blind(Mathf.Clamp01((1 - d / 14f) * (0.35f + 0.65f * Mathf.Max(0, facing)) * 1.5f));
            }
        }

        public void ClearAll()
        {
            foreach (var h in holes) h.gameObject.SetActive(false);
            foreach (var p in pools) if (p) Destroy(p.gameObject);
            pools.Clear();
            foreach (var m in mags) if (m.t) Destroy(m.t.gameObject);
            mags.Clear();
            foreach (var b in bangs) if (b.t) Destroy(b.t.gameObject);
            bangs.Clear();
            foreach (var s in shells) { s.live = false; s.t.gameObject.SetActive(false); }
            dust.Clear(); sparks.Clear(); blood.Clear();
        }
    }
}
