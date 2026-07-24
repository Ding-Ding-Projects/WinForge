# Feature Handoff: Container Manager, Themes, and Hooks

## Purpose

The container manager stores registered encrypted containers, per-container theme preferences, and multiple open/close actions for scripts, Discord webhooks, and Home Assistant webhooks.

## User-facing surfaces

- Python GUI Containers tab.
- CLI: `container-hooks` and `container-history`.
- C# GUI Containers tab drafts container details and can call hook/history CLI commands.
- Quick create registers a new container profile automatically.

## Data model

- `ContainerProfile`: name, path, mount path, theme, and hook list.
- `ThemeSettings`: background, foreground, accent, font family, and font size.
- `HookAction`: name, event (`open`/`close`), kind (`script`/`discord`/`home_assistant`), target, payload, and enabled flag.

## Hook variables

Hooks can use `{event}`, `{container}`, `{container_name}`, `{container_stem}`, `{mount_path}`, `{user}`, `{host}`, and `{timestamp}`. Script hooks also receive these values in environment variables prefixed with `DEW_`.

## Execution logic

- `find_container_profile` matches a saved profile path to a container path.
- `run_container_hooks` expands template variables, runs scripts with `shell=True`, or POSTs JSON for webhook kinds.
- The GUI test buttons run hooks without mounting containers so users can validate targets/payloads.

## Change checklist

- If adding hook kinds, update dataclass defaults, GUI combobox values, README, this handoff, and docs pages.
- Validate user-supplied JSON payloads carefully if adding stricter UI validation.
- Theme settings are currently stored and surfaced; applying themes deeply across widgets is a future enhancement area.
