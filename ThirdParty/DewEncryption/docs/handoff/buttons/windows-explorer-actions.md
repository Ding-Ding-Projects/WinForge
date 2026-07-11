# Action Handoff: Windows Explorer Menu

| Menu verb | Registry scope | Command behavior |
|---|---|---|
| dew encryption | files, directories, directory background | Runs `python -m dew_encryption` on `%1` or `%V`. |
| dew encryption start file history | directories, directory background | Starts hidden `python -m dew_encryption watch` for the folder. |
| dew encryption file history manager | directories, directory background | Opens `python -m dew_encryption.gui <folder> --history`. |
| dew encryption quick create container | files, directories | Runs `python -m dew_encryption container-quick-create`. |
| dew encryption VeraCrypt encrypt | files, directories | Runs `python -m dew_encryption veracrypt-encrypt`. |
| dew encryption VeraCrypt decrypt | `.hc` files | Runs `python -m dew_encryption veracrypt-decrypt`. |
| dew encryption create elevated tasks | directory background | Starts admin-consented scheduled-task creation. |
| dew encryption remove elevated tasks | directory background | Starts admin-consented scheduled-task removal. |

## Notes

- VeraCrypt and quick-create actions use `MultiSelectModel=Player` to support multi-select.
- Icon choices are based on the action path.
- Commands are registered under `HKCU`, so normal installation does not require administrator rights.
