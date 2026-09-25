using UnityEngine;

namespace E7
{
    public enum PickupType { Medkit, Ammo }

    /// <summary>Trousse de soins ou boîte de munitions posée au sol.</summary>
    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        public bool taken;
        GameObject body;

        public static Pickup Create(Transform parent, PickupType t, Vector3 pos)
        {
            var go = U.Child(parent, t == PickupType.Medkit ? "Soins" : "Munitions", pos);
            var pk = go.AddComponent<Pickup>();
            pk.type = t;
            pk.body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pk.body.transform.SetParent(go.transform, false);
            pk.body.transform.localPosition = new Vector3(0, 0.09f, 0);
            pk.body.transform.localScale = t == PickupType.Medkit ? new Vector3(0.36f, 0.18f, 0.26f) : new Vector3(0.34f, 0.2f, 0.2f);
            pk.body.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            Destroy(pk.body.GetComponent<Collider>());
            var m = E7Assets.Simple(Color.white, 0.1f, 0.6f);
            m.SetTexture("_MainTex", E7Assets.Tex(t == PickupType.Medkit ? "med" : "ammo"));
            pk.body.GetComponent<MeshRenderer>().sharedMaterial = m;
            return pk;
        }

        public void ResetFor(bool active) { taken = false; body.SetActive(active); enabled = active; }

