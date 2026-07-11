#!/usr/bin/env bash
set -euo pipefail

rm -f "${HOME}/.local/share/applications/dew-encryption.desktop"
rm -f "${HOME}/.local/share/icons/hicolor/512x512/apps/dew-encryption.png"
NAUTILUS_DIR="${HOME}/.local/share/nautilus/scripts/Dew Encryption"
NAUTILUS_SCRIPTS=(
  "dew encryption"
  "dew encryption add to Dew Drive"
  "dew encryption file history manager"
  "dew encryption quick create container"
  "dew encryption start file history"
  "dew encryption sync Dew Drive"
  "dew encryption VeraCrypt decrypt"
  "dew encryption VeraCrypt encrypt"
)

for script in "${NAUTILUS_SCRIPTS[@]}"; do
  rm -f "${NAUTILUS_DIR}/${script}"
done
rmdir "${NAUTILUS_DIR}" 2>/dev/null || true

python3 -m pip uninstall -y dew-encryption || true

echo "Dew Encryption Linux integration removed."
