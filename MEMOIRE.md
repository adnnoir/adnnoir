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
- Export : `scratchpad/exporter.js`, injecté dans une copie du jeu web (`bodycam/_export.html`, à ne pas commiter), puis lancé par `t_export.js`.

## Outils installés
- Skills (`.claude/skills`) : 23 skills de départ, plus emil-design-eng, frontend-design, godot-code-gen, godot-scene-design, godot-shader et prof.
- Plugins : superpowers et godot (déclarés dans `.claude/settings.json`).
- Serveurs MCP (`.mcp.json`) :
  - `godot-docs` : il faut Bun pour le lancer, d'où `npx -y bun x --bun …` ;
  - `godot` : il faut Godot installé sur le PC.

## Journal
- 2026-09-25 (suite) : export des modèles web vers Unity, puis code Unity (niveau, audio, effets, armes) en cours.
- 2026-09-25 : visée corrigée, sauvegarde de carrière, installations (plugins, skills, MCP), skill prof, vrais sons de tir et voix dans le jeu web.
