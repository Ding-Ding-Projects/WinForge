# Action Handoff: Linux Nautilus Scripts

| Script | Expected behavior |
|---|---|
| `dew encryption` | Runs the default archive snapshot command on selected paths. |
| `dew encryption start file history` | Starts the CLI watcher on selected folder/path context. |
| `dew encryption file history manager` | Opens the Python GUI history manager. |
| `dew encryption quick create container` | Runs quick VeraCrypt container creation and profile registration. |
| `dew encryption VeraCrypt encrypt` | Runs VeraCrypt encryption for selected files/folders. |
| `dew encryption VeraCrypt decrypt` | Runs VeraCrypt decryption for selected containers. |

## Notes

- The Linux installer copies all scripts into `~/.local/share/nautilus/scripts/Dew Encryption` and makes them executable.
- Nautilus exposes these as Scripts menu entries rather than normal Explorer-style registry verbs.
