#if E7_MLAGENTS
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

namespace E7.ML
{
    /// <summary>
    /// Arène d'entraînement : dans l'entrepôt, des suspects-agents (apprenants) affrontent des policiers IA (règles).
    /// Chaque « épisode » se termine quand un camp est éliminé ou après 60 s ; tout est alors remis en place.
    /// Lancer l'entraînement : voir ml/README.md (commande mlagents-learn), puis Play et « ARÈNE IA ».
    /// </summary>
    public class Arena : MonoBehaviour
    {
        public static Arena I;
        readonly List<SuspectAgent> agents = new List<SuspectAgent>();
        float episodeT;
        public int episodes;
        const int Agents = 3, Officers = 2;
        const float EpisodeLength = 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            MLHooks.Available = true;
            MLHooks.StartArena = () =>
            {
                if (I == null) I = new GameObject("Arene").AddComponent<Arena>();
                I.enabled = true; I.NewEpisode();
            };
            MLHooks.StopArena = () => { if (I != null) { I.Clear(); I.enabled = false; } };
            // en mission, si le réglage « Réseau » est choisi : l'agent (avec son modèle .onnx) pilote le suspect
            MLHooks.MakeBrain = s =>
            {
                if (Game.I != null && Game.I.Mode == GameMode.Arena) return null; // l'arène branche ses propres agents
                var model = SuspectAgent.FindModel();
                if (model == null) return null; // pas de modèle entraîné : le suspect garde les règles
                var a = SuspectAgent.Attach(s, false);
                SuspectAgent.ApplyModel(a, model);
                return new AgentBrain(a);
            };
        }

        void Clear()
        {
            foreach (var a in agents) if (a != null) Destroy(a.gameObject);
            agents.Clear();
            if (Squad.I != null) Squad.I.Clear();
        }

        void NewEpisode()
        {
            // les anciens agents terminent leur épisode avant d'être remplacés
            foreach (var a in agents) if (a != null) a.EndEpisode();
            Clear();
            episodes++; episodeT = 0;
            var sq = Squad.I;
            sq.diff = Difficulty.Get(Save.Data.settings.diff);
            var spawns = new List<Vector3>(Level.I.EnemySpawns);
            for (int i = 0; i < Agents; i++)
            {
                var p = spawns[Random.Range(0, spawns.Count)]; spawns.Remove(p);
                var s = SuspectAI.Create(sq.transform, p, i, Squad.BotWeapons[Random.Range(0, Squad.BotWeapons.Length)]);
                var agent = SuspectAgent.Attach(s, true);
                s.brain = new AgentBrain(agent);
                s.Engage(true); // dans l'arène, tout le monde est déjà en alerte
                sq.suspects.Add(s);
                agents.Add(agent);
            }
            for (int i = 0; i < Officers; i++)
            {
                var t = Level.I.WalkTiles[Random.Range(0, Level.I.WalkTiles.Count)];
                var m = TeammateAI.Create(sq.transform, Level.TileCenter(t.x, t.y), i);
                m.Hold(m.transform.position);
                sq.mates.Add(m);
            }
        }

        void Update()
        {
            if (Game.I == null || Game.I.Mode != GameMode.Arena || Game.I.State != GameState.Playing) return;
            episodeT += Time.deltaTime;
            bool agentsDown = agents.TrueForAll(a => a == null || a.me == null || !a.me.Alive);
            bool officersDown = Squad.I.mates.TrueForAll(m => m == null || !m.Alive);
            if (agentsDown || officersDown || episodeT > EpisodeLength)
            {
                foreach (var a in agents)
                {
                    if (a == null || a.me == null) continue;
                    if (officersDown && a.me.Alive) a.AddReward(1f);        // victoire
                    if (!a.me.Alive) a.AddReward(-1f);                      // mort
                }
                NewEpisode();
            }
        }

        void OnGUI()
        {
            if (Game.I == null || Game.I.Mode != GameMode.Arena) return;
            float sum = 0; foreach (var a in agents) if (a != null) sum += a.GetCumulativeReward();
            GUI.Label(new Rect(20, 120, 600, 60), "ARÈNE IA · épisode " + episodes + " · " + Mathf.RoundToInt(episodeT) + " s · récompense moyenne " + (agents.Count > 0 ? (sum / agents.Count).ToString("0.00") : "0") + "\n" + (Academy.Instance.IsCommunicatorOn ? "Connecté à Python : entraînement en cours" : "Pas de Python connecté : l'agent utilise son modèle ou le clavier (flèches, Espace)"));
        }
    }
}
#endif
