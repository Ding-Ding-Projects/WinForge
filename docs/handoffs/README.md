# WinForge feature handoffs · 功能交接

This folder holds **one handoff document per requested feature**. They are specs to **implement later** —
nothing here is built yet. Each handoff is self-contained: source, recommended approach, feature list,
WinForge integration plan, dependencies/risks, and acceptance criteria.

## Global strategy (applies to every handoff)

1. **Clone natively in the C# WinUI GUI** wherever feasible — the feature should live *inside* WinForge as a module.
2. **If it can't realistically be reimplemented in C#** (large C/C++/Qt/Rust/Electron/Python codebases, kernel
   drivers, packet capture, 3D engines, office suites) → **wrap its CLI / binary / local API** and build a rich
   WinUI front-end around it (install the binary via winget where possible).
3. **Everything becomes a WinForge module** wired into the navigation (no external redirects beyond launching the
   wrapped tool). De-brand clones where a license requires it (e.g. the VeraCrypt-derived vault).

### WinForge integration checklist (every feature)
- New `Pages/<X>Module.xaml(.cs)`, `Services/<X>Service.cs`, and/or `Catalog/<X>Operations.cs`.
- Nav: `MainWindow.xaml` NavigationViewItem (`Tag="module.<key>"`), `Services/ModuleRegistry.cs` entry,
  and `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, optional `ApplyStartPage`).
- Use `Services/FileDialogs.cs` (never WinRT pickers); `ShellRunner` for CLIs; `EngineBars.AutoInstallButton`
  for winget installs; `Controls/TweakCard` for op lists.
- Bilingual (English + 粵語) strings everywhere; builds clean Debug + Release x64.

## Index

| # | Feature | Source | Approach |
|---|---------|--------|----------|
| 01 | [WebView2 in-app login](01-webview2-login.md) | NuGet WebView2 | Native |
| 02 | [SSH toolset (terminal, profiles, passwordless deploy)](02-ssh-toolset.md) | ssh/SSH.NET/ConPTY | Native |
| 03 | [Config exports include secrets](03-exports-include-secrets.md) | ConfigBackupService | Native |
| 04 | [AI agent config editors](04-ai-agent-config-editors.md) | claude/codex/opencode/openclaw/hermes | Native |
| 05 | [WinForge Vault volume engine](05-vault-volume-engine.md) | SecureVault (local) | Hybrid (de-brand) |
| 06 | [Resume & cover-letter writer](06-resume-cover-letter-writer.md) | opencode/claude | Native |
| 07 | [Scheduled settings auto-sync to git](07-settings-auto-sync-git.md) | ConfigBackup + git | Native |
| 08 | [README + wiki screenshots](08-readme-wiki-screenshots.md) | docs/wiki | Docs |
| 09 | [VirtualBox manager](09-virtualbox-manager.md) | VBoxManage CLI | CLI wrap |
| 10 | [Audio editor (Audacity-style)](10-audacity-audio-editor.md) | NAudio + ffmpeg | Hybrid |
| 11 | [VLC media player (embedded)](11-vlc-media-player.md) | LibVLCSharp | Hybrid |
| 12 | [Email client (Thunderbird-style)](12-thunderbird-mail.md) | MailKit | Native |
| 13 | [BUG: PNG images not showing](13-fix-png-not-showing.md) | WinForge | Bugfix |
| 14 | [Amulet Minecraft world editor](14-amulet-map-editor.md) | Amulet zip (local) | Wrap/launch |
| 15 | [Packer (image builder)](15-packer.md) | codingmachineedge/packer | CLI wrap |
| 16 | [ViaProxy (Minecraft proxy)](16-viaproxy.md) | codingmachineedge/ViaProxy | Wrap jar |
| 17 | [qBittorrent](17-qbittorrent.md) | codingmachineedge/qBittorrent | Wrap Web API |
| 18 | [VS Code integration](18-vscode.md) | codingmachineedge/vscode | Wrap CLI |
| 19 | [yt-dlp downloader](19-yt-dlp.md) | yt-dlp | CLI wrap |
| 20 | [WorldMonitor](20-worldmonitor.md) | koala73/worldmonitor | Research |
| 21 | [AI website cloner](21-ai-website-cloner.md) | JCodesMore/ai-website-cloner-template | Native + AI |
| 22 | [Minecraft server setupper (Paper/Spigot + plugins)](22-minecraft-server-setupper.md) | PaperMC/BuildTools | Native |
| 23 | [Winfetch (system info)](23-winfetch.md) | lptstr/winfetch | Native clone |
| 24 | [Gitty](24-gitty.md) | Omibranch/gitty | Research |
| 25 | [btop4win resource monitor](25-btop4win.md) | aristocratos/btop4win | Native clone |
| 26 | [7+ Taskbar Tweaker](26-taskbar-tweaker.md) | m417z/7-Taskbar-Tweaker | Hybrid |
| 27 | [Nilesoft Shell (context menu)](27-nilesoft-shell.md) | moudey/shell | Wrap + config |
| 28 | [EarTrumpet (per-app volume)](28-eartrumpet.md) | File-New-Project/EarTrumpet | Native clone |
| 29 | [Windhawk (mod manager)](29-windhawk.md) | ramensoftware/windhawk | Wrap/manage |
| 30 | [Rainmeter (desktop widgets)](30-rainmeter.md) | rainmeter/rainmeter | Wrap + skins |
| 31 | [TestDisk / PhotoRec recovery](31-testdisk-photorec.md) | cgsecurity testdisk | CLI wrap |
| 32 | [pgAdmin 4 / Postgres tool](32-pgadmin4.md) | pgadmin-org/pgadmin4 | Hybrid (Npgsql) |
| 33 | [FileZilla-style FTP/SFTP](33-filezilla.md) | FluentFTP + SSH.NET | Native clone |
| 34 | [Aseprite-style pixel editor](34-aseprite.md) | aseprite/aseprite | Hybrid |
| 35 | [Blender integration](35-blender.md) | blender | Wrap CLI |
| 36 | [Android SDK tools manager](36-android-sdk-tools.md) | sdkmanager/avdmanager | CLI wrap |
| 37 | [Ollama local LLM runner](37-ollama.md) | ollama/ollama | Native (REST) |
| 38 | [Wireshark / packet capture](38-wireshark.md) | tshark/dumpcap | CLI wrap |
| 39 | [Nmap scanner](39-nmap.md) | nmap/nmap | CLI wrap |
| 40 | [LibreOffice integration](40-libreoffice.md) | libreoffice | Wrap CLI |
| 41 | [Bitwarden password manager](41-bitwarden.md) | bw CLI | Native (CLI) |
| 42 | [TimeLens](42-timelens.md) | 0pandadev/timelens | Research |
| 43 | [RustDesk remote desktop](43-rustdesk.md) | rustdesk/rustdesk | Wrap/manage |
| 44 | [Rufus USB imager](44-rufus.md) | pbatard/rufus | Hybrid |
| 45 | [Windows Terminal integration](45-windows-terminal.md) | microsoft/terminal | Wrap + ConPTY |
| 46 | [GitHub Desktop features](46-github-desktop.md) | desktop/desktop | Native (Git module) |
| 47 | [ScreenToGif](47-screentogif.md) | NickeManarin/ScreenToGif | Native clone |
| 48 | [AltSnap (window dragging)](48-altsnap.md) | RamonUnch/AltSnap | Wrap/manage |
| 49 | [FancyZones (PowerToys zones)](49-fancyzones.md) | PowerToys | Wrap/manage |
| 50 | [Komorebi tiling WM](50-komorebi.md) | LGUG2Z/komorebi | CLI wrap |
| 51 | [GlazeWM tiling WM](51-glazewm.md) | glzr-io/glazewm | Wrap + config |

> Pick any row to implement. Suggested early wins (high value, mostly native, low risk): **13 (PNG bugfix)**,
> **37 (Ollama)**, **19 (yt-dlp)**, **23 (winfetch)**, **33 (FileZilla SFTP)**, **02 (SSH)**, **07 (auto-sync)**.