        void Update()
        {
            if (taken || Player.I == null || !Player.I.Alive) return;
            body.transform.Rotate(0, 20 * Time.deltaTime, 0);
            if (U.FlatDist(Player.I.transform.position, transform.position) > 0.9f) return;
            if (type == PickupType.Medkit)
            {
                if (Player.I.Health >= 100) return;
                Player.I.Heal(45);
                Hud.Toast("+45 SANTÉ");
            }
            else
            {
                Player.I.Weapons.RefillReserve();
                Hud.Toast("MUNITIONS RÉCUPÉRÉES");
            }
            taken = true; body.SetActive(false);
            AudioSys.I.Sfx("pickup");
        }
    }

    /// <summary>Cible d'entraînement : papier (bascule quand on la touche) ou acier (balance et sonne).</summary>
    public class TrainingTarget : MonoBehaviour
    {
        public string kind;
        Transform pivot, board;
        bool down; float t, swing, swingV;
        readonly System.Collections.Generic.List<GameObject> holes = new System.Collections.Generic.List<GameObject>();
        static Material holeMat, targetMat, steelMat;

        public static TrainingTarget Create(Transform parent, Vector3 p, string kind)
        {
            var go = U.Child(parent, "Cible_" + kind, new Vector3(p.x + Random.Range(-0.3f, 0.3f), 0, p.z + Random.Range(-0.3f, 0.3f)));
            // la cible regarde vers -x (vers le joueur au stand de tir)
            go.transform.rotation = Quaternion.Euler(0, -90 + Random.Range(-14f, 14f), 0);
            var tt = go.AddComponent<TrainingTarget>();
            tt.kind = kind;
            var steel = E7Assets.Mat("world", "steel");
            var post = Box(go.transform, new Vector3(0, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.04f), steel);
            Box(go.transform, new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.04f, 0.3f), steel);
            tt.pivot = U.Child(go.transform, "pivot", new Vector3(0, 0.72f, 0)).transform;
            if (!targetMat) { targetMat = E7Assets.Simple(Color.white, 0, 0.9f); targetMat.SetTexture("_MainTex", E7Assets.Tex("target")); }
            if (!steelMat) steelMat = E7Assets.Simple(U.Hex("#8a8d90"), 0.8f, 0.35f);
            if (!holeMat) { holeMat = new Material(E7Assets.LitFade); holeMat.SetTexture("_MainTex", E7Assets.Tex("hole")); holeMat.SetFloat("_Roughness", 0.9f); }
            if (kind == "paper")
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "board"; q.transform.SetParent(tt.pivot, false);
                q.transform.localPosition = new Vector3(0, 0.49f, 0); q.transform.localScale = new Vector3(0.56f, 0.98f, 1);
                // un Quad Unity est visible depuis -z : on le tourne pour qu'il regarde vers l'avant de la cible (+z local)
                q.transform.localRotation = Quaternion.Euler(0, 180, 0);
                q.GetComponent<MeshRenderer>().sharedMaterial = targetMat;
                Destroy(q.GetComponent<Collider>());
                var bc = q.AddComponent<BoxCollider>(); bc.size = new Vector3(1, 1, 0.02f);
                q.AddComponent<Surface>().kind = "cardboard";
                tt.board = q.transform;
                Box(tt.pivot, new Vector3(0, 0.49f, -0.012f), new Vector3(0.6f, 1.02f, 0.015f), E7Assets.Mat("world", "wood"));
            }
            else
            {
                // disque d'acier au bout d'une tige, qui pivote en haut du poteau
                Box(tt.pivot, new Vector3(0, 0.3f, 0), new Vector3(0.01f, 0.56f, 0.01f), steel);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "board"; disc.transform.SetParent(tt.pivot, false);
                disc.transform.localPosition = new Vector3(0, 0.72f, 0);
                disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
                disc.transform.localScale = new Vector3(0.32f, 0.006f, 0.32f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
                Destroy(disc.GetComponent<Collider>());
                var sc = disc.AddComponent<BoxCollider>(); sc.size = new Vector3(1, 2, 1);
                disc.AddComponent<Surface>().kind = "metal";
                tt.board = disc.transform;
            }
            U.SetLayer(go, L.Default);
            go.SetActive(false);
            return tt;
        }

        static Transform Box(Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos; b.transform.localScale = size;
            Destroy(b.GetComponent<Collider>());
            b.GetComponent<MeshRenderer>().sharedMaterial = m;
            return b.transform;
        }

        /// <summary>Renvoie la zone touchée : "head", "center", "body" ou "steel".</summary>
        public string Hit(Vector3 point)
        {
            if (kind == "paper")
            {
                var lp = board.InverseTransformPoint(point);
                float u = lp.x + 0.5f, v = lp.y + 0.5f;
                string zone = "body";
                if (v > 0.72f && Mathf.Abs(u - 0.5f) < 0.17f) zone = "head";
                else if (Mathf.Pow((u - 0.5f) / 0.24f, 2) + Mathf.Pow((v - 0.442f) / 0.18f, 2) < 1f) zone = "center";
                if (holes.Count < 24)
                {
                    var h = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(h.GetComponent<Collider>());
                    h.transform.SetParent(board, false);
                    h.transform.localPosition = new Vector3(lp.x, lp.y, -0.003f);
                    h.transform.localScale = new Vector3(0.028f / 0.56f, 0.028f / 0.98f, 1);
                    h.GetComponent<MeshRenderer>().sharedMaterial = holeMat;
                    holes.Add(h);
                }
                if (!down) { down = true; t = 0; }
                AudioSys.I.Sfx3D("paper", point, 0.6f, false);
                return zone;
            }
            swingV += 3f;
            AudioSys.I.Sfx3D("ding", point, 0.8f, false);
            return "steel";
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (kind == "paper")
            {
                if (down) { t += dt; if (t > 2.6f) { down = false; foreach (var h in holes) Destroy(h); holes.Clear(); AudioSys.I.Sfx3D("click", transform.position, 0.3f, false); } }
                float target = down ? -85f : 0f;
                var e = pivot.localEulerAngles; float cur = e.x > 180 ? e.x - 360 : e.x;
                cur = U.Damp(cur, target, down ? 14f : 6f, dt);
                pivot.localEulerAngles = new Vector3(cur, 0, 0);
            }
            else
            {
                swingV += -swing * 40f * dt; swingV *= Mathf.Exp(-dt * 1.5f); swing += swingV * dt;
                pivot.localEulerAngles = new Vector3(swing * 0.8f * Mathf.Rad2Deg, 0, 0);
            }
        }
    }
}
