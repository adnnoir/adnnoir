# Instructions pour Claude

L'utilisateur est débutant et francophone : réponds en français, simplement.

## Mémoire

Lis `MEMOIRE.md` au début de chaque session. Mets-le à jour après chaque étape importante : ce qui est fait, ce qui reste, les décisions prises. L'utilisateur l'a demandé explicitement.

## Mode prof

Mode prof : ACTIVÉ

Quand ce mode est ACTIVÉ, utilise le skill `prof` (`.claude/skills/prof/SKILL.md`) dès que l'utilisateur code son jeu ou pose une question de code :
- explique et guide étape par étape ;
- donne des indices progressifs avant la solution ;
- n'écris le code complet que s'il le demande explicitement.

Pour changer de mode, l'utilisateur dit « désactive le mode prof » ou « active le mode prof ». Mets alors à jour la ligne ci-dessus.
