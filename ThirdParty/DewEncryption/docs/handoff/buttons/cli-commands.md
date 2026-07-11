# Action Handoff: CLI Commands

| Command | Purpose |
|---|---|
| `dew-encryption <paths...> [--password]` | Git snapshot plus `.7z` archive. |
| `watch <paths...> [--interval] [--once]` | Polling auto-history snapshots. |
| `history <repo> [--limit]` | Print tab-delimited recent commits. |
| `details <repo> <commit>` | Print commit metadata and changed files. |
| `restore <repo> <commit> <source>` | Restore source from a commit. |
| `veracrypt-settings` | Show/update remembered VeraCrypt defaults. |
| `veracrypt-encrypt <sources...>` | Create encrypted containers from sources. |
| `veracrypt-decrypt <containers...>` | Extract encrypted container contents. |
| `container-quick-create <sources...>` | Encrypt and automatically register profiles. |
| `container-history <container> [--snapshot]` | Snapshot/list container-file history. |
| `container-hooks <open|close> <container>` | Run saved hooks for a container. |
| `portable --init/--show` | Manage portable settings mode. |
