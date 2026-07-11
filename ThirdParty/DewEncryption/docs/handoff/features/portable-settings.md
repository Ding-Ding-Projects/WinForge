# Feature Handoff: Portable Settings

## Purpose

Portable mode stores settings beside the application/executable instead of in user config directories. This supports the portable Windows zip workflow.

## User-facing surfaces

- CLI: `dew-encryption portable --init` creates `portable.flag` beside the app.
- CLI: `dew-encryption portable --show` prints app base, portable state, marker path, and settings path.
- Environment: `DEW_ENCRYPTION_PORTABLE=1` forces portable mode.
- Environment: `DEW_ENCRYPTION_CONFIG=/path/to/settings.json` overrides all settings location logic.

## Code path

- `app_base_dir` returns the executable folder for frozen builds or repo/package root for source runs.
- `config_file` checks explicit config path, portable env/flag, then falls back to `%APPDATA%/DewEncryption/settings.json` or `~/.config/DewEncryption/settings.json`.
- `load_settings` tolerates missing or invalid JSON and returns defaults.
- `save_settings` creates the parent directory and writes JSON.

## Change checklist

- Keep frozen-build and source-checkout behavior separate.
- Update release packaging if marker naming changes.
- Settings schema changes should be backward compatible because invalid/missing keys are defaulted.
