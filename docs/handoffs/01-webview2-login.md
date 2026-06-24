# Handoff: WebView2 In-App Login

| | |
|---|---|
| **Status** | Not started |
| **Source** | NuGet: `Microsoft.Web.WebView2` (control). Runtime: Microsoft Edge WebView2 Runtime (Evergreen), preinstalled on Windows 11 |
| **License** | WebView2 SDK NuGet — proprietary Microsoft license (free to redistribute/use). Runtime — Microsoft EULA, redistributable. Not open source, but freely usable in shipping apps |
| **Proposed module** | "In-App Login / 內置登入" · group **Tools / Network** (under a shared "Accounts & Auth / 帳戶與認證" item) · Tag `module.weblogin` |
| **Effort** | M — the control is a single NuGet add and the redirect/cookie capture is straightforward; the work is making it a clean, reusable, bilingual dialog that other modules can call |

## What the user asked for
Add a WebView2-backed in-app browser/login so OAuth and web sign-ins (GitHub, Cloudflare, AI providers, Bitwarden, etc.) happen inside WinForge instead of bouncing to an external browser. Build a reusable `LoginWebView` control/dialog that navigates to a URL, watches for a redirect/callback or a target cookie, and returns the captured token/cookies to the caller. It becomes shared auth plumbing for the Git, Cloudflare, and AI Agents modules.

## Recommended approach
**Native C# (WinUI) — wrap the official control.** WebView2 *is* the native primitive for embedding Edge/Chromium; there is nothing to reimplement. This fits the global strategy: a native WinUI surface around a first-party component, no external redirects. v1 scope: a reusable `LoginDialog` (ContentDialog hosting a `WebView2`) plus a `WebLoginService` that exposes `Task<LoginResult> CaptureAsync(LoginRequest)`. The caller passes a start URL plus completion rules (redirect-URI prefix to match, and/or cookie name(s)/domain to capture); the service drives navigation, detects completion, extracts the query/fragment params and cookies via `CoreWebView2.CookieManager`, and returns them. The standalone `module.weblogin` page is a thin demo/manual-browser shell over the same service so the feature is testable on its own.

## Features to implement (v1 → later)
- v1: `LoginDialog` hosting `WebView2`; navigate to start URL; match a redirect-URI prefix (`NavigationStarting`/`SourceChanged`) to capture an OAuth `code`/`token` from query or fragment; capture named cookies via `CoreWebView2.CookieManager.GetCookiesAsync`; return a `LoginResult { Success, RedirectUri, QueryParams, Cookies, RawUrl }`; Cancel/Close handling; loading + error states; bilingual UI.
- v1: per-profile data isolation — set a unique `UserDataFolder` (under `LocalAppData\WinForge\WebView2\<profile>`) so different services keep separate cookie jars; a "Sign out / clear" that wipes the profile folder.
- later: PKCE helper (generate verifier/challenge, build authorize URL); pluggable provider presets (GitHub, Cloudflare, OpenAI/Anthropic, Bitwarden); persistent session reuse; download interception; custom user-agent; deep-link `--page weblogin?url=...`.

## Integration plan (WinForge specifics)
- New files: `Services/WebLoginService.cs` (request/result types + `CaptureAsync`), `Controls/LoginDialog.xaml(.cs)` (ContentDialog + `WebView2`), `Pages/WebLoginModule.xaml(.cs)` (standalone browser/test page).
- Nav wiring: add `NavigationViewItem Tag="module.weblogin"` in `MainWindow.xaml` under the Tools/Network group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) with EN+ZH name/keywords for master search; wire the Tag in `MainWindow.xaml.cs` `MapType` and `NavView_SelectionChanged`, and add an `ApplyStartPage` case for `--page weblogin` deep-links.
- Engine/install: winget id **n/a**. WebView2 Runtime ships with Win11, so no `EngineBars.AutoInstallButton`. Defensively call `CoreWebView2Environment.GetAvailableBrowserVersionString()`; if null/empty, show an InfoBar with a link to the Evergreen Runtime bootstrapper rather than hard-crashing.
- Key APIs: `WebView2.EnsureCoreWebView2Async(env)`; `CoreWebView2Environment.CreateAsync(userDataFolder: ...)`; `CoreWebView2.NavigationStarting` / `SourceChanged` / `NavigationCompleted`; `CoreWebView2.CookieManager.GetCookiesAsync(uri)`; parse return URL with `Uri` + `WwwFormUrlDecoder`.

## Dependencies & risks
- Must call `EnsureCoreWebView2Async` before touching `CoreWebView2`; all WebView2 calls are UI-thread bound — marshal results back to callers.
- Runtime assumed present on Win11 but verify and degrade gracefully on older/stripped images.
- Tokens/cookies are sensitive: keep them in memory, never log them, and prefer DPAPI if persisted. Match redirect URIs strictly (full prefix) to avoid leaking a code to the wrong host.
- Set distinct `UserDataFolder` per profile so providers don't share a cookie jar; folder is locked while in use — close the WebView before clearing.
- Works under elevation, but file pickers used elsewhere must still go through `Services/FileDialogs.cs` (never WinRT pickers).

## Acceptance criteria
- Builds clean (Debug + Release, x64); `module.weblogin` appears in the left nav and master search; opening it shows an embedded WebView2 that can navigate.
- `WebLoginService.CaptureAsync` returns a populated `LoginResult` for both a redirect-URI flow and a cookie-capture flow; Cancel returns a non-success result without throwing.
- All user-facing strings are bilingual (English + 粵語); graceful InfoBar when the Runtime is missing; no WinRT pickers anywhere in the feature.
