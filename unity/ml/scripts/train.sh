#!/usr/bin/env bash
# Lance un entraînement avec un numéro de version automatique (suspect_v01, suspect_v02…).
# Utilisation : ./scripts/train.sh [notes]   (depuis unity/ml)
set -e
cd "$(dirname "$0")/.."
n=1
while [ -d "results/suspect_v$(printf '%02d' $n)" ]; do n=$((n+1)); done
RUN="suspect_v$(printf '%02d' $n)"
echo "▶ Entraînement $RUN — appuie sur Play dans Unity puis « ARÈNE IA »"
mlagents-learn config/suspect_ppo.yaml --run-id="$RUN"
python scripts/register_model.py "$RUN" --notes "${1:-}"
