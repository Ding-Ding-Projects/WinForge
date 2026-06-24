# Handoff: Wireshark / Packet Capture

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: tshark / dumpcap (wireshark.org · github.com/wireshark/wireshark) |
| **License** | GPL-2.0-or-later (open source). Npcap (bundled) is proprietary but free for end users; do not redistribute — install via winget. |
| **Proposed module** | Packet Capture · Network / Tools group · Tag `module.wireshark` |
| **Effort** | L — no native reimplementation; effort is in a rich WinUI front-end, robust JSON/field stream parsing, admin/Npcap handling, and a responsive live packet grid. |

## What the user asked for
A WinUI front-end over Wireshark's CLI tools: list capture interfaces, start/stop a capture to a `.pcap`/`.pcapng` file, show a live packet summary (no/time/src/dst/proto/length/info), apply capture and display filters, and open the saved file in the full Wireshark GUI. Install Wireshark (with Npcap) via `winget WiresharkFoundation.Wireshark`. Requires admin.

## Recommended approach
**CLI/binary wrap.** Wireshark's dissection engine is a huge C/Qt codebase that depends on the Npcap kernel driver for capture — cloning it in C# is infeasible and unsafe per the global strategy, so we wrap `dumpcap.exe` (capture) and `tshark.exe` (parse/filter) and build a self-contained WinUI module that never redirects the user. Realistic v1: pick interface → live capture to file with a scrolling summary grid → stop → open in Wireshark. Use `dumpcap` for the actual capture (lighter, the recommended capture child) and `tshark` for reading/summarizing.

## Features to implement (v1 → later)
- v1: Interface list via `tshark -D` (or `dumpcap -D`); capture filter box (BPF, `-f`); display filter box (`-Y`); Start/Stop buttons; live summary grid streamed from `tshark -i <if> -T fields -e frame.number -e frame.time_relative -e ip.src -e ip.dst -e _ws.col.Protocol -e frame.len -e _ws.col.Info -E separator=\t` (or `-T ek`/json); output file picker (FileDialogs, default `%TEMP%\WinForge-<timestamp>.pcapng`); packet count/byte counters; "Open in Wireshark" button; clear/restart.
- later: Capture to ring buffer / size+time limits (`-b filesize:`, `-a duration:`); selectable column sets; protocol statistics (`tshark -z io,phs -z conv,tcp`); follow TCP/HTTP stream; detail/hex pane for a selected packet (`tshark -V -r file -Y frame.number==N`); read existing `.pcap`; export filtered subset; promiscuous toggle; remember last interface/filters.

## Integration plan (WinForge specifics)
- New files: `Services/WiresharkService.cs` (locate install via registry/`%ProgramFiles%\Wireshark`, build arg lists, run `dumpcap`/`tshark` via `ShellRunner` with async stdout streaming, parse field/json lines into a `PacketRow` model, expose Start/Stop/events, detect Npcap + admin); `Pages/WiresharkModule.xaml` + `.cs` (interface combo, filter boxes, live `ListView`/`DataGrid`, counters, log pane). Optionally `Catalog/WiresharkOperations.cs` for TweakCard ops (check Npcap service, show version, open default capture folder).
- Nav wiring: add `NavigationViewItem Tag="module.wireshark"` in `MainWindow.xaml` (Network / Tools group); add a `ModuleRegistry` entry (`Services/ModuleRegistry.cs`); wire the Tag in `MainWindow.xaml.cs` `MapType` and `NavView_SelectionChanged`, plus `ApplyStartPage` for `--page wireshark`.
- Engine/install: `EngineBars.AutoInstallButton("WiresharkFoundation.Wireshark", "Install Wireshark (+ Npcap)", "安裝 Wireshark（含 Npcap）", recheck, rescan)`. Npcap ships in the installer but may require an interactive elevation step — surface a clear bar if `tshark -D` returns no interfaces.
- Key CLIs to call: `tshark -D`, `dumpcap -i <if> -w <file> [-f "<bpf>"] [-b ...] [-a ...]`, `tshark -i <if> -T fields ... -E separator=\t` (live summary), `tshark -r <file> -Y "<display filter>" -T fields ...`, `tshark -V -r <file> -Y frame.number==N` (detail), `& "$wsDir\Wireshark.exe" <file>` (open GUI), `tshark -v` (version check).

## Dependencies & risks
- Requires `dumpcap.exe`/`tshark.exe` on disk AND the Npcap driver/service running — capture silently yields zero interfaces otherwise; detect both before enabling Start and surface install/repair bars.
- Requires elevation to capture; if WinForge is not elevated, prompt to relaunch admin (Npcap can be installed in "allow non-admin capture" mode, but do not assume it).
- High packet rates can flood the UI — batch updates, cap the in-memory grid (e.g. last N rows), and stream parsing off the UI thread; always capture to file via `dumpcap` so nothing is lost.
- tshark field/json output can shift between versions — parse defensively and keep a raw log pane fallback.
- Clean cancel/kill: stop `dumpcap` gracefully (it flushes the file), dispose processes, never leave orphans.
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Network / Tools.
- Core flow works: list interfaces, start a capture with optional filters, see a live summary grid update, stop, and produce a valid `.pcapng`; "Open in Wireshark" launches the GUI on that file.
- Install bar appears when Wireshark/Npcap is missing and `AutoInstallButton` installs it via winget; a clear message appears when admin/Npcap is required.
- All UI strings bilingual (English + Cantonese); file selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
