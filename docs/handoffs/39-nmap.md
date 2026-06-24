# Handoff: Nmap scanner

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/nmap/nmap (C/C++) · CLI: `nmap.exe` |
| **License** | Nmap Public Source License (NPSL, GPL-derived, open source). WinForge only invokes the binary out-of-process — no source linkage, so the license does not infect WinForge. |
| **Proposed module** | Nmap Scanner · System / Network group (next to Connections) · Tag `module.nmap` |
| **Effort** | M — wrapping is straightforward; the real work is the scan-profile UI and parsing `-oX` XML into a clean hosts/ports/services grid. |

## What the user asked for
Wrap the Nmap CLI behind a WinUI front-end: enter target(s), pick a scan profile (ping / quick / full / service / OS / script), toggle common flags, run the scan, parse the XML output into a hosts/ports/services grid, and save results. Install via winget `Insecure.Nmap`. Ties into the Connections module.

## Recommended approach
**CLI/binary wrap.** Nmap is a large C/C++ codebase with a custom raw-packet engine, the Npcap driver dependency, and a Lua scripting engine (NSE). Per the global strategy this is firmly "cannot reimplement in reasonable scope," so we install the official binary via winget and build a rich WinUI front-end around `nmap.exe`. A native C# port is out of scope.

v1 scope: detect/install Nmap, compose a command from a target box + profile dropdown + flag toggles, always append `-oX -` so we get machine-readable XML on stdout, run via `ShellRunner.Capture`, parse the XML, and bind the result to a hosts/ports/services grid with a save action.

## Features to implement (v1 -> later)
- v1: Detect install (registry / `where nmap`); `EngineBars.AutoInstallButton` if missing.
- v1: Target input (single IP, hostname, CIDR `192.168.1.0/24`, range); validate non-empty before run.
- v1: Profile dropdown -> flag presets: Ping sweep `-sn`, Quick `-T4 -F`, Full TCP `-p-`, Service/version `-sV`, OS detect `-O` (needs admin), Default scripts `-sC`.
- v1: Common-flag toggles: `-sV`, `-O`, `-sC`, `-Pn` (skip ping), `-A` (aggressive), `-T4` timing, UDP `-sU`. Build the final command string and show it read-only before running.
- v1: Run with `-oX -`; parse `<host>`/`<address>`/`<hostnames>`/`<ports>/<port>`/`<service>`/`<os>` into a grid (Host, Port, Proto, State, Service, Version). Show live status; allow cancel (kill the process).
- v1: Save results — write the raw XML (or a flattened CSV) via FileDialogs save picker.
- later: NSE script picker/search, scan history, "rescan host", export to HTML, send a discovered host:port into the Connections module for live socket inspection, scheduled scans via the Scheduled Tasks module.

## Integration plan (WinForge specifics)
- New files: `Services/NmapService.cs` (locate `nmap.exe`, build args from profile + toggles, run via `ShellRunner.Capture`, deserialize the `-oX` XML into host/port/service models, cancel support); `Pages/NmapModule.xaml(.cs)` (target box, profile ComboBox, flag ToggleSwitches, command preview, run/cancel buttons, results ListView grid mirroring the Connections grid layout, save button). Optional `Catalog/NmapOperations.cs` for one-shot ops (open Zenmap GUI, print version) as `Tweak.Cmd`/`Shell` rendered by TweakCard.
- Nav wiring: add `NavigationViewItem Content="Nmap Scanner · 網絡掃描" Tag="module.nmap"` in MainWindow.xaml right after the Connections item; add a `ModuleRegistry` entry (`Tag = "module.nmap"`, `En = "Nmap Scanner"`, `Zh = "網絡掃描"`, `Keywords = "nmap port scan network security host service os 掃描 端口"`); wire the Tag in MainWindow.xaml.cs `MapType` (`"module.nmap" => typeof(NmapModule)`) and `NavView_SelectionChanged`; add an `ApplyStartPage` case for `--page nmap`.
- Engine/install: winget id `Insecure.Nmap` via `EngineBars.AutoInstallButton("Insecure.Nmap", "Install Nmap", "安裝 Nmap", recheck, rescan)`. Note Npcap is bundled by the installer.
- Key CLIs/APIs: `nmap.exe <flags> -oX - <target>` (XML on stdout); parse with `System.Xml.Linq`/`XmlSerializer`; `ShellRunner.Capture` to run; FileDialogs for the save picker.

## Dependencies & risks
- OS detection (`-O`), raw SYN scans, and some timing options require Administrator + the Npcap driver — detect lack of elevation and surface a clear bilingual InfoBar instead of a cryptic Nmap error.
- Long scans (`-p-`, large CIDR) can run for minutes — must run off the UI thread, show progress/status, and support cancel by killing the child process.
- Always parse the XML (`-oX -`), never scrape human-readable stdout; exit code alone is not reliable.
- Sanitize/validate the target field; never shell-concatenate raw user text into a command line unsafely.
- Make clear in UI copy that scanning networks you do not own may be against policy/law.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; can install Nmap, run a quick scan of a target, see parsed hosts/ports/services in the grid, cancel a running scan, and save results; all user-facing strings bilingual (English + 粵語); uses FileDialogs (no WinRT pickers).
