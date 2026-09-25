# Mémoire du projet

Ce fichier est mis à jour à chaque étape. Claude le relit au début de chaque session pour savoir où on en est.

## Profil
- Débutant, francophone, écrit vite (style SMS) : répondre simplement, en français.
- Mode prof : voir `CLAUDE.md`. Il est ACTIVÉ, mais l'utilisateur a demandé « code tout » pour le projet Unity : l'écrire complètement, puis expliquer.
- Branche de travail : `claude/sharp-wozniak-hwlwt9` (dépôt `adnnoir/adnnoir`).

## Projets

### 1. ADN Noir — jeu clicker (`clicker/`)
- Terminé : sons, succès, skins, achat ×10, événements, combos, créatures, missions, boss.
- En attente : classement en ligne Supabase. Il faut le Project URL et la clé **anon** (jamais la clé secrète).
- En ligne : https://adnnoir.github.io/adnnoir/clicker/

### 2. Entrepôt 7 — FPS bodycam web (`bodycam/`)
- Three.js, un seul fichier `index.html`, et les sons dans `sfx/` et `vo/`.
- Page Artifact : https://claude.ai/artifact/2pSeyGz41mcfUBidLyGXM1
- Fait :
  - menus (arsenal, personnage, paramètres, dossier) ;
  - bots humains (A*, couverture, reddition, menottes) ;
  - armes refaites en détail et visée corrigée ;
  - **modèles d'armes refaits (2e passe)** : profils arrondis (fonctions `fillet` et `soften`), fentes M-LOK visibles (`sideSlot`, `botSlot`), crosse amincie (`taperW`) :
    - carabine : carcasse usinée, poignée ergonomique, crosse évidée, chargeur courbe, capot d'éjection, assistance à la fermeture ;
    - pistolet : carcasse et pontet carré, rainures pour les doigts, queue de castor ;
    - fusil à pompe : crosse galbée, pompe à anneaux, fenêtre d'éjection avec culasse ;
    - PM : poignée rainurée, chargeur courbe à nervure, plaque de crosse galbée ;
    - le cache du point rouge ne bouche plus la visée ;
  - sauvegarde de carrière (dossier, historique, export/import) ;
  - **vrais sons** : tirs enregistrés (CC0) et voix Piper (CC BY 4.0), crédits dans `bodycam/CREDITS.md`.
- Pour tester : `python3 -m http.server` dans `bodycam/`, sinon les sons ne se chargent pas en `file://`.

### 3. Entrepôt 7 — version Unity (`unity/Entrepot7/`)
- Demandé : tout porter sous Unity, refaire les modèles d'armes, bots IA mieux faits, coéquipiers IA, base d'IA en RL (ML-Agents) et gestion des versions d'IA (MLOps).
- L'utilisateur fera lui-même une partie de l'IA RL, avec le mode prof.
- Structure : `unity/Entrepot7/` (projet Unity, pipeline « Built-in », scène créée par code au lancement).
  - `Assets/Entrepot7/Resources/E7/Models` : modèles exportés du jeu web (`.bytes`) et matériaux (`.json`).
  - `Resources/E7/Textures` : textures en PNG. `Resources/E7/Audio` : sons et voix. `Resources/E7/Shaders` : shaders E7/Lit, E7/LitFade, E7/Unlit et Hidden/E7/Bodycam.
  - `Scripts` : Core (sauvegarde, entrées, chargeur de modèles), Audio, World (niveau), FX, Player, AI, UI, ML.
- Vérification : le C# est compilé avec `dotnet` et les références Unity (projet `scratchpad/check`), mais le jeu n'est pas lancé : Unity n'est pas disponible dans le conteneur.
- Export web → Unity : `unity/tools/export/` (`exporter.js` + `export.js`, voir son README).
- Fait (compile sans erreur avec dotnet) :
  - `Game` : démarrage automatique, états et radio ;
  - `Level` : carte, NavMesh, abris, néons, cibles, objets ;
  - `Player` et `WeaponSystem` : visée, recul, rechargements animés, pompe, grenade, laser ;
  - `SuspectAI` : couverture, tir en se relevant, contournement, suppression, jetons de tir, reddition ;
  - `TeammateAI` : suivre, tenir, engager, sommer, menotter (touches T et Y) ;
  - `Squad`, `Humanoid` (corps animé, IK des bras), `FX`, `BodycamFX` (rendu + bloom), `AudioSys` et `SoundSynth` ;
  - `Hud`, `Menus` (principal, briefing, arsenal 3D, personnage, paramètres en 4 onglets, dossier, pause, fin), `Save` (JSON).
- ML : `Scripts/ML` (SuspectAgent, Arena), actif seulement si le paquet `com.unity.ml-agents` est installé. MLOps dans `unity/ml` (config PPO, `register_model.py`, `compare_models.py`, `train.sh`, `registry.json`).
- À vérifier par l'utilisateur dans Unity 6 : c'est un premier lancement sans test réel, donc il faut corriger ce qui s'affiche dans la console.

## Outils installés
- Skills (`.claude/skills`) : 23 skills de départ, plus emil-design-eng, frontend-design, godot-code-gen, godot-scene-design, godot-shader et prof.
- Plugins : superpowers et godot (déclarés dans `.claude/settings.json`).
- Serveurs MCP (`.mcp.json`) :
  - `godot-docs` : il faut Bun pour le lancer, d'où `npx -y bun x --bun …` ;
  - `godot` : il faut Godot installé sur le PC.

## Journal
- 2026-09-25 (suite 5) : pistolet **réaliste** (Poly Haven « Service Pistol », CC0, glTF dans `bodycam/models/`) branché dans le web (`buildPistolReal`, repli sur le P17 en code) et exporté vers Unity (textures JPG copiées, shader E7/Lit avec carte de métal). Pas de personnages ni d'autres armes réalistes disponibles sans compte : il faut Mixamo / Asset Store avec l'utilisateur. Bug « première personne » toujours pas décrit.
- 2026-09-25 (suite 4) : l'utilisateur a ouvert le projet dans Unity 6 et le jeu se lance. Corrigé : pause auto quand l'éditeur perd le focus, cônes de lumière (shader Unlit sans couleur de sommet, non testé). En attente : capture du bug « première personne » ; il veut des modèles « comme IRL » (joueurs + armes) → proposé : il télécharge des modèles réalistes gratuits (Mixamo SWAT, Asset Store), je code leur chargement.
- 2026-09-25 (suite 3) : 2e passe sur les modèles des 4 armes (web), puis réexport vers Unity (`w_*.bytes`, point de visée de la carabine abaissé de 4 mm pour poser le viseur sur le rail).
- 2026-09-25 (suite 2) : projet Unity complet (jeu, IA, coéquipiers, menus, sauvegarde, ML-Agents, MLOps). Poussé sur la branche.
- 2026-09-25 (suite) : export des modèles web vers Unity, puis code Unity (niveau, audio, effets, armes) en cours.
- 2026-09-25 : visée corrigée, sauvegarde de carrière, installations (plugins, skills, MCP), skill prof, vrais sons de tir et voix dans le jeu web.
