# Feature Handoff: VeraCrypt Containers

## Purpose

VeraCrypt actions create encrypted `.dew.hc` containers from selected files/folders, decrypt containers back beside the container, and provide remembered defaults for algorithms, sizes, PIM, filesystem, and source/container retention.

## User-facing surfaces

- CLI: `veracrypt-encrypt`, `veracrypt-decrypt`, `container-quick-create`, and `veracrypt-settings`.
- Python GUI VeraCrypt tab for settings.
- Windows Explorer: quick create, VeraCrypt encrypt, and VeraCrypt decrypt actions.
- Linux Nautilus: quick create, VeraCrypt encrypt, and VeraCrypt decrypt scripts.

## Code path

- `veracrypt_create_container` resolves settings, creates a container, mounts it, copies source content into the mount, dismounts, and optionally deletes the source.
- `veracrypt_decrypt_container` mounts an existing container, copies mounted content out, dismounts, and optionally deletes the container.
- `estimated_container_size` adds padding, multiplier overhead, and a minimum size.
- Windows uses VeraCrypt `/create`, `/volume`, `/letter`, and `/dismount` arguments.
- Linux uses `veracrypt --text --create`, temporary mount directories, and text-mode mount/dismount commands.

## Defaults and naming

- New containers are named `<source-name>.dew.hc`.
- Decrypted default output strips `.dew.hc`, strips `.hc`, or appends `.decrypted` for unknown names.
- Default settings are AES, SHA-512, exFAT, PIM `0`, 64 MB padding, 1.25 multiplier, 32 MB minimum, remove source after encrypt, and keep container after decrypt.

## Change checklist

- Test both Windows and Linux command-line generation when editing VeraCrypt logic.
- Do not log passwords.
- Keep `container-quick-create` profile registration in sync with container manager schema.
