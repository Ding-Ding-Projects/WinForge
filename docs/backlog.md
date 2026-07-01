# WinForge Feature Backlog · 功能待辦

North star: **≥1000 working features across 200+ modules**, run as a continuous Ralph loop. Each iteration: pull from this backlog, implement real (pure-managed) features + modules, fix bugs, verify **build + tests green**, then push → deploy → merge to main. Never stop adding ideas. Themes: **(A) improve Windows · (B) embed open-source apps (respect licenses: compatibility + attribution) · (C) extend existing modules**.

Guardrail: nothing merges/deploys unless `dotnet build WinForge.sln -c Debug -p:Platform=x64` = 0 errors **and** `tests/ReactorSim.Tests` = 62/62.

## Status
- Baseline: 144 modules (2026-07-01).
- **Batch 1 — DONE: +12 developer/text-utility modules → 156.**
  GUID/ID Generator, Hash & Checksum, Encode/Decode, JSON & XML Tools, Regex Tester, Password Generator, Text Tools, Base Converter, Epoch Converter, Unit Converter, Character Map, Color Tools.
- **Batch 2 — DONE: +12 utility/time/dev modules → 168.**
  Cron Builder, Data Faker, CSV/JSON, Timer & Stopwatch (Pomodoro), World Clock, Scratchpad (persisted notes), Calculator (expression evaluator), Randomizer, Date Calculator, URL Tools, Markdown Preview (WebView2), Number to Words. All pure-managed, control-based (no tweak cards), bilingual, leak-safe (named LanguageChanged handlers).
- **Batch 3 — DONE: +12 networking/dev/Windows-power modules → 180.**
  PATH Doctor, Subnet Calculator, Ping & Traceroute, Port Scanner, Wake-on-LAN, DNS Lookup (DoH), MAC Address Tools, Base32/58/85, JWT Inspector (HMAC verify), Env Snapshot & Diff, HTTP Header Inspector, IP & Network Info. All pure-managed, control-based, bilingual, leak-safe, async I/O.
- **Batch 4 — DONE: +20 dev/format/calc/text/system modules → 200. 🎯 200-MODULE MILESTONE REACHED.**
  JSON Query, SQL Formatter, XPath Tester, String Inspector, Gitignore Generator, Percentage Calculator, Loan Calculator, Classic Ciphers, Check Digit Validator, TOTP Authenticator, Namespaced UUID, Table Formatter, Text Statistics, HTML Preview, Recycle Bin Manager, File Split & Join, Encoding Converter, Health Calculators, ASCII Banner, MIME Type Lookup. All pure-managed, control-based, bilingual, leak-safe.
- **Batch 5 — DONE: +6 depth modules → 206.**
  Case Converter, HTML to Markdown, Color Palette, Number Sequence, Find & Replace (multi-rule regex), Duration Calculator. Plus **first 2 tweak-card conversions** and **freeze rounds 2+3 progress** (see below).
- **Batch 6 — DONE: +4 dev modules → 210.**
  JSON Diff, Text Diff (LCS), INI Editor, Image ↔ Base64. Plus **4 more tweak-card conversions** and **freeze round 2** (see below).
- **Batch 7 — DONE: +5 depth modules → 215.**
  String Escaper (11 syntaxes), Line Tools, Gradient Generator, Fancy Text (15 Unicode styles), Tally Counter. Plus **5 more tweak-card conversions** and **freeze round 2** (see below).
- **Batch 8 — DONE: +5 depth modules → 220.**
  HTML Formatter, CSS Formatter, Emoji Picker (~300 emoji), Symbols Palette, Text↔Binary. Plus **6 more tweak-card conversions** and **freeze round 2** (see below).
- **Batch 9 — DONE: +4 dev modules → 224.**
  Template Renderer, ASCII Table, Meta Tag Generator, chmod Calculator. Plus the **`Controls/ControlRowList` renderer + SettingsHub + GitHub/AudioEditor/Ollama conversions**, and **freeze round 2 essentially finished** (see below).
- **Batch 10 — DONE: +4 modules → 228.**
  JSON Analyzer, Aspect Ratio, Scientific Notation, Color Blindness Sim. Plus the **FINAL 6 tweak-card conversions → 🎉 ZERO TWEAK CARDS** (see below).
