# Handoff: Fix PNG images not showing in the GUI

| | |
|---|---|
| **Status** | Not started |
| **Source** | WinForge (local) — `Converters/UriToImageConverter.cs`, `Assets/`, `WinForge.csproj` Content items |
| **License** | Project-internal (WinForge) — no third-party code introduced |
| **Proposed module** | n/a — cross-cutting bugfix (touches existing modules: App Uninstaller, Clipboard, Capture Studio, About) |
| **Effort** | S — one converter rewrite + csproj asset audit + per-module source normalization, no new UI surface |

## What the user asked for
PNG images are not rendering anywhere in the GUI. Investigate and fix so that asset PNGs, runtime-generated PNGs (clipboard image items, capture thumbnails), and external logos (app uninstaller icons) all display reliably in both packaged and unpackaged/elevated runs.

## Recommended approach
Native C# fix (no external tool) — this is a WinUI image-pipeline bug, fully fixable in-tree. WinForge runs **unpackaged** (`<WindowsPackageType>None</WindowsPackageType>`, `WindowsAppSDKSelfContained=true`), so `ms-appx:///` URIs are unreliable and `ms-appdata:///` is unavailable. The current `UriToImageConverter` (Converters/UriToImageConverter.cs) does `new BitmapImage(new Uri(s))` — this throws/returns null for bare local file paths (e.g. `C:\Users\...\icon.png` is not a valid absolute URI without a `file:///` scheme), which is the most common runtime case for uninstaller logos and clipboard/capture PNGs. v1 scope: make every PNG path flow through one robust converter and guarantee build-action coverage.

## Features to implement (v1 → later)
- v1: Rewrite `UriToImageConverter.Convert` to normalize: (1) `http(s)://` → pass through; (2) `ms-appx:///` / `ms-appdata:///` → pass through; (3) anything resolving to an existing absolute file path → wrap as `new Uri(fullPath)` (produces `file:///`); (4) relative path → resolve against `AppContext.BaseDirectory`. Return null on failure (fallback glyph still works).
- v1: Add a `BitmapImage` with `DecodePixelWidth` cap and a `UriSource` set after construction (avoids the WinUI race where `new BitmapImage(uri)` swallows load errors silently).
- v1: Audit `WinForge.csproj` — asset PNGs are listed as `<Content Include>` (good), but confirm any decorative/screenshot PNGs added later inherit `CopyToOutputDirectory`/Content build action. Add a wildcard `<Content Include="Assets\**\*.png" CopyToOutputDirectory="PreserveNewest" />` if loose PNGs exist outside the explicit list.
- v1: Apply the converter consistently in `Pages/AppUninstallerModule.xaml` (already references it), `Pages/ClipboardModule.xaml.cs`, `Pages/CaptureStudioModule.xaml.cs`/`Services/CaptureService.cs`, `Pages/AboutPage.xaml.cs`.
- later: Image cache + async decode for large lists; broken-image fallback PNG; HiDPI scale-aware decode.

## Integration plan (WinForge specifics)
- New files: none. Edit `Converters/UriToImageConverter.cs` (core fix); audit `WinForge.csproj` Content items; verify usages in the 4 modules above.
- Nav wiring: none (no new module).
- Engine/install: n/a — pure managed code, no winget binary.
- Key APIs/CLIs to call: `Microsoft.UI.Xaml.Media.Imaging.BitmapImage` (`UriSource`, `DecodePixelWidth`), `System.IO.Path.IsPathRooted`/`File.Exists`, `System.AppContext.BaseDirectory`, `new Uri(absolutePath)` for `file:///`. Use `FileDialogs` (Services/FileDialogs.cs) if any picker is needed — never WinRT pickers.

## Dependencies & risks
- Unpackaged elevated runs: an admin-elevated process may lack read access to a non-admin user profile PNG — surface a fallback glyph rather than crashing; log the path.
- `ms-appx:///` works at design-time but can 404 at runtime unpackaged — prefer `file:///` from BaseDirectory for shipped assets.
- Silent failure mode: `BitmapImage` swallows decode errors; wire `ImageFailed`/`ImageOpened` during dev to confirm.
- Keep `Convert` total — never throw (XAML binding errors are hard to diagnose).

## Acceptance criteria
- Builds clean (Debug + Release x64).
- Asset PNGs render in About/header; runtime PNGs render in Clipboard image items and Capture Studio thumbnails; external app logos render in App Uninstaller.
- Converter handles file path, `file:///`, `ms-appx:///`, and `http(s)` sources; returns null (fallback glyph) on failure with no crash.
- Any user-facing strings added remain bilingual (English + 粵語); no WinRT pickers introduced.
