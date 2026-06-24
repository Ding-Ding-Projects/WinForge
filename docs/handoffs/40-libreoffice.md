# Handoff: LibreOffice Integration

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `soffice` / `soffice.com` (libreoffice.org; source github.com/LibreOffice/core, C++) |
| **License** | MPL-2.0 (open source). WinForge only shells out to the installed binary — no LibreOffice code is bundled or redistributed. |
| **Proposed module** | Document Converter · Media / Tools group · Tag `module.libreoffice` |
| **Effort** | M — no native reimplementation; effort is a rich WinUI front-end over the headless CLI plus robust batch/progress handling. |

## What the user asked for
Integrate LibreOffice so WinForge can open documents and, primarily, run headless batch document **conversion** (`soffice --headless --convert-to pdf/docx/xlsx/odt`). Install via `winget TheDocumentFoundation.LibreOffice`. Pairs with the suite's existing DOCX/PDF needs.

## Recommended approach
**CLI/binary wrap.** LibreOffice is a massive C++/UNO office suite — reimplementing its filters and rendering in C# is wholly infeasible, so per the global strategy this is a legitimate wrap. WinForge already shells out to external tools via `ShellRunner`, so we build a self-contained WinUI module that drives `soffice` headlessly and never redirects the user out of the app. Realistic v1: a drag-and-add file list, pick a target format, choose an output folder, convert the batch with per-file status, plus an "Open in LibreOffice" action for editing.

## Features to implement (v1 → later)
- v1: Add files (FileDialogs multi-select) into a convertible list with type/size; target-format dropdown (PDF, DOCX, XLSX, ODT, ODS, PPTX, CSV, TXT, PNG); output-folder picker (default = source folder); Convert button running `soffice --headless --convert-to <fmt> --outdir <dir> <files...>`; per-file status (queued/converting/done/failed) + overall progress; "Open in LibreOffice" (launch `soffice <file>`); raw log pane.
- later: Per-extension filter overrides (e.g. `pdf:writer_pdf_Export`, `csv:Text - txt - csv (StarCalc)` with field options); recursive folder conversion with mirrored output tree; convert presets saved per session; PDF export quality/range options; merge-to-single-PDF; queue/cancel of long batches; drag-drop from Explorer; remember last format/folder.

## Integration plan (WinForge specifics)
- New files: `Services/LibreOfficeService.cs` (locate `soffice.com`/`soffice.exe`, build arg lists, run via `ShellRunner.Capture`, run conversions sequentially off the UI thread, parse "convert ... -> ..." stdout lines, map source→output path, expose status events + version probe); `Pages/LibreOfficeModule.xaml` + `.cs` (file list, format dropdown, folder picker, convert/cancel, progress, log). Optionally `Catalog/LibreOfficeOperations.cs` for TweakCard-style ops (check install, kill stray `soffice` processes).
- Nav wiring: add `NavigationViewItem Tag="module.libreoffice"` in `MainWindow.xaml` under the Media / Tools group; add a `ModuleRegistry` entry (`Services/ModuleRegistry.cs`) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType` and `NavView_SelectionChanged`, plus `ApplyStartPage` for `--page libreoffice`.
- Engine/install: `EngineBars.AutoInstallButton("TheDocumentFoundation.LibreOffice", "Install LibreOffice", "安裝 LibreOffice", recheck, rescan)`; detect `soffice` before enabling Convert and show the install bar when missing.
- Key CLIs to call: `soffice --headless --convert-to <fmt[:filter[:opts]]> --outdir "<dir>" "<file>"`; `soffice <file>` (open for editing); `soffice --version`. Prefer `soffice.com` on Windows for stdout capture; always pass a unique `-env:UserInstallation=file:///<temp>` profile so conversions work even while the desktop app is open, and add `--norestore --nolockcheck`.

## Dependencies & risks
- Requires LibreOffice installed; `soffice.exe` is typically under `C:\Program Files\LibreOffice\program\` and not on PATH — resolve via registry/known paths, not just `where`.
- `soffice` exits 0 even on some failures and won't run two instances against one profile — use the isolated `-env:UserInstallation` profile and verify the output file exists per item.
- stdout/exit-code behavior differs `soffice.exe` vs `soffice.com`; conversions can be slow/hang on locked or corrupt files — run sequentially, support cancel/kill, keep a raw log fallback.
- Some target formats need explicit filter names; an ambiguous `--convert-to` can pick the wrong filter — allow per-format filter overrides (the "later" item).
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Media / Tools.
- Core flow works: add several documents, pick a target format (e.g. PDF), choose an output folder, convert the batch with per-file status, and produce the files; "Open in LibreOffice" launches the file.
- Install bar appears when LibreOffice is missing and `AutoInstallButton` installs it via winget; conversion succeeds even with the desktop app already running.
- All UI strings bilingual (English + Cantonese); file/folder selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
