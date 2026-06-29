# Handoff 54 — Rich-Text Toolbar rollout

**Goal:** give every rich / multi-line / read-long text surface in WinForge the same neat formatting
toolbar that the first-launch **Terms & Conditions** reader now has — **font family, font size, bold /
italic / strikethrough, font colour, theme, copy, Save TXT, Save PDF** (and optional language switch).

**Hard requirement (read this twice):** the toolbar state is **per text-box instance**. Each surface keeps
its **own** font / size / colour / bold-italic-strike / theme. Changing the formatting of one box must
**never** change another box or anything else in the app. No global or `static` formatting state, and no
app-wide theme calls from a per-surface toolbar.

---

## 1. What already exists (the reference implementation)

The pattern is fully working in [Services/TermsService.cs](../../Services/TermsService.cs):

- `ShowTermsAsync` (≈ line 86) builds the toolbar + reading pane inline. It demonstrates every feature:
  - Language `ComboBox` → re-renders the body text (English / 繁中・粵語 / Bilingual).
  - Font-family `ComboBox` (curated `FontChoices`, line ≈ 387) → `body.FontFamily`.
  - Size `NumberBox` → `body.FontSize` + `LineHeight`.
  - Bold / Italic / Strikethrough `ToggleButton`s → `FontWeight` / `FontStyle` / `TextDecorations`.
    **Use text glyphs (`B`, italic `I`, struck-through `S`), not Segoe icon glyphs** — the icon font's
    strikethrough glyph renders like an underline (we already hit that bug).
  - Font colour via a `ColorPicker` in a `Flyout` (+ a reset button).
  - Theme `ComboBox` → here it calls `App.SetTheme(...)` **app-wide on purpose** (it's the startup gate).
    **The reusable control must NOT do this** — see §3.
  - Copy → `DataPackage` / `Clipboard`.
  - Save TXT → `FileDialogs.SaveFileAsync(..., ".txt")` then `File.WriteAllText`.
  - Save PDF → `RenderPdf` (≈ line 313) + `WrapLine` (≈ line 357): PdfSharp `XGraphics` /
    `XFont`, honouring family / size / bold / italic / strikethrough / colour, with manual word-wrap +
    pagination and a CJK-safe font fallback.
- The toolbar is laid out as **two rows** (no horizontal scroll) so nothing is hidden.

Copy the *mechanics* from there, but **do not** copy the app-wide theme behaviour or the inline structure.

---

## 2. The deliverable — a reusable control

Create **`Controls/RichTextToolbar.cs`** (code-behind control, namespace `WinForge.Controls`). It owns its
formatting state in **instance fields** and applies it to **one** target supplied at construction:

```csharp
// Read-only surface (TextBlock / SelectableTextBlock):
var bar = new RichTextToolbar(target, RichTextToolbar.Mode.ReadOnly);

// Editable surface (RichEditBox / TextBox):
var bar = new RichTextToolbar(target, RichTextToolbar.Mode.Editable);
```

Suggested shape:

- Constructor takes the target `FrameworkElement` (a `TextBlock`/`RichTextBlock` for read-only, or a
  `RichEditBox`/`TextBox` for editable) plus a `Mode`.
- All state — `FontFamily`, `double FontSize`, `bool Bold/Italic/Strike`, `Color? Colour`,
  `ElementTheme Theme` — lives as **private instance fields**, initialised from the target's current values.
- One private `Apply()` that writes those fields onto the target only.
- Reuse `FontChoices` and the PDF rendering by **extracting them into a shared helper**
  `Services/TextExportService.cs` (move `RenderPdf` + `WrapLine` + `FontChoices` there; have both
  `TermsService` and `RichTextToolbar` call it — do not duplicate).
- A `string GetPlainText()` strategy per mode (TextBlock.Text, or RichEditBox `ITextDocument.GetText`).
- Expose an optional `LanguageProvider`/`Func<AppLanguage,string>` hook for surfaces that have bilingual
  source text (most won't — make it optional).

### Editable surfaces (RichEditBox)
For `RichEditBox`, formatting should apply to the **current selection** (or whole doc if none) via
`target.Document.Selection.CharacterFormat` (`Bold`, `Italic`, `Strikethrough`, `Size`, `Name`,
`ForegroundColor`). Save PDF/TXT pulls text via `Document.GetText(TextGetOptions.None, out var s)`.

---

## 3. Theme scoping — the make-or-break rule

The user's explicit requirement: **one box's theme change must not theme the whole app.**

- **Do NOT** call `App.SetTheme(...)` or touch `App.Shell` from `RichTextToolbar`.
- Instead set `RequestedTheme` on the **smallest container that wraps just this surface + its toolbar**
  (e.g. the `Grid`/`StackPanel` you put the toolbar and target in). `ElementTheme` inherits down the visual
  tree, so a scoped container themes only its own subtree.
- Same principle for every other setting: write to the **target**, never to shared/static/app state.

A quick self-test for the reviewer: open two surfaces, set one to Dark + Consolas + red + bold, leave the
other default → the second must be visually unchanged.

---

## 4. Where to roll it out

~36 files contain rich / multi-line / selectable text surfaces (found via
`RichEditBox|RichTextBlock|AcceptsReturn|IsTextSelectionEnabled`). Prioritise the genuinely
*document-like* ones first; skip tiny single-line inputs and terminal/console views (they have their own
rendering). Non-exhaustive candidates:

- **Editable docs:** `ResumeWriterModule`, `DiffMergeModule`, `FlashcardsModule`, `AiChatModule`,
  `AiAgentsModule`, `OllamaModule`, `SqliteBrowserModule` (query box), `RegistryEditor` (value text).
- **Read-long / viewer:** `LicensesPage`, `PackageDetailsDialog`, `OpenSourceAppHubModule`,
  `WinfetchModule`, `SystemDoctorsModule`, `PdfToolkitModule.Viewer`, `PeekModule`.

Treat that list as a starting point — grep again and use judgement. **Each rollout site constructs its own
`RichTextToolbar` instance** (that's what guarantees independent settings).

For a brand-new module, this still follows the standard "touch 4 places" rule in
[CLAUDE.md](../../CLAUDE.md); the toolbar is just a control you drop next to the text surface.

---

## 5. Gotchas / conventions (from building the Terms reader)

- **Icons:** use plain text glyphs for B / I / S, not Segoe MDL2 glyphs (strikethrough looks like underline).
- **Pickers:** always `Services/FileDialogs.cs` — never WinRT pickers (they fail when elevated).
- **PDF:** PdfSharp uses Windows system fonts automatically; wrap `new XFont(family, …)` in try/catch and
  fall back to `"Microsoft JhengHei"` for CJK. Strikethrough = `XFontStyleEx.Strikeout`. Do PDF work on a
  background thread (`Task.Run`) — it's CPU work.
- **Bilingual:** all visible toolbar labels/tooltips go through `Loc.I.Pick(en, zh)` (Cantonese, not
  Mandarin). See [CLAUDE.md](../../CLAUDE.md).
- **Toolbar layout:** keep it to ≤ 2 rows of buttons so nothing scrolls off; the Terms reader shows the
  working layout.
- **No static state:** if you find yourself adding a `static` field for "current font", stop — that's the
  exact cross-contamination bug this handoff exists to prevent.

---

## 6. Acceptance checklist

- [ ] `Controls/RichTextToolbar.cs` exists; `RenderPdf`/`WrapLine`/`FontChoices` moved to
      `Services/TextExportService.cs`; `TermsService` refactored to use it (behaviour unchanged).
- [ ] Read-only mode (TextBlock) and editable mode (RichEditBox selection formatting) both work.
- [ ] Two surfaces open at once have **fully independent** font/size/colour/style/theme.
- [ ] Theme changes are scoped to the surface's container, never `App.SetTheme`.
- [ ] Copy / Save TXT / Save PDF all work; PDF honours the live formatting incl. CJK.
- [ ] `dotnet build WinForge.sln -c Debug -p:Platform=x64` → 0 errors.
- [ ] Rolled out to at least the priority surfaces in §4, each with its own instance.

---

*Created session 54. Reference implementation: `Services/TermsService.cs`. Prior context:
[52-omega-session-handoff.md](52-omega-session-handoff.md), [53-ui-modernization-stop-handoff.md](53-ui-modernization-stop-handoff.md).*
