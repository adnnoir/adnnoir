# IA des suspects par renforcement (ML-Agents) et versions d'IA (MLOps)

Les suspects du jeu ont deux « cerveaux » possibles :

- **Règles** (par défaut) : le code de `Scripts/AI/SuspectAI.cs`. Les suspects patrouillent, se mettent à couvert, contournent, se rendent…
- **Réseau** : un réseau de neurones entraîné par **apprentissage par renforcement**. Le suspect essaie des actions, reçoit des points (récompenses) quand c'est bien et en perd quand c'est mal, et s'améliore tout seul au fil de milliers de parties.

Ce dossier sert à entraîner ce réseau et à **gérer ses versions** (v01, v02…) : c'est la partie « MLOps ».

## 1. Installer (une seule fois)

1. **Dans Unity** : `Window → Package Manager → + → Add package by name…` et tape `com.unity.ml-agents`.
   Le module `Scripts/ML` s'active alors tout seul, et un bouton **ARÈNE IA** apparaît dans le menu du jeu.
2. **Python 3.10** : crée un environnement, puis installe les dépendances.

   ```bash
   cd unity/ml
   python -m venv .venv
   .venv\Scripts\activate          # Windows  (Mac/Linux : source .venv/bin/activate)
   pip install -r requirements.txt
   ```

   La version de `mlagents` doit correspondre à celle du paquet Unity : vérifie le tableau des versions dans la doc ML-Agents.

## 2. Entraîner une version

```bash
cd unity/ml
./scripts/train.sh "ce que j'ai changé"      # ou : mlagents-learn config/suspect_ppo.yaml --run-id=suspect_v01
```

Quand le terminal affiche « Listening on port 5004 », lance le jeu dans Unity (Play) et clique sur **ARÈNE IA**.

Dans l'arène, 3 suspects-agents affrontent 2 policiers IA (les coéquipiers). Chaque partie dure au plus 60 secondes.

Pour suivre les progrès, lance `tensorboard --logdir results` puis ouvre http://localhost:6006. La courbe « Cumulative Reward » doit monter.

## 3. Publier la version dans le jeu

```bash
python scripts/register_model.py suspect_v01 --notes "premier essai"
python scripts/compare_models.py        # tableau de toutes les versions
```

Ces scripts :

1. copient `results/suspect_v01/Suspect.onnx` dans `Assets/Entrepot7/Resources/E7/Brains/` ;
2. notent la version dans `registry.json`, avec la date, le nombre de pas, la récompense et tes notes.

Ensuite, dans le jeu : **Paramètres → Jeu → Cerveau IA → Réseau (RL)**. La version la plus récente est chargée automatiquement.

Nomme toujours tes versions avec 2 chiffres (`suspect_v01`, `suspect_v02`…). Sinon, `v10` serait classée avant `v9`.

## 4. Comment l'agent « voit » et agit

Le fichier est `Scripts/ML/SuspectAgent.cs`.

| | Contenu |
|---|---|
| **Observations** (32 nombres) | cible vue ou non, sa position relative, sa distance, ta vie, tes munitions, rechargement en cours, balles qui sifflent, obstacles dans 8 directions, abri le plus proche, orientation vers la cible, nombre de suspects et de policiers encore debout |
| **Actions** | avancer ou reculer, pas de côté, tourner, tirer, s'accroupir |
| **Récompenses** | + toucher la cible · +1 la neutraliser · − être touché · −1 mourir · +1 victoire d'équipe · petit bonus pour recharger à l'abri · petit malus quand le temps passe |

## 5. À toi de jouer (mode prof)

Voici des pistes pour améliorer l'IA toi-même. Demande-moi des indices : je te guide étape par étape.

1. **Curriculum** : commencer avec 1 policier immobile, puis 2 qui bougent. Indice : cherche « curriculum » dans la doc ML-Agents, et regarde `Arena.NewEpisode()`.
2. **Récompense de contournement** : donner des points quand l'agent touche la cible par le côté ou par derrière.
3. **Mémoire** : empiler les dernières observations (`NumStackedVectorObservations`) pour que l'agent se souvienne d'où venait le tir.
4. **Auto-entraînement (self-play)** : les policiers aussi deviennent des agents qui apprennent.
5. **MLOps** : ajouter à `compare_models.py` un vrai match entre deux versions (taux de victoire).
