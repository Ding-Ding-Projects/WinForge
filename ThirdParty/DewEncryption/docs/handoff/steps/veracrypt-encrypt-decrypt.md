# Step Handoff: VeraCrypt Encrypt/Decrypt

## Encrypt

1. User launches `veracrypt-encrypt` or `container-quick-create` from CLI/shell/GUI workflow.
2. Password is read from `--password` or a hidden prompt.
3. Settings are loaded from `settings.json` or defaults.
4. Container path is calculated as `<source>.dew.hc`.
5. Container size is estimated from source size plus padding/multiplier/minimum.
6. VeraCrypt creates and mounts the container.
7. The source file/folder is copied into the mount.
8. VeraCrypt dismounts the container in a `finally` path.
9. Source is removed unless settings/flags say to keep it.
10. Quick-create also appends a `ContainerProfile` and saves settings.

## Decrypt

1. User launches `veracrypt-decrypt` on one or more container files.
2. Password is read from `--password` or a hidden prompt.
3. Default output path is inferred beside the container unless `--output` is supplied for a single container.
4. VeraCrypt mounts the container.
5. Mounted contents are copied to the output path.
6. VeraCrypt dismounts the container in a `finally` path.
7. Container is deleted only if flags/settings request removal.
