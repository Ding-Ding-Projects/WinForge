# Handoff 57 — In-app login browser is too small (resizable / responsive login dialog)

| | |
|---|---|
| **Status** | Not started |
| **Type** | Fix / UX |
| **Files** | `Controls/LoginDialog.xaml(.cs)` (the reusable WebView2 login popup), `Services/WebLoginService.cs`, `Pages/WebLoginModule.xaml(.cs)` |
| **Effort** | S — sizing + responsiveness; no auth-logic changes. |

## The problem

The in-app login browser is **way too small** — real login pages (Google, GitHub, Microsoft, Bitwarden,
etc.) are cramped and sometimes unusable. Root cause:

- `Controls/LoginDialog.xaml` hosts the `WebView2` in a **fixed `Grid Width="860" Height="640"`**, but the
  control is shown as a **`ContentDialog`**, whose **default `ContentDialogMaxWidth` (~548) and
  `ContentDialogMaxHeight`** clamp it. So the 860×640 is silently cut down and the embedded browser ends up
  tiny. (We hit the exact same clamp on the Terms reader and fixed it by overriding
  `ContentDialogMaxWidth` — see `Services/TermsService.cs`.)

## The fix

Make the login browser **large and responsive**, not a fixed size that gets clamped:

1. **Lift the ContentDialog clamps.** Set `dlg.Resources["ContentDialogMaxWidth"]` and
   `["ContentDialogMaxHeight"]` to large values (or `double.PositiveInfinity`) so the dialog can actually
   reach the size we want. (Reference: the Terms reader sets `ContentDialogMaxWidth = 1200`.)
2. **Size to the window, not a magic constant.** Replace the fixed `860×640` with a size derived from the
   host window / `XamlRoot.Size` — e.g. ~`min(1200, 90% of window width)` × ~`85% of window height`, with
   sensible **minimums** (≈ 900×680) so it's never tiny. Recompute on `XamlRoot` size changes so it stays
   responsive.
3. **Let the WebView2 fill** its row (`HorizontalAlignment/VerticalAlignment = Stretch`, no fixed child
   size) so all extra space goes to the browser.
4. **Consider a non-dialog option for heavy logins.** A `ContentDialog` is inherently bounded by the app
   window. If even a maximized dialog feels cramped, host the login `WebView2` in a **dedicated resizable
   window** (the app already uses code-hosted windows, e.g. `Pages/ReactorHtmlWindow.cs`) the user can
   maximize/resize freely. Recommended: keep the dialog for quick logins but offer an **"Open larger" /
   pop-out** button that re-hosts the same `CoreWebView2` flow in a real window.

Also check the **in-page** `WebView2` inside `Pages/WebLoginModule.xaml` (the browser embedded in the
module itself) — give its row a sensible `MinHeight` so it isn't squeezed by the toolbar rows above it.

## Constraints / notes

- **Don't touch the auth flow** — only sizing/hosting. The login completion detection, cookie/session
  handling and provider presets in `WebLoginService` stay as-is.
- Same WebView2 environment/runtime checks as today (`CoreWebView2Environment`); keep the "WebView2 Runtime
  not found" engine banner.
- Apply [54](54-rich-text-toolbar-rollout.md) §4b error handling: guard handlers, wrap `ShowAsync`, fail
  open — a sizing/timing error must never crash or freeze login.
- Per-instance state if you add a pop-out window: each login window owns its own WebView, independent of
  others (handoff 54/55 isolation rule).

## Acceptance criteria

- [ ] The in-app login browser opens **large** (fills most of the window) and is **never** clamped to the
      old tiny size; honours sensible minimums.
- [ ] It **resizes responsively** with the host window (or is a freely resizable/maximizable pop-out).
- [ ] The embedded `WebView2` in `WebLoginModule` has a usable minimum height.
- [ ] Auth flow unchanged; WebView2 runtime banner still works.
- [ ] Bilingual strings; robust per §4b; `dotnet build … -p:Platform=x64` → 0 errors.

---

*Created session 57. Reference: the ContentDialog max-width clamp + fix in `Services/TermsService.cs`;
resizable code-hosted window pattern in `Pages/ReactorHtmlWindow.cs`.*
