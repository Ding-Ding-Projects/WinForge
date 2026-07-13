# Smoke Launch Batch 13 · 第十三批冒煙啟動

## Scope · 範圍

**EN —** This final route slice covers inventory indices 300–322: VirtualBox,
ViveTool, Voice, VPN Mesh, VS Code, Visual Studio Installer, Web Cloner, Web Login,
Windhawk, Window Manager, Winfetch, Wireshark, Wake-on-LAN, Word Frequency,
Workspaces, World Clock, World Monitor, WSL VM, XPath Tester, YAML ⇄ JSON, yt-dlp,
ZoomIt, and the shell-level All Apps picker.

**粵語 —** 呢個最後 route slice 覆蓋 inventory indices 300–322：VirtualBox、
ViveTool、Voice、VPN Mesh、VS Code、Visual Studio Installer、Web Cloner、Web Login、
Windhawk、Window Manager、Winfetch、Wireshark、Wake-on-LAN、字頻統計、Workspaces、
World Clock、World Monitor、WSL VM、XPath Tester、YAML ⇄ JSON、yt-dlp、ZoomIt，同埋
shell-level All Apps picker。

## Evidence · 證據

- The generated inventory records 323 routes, 805 aliases, 1,296 source-review files,
  350,519 source lines, 22 test projects, and no structural routing mismatch.
- The repository-local launch runner exercised indices 300–321 with a 5-second initial
  wait and bounded 15-second retry. All **22/22** passed at the first wait, with no
  retry or failure.
- `shell.allapps` is a modal shell route, so it was exercised separately with the
  focused UI Automation verifier. Initial verification exposed a real defect: `--page
  shell.allapps` did not enter the start-page routing switch. It now queues a one-shot
  NavigationView-loaded path, keeps the All Apps navigation item selected, and opens the
  picker once. The live verifier passed: `NewTabPickerDialog`, its search box, and the
  selected All Apps navigation item were all found without choosing a module.
- Source review repaired Word Frequency character mode to enumerate Unicode scalars
  rather than split emoji surrogate pairs; YAML ⇄ JSON now round-trips root scalars and
  empty collections and correctly tracks escaped quotes before comments.
  `tests/StructuredTextTools.Tests` passes **7/7** and
  `tests/ShellAllAppsRoute.Tests` passes **3/3**. A direct review of the 40 resolved
  final-route page/service files found no TODO, FIXME, or `NotImplementedException`
  marker. The XAML literal-safety guard passed, and the Debug x64 solution build
  completed with **0 errors**.

**Safety disposition · 安全處置：** This is launch/static and focused safe-functional
evidence, not permission to run user-machine actions. VM/software installation,
feature-flag changes, voice/VPN controls, website cloning/login, registry or Windows
tweaks, packet capture, Wake-on-LAN, workspace/process changes, WSL VM actions,
downloads, and ZoomIt controls were not live-executed. · 呢個係啟動／靜態同專注安全
功能證據，唔代表有權操作使用者電腦。VM／軟件安裝、feature-flag 變更、voice／VPN controls、
website cloning/login、registry 或 Windows tweaks、packet capture、Wake-on-LAN、
workspace／process changes、WSL VM actions、downloads 同 ZoomIt controls 都冇 live-run。

## Visual status · 視覺狀態

Fresh self-contained `driver.ps1 -Out` attempts for `virtualbox` and the repaired
`shell.allapps` route reached the WinForge window, but `CopyFromScreen` was unavailable.
The `PrintWindow` fallback produced a uniform frame, so graphics capture is unavailable
in this desktop session. No PNG was produced, inspected, replaced, or reused. Batch 13
is **capture-blocked**, never visual-pass. · 新嘅 self-contained `virtualbox` 同修正後
`shell.allapps` `driver.ps1 -Out` 嘗試有去到 WinForge 視窗，但 `CopyFromScreen` 唔可用；
`PrintWindow` fallback 產生 uniform frame，所以呢個 desktop session graphics capture
唔可用。冇 PNG 產生、檢查、替換或者重用。Batch 13 係 **capture-blocked**，絕對唔係
visual-pass。

Raw, ignored evidence is under `artifacts/smoke/batch13/` in the task worktree.
