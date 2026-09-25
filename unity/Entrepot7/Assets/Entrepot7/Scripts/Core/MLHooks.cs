using System;

namespace E7
{
    /// <summary>
    /// Points d'accroche pour l'IA par renforcement (dossier ML, compilé seulement si le paquet ML-Agents est installé).
    /// Le jeu fonctionne sans : ces champs restent vides.
    /// </summary>
    public static class MLHooks
    {
        /// <summary>Vrai si le module ML (ML-Agents) est présent.</summary>
        public static bool Available;
        /// <summary>Lance l'arène d'entraînement (suspects-agents contre policiers IA).</summary>
        public static Action StartArena;
        /// <summary>Arrête l'arène.</summary>
        public static Action StopArena;
        /// <summary>Donne un cerveau entraîné à un suspect (réglage « Cerveau des suspects : Réseau »).</summary>
        public static Func<SuspectAI, ISuspectBrain> MakeBrain;
    }
}
