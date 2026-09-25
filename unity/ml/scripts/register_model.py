#!/usr/bin/env python3
"""
Publie une version entraînée de l'IA dans le jeu (MLOps « maison »).

Ce que fait le script :
  1. retrouve le modèle entraîné : results/<run-id>/Suspect.onnx ;
  2. lit les statistiques de l'entraînement (récompense moyenne, nombre de pas) ;
  3. copie le modèle dans Unity : Assets/Entrepot7/Resources/E7/Brains/<run-id>.onnx ;
  4. ajoute une ligne dans registry.json (version, date, stats, notes).

Utilisation (depuis unity/ml) :
  python scripts/register_model.py suspect_v01 --notes "premier essai, 500k pas"
"""
import argparse, datetime, json, pathlib, shutil, sys

HERE = pathlib.Path(__file__).resolve().parent.parent          # unity/ml
BRAINS = HERE.parent / "Entrepot7" / "Assets" / "Entrepot7" / "Resources" / "E7" / "Brains"
REGISTRY = HERE / "registry.json"


def read_stats(run_dir: pathlib.Path) -> dict:
    """Lit run_logs/training_status.json (écrit par mlagents-learn) pour récupérer quelques chiffres."""
    stats = {}
    status = run_dir / "run_logs" / "training_status.json"
    if status.exists():
        data = json.loads(status.read_text())
        beh = data.get("Suspect", {})
        cps = beh.get("checkpoints", [])
        if cps:
            last = cps[-1]
            stats["steps"] = last.get("steps")
            stats["reward"] = last.get("reward")
    return stats


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("run_id", help="nom du run, ex : suspect_v01 (mêmes chiffres que --run-id)")
    ap.add_argument("--results", default=str(HERE / "results"), help="dossier results de mlagents-learn")
    ap.add_argument("--notes", default="", help="ce qui a changé dans cette version")
    a = ap.parse_args()

    run_dir = pathlib.Path(a.results) / a.run_id
    onnx = run_dir / "Suspect.onnx"
    if not onnx.exists():
        sys.exit(f"Modèle introuvable : {onnx}\nAs-tu bien lancé : mlagents-learn config/suspect_ppo.yaml --run-id={a.run_id} ?")

    BRAINS.mkdir(parents=True, exist_ok=True)
    dest = BRAINS / f"{a.run_id}.onnx"
    shutil.copy2(onnx, dest)

    reg = json.loads(REGISTRY.read_text()) if REGISTRY.exists() else {"behavior": "Suspect", "versions": []}
    reg["versions"] = [v for v in reg["versions"] if v["id"] != a.run_id]
    reg["versions"].append({
        "id": a.run_id,
        "date": datetime.datetime.now().isoformat(timespec="minutes"),
        "stats": read_stats(run_dir),
        "notes": a.notes,
        "file": str(dest.relative_to(HERE.parent)),
    })
    reg["versions"].sort(key=lambda v: v["id"])
    REGISTRY.write_text(json.dumps(reg, indent=2, ensure_ascii=False))
    print(f"✔ {a.run_id} publié dans le jeu : {dest}")
    print("  Dans Unity : Paramètres → Jeu → Cerveau IA → Réseau (la version la plus récente est chargée).")
    print("  Pense à commiter : git add unity/ml/registry.json " + str(dest.relative_to(HERE.parent.parent)))


if __name__ == "__main__":
    main()
