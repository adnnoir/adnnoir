using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace E7
{
    /// <summary>
    /// L'entrepôt, construit à partir de la même carte que la version web.
    /// # mur · S étagère · C caisse · B fûts · I poteau · D porte · L néon · H suspension
    /// P joueur · E suspect · M soins · A munitions
    /// </summary>
    public class Level : MonoBehaviour
    {
        public static Level I { get; private set; }

        public static readonly string[] MAP =
        {
            "############################",
            "#P.....#.......H.......#...#",
            "#......#.I...........I.#.E.#",
            "#..L...D...C...C...C...D...#",
            "#.M..B.#...............#..A#",
            "#..C...#...SSSSSSSS....#.L.#",
            "###D####.I...........I.#...#",
            "#......#...H......H....##D##",
            "#..E...#...SSSSSSSS....#...#",
            "#......D.C.............D.E.#",
            "#..L...#.I....H......I.#..B#",
            "#.B....#...SSSSSSSS....#.M.#",
            "####D###.......E.......##D##",
            "#......#..C..B....C....#...#",
            "#..A...#.I.....H.....I.#.L.#",
            "#..L...D...SSSSSSSS....D...#",
            "#....E.#.............E.#.E.#",
            "############################",
        };
        public const float T = 2f, WALL_H = 3.2f, DOOR_H = 2.3f;
        public static int Rows => MAP.Length;
        public static int Cols => MAP[0].Length;

        public static char Tile(int c, int r) => (r < 0 || r >= Rows || c < 0 || c >= Cols) ? '#' : MAP[r][c];
        public static bool BlocksWalk(char ch) => "#SCBI".IndexOf(ch) >= 0;
        public static Vector3 TileCenter(int c, int r) => new Vector3((c + 0.5f) * T, 0, (r + 0.5f) * T);
        public static Vector2Int TileOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(p.z / T));

        public Vector3 PlayerSpawn { get; private set; }
        public Vector3 TrainingSpawn => TileCenter(10, 9);
        public readonly List<Vector3> EnemySpawns = new List<Vector3>();
        public readonly List<Vector2Int> WalkTiles = new List<Vector2Int>();
        public readonly List<CoverPoint> Covers = new List<CoverPoint>();
        public readonly List<Pickup> Pickups = new List<Pickup>();
        public readonly List<TrainingTarget> Targets = new List<TrainingTarget>();
        readonly List<Fixture> fixtures = new List<Fixture>();
        Transform root;
        Collider ceilingCol;
        NavMeshDataInstance navInstance;
        public int NavAgentType { get; private set; }

        public static Level Build(Transform parent)
        {
            var go = new GameObject("Level");
            go.transform.SetParent(parent, false);
            var lv = go.AddComponent<Level>();
            I = lv;
            lv.root = go.transform;
            lv.Construct();
            return lv;
        }

        Material M(string key) => E7Assets.Mat("world", key);

        void Construct()
        {
            Random.InitState(7);
            var batches = new Dictionary<Vector2Int, MeshGen.Batch>();
            MeshGen.Batch B(Vector3 p)
            {
                // découpe en morceaux de 7 × 6 cases : chaque morceau n'est éclairé que par les lumières proches
                var k = new Vector2Int(Mathf.FloorToInt(p.x / (7 * T)), Mathf.FloorToInt(p.z / (6 * T)));
                if (!batches.TryGetValue(k, out var b)) batches[k] = b = new MeshGen.Batch();
                return b;
            }
            var lightSpots = new List<Vector3>(); var pendantSpots = new List<Vector3>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    char ch = Tile(c, r); var p = TileCenter(c, r);
                    if (!BlocksWalk(ch)) WalkTiles.Add(new Vector2Int(c, r));
                    switch (ch)
                    {
                        case '#':
                            B(p).Box("wall", p + Vector3.up * WALL_H / 2, new Vector3(T, WALL_H, T));
                            break;
                        case 'D':
                        {
                            B(p).Box("wall", p + Vector3.up * (WALL_H + DOOR_H) / 2, new Vector3(T, WALL_H - DOOR_H, T));
                            Solid(p + Vector3.up * (WALL_H + DOOR_H) / 2, new Vector3(T, WALL_H - DOOR_H, T), "concrete");
                            bool vertical = BlocksWalk(Tile(c - 1, r));
                            if (vertical)
                            {
                                B(p).Box("steel", new Vector3(p.x, DOOR_H - 0.05f, p.z - T / 2 + 0.06f), new Vector3(T, 0.1f, 0.12f));
                                B(p).Box("steel", new Vector3(p.x, DOOR_H - 0.05f, p.z + T / 2 - 0.06f), new Vector3(T, 0.1f, 0.12f));
                                var leaf = new Vector3(p.x + T / 2 - 0.1f, (DOOR_H - 0.05f) / 2, p.z - T / 2 + 0.5f);
                                B(p).Box("steel", leaf, new Vector3(0.05f, DOOR_H - 0.05f, 0.95f));
                                Solid(leaf, new Vector3(0.05f, DOOR_H - 0.05f, 0.95f), "metal");
                            }
                            else
                            {
                                B(p).Box("steel", new Vector3(p.x - T / 2 + 0.06f, DOOR_H - 0.05f, p.z), new Vector3(0.12f, 0.1f, T));
                                B(p).Box("steel", new Vector3(p.x + T / 2 - 0.06f, DOOR_H - 0.05f, p.z), new Vector3(0.12f, 0.1f, T));
                                var leaf = new Vector3(p.x - T / 2 + 0.5f, (DOOR_H - 0.05f) / 2, p.z + T / 2 - 0.1f);
                                B(p).Box("steel", leaf, new Vector3(0.95f, DOOR_H - 0.05f, 0.05f));
                                Solid(leaf, new Vector3(0.95f, DOOR_H - 0.05f, 0.05f), "metal");
                            }
                            break;
                        }
                        case 'C':
                        {
                            float s = Random.Range(1.15f, 1.4f), ry = Random.Range(-14f, 14f);
                            B(p).Box("crate", p + Vector3.up * 0.6f, new Vector3(s, 1.2f, s), ry);
                            var col = Solid(p + Vector3.up * 0.6f, new Vector3(s, 1.2f, s), "wood"); col.transform.rotation = Quaternion.Euler(0, ry, 0);
                            float h = 1.2f;
                            if (Random.value < 0.5f)
                            {
                                float s2 = Random.Range(0.7f, 0.9f), ry2 = Random.Range(-34f, 34f);
                                var p2 = new Vector3(p.x + Random.Range(-0.15f, 0.15f), 1.6f, p.z + Random.Range(-0.15f, 0.15f));
                                B(p).Box("crate", p2, new Vector3(s2, 0.8f, s2), ry2);
                                var c2 = Solid(p2, new Vector3(s2, 0.8f, s2), "wood"); c2.transform.rotation = Quaternion.Euler(0, ry2, 0);
                                h = 2f;
                            }
                            AddCoversAround(c, r, h < 1.5f);
                            break;
                        }
                        case 'S':
                        {
                            foreach (float sx in new[] { -0.95f, 0.95f }) foreach (float sz in new[] { -0.45f, 0.45f })
                                B(p).Box("rackPost", new Vector3(p.x + sx, 1.45f, p.z + sz), new Vector3(0.08f, 2.9f, 0.08f));
                            foreach (float y in new[] { 0.12f, 1.02f, 1.92f, 2.8f })
                            {
                                B(p).Box("rackShelf", new Vector3(p.x, y, p.z), new Vector3(2f, 0.05f, 0.96f));
                                if (y < 2.7f)
                                {
                                    float x = p.x - 0.9f;
                                    while (x < p.x + 0.7f)
                                    {
                                        float bw = Random.Range(0.35f, 0.75f), bh = Random.Range(0.28f, 0.72f), bd = Random.Range(0.5f, 0.85f);
                                        if (x + bw > p.x + 0.95f) break;
                                        if (Random.value < 0.8f) B(p).Box("card", new Vector3(x + bw / 2, y + 0.025f + bh / 2, p.z + Random.Range(-0.05f, 0.05f)), new Vector3(bw, bh, bd), Random.Range(-4.5f, 4.5f));
                                        x += bw + Random.Range(0.02f, 0.25f);
                                    }
                                }
                            }
                            Solid(p + Vector3.up * 1.5f, new Vector3(2f, 3f, 1f), "cardboard");
                            AddCoversAround(c, r, false);
                            break;
                        }
                        case 'B':
                        {
                            int n = 2 + Random.Range(0, 2);
                            for (int i = 0; i < n; i++)
                            {
                                float a = i / (float)n * Mathf.PI * 2 + Random.Range(0f, 1f);
                                B(p).Cyl("barrel", new Vector3(p.x + Mathf.Cos(a) * 0.35f, 0.44f, p.z + Mathf.Sin(a) * 0.35f), 0.29f, 0.88f);
                            }
                            Solid(p + Vector3.up * 0.45f, new Vector3(1.4f, 0.9f, 1.4f), "metal");
                            AddCoversAround(c, r, true);
                            break;
                        }
                        case 'I':
                            B(p).Box("steel", new Vector3(p.x, WALL_H / 2, p.z - 0.13f), new Vector3(0.3f, WALL_H, 0.04f));
                            B(p).Box("steel", new Vector3(p.x, WALL_H / 2, p.z + 0.13f), new Vector3(0.3f, WALL_H, 0.04f));
                            B(p).Box("steel", new Vector3(p.x, WALL_H / 2, p.z), new Vector3(0.04f, WALL_H, 0.26f));
                            B(p).Box("steel", new Vector3(p.x, 0.01f, p.z), new Vector3(0.45f, 0.02f, 0.45f));
                            Solid(p + Vector3.up * WALL_H / 2, new Vector3(0.36f, WALL_H, 0.36f), "metal");
                            break;
                        case 'L': lightSpots.Add(p); break;
                        case 'H': pendantSpots.Add(p); break;
                        case 'E': EnemySpawns.Add(p); break;
                        case 'P': PlayerSpawn = p; break;
                        case 'M': case 'A': Pickups.Add(Pickup.Create(root, ch == 'M' ? PickupType.Medkit : PickupType.Ammo, p)); break;
                    }
                }
            }
            WallColliders();
            // poutres et tuyaux au plafond du grand hall
            for (int x = 8; x <= 23; x += 2) { var bp = new Vector3(x * T, WALL_H - 0.14f, 9 * T); B(bp).Box("steel", bp, new Vector3(0.18f, 0.28f, 16 * T)); }
            foreach (float z in new[] { 2.3f, 33.7f })
            {
                var pp = new Vector3(15.5f * T, WALL_H - 0.35f, z);
                B(pp).Add("steel", MeshGen.Cylinder, Matrix4x4.TRS(pp, Quaternion.Euler(0, 0, 90), new Vector3(0.16f, 15 * T * 0.5f, 0.16f)));
            }
            // palettes au sol
            for (int i = 0; i < 7; i++)
            {
                var t = WalkTiles[Random.Range(0, WalkTiles.Count)]; var p = TileCenter(t.x, t.y);
                if (Vector3.Distance(p, PlayerSpawn) < 3) continue;
                float ry = Random.Range(0, 180f); var o = new Vector3(p.x + Random.Range(-0.4f, 0.4f), 0, p.z + Random.Range(-0.4f, 0.4f));
                var q = Quaternion.Euler(0, ry, 0);
                for (int k = 0; k < 5; k++) B(o).Add("wood", MeshGen.Cube, Matrix4x4.TRS(o + q * new Vector3(0, 0.13f, -0.5f + k * 0.25f), q, new Vector3(1.2f, 0.025f, 0.12f)));
                for (int k = 0; k < 3; k++) B(o).Add("wood", MeshGen.Cube, Matrix4x4.TRS(o + q * new Vector3(-0.5f + k * 0.5f, 0.06f, 0), q, new Vector3(0.1f, 0.11f, 1.1f)));
            }
            // fusion : un objet par matériau et par morceau de carte
            var surfaceOf = new Dictionary<string, string> { { "wall", "concrete" }, { "crate", "wood" }, { "rackPost", "metal" }, { "rackShelf", "metal" }, { "card", "cardboard" }, { "steel", "metal" }, { "barrel", "metal" }, { "wood", "wood" } };
            foreach (var kv in batches)
            {
                foreach (var m in kv.Value.Build())
                {
                    var go = U.Child(root, m.Key + "_" + kv.Key.x + "_" + kv.Key.y);
                    go.AddComponent<MeshFilter>().sharedMesh = m.Value;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = M(m.Key);
                    mr.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            // sol et plafond
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor"; floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(Cols * T / 2, 0, Rows * T / 2);
            floor.transform.rotation = Quaternion.Euler(90, 0, 0);
            floor.transform.localScale = new Vector3(Cols * T, Rows * T, 1);
            floor.GetComponent<MeshRenderer>().sharedMaterial = M("floor");
            Destroy(floor.GetComponent<Collider>());
            Solid(new Vector3(Cols * T / 2, -0.25f, Rows * T / 2), new Vector3(Cols * T, 0.5f, Rows * T), "concrete").name = "FloorCollider";
            var ceil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceil.name = "Ceiling"; ceil.transform.SetParent(root, false);
            ceil.transform.position = new Vector3(Cols * T / 2, WALL_H, Rows * T / 2);
            ceil.transform.rotation = Quaternion.Euler(-90, 0, 0);
            ceil.transform.localScale = new Vector3(Cols * T, Rows * T, 1);
            ceil.GetComponent<MeshRenderer>().sharedMaterial = M("ceil");
            Destroy(ceil.GetComponent<Collider>());
            ceilingCol = Solid(new Vector3(Cols * T / 2, WALL_H + 0.25f, Rows * T / 2), new Vector3(Cols * T, 0.5f, Rows * T), "concrete");
            Decor();
            Lights(lightSpots, pendantSpots);
            MoonWindows();
            foreach (var spot in new[] { (12, 9, "paper"), (16, 9, "paper"), (20, 10, "paper"), (17, 13, "paper"), (14, 13, "paper"), (20, 2, "paper"), (15, 2, "steel"), (11, 2, "paper"), (19, 16, "steel"), (13, 16, "paper"), (22, 9, "steel") })
                if (!BlocksWalk(Tile(spot.Item1, spot.Item2))) Targets.Add(TrainingTarget.Create(root, TileCenter(spot.Item1, spot.Item2), spot.Item3));
            BuildNavMesh();
            // sonde de reflets : donne leurs reflets aux armes métalliques
            var probeGo = U.Child(root, "Reflets", new Vector3(15 * T, 1.6f, 9 * T));
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.size = new Vector3(Cols * T, WALL_H * 2, Rows * T);
            probe.intensity = 0.7f;
            probe.RenderProbe();
        }

        // ---------------- collisions ----------------
        Collider Solid(Vector3 center, Vector3 size, string surface)
        {
            var go = new GameObject("col_" + surface);
            go.transform.SetParent(root, false);
            go.transform.position = center;
            var bc = go.AddComponent<BoxCollider>(); bc.size = size;
            go.AddComponent<Surface>().kind = surface;
            go.isStatic = true;
            return bc;
        }

        void WallColliders()
        {
            // murs : on fusionne les murs voisins d'une même ligne en une seule boîte
            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Cols)
                {
                    if (Tile(c, r) != '#') { c++; continue; }
                    int s = c;
                    while (c < Cols && Tile(c, r) == '#') c++;
                    var a = TileCenter(s, r); var b = TileCenter(c - 1, r);
                    Solid((a + b) / 2 + Vector3.up * WALL_H / 2, new Vector3((c - s) * T, WALL_H, T), "concrete");
                }
            }
        }

        // ---------------- couverture pour l'IA ----------------
        void AddCoversAround(int c, int r, bool low)
        {
            var obst = TileCenter(c, r);
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                char n = Tile(c + d.x, r + d.y);
                if (BlocksWalk(n) || n == 'D') continue;
                var p = TileCenter(c + d.x, r + d.y) - new Vector3(d.x, 0, d.y) * 0.35f;
                Covers.Add(new CoverPoint { pos = p, towards = (obst - p).normalized, low = low });
            }
        }

        // ---------------- NavMesh (carte de déplacement de l'IA) ----------------
        void BuildNavMesh()
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1.75f;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 40f;
            NavAgentType = settings.agentTypeID;
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(root, 1 << L.Default, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(s => s.component == ceilingCol);
            var bounds = new Bounds(new Vector3(Cols * T / 2, 1.5f, Rows * T / 2), new Vector3(Cols * T + 2, 6, Rows * T + 2));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) navInstance = NavMesh.AddNavMeshData(data);
            else Debug.LogWarning("[E7] NavMesh non construit");
        }

        void OnDestroy() { if (navInstance.valid) navInstance.Remove(); if (I == this) I = null; }

        // ---------------- décor ----------------
        void Decor()
        {
            var paperMat = E7Assets.Simple(Color.white, 0, 0.9f); paperMat.SetTexture("_MainTex", E7Assets.Tex("paper")); paperMat.SetFloat("_Cull", 0);
            var canMat = E7Assets.Simple(U.Hex("#a33a2a"), 0.8f, 0.35f);
            var papers = new MeshGen.Batch(); var cans = new MeshGen.Batch();
            for (int i = 0; i < 45; i++)
            {
                var t = WalkTiles[Random.Range(0, WalkTiles.Count)]; var p = TileCenter(t.x, t.y);
                var pos = new Vector3(p.x + Random.Range(-0.9f, 0.9f), 0.006f + i * 0.00005f, p.z + Random.Range(-0.9f, 0.9f));
                papers.Add("paper", MeshGen.Quad, Matrix4x4.TRS(pos, Quaternion.Euler(90, Random.Range(0, 360f), 0), new Vector3(0.21f, 0.28f, 1)));
            }
            for (int i = 0; i < 12; i++)
            {
                var t = WalkTiles[Random.Range(0, WalkTiles.Count)]; var p = TileCenter(t.x, t.y);
                var pos = new Vector3(p.x + Random.Range(-0.8f, 0.8f), 0.033f, p.z + Random.Range(-0.8f, 0.8f));
                cans.Add("can", MeshGen.Cylinder, Matrix4x4.TRS(pos, Quaternion.Euler(90, Random.Range(0, 360f), 0), new Vector3(0.066f, 0.06f, 0.066f)));
            }
            foreach (var (b, mat) in new[] { (papers, paperMat), (cans, canMat) })
                foreach (var m in b.Build())
                {
                    var go = U.Child(root, m.Key);
                    go.AddComponent<MeshFilter>().sharedMesh = m.Value;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                }
            // panneau SORTIE (vert lumineux) au-dessus de la porte de la pièce de départ
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Sortie"; sign.transform.SetParent(root, false);
            sign.transform.position = new Vector3(7 * T - 0.05f, DOOR_H + 0.25f, 3.5f * T);
            sign.transform.localScale = new Vector3(0.06f, 0.16f, 0.42f);
            Destroy(sign.GetComponent<Collider>());
            sign.GetComponent<MeshRenderer>().sharedMaterial = E7Assets.UnlitMat(new Color(0.2f, 1.6f, 0.5f));
            var sl = U.Child(root, "SortieLight", sign.transform.position + Vector3.left * 0.3f).AddComponent<Light>();
            sl.type = LightType.Point; sl.color = new Color(0.2f, 1f, 0.4f); sl.intensity = 0.6f; sl.range = 3f;
        }

        void MoonWindows()
        {
            var moon = E7Assets.UnlitMat(new Color(0.35f, 0.48f, 0.66f));
            foreach (int x in new[] { 10, 13, 16, 19 })
                foreach (int side in new[] { 0, 1 })
                {
                    float z = side == 1 ? (Rows - 1) * T - 0.004f : T + 0.004f;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Fenetre"; q.transform.SetParent(root, false);
                    q.transform.position = new Vector3(x * T + T / 2, 2.65f, z);
                    q.transform.rotation = Quaternion.Euler(0, side == 1 ? 0 : 180, 0);
                    q.transform.localScale = new Vector3(1.2f, 0.55f, 1);
                    Destroy(q.GetComponent<Collider>());
                    q.GetComponent<MeshRenderer>().sharedMaterial = moon;
                }
            // lueur bleutée de la lune
            var d = U.Child(root, "Lune").AddComponent<Light>();
            d.type = LightType.Directional; d.color = new Color(0.45f, 0.55f, 0.8f); d.intensity = 0.06f; d.shadows = LightShadows.None;
            d.transform.rotation = Quaternion.Euler(50, 30, 0);
        }

        void Lights(List<Vector3> neons, List<Vector3> pendants)
        {
            var housingMat = E7Assets.Simple(U.Hex("#2a2b2d"), 0.4f, 0.7f);
            var shadeMat = E7Assets.Simple(U.Hex("#3b4a3f"), 0.6f, 0.45f); shadeMat.SetFloat("_Cull", 0);
            var coneTex = E7Assets.Tex("cone");
            bool high = Save.Data.settings.quality == "haut";
            for (int i = 0; i < neons.Count; i++)
            {
                var p = neons[i];
                var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                housing.name = "Neon"; housing.transform.SetParent(root, false);
                housing.transform.position = new Vector3(p.x, WALL_H - 0.08f, p.z); housing.transform.localScale = new Vector3(1.35f, 0.07f, 0.26f);
                Destroy(housing.GetComponent<Collider>()); housing.GetComponent<MeshRenderer>().sharedMaterial = housingMat;
                var tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tube.name = "Tube"; tube.transform.SetParent(root, false);
                tube.transform.position = new Vector3(p.x, WALL_H - 0.13f, p.z); tube.transform.localScale = new Vector3(1.2f, 0.035f, 0.09f);
                Destroy(tube.GetComponent<Collider>());
                var tubeMat = E7Assets.UnlitMat(new Color(3.4f, 3.6f, 4f));
                tube.GetComponent<MeshRenderer>().sharedMaterial = tubeMat;
                var l = U.Child(root, "NeonLight", new Vector3(p.x, WALL_H - 0.35f, p.z)).AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.87f, 0.9f, 1f); l.range = 14f; l.intensity = 2.1f;
                l.shadows = i == 0 || high ? LightShadows.Soft : LightShadows.None;
                float roll = Random.value;
                var kind = i == 0 ? Fixture.Kind.Ok : roll < 0.62f ? Fixture.Kind.Ok : roll < 0.87f ? Fixture.Kind.Flicker : Fixture.Kind.Dead;
                fixtures.Add(new Fixture(l, tubeMat, kind, 2.1f, new Color(3.4f, 3.6f, 4f), null));
            }
            foreach (var p in pendants)
            {
                var cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cable.name = "Cable"; cable.transform.SetParent(root, false);
                cable.transform.position = new Vector3(p.x, WALL_H - 0.45f, p.z); cable.transform.localScale = new Vector3(0.016f, 0.45f, 0.016f);
                Destroy(cable.GetComponent<Collider>()); cable.GetComponent<MeshRenderer>().sharedMaterial = housingMat;
                var shade = U.Child(root, "Abat-jour", new Vector3(p.x, WALL_H - 1.13f, p.z));
                shade.AddComponent<MeshFilter>().sharedMesh = MeshGen.Cone(0.09f, 0.32f, 0.26f);
                shade.AddComponent<MeshRenderer>().sharedMaterial = shadeMat;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Ampoule"; bulb.transform.SetParent(root, false);
                bulb.transform.position = new Vector3(p.x, WALL_H - 1.1f, p.z); bulb.transform.localScale = Vector3.one * 0.14f;
                Destroy(bulb.GetComponent<Collider>());
                var bulbMat = E7Assets.UnlitMat(new Color(6f, 4.6f, 3f)); bulb.GetComponent<MeshRenderer>().sharedMaterial = bulbMat;
                var l = U.Child(root, "Suspension", new Vector3(p.x, WALL_H - 1.2f, p.z)).AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.81f, 0.59f); l.range = 12f; l.intensity = 2.6f;
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.9f;
                // cône de lumière (faux volume)
                var cone = U.Child(root, "Cone", new Vector3(p.x, 0.02f, p.z));
                cone.AddComponent<MeshFilter>().sharedMesh = MeshGen.Cone(0.2f, 1.6f, WALL_H - 1.1f, 20, false);
                var coneMat = E7Assets.UnlitMat(new Color(1f, 0.79f, 0.54f, 0.05f), coneTex, true, false, true);
                cone.AddComponent<MeshRenderer>().sharedMaterial = coneMat;
                var kind = Random.value < 0.2f ? Fixture.Kind.Flicker : Fixture.Kind.Ok;
                fixtures.Add(new Fixture(l, bulbMat, kind, 2.6f, new Color(6f, 4.6f, 3f), cone));
            }
        }

        void Update()
        {
            float hum = 0; var cam = Camera.main;
            foreach (var f in fixtures)
            {
                f.Update(Time.deltaTime);
                if (cam && f.light.intensity > 0) { float d = Vector3.Distance(f.light.transform.position, cam.transform.position); hum += 1f / (d * d + 1f); }
            }
            if (AudioSys.I) AudioSys.I.Hum(hum * 0.06f);
        }

        public void SetMode(bool training)
        {
            foreach (var t in Targets) t.gameObject.SetActive(training);
            foreach (var p in Pickups) p.ResetFor(!training);
        }

        public Vector3 RandomFloorPoint()
        {
            var t = WalkTiles[Random.Range(0, WalkTiles.Count)];
            var p = TileCenter(t.x, t.y);
            return new Vector3(p.x + Random.Range(-0.8f, 0.8f), 0, p.z + Random.Range(-0.8f, 0.8f));
        }

        public bool SameRoomish(Vector3 a, Vector3 b) => !Physics.Linecast(a + Vector3.up * 1.5f, b + Vector3.up * 1.5f, L.SightMask, QueryTriggerInteraction.Ignore);
    }

    public class CoverPoint
    {
        public Vector3 pos, towards;
        public bool low;
        public object owner; // IA qui l'occupe
    }

    /// <summary>Néon ou suspension, qui peut clignoter ou être en panne.</summary>
    public class Fixture
    {
        public enum Kind { Ok, Flicker, Dead }
        public readonly Light light;
        readonly Material mat; readonly Kind kind; readonly float baseI; readonly Color emis; readonly GameObject cone;
        float t; bool on;

        public Fixture(Light l, Material m, Kind k, float intensity, Color emission, GameObject coneGo)
        {
            light = l; mat = m; kind = k; baseI = intensity; emis = emission; cone = coneGo;
            on = k != Kind.Dead; t = Random.Range(0f, 10f);
            if (k == Kind.Dead) { light.intensity = 0; mat.SetColor("_Color", emis * 0.01f); }
        }

        public void Update(float dt)
        {
            if (kind != Kind.Flicker) return;
            t -= dt;
            if (t <= 0) { on = !on; t = on ? Random.Range(0.2f, 4f) : Random.Range(0.03f, 0.25f); }
            light.intensity = on ? baseI : 0;
            mat.SetColor("_Color", on ? emis : emis * 0.015f);
            if (cone) cone.SetActive(on);
        }
    }
}
