# WinForge Feature Backlog · 功能待辦

North star: **≥1000 working features across 200+ modules**, run as a continuous Ralph loop. Each iteration: pull from this backlog, implement real (pure-managed) features + modules, fix bugs, verify **build + tests green**, then push → deploy → merge to main. Never stop adding ideas. Themes: **(A) improve Windows · (B) embed open-source apps (respect licenses: compatibility + attribution) · (C) extend existing modules**.

Guardrail: nothing merges/deploys unless `dotnet build WinForge.sln -c Debug -p:Platform=x64` = 0 errors **and** `tests/ReactorSim.Tests` = 62/62.

## Status
- Baseline: 144 modules (2026-07-01).
- **Batch 1 — DONE: +12 developer/text-utility modules → 156.**
  GUID/ID Generator, Hash & Checksum, Encode/Decode, JSON & XML Tools, Regex Tester, Password Generator, Text Tools, Base Converter, Epoch Converter, Unit Converter, Character Map, Color Tools.
- **Batch 2 — DONE: +12 utility/time/dev modules → 168.**
  Cron Builder, Data Faker, CSV/JSON, Timer & Stopwatch (Pomodoro), World Clock, Scratchpad (persisted notes), Calculator (expression evaluator), Randomizer, Date Calculator, URL Tools, Markdown Preview (WebView2), Number to Words. All pure-managed, control-based (no tweak cards), bilingual, leak-safe (named LanguageChanged handlers).
- Target to 200: **+32 modules** remaining across the batches below.

## Bug / hardening priorities (fold into every iteration)
- [x] **Freezes — round 1:** offloaded WMI/PDH/`Process.GetProcesses`/sensor ticks to `Task.Run` (SystemMonitor, ProcessExplorer, Connections, BatteryThermal, NativeUtilities); off-thread registry-hive enumeration; Unloaded timer stops (ScreenRecorder); calmed CakeFactory 80ms→200ms; fixed `LanguageChanged` leaks on the hot pages.
- [~] **Freezes — round 2 (structural):** batch-1 + batch-2 modules now use named `LanguageChanged` handlers + Unloaded teardown (no leak). STILL TODO: introduce a `LocalizedPage : Page` base and convert the ~100 remaining inline-lambda pages (the bulk of the leak + the language-toggle stall).
- [ ] **Freezes — round 3:** make `CropAndLockService.CreateFromScreenRect` async (remove `req.Done.Wait(4000)`); `NativeMessagePump._ready.Wait(1500)` → await; audit remaining sync-over-async (`.Result`/`.GetAwaiter().GetResult()`).

## UI overhaul epic — ZERO tweak cards (theme C)
Replace `Controls/TweakCard` (generic `TweakDefinition` renderer, 11 Kinds) with structured, animated, real-control UI (NOT card chrome). Data model: `Catalog/TweakCatalog.cs` → `TweakDefinition`. Conversion order (lowest risk first):
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
