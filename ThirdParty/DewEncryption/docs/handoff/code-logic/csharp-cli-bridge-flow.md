# Code Logic Handoff: C# GUI CLI Bridge

## Structure

- `MainWindow.axaml` defines tabs and buttons.
- `MainWindow.axaml.cs` owns collections, click handlers, logging, and process execution.
- `DewPathItem` is the row model for selected paths.

## CLI bridge

1. Button handlers validate required UI state.
2. Handlers call `RunCliAsync` for `dew-encryption` commands or `RunProcessAsync` for another executable.
3. `ProcessStartInfo` disables shell execution and adds each argument to `ArgumentList`.
4. Stdout, stderr, and exit code are appended to `LogBox`.
