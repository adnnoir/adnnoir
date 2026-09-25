#!/usr/bin/env python3
"""Affiche un tableau des versions de l'IA enregistrées (registry.json) pour les comparer."""
import json, pathlib

REG = pathlib.Path(__file__).resolve().parent.parent / "registry.json"
reg = json.loads(REG.read_text())
rows = reg.get("versions", [])
if not rows:
    print("Aucune version enregistrée. Entraîne puis lance scripts/register_model.py <run-id>.")
else:
    print(f"{'version':<16}{'date':<18}{'pas':>10}{'récompense':>12}  notes")
    best = max(rows, key=lambda v: (v.get('stats', {}).get('reward') or -1e9))
    for v in rows:
        s = v.get("stats", {})
        mark = "  ← meilleure" if v is best else ""
        r = s.get('reward')
        print(f"{v['id']:<16}{v['date']:<18}{str(s.get('steps', '?')):>10}{(f'{r:.3f}' if isinstance(r, (int, float)) else '?'):>12}  {v.get('notes', '')}{mark}")
