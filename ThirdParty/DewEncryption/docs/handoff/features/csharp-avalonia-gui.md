# Feature Handoff: C# Avalonia GUI Target

## Purpose

The C# Avalonia project is a native cross-platform GUI shell that drives the mature Python `dew-encryption` CLI while a richer native interface is developed.

## Current capabilities

- Files tab: add files, add folder, remove selected, run Dew Encryption, log output.
- History tab: refresh history, snapshot now, open Python history manager.
- Containers tab: register draft container labels, snapshot container history, test open hooks, test close hooks.
- Header: open CLI help.

## Code path

- `MainWindow.axaml` declares the UI and click handler names.
- `MainWindow.axaml.cs` stores selected paths in `ObservableCollection<DewPathItem>` and invokes `dew-encryption` or `dew-encryption-gui` using `ProcessStartInfo.ArgumentList`.
- CLI stdout/stderr and exit code are appended to the log box.

## Change checklist

- Keep CLI calls argument-list based rather than shell-string based.
- As native feature parity grows, decide whether to call Python APIs directly or keep the CLI boundary.
- Ensure .NET build remains covered in CI if productionizing the Avalonia target.
