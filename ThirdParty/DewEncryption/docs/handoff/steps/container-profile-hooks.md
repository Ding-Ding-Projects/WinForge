# Step Handoff: Container Profiles and Hooks

## Create or edit a profile

1. Open the Python GUI Containers tab.
2. Click `New` or select an existing profile.
3. Fill name, container path, optional mount path, and theme values.
4. Click `Save`.
5. Settings are serialized to the active settings path.

## Add a hook

1. Select or save a container profile.
2. Choose event: `open` or `close`.
3. Choose kind: `script`, `discord`, or `home_assistant`.
4. Enter target and optional payload.
5. Click `Add Action`.
6. Test with `Test Open Hooks` or `Test Close Hooks`.

## Snapshot container history

1. Select a saved profile with an existing container path.
2. Click `Snapshot History` or run `container-history <container> --snapshot`.
3. The container file is copied into a container-history Git repo and committed.
4. Click `Refresh History` to view the commit list.
