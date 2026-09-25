#if E7_MLAGENTS
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace E7.ML
{
    /// <summary>
    /// Agent ML-Agents qui pilote un suspect.
    ///
    /// Observations (32 nombres) : ce que le suspect « sait » (cible vue ?, où ?, sa vie, ses munitions,
    /// les murs autour de lui, l'abri le plus proche…).
    /// Actions : 2 continues (avancer/reculer, pas de côté) + 3 choix (tourner gauche/rien/droite, tirer, s'accroupir).
    /// Récompenses : + toucher la cible, ++ la neutraliser, − être touché, −− mourir, petit bonus d'abri en rechargeant.
    ///
    /// Idées d'amélioration (à faire toi-même avec le mode prof !) : récompense pour contourner, pénalité si on tire
    /// sans voir la cible, curriculum (d'abord 1 policier immobile, puis 2 qui bougent).
    /// </summary>
    public class SuspectAgent : Agent
    {
        public const string BehaviorName = "Suspect";
        public const int ObsSize = 32;
        public SuspectAI me;
        public bool training;
        float episodeT;

        /// <summary>Ajoute les composants ML-Agents à un suspect.</summary>
        public static SuspectAgent Attach(SuspectAI s, bool training)
        {
            var go = s.gameObject;
            var bp = go.GetComponent<BehaviorParameters>();
            if (bp == null) bp = go.AddComponent<BehaviorParameters>();
            bp.BehaviorName = BehaviorName;
            bp.BrainParameters.VectorObservationSize = ObsSize;
            bp.BrainParameters.NumStackedVectorObservations = 1;
            bp.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 3, 2, 2 });
            bp.BehaviorType = BehaviorType.Default; // entraînement si Python est connecté, sinon modèle ou règles
            var a = go.AddComponent<SuspectAgent>();
            a.me = s; a.training = training;
            a.MaxStep = training ? 3000 : 0;
            var dr = go.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 5; dr.TakeActionsBetweenDecisions = true;
            s.OnHurt += d => a.AddReward(-d / 100f);
            s.OnDealt += (d, kill) => { a.AddReward(d / 60f); if (kill) a.AddReward(1f); };
            return a;
        }

        public override void OnEpisodeBegin() { episodeT = 0; }

        /// <summary>
        /// Charge le réseau entraîné le plus récent (fichiers .onnx dans Resources/E7/Brains, ex. suspect_v3.onnx).
        /// Passe par la réflexion pour marcher avec ML-Agents 2 (Barracuda), 3 (Sentis) ou 4 (Inference Engine).
        /// </summary>
        public static Object FindModel()
        {
            System.Type t = null;
            foreach (var n in new[] { "Unity.InferenceEngine.ModelAsset, Unity.InferenceEngine", "Unity.Sentis.ModelAsset, Unity.Sentis", "Unity.Barracuda.NNModel, Unity.Barracuda" })
            { t = System.Type.GetType(n); if (t != null) break; }
            if (t == null) { Debug.LogWarning("[E7] Aucun moteur d'inférence trouvé (Sentis / Inference Engine / Barracuda)"); return null; }
            var all = Resources.LoadAll("E7/Brains", t);
            if (all == null || all.Length == 0) { Debug.LogWarning("[E7] Aucun cerveau entraîné dans Resources/E7/Brains : les suspects gardent les règles"); return null; }
            Object best = null;
            // le plus récent par ordre alphabétique : nomme tes versions suspect_v01, suspect_v02… (voir ml/README.md)
            foreach (var m in all) if (best == null || string.CompareOrdinal(m.name, best.name) > 0) best = m;
            return best;
        }

        public static void ApplyModel(SuspectAgent a, Object model)
        {
            var bp = a.GetComponent<BehaviorParameters>();
            var prop = typeof(BehaviorParameters).GetProperty("Model");
            if (prop == null || model == null) return;
            prop.SetValue(bp, model);
            bp.BehaviorType = BehaviorType.InferenceOnly;
            Debug.Log("[E7] Cerveau des suspects : " + model.name);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (me == null) { for (int i = 0; i < ObsSize; i++) sensor.AddObservation(0f); return; }
            var t = me.transform;
            // 1-7 : la cible
            bool has = me.target != null && me.target.Alive;
            sensor.AddObservation(has ? 1f : 0f);
            sensor.AddObservation(me.sees ? 1f : 0f);
            Vector3 rel = has ? t.InverseTransformPoint(me.target.Pos) : t.InverseTransformPoint(me.lastKnown);
            sensor.AddObservation(Mathf.Clamp(rel.x / 20f, -1, 1));
            sensor.AddObservation(Mathf.Clamp(rel.z / 20f, -1, 1));
            sensor.AddObservation(Mathf.Clamp01(rel.magnitude / 30f));
            sensor.AddObservation(has && me.target.Moving ? 1f : 0f);
            sensor.AddObservation(has ? me.target.Crouch : 0f);
            // 8-13 : soi-même
            sensor.AddObservation(me.hp / 100f);
            sensor.AddObservation(me.ammo / (float)me.ew.ammo);
            sensor.AddObservation(me.IsReloading ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp01(me.Suppression / 3f));
            sensor.AddObservation(me.Crouch);
            sensor.AddObservation(me.agent != null ? Mathf.Clamp01(me.agent.velocity.magnitude / 3.3f) : 0f);
            // 14-21 : distances aux obstacles autour (8 directions)
            for (int i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0, i * 45f, 0) * t.forward;
                float d = Physics.Raycast(t.position + Vector3.up * 1.0f, dir, out var h, 10f, L.WorldMask, QueryTriggerInteraction.Ignore) ? h.distance : 10f;
                sensor.AddObservation(d / 10f);
            }
            // 22-26 : abri le plus proche
            var c = me.NearestCover();
            Vector3 cr = c != null ? t.InverseTransformPoint(c.pos) : Vector3.zero;
            sensor.AddObservation(c != null ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp(cr.x / 12f, -1, 1));
            sensor.AddObservation(Mathf.Clamp(cr.z / 12f, -1, 1));
            sensor.AddObservation(c != null && c.low ? 1f : 0f);
            bool hidden = !has || Physics.Linecast(me.EyePos, me.target.Eye, L.SightMask, QueryTriggerInteraction.Ignore);
            sensor.AddObservation(hidden ? 1f : 0f);
            // 27-30 : orientation par rapport à la cible
            float ang = has ? Vector3.SignedAngle(t.forward, U.Flat(me.target.Pos - t.position), Vector3.up) : 0f;
            sensor.AddObservation(Mathf.Sin(ang * Mathf.Deg2Rad));
            sensor.AddObservation(Mathf.Cos(ang * Mathf.Deg2Rad));
            sensor.AddObservation(Squad.I != null ? Mathf.Clamp01(Squad.I.suspects.FindAll(x => x.Hostile).Count / 7f) : 0f);
            sensor.AddObservation(Squad.I != null ? Mathf.Clamp01(Squad.I.mates.FindAll(x => x.Alive).Count / 3f) : 0f);
            // 31-32 : temps et type d'arme
            sensor.AddObservation(Mathf.Clamp01(episodeT / 60f));
            sensor.AddObservation(me.wtype == "shotgun" ? 1f : me.wtype == "pistol" ? 0.5f : 0f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (me == null || !me.Alive) return;
            float dt = Time.deltaTime;
            episodeT += dt;
            var ca = actions.ContinuousActions; var da = actions.DiscreteActions;
            var t = me.transform;
            var move = t.forward * Mathf.Clamp(ca[0], -1, 1) + t.right * Mathf.Clamp(ca[1], -1, 1);
            me.BrainMove(move, 2.8f);
            me.BrainTurn((da[0] - 1) * 160f);
            if (da[1] == 1) { if (me.BrainShoot(dt)) { } else if (!me.sees) AddReward(-0.0005f); }
            me.BrainCrouch(da[2] == 1);
            if (me.ammo <= 0) me.BrainReload();
            // petit bonus : recharger à l'abri
            if (me.IsReloading && me.target != null && Physics.Linecast(me.EyePos, me.target.Eye, L.SightMask, QueryTriggerInteraction.Ignore)) AddReward(0.0005f);
            AddReward(-0.0002f); // le temps passe : pousse à agir
        }

        /// <summary>Contrôle au clavier pour tester l'agent sans réseau (flèches + Espace + Ctrl droit).</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var ca = actionsOut.ContinuousActions; var da = actionsOut.DiscreteActions;
            ca[0] = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            ca[1] = 0;
            da[0] = Input.GetKey(KeyCode.LeftArrow) ? 0 : Input.GetKey(KeyCode.RightArrow) ? 2 : 1;
            da[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;
            da[2] = Input.GetKey(KeyCode.RightControl) ? 1 : 0;
        }
    }

    /// <summary>Branche l'agent comme « cerveau » d'un suspect (voir ISuspectBrain).</summary>
    public class AgentBrain : ISuspectBrain
    {
        readonly SuspectAgent agent;
        public AgentBrain(SuspectAgent a) { agent = a; }

        public bool Decide(SuspectAI me, float dt)
        {
            // sonné, rendu ou mort : on laisse les règles gérer
            if (me.state == SuspectAI.S.Stunned || me.state == SuspectAI.S.Surrender || me.state == SuspectAI.S.Arrested || me.state == SuspectAI.S.Dead) return false;
            if (me.state == SuspectAI.S.Patrol) return false; // l'agent ne prend la main qu'une fois alerté
            return true; // les actions arrivent par OnActionReceived
        }
    }
}
#endif