- **Batch 11 — DONE: +6 modules → 234; `Controls/TweakCard.xaml(.cs)` DELETED (dead code gone).**
  Markdown TOC, JSON→Types (TS/C#), Named Colors (nearest of 148 CSS colours), Number Formatter, Text Redactor (PII masking), String Compare (Levenshtein/Jaro-Winkler/LCS). Also tidied the last dangling `<see cref="…TweakCard"/>` doc-comments.
- **Batch 12 — DONE: +8 modules → 242 (pure depth phase).**
  Password Strength, Habit Tracker (persisted), Expense Splitter (settle-up), Name Generator, Event Countdown (persisted), Column Tools, JSON Flatten/Unflatten, Unit Price comparison. (Fixed 2 agent slips pre-merge: unqualified `Color.FromArgb` under `using Microsoft.UI` in HabitTracker/UnitPrice → `Windows.UI.Color`.)
- **Batch 13 — DONE: +7 modules → 249.**
  cURL/fetch/PowerShell Generator, Clipboard Inspector, Timezone Planner, HTTP Status Codes reference, WCAG Contrast Grid, Entropy Analyzer, IPv6 Tools. (Fixed 1 agent slip: `ScrollBarVisibility` on a TextBox → attached `ScrollViewer.*` form in CurlGen.)
- **Batch 14 — DONE: +7 modules → 256 🎉 (crossed 250).**
  JSON Patch (RFC 6902 diff+apply), JSONL/NDJSON Tools, Text Wrap/Reflow, Short ID Encoder (Base62/58/36/Crockford + NanoID), Dotenv Editor (parse/validate/convert to shell·JSON·docker), HTTP Headers Reference (~80 headers), Calendar (month grid + ISO weeks). Clean pitfall-scan — no agent slips this batch; build 0 errors, tests 62/62.
- **Batch 15 — DONE: +8 modules → 264.**
  JSON Merge Patch (RFC 7386), ULID/Snowflake generator+decoder, Hosts File Editor (safe non-elevated + block-list presets), iCalendar (.ics) Builder (RRULE/VALARM), Ascii85/Base85 (Adobe·Z85·RFC1924), Security Header Scorecard (A+..F grading), Box & Banner Text, Number-to-Words+ (currency + Chinese 大寫). Clean pitfall-scan — no agent slips; build 0 errors, tests 62/62. Also fixed CI: `pages.yml` now chains off the site-data workflow via `workflow_run` (token-authored regen commits couldn't trigger the deploy → live site had gone stale); README/CLAUDE.md counts refreshed to 264; GitHub repo description updated.
- **Batch 16 — DONE: +8 modules → 272.**
  CSV Linter (RFC 4180 lint+repair), JWT Builder (HMAC HS256/384/512 sign+verify), TOML↔JSON (hand-written parser), Cron Next Runs (5-field + macros, timezone-aware), Markdown Table generator/reformatter, Semver Range Tester (node-semver), Glob Tester (glob→regex), HAR Analyzer (HTTP Archive). Clean pitfall-scan — no agent slips; build 0 errors, tests 62/62.
- **Next focus:** keep the depth cadence toward **1000+ working features** (add ~6-8 modules/iteration + extend existing modules, theme C). Big epics DONE: zero tweak cards (TweakCard deleted), freeze rounds essentially complete (~3 accepted-risk pages). At **272 modules** / 16 iterations.

## Bug / hardening priorities (fold into every iteration)
- [x] **Freezes — round 1:** offloaded WMI/PDH/`Process.GetProcesses`/sensor ticks to `Task.Run` (SystemMonitor, ProcessExplorer, Connections, BatteryThermal, NativeUtilities); off-thread registry-hive enumeration; Unloaded timer stops (ScreenRecorder); calmed CakeFactory 80ms→200ms; fixed `LanguageChanged` leaks on the hot pages.
- [~] **Freezes — round 2 (structural):** all batch-1/2/3 modules ship leak-safe (named `LanguageChanged` handlers). Round 2 also converted **20 existing pages** off the inline-lambda leak (AdvancedPaste, AltSnap, AppUninstaller, Awake, BulkOps, Clipboard, ColorPicker, CommandPalette, Communications, ContextMenu, Devices, Decompiler, CropAndLock, Camoufox, CaptureStudio, ConfigBackup, Connectors, AndroidAdb, Rename, EnvVars). Round 2 continued: +10 pages, then +12 more (Blender, DiagramEditor, DiffMerge, DiskAnalyzer, DiskBenchmark, Duplicates, Emulator, EventViewer, EverythingSearch, Fastboot, FileLocksmith, FileServer). Round 2 continued again: +12 more (ZoomIt, TimeUnit, ScreenRuler, QuickType, TextOcr, HexEditor, PdfToolkit, TestDisk, RustDesk, WebCloner, WorldMonitor, ImagingGame). Round 2 continued again: +12 more (Winfetch, FontManager, GifLab, Flashcards, NewPlus, DiskHealth, PowerToysExtras, PixelEditor, VolumeMixer, Keyboard, WindowManager, ImageEditor). Round 2 continued again: +12 more (Mouse, Drives, OneDrive, VaultVolumes, MouseUtils, SystemDoctors, VpnMesh, Workspaces, YtDlp, Torrent, Proxmox, Docker), then +12 more (AiAgents, AiChat, CategoryPage, Dashboard, HomeAssistant, LightSwitch, Mail, MinecraftLauncher, MinecraftServer, MinecraftWorldTools, MouseWithoutBorders, SearchResults). **Leaking pages: ~96 → ~3 — round 2 ESSENTIALLY DONE.** Remaining ~3 are deliberately-skipped complex ones (PackageManager — background scheduler + multiple CTS; Amulet — external process tracking) accepted as low-risk; convert only if touching them anyway.
- [~] **Freezes — round 3:** DONE `CropAndLockService.CreateFromScreenRectAsync` (removed the `Done.Wait(4000)` UI-thread block via `TaskCompletionSource` + `await Task.WhenAny(..., Task.Delay(4000))`; caller updated). LEFT `NativeMessagePump._ready.Wait(1500)` (inside a `lock`, shared by 4 services, near-instant — risky to convert). STILL TODO: audit remaining sync-over-async (`.Result`/`.GetAwaiter().GetResult()` in AdvancedPaste/PlainTextPaste/ReactorStatusClient/Ftp).

## UI overhaul epic — ZERO tweak cards (theme C)
Replace `Controls/TweakCard` (generic `TweakDefinition` renderer, 11 Kinds) with structured, animated, real-control UI (NOT card chrome). Data model: `Catalog/TweakCatalog.cs` → `TweakDefinition`.
**Phase 1 — 20 modules + SettingsHub converted; shared renderer built.** Converted off TweakCard: Archives, Cloudflare, TaskbarTweaker, Media, Rainmeter, GlazeWm, Windhawk, ShellMenu, Nmap, NilesoftShell, LibreOffice, FancyZones, Packer, Komorebi, VsCode, Wireshark, Terminal, GitHub, AudioEditor, Ollama — each tweak → a Grid row (Action→Button awaiting RunAsync, Toggle→ToggleSwitch, Choice/RadioGroup→ComboBox/RadioButtons, Slider/Number→Slider/NumberBox, Info→refreshable TextBlock), one persistent `InfoBar`, `EntranceThemeTransition`, subtle dividers (no card chrome), behavior preserved.
**`Controls/ControlRowList.cs` — the reusable renderer** (`SetTweaks`/`Clear`) now backs the catalog-driven pages **SettingsHub, CategoryPage, SearchResultsPage, DashboardPage**, and the last stragglers **Amulet, Blender, FileZilla** (which used the fully-qualified `new Controls.TweakCard()` form) were converted too.
## 🎉 ZERO TWEAK CARDS — DONE.
No page instantiates `TweakCard` anymore (grep-verified: 0 `new TweakCard`/`new Controls.TweakCard`/`SetTweak` across Pages/). Every tweak now renders as a real, animated control row via per-module builders or the shared `ControlRowList`. **Cleanup left:** `Controls/TweakCard.xaml(.cs)` is now dead code — delete it (+ its `App.xaml` resource refs, and stale mentions in AGENTS.md) in a dedicated low-risk iteration.
Conversion order (lowest risk first):
1. Info/status catalogs (read-only) → structured status sections.
2. Simple toggle catalogs (Appearance, Explorer) → labelled `ToggleSwitch` rows.
3. Action-heavy modules (Terminal, Packer, Komorebi, GitHub) → control rows + persistent result `InfoBar`.
4. `SettingsHubModule` / `CategoryPage` / `SearchResultsPage` (catalog-driven, ~1,174 tweaks) → `Expander`-grouped control grids.
5. Wizard/multi-step last → `FlipView`/paged navigation.
Add Fluent entrance/state animations throughout.

## Module backlog (theme A/B/C) — candidates for coming batches
**Batch 2 — dev/data (A/C):** Markdown Preview (WebView2), CSV/TSV Viewer, YAML↔JSON, Cron Expression Builder, JWT Inspector (verify HS256), UUID/QR studio, Diff-as-you-type, Hex↔ASCII inspector, .env editor, HTTP header inspector.
**Batch 3 — Windows power (A):** Context-menu manager, Startup delay tuner, Storage Sense tuner, God-Mode launcher, Sysinternals-style handle viewer, Scheduled-task builder wizard, Firewall rule manager, Restore-point manager, WSL manager, Hyper-V quick-switch.
**Batch 4 — networking (A):** Ping/Traceroute/DNS-lookup, Port scanner (managed), Whois, Subnet calculator, mDNS/SSDP browser, Speedtest (managed), Wake-on-LAN, ARP table, Hosts-profile switcher.
**Batch 5 — OSS embeds (B, license-checked):** each entry must record license + attribution before embed. Candidates: a managed Markdown editor, a managed image-format toolkit (ImageSharp — Apache-2.0), a managed QR lib, a managed torrent (MonoTorrent — MIT, already present), a managed SSH/SFTP (SSH.NET — MIT). Prefer permissive (MIT/Apache/BSD); avoid GPL in-proc linking.
**Batch 6 — extend existing (C):** more effects in Audio Editor; more filters in Image Editor; more inspectors in Process Explorer; more SMART attributes in Disk Health; more presets in FancyZones.

## Idea intake (never stop)
Append new ideas here as they surface; promote to a batch when scoped. Keep each idea one line with its theme tag (A/B/C).

- (A) PATH doctor — edit/validate/dedupe user+system PATH, flag dead entries.
- (A) DNS-over-HTTPS / DNS lookup tester (managed HttpClient + raw DNS).
- (A) Focus/DND scheduler; (A) Clipboard-history pinning; (A) Windows Update history viewer.
- (A) Group Policy quick-toggles (registry-backed, reversible).
- (B) Managed EPUB/CBZ/CBR reader (System.IO.Compression; own rendering code — record license).
- (B) QR code generator — reimplement the encoder ourselves (no GPL lib) → theme B/own-code.
- (C) Calculator: matrix mode, variable memory, unit-aware arithmetic.
- (C) Hasher: add xxHash/BLAKE2 (managed); (C) Encoder: add Base32/Base58/Ascii85.
- (C) Text Tools: add JSON-path extract, column select, find/replace-regex.
- (A) Env-var diff between snapshots; (A) Services dependency grapher.
- (C) Extend Hasher with xxHash/BLAKE2; extend Encoder with Base45/Z85; extend Calculator with variables + unit-aware mode.
- (C) Cron → human-readable + timezone-aware next runs; (C) Regex → sample-string generator.
- (A) Scheduled-task quick-creator wizard; (A) Firewall rule viewer/toggler; (A) Restore-point create/list.
- (B) Managed EPUB/CBZ reader (System.IO.Compression, own render — record license); (B) QR encoder written from scratch (own code).
- (C) Time-zone meeting planner (overlap finder); (C) Pomodoro stats/history; (C) Text → speech via Windows.Media.SpeechSynthesis.
- (A) Clipboard-format inspector (what formats are on the clipboard); (C) URL → cURL / fetch snippet generator.
- Batch-14 fresh ideas (never stop):
  - (C) JSON Patch: add merge-patch (RFC 7386) mode; (C) JSONL Tools: add sort-by-key + jq-lite field select.
  - (A) HOSTS-file editor with block-list presets; (A) Windows scheduled-task viewer (read-only) → toggle enabled.
  - (B) Managed diff-match-patch text merge (own port, record license); (B) Base-N big-file encoder (Ascii85/Z85) streaming.
  - (C) Calendar: add iCal (.ics) event export + recurrence preview; (C) Dotenv: add .env ↔ appsettings.json bridge.
  - (C) Short ID: add ULID + Snowflake decode (timestamp/machine/seq breakdown); (A) GUID/ULID timestamp extractor.
  - (A) Reg-hive value search across HKCU/HKLM; (C) HTTP Headers: add security-header scorecard (given a paste of a response).
  - (C) Text Wrap: add box-drawing/banner mode; (C) Number → words: add currency + ordinal + Chinese-numeral modes.
