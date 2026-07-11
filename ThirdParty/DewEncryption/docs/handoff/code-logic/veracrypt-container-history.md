# Code Logic Handoff: VeraCrypt and Container History

## VeraCrypt functions

- `find_veracrypt`: resolves configured path, PATH entries, and platform fallbacks.
- `container_path_for_source`: appends `.dew.hc` to source name.
- `source_path_for_container`: infers output path during decrypt.
- `estimated_container_size`: computes the VeraCrypt size string.
- `free_windows_drive_letter`: picks an unused Windows drive letter.
- `copy_into_mount` and `copy_from_mount`: transfer payload content between source/output and mounted container.
- `veracrypt_create_container`: platform-specific create/mount/copy/dismount/remove-source flow.
- `veracrypt_decrypt_container`: platform-specific mount/copy/dismount/remove-container flow.

## Container history functions

- `container_history_repo`: stores per-container repos under `Dew Encryption Archives/Container History/<container-stem>/.dew-encryption-repo`.
- `snapshot_container`: snapshots the container file into its history repo.
- `container_history`: delegates to generic `history` for the container repo.
