# Entrepôt 7 — version Unity

C'est le portage Unity du FPS « bodycam » (d'abord fait en web dans `bodycam/`). On y retrouve :

- **les mêmes armes détaillées**, exportées directement depuis la version web, avec leurs accessoires : point rouge, holographique, silencieux, lampe et laser ;
- **les vrais sons** : coups de feu enregistrés et voix ;
- **des suspects plus malins** : NavMesh, couverture, tir en se relevant, contournement, réaction aux balles qui passent près ;
- **des coéquipiers IA** : ils suivent, couvrent, tirent, crient « Police ! » et menottent ;
- **les menus complets**, **la sauvegarde** et **une base d'IA par renforcement** (ML-Agents, voir `ml/README.md`).

## Ouvrir le projet

1. Installe **Unity Hub**, puis **Unity 6** (version `6000.0` LTS), sans module particulier.
2. Dans Unity Hub : `Add → Add project from disk` et choisis le dossier `unity/Entrepot7`.
3. À la première ouverture, Unity importe tout (quelques minutes). Le script `Editor/E7Setup.cs` crée alors la scène `Assets/Entrepot7/Scenes/Main.unity` et règle les couleurs.
4. Appuie sur **Play** ▶ : le jeu se construit tout seul (niveau, joueur, IA, menus).

Si Unity demande de redémarrer à cause du « système d'entrées », accepte et rouvre le projet : c'est normal.

## Touches

| Action | Touche |
|---|---|
| Se déplacer | **ZQSD** (AZERTY) ou **WASD** (QWERTY) · réglage dans Paramètres → Jeu |
| Courir · s'accroupir | **Maj** · **C** |
| Se pencher | **A** / **E** (QWERTY : Q / E) |
| Tirer · viser | **clic gauche** · **clic droit** |
| Recharger · mode de tir | **R** · **B** |
| Changer d'arme | **1** / **2** · molette |
| Lampe (et laser) | **F** |
| Grenade flash · inspecter | **X** · **I** |
| Crier « Police ! » · menotter | **V** · **G** |
| Coéquipiers : avec moi / tenez la position | **T** |
| Coéquipiers : allez là où je vise | **Y** |
| Pause | **Échap** |

## Comment c'est organisé

```
Entrepot7/Assets/Entrepot7/
├─ Resources/E7/
│  ├─ Models/     modèles exportés du web (.bytes) + matériaux (.json) + infos des armes
│  ├─ Textures/   textures (béton, murs, caisses, cartons, tissus…)
│  ├─ Audio/      sfx/ (coups de feu CC0) et vo/ (voix Piper, CC BY 4.0)
│  ├─ Shaders/    E7/Lit, E7/LitFade, E7/Unlit, effet bodycam
│  └─ Brains/     réseaux entraînés (.onnx) pour l'IA « Réseau »
├─ Scripts/
│  ├─ Core/    Game (chef d'orchestre), Save (sauvegarde), GameInput (touches), E7Assets (chargement), MiniJson
│  ├─ World/   Level (l'entrepôt, NavMesh, abris), Pickup (soins, munitions, cibles), MeshGen
│  ├─ Player/  Player (déplacement, caméra), WeaponSystem (armes à la 1re personne), WeaponDefs
│  ├─ AI/      SuspectAI, TeammateAI, Squad (coordination), Humanoid (corps animé)
│  ├─ FX/      FX (impacts, sang, douilles, grenades), BodycamFX (rendu final)
│  ├─ Audio/   AudioSys (sons, voix, radio), SoundSynth (bruitages synthétisés)
│  ├─ UI/      Hud, Menus, UIKit
│  └─ ML/      agent ML-Agents + arène (compilé seulement si ML-Agents est installé)
└─ Editor/     préparation automatique du projet
```

Il n'y a **pas de préfabriqués ni de scène à monter** : tout est créé par le code au lancement. Pour modifier le jeu, on modifie les scripts. Quelques exemples :

- **Carte** : le tableau `MAP` dans `World/Level.cs`. `#` = mur, `S` = étagère, `C` = caisse, `E` = suspect, `P` = joueur…
- **Armes** : `Player/WeaponDefs.cs` (dégâts, cadence, recul…).
- **Difficulté** : `AI/Squad.cs` (classe `Difficulty`).
- **Suspects** : `AI/SuspectAI.cs`. **Coéquipiers** : `AI/TeammateAI.cs`.

## Sauvegarde

La progression (paramètres, équipement, personnage, dossier de carrière) est enregistrée automatiquement dans `entrepot7_save.json`. Ce fichier se trouve dans le dossier de données d'Unity. Pour l'ouvrir : menu du jeu **Dossier → Ouvrir le dossier de sauvegarde**, ou dans l'éditeur `Entrepôt 7 → Ouvrir le dossier de sauvegarde`.

## Refaire l'export depuis la version web

Les modèles, les matériaux et les textures viennent du jeu web (`bodycam/index.html`). Après avoir modifié une arme dans le web, relance l'export (voir `tools/export/README.md`).

## Crédits

- Coups de feu : *The Free Firearm Sound Library* (CC0).
- Voix : Piper, avec les voix MLS et SIWIS (CC BY 4.0).

Le détail est dans `bodycam/CREDITS.md`.
