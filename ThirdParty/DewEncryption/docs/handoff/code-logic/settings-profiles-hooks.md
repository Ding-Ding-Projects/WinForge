# Code Logic Handoff: Settings, Profiles, and Hooks

## Settings flow

1. `config_file` chooses explicit, portable, or user config settings location.
2. `load_settings` reads JSON and merges known keys into dataclass defaults.
3. Unknown keys are ignored; missing keys are defaulted.
4. `save_settings` writes `asdict(settings)` as formatted JSON.

## Profile flow

- `ContainerProfile` holds user-facing container metadata and hook/theme settings.
- Quick create appends a profile after successful encryption.
- The Python GUI edits profiles in memory, then persists with `save_settings`.

## Hook flow

1. `hook_variables` builds template values.
2. `expand_hook_text` performs simple token replacement.
3. `run_container_hooks` filters enabled hooks by event.
4. Script hooks run via the shell with `DEW_*` environment variables.
5. Discord/Home Assistant hooks POST JSON to their targets.
6. Success/failure messages are returned to GUI/CLI callers.
