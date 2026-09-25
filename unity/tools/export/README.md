# Export web → Unity

`exporter.js` est injecté dans une copie du jeu web (`bodycam/index.html`). Il réutilise les fonctions de construction du jeu (`buildCarbine`, `buildHuman`, `buildGlove`…) et écrit :

- **`Models/*.bytes`** : les modèles au format « E7M2 ». Ce format contient :
  - les nœuds (culasse, chargeur, pompe, os du corps…) ;
  - les pièces, chacune avec une étiquette (`base`, `optic:reddot`, `muzzle:suppressor`, `headgear:cap`…) et un matériau.
- **`Models/*_materials.json`** : couleur, métal, rugosité, textures et émission de chaque matériau.
- **`Models/w_*.json`** : les infos des armes (position à la hanche, point de visée pour chaque viseur, bouche, laser, poses de la main gauche…).
- **`Textures/*.png`** : les textures dessinées par le jeu web.

Les coordonnées sont converties vers le repère d'Unity : on applique un miroir, et l'ordre des triangles est inversé.

Pour relancer l'export, suis les instructions en haut de `export.js`.
