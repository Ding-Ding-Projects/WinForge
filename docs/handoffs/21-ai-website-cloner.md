# Handoff: AI Website Cloner

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/JCodesMore/ai-website-cloner-template |
| **License** | MIT (permissive open source) — design/workflow only; our implementation is original C# |
| **Proposed module** | Website Cloner · AI & Web group · Tag `module.webcloner` |
| **Effort** | L — fetching + asset rewriting is moderate; agent-driven reconstruction adds prompt/orchestration work |

## What the user asked for
A WinForge tool that takes a URL, fetches the live page, and uses an AI agent (opencode/claude via `AiAgentService`) to reconstruct/clone the site into local HTML/CSS/JS in a chosen folder, following the design of the `ai-website-cloner-template` repo.

## Recommended approach
**Hybrid (native fetch + AI agent).** The upstream template is a Next.js/Puppeteer/Claude-Code pipeline — we do NOT wrap Next.js. Per the global strategy we clone its *core idea* natively: native C# fetch + asset capture + file writing, then delegate the "intelligence" (turning raw HTML into clean, componentized, editable HTML/CSS/JS) to our existing `AiAgentService` (opencode/claude). Be honest about scope: a pixel-perfect SPA clone needs a real headless browser. v1 should target static/server-rendered pages and a single-page snapshot; JS-heavy SPAs are best-effort and clearly marked as such.

## Features to implement (v1 → later)
- v1: URL input box; destination folder via `FileDialogs.PickFolder`; fetch page via `HttpClient` (fallback to WebView2 `CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML")` for JS-rendered DOM); download referenced assets (img/css/js/fonts), rewrite URLs to local relative paths; write `index.html` + `/assets`; optional AI pass that feeds the saved HTML/CSS to `AiAgentService` to clean up, deduplicate styles, and emit tidy `index.html`/`styles.css`/`script.js`; progress log + "Open folder" button.
- later: multi-page crawl (same-origin, depth limit); WebView2-based screenshot for visual before/after compare; design-token extraction (computed colors/fonts) into a summary; per-section AI rebuild; export as a self-contained zip.

## Integration plan (WinForge specifics)
- New files: `Services/WebsiteClonerService.cs` (fetch, asset download, URL rewriting, save), `Pages/WebClonerModule.xaml(.cs)` (URL field, folder picker, mode toggle Native/AI, progress log). Reuse existing `AiAgentService` for the reconstruction prompt — no new agent service.
- Nav wiring: add `NavigationViewItem Tag="module.webcloner"` in `MainWindow.xaml` (AI & Web group); add `ModuleRegistry` entry (en "Website Cloner" / zh 「網站複製器」) for master search; wire Tag in `MainWindow.xaml.cs` `MapType` + `NavView_SelectionChanged`; add `ApplyStartPage` case for `--page webcloner`.
- Engine/install: winget id `n/a`. WebView2 runtime ships with WinUI 3; if AI mode is used, reuse the opencode/claude install path already exposed by `AiAgentService` (surface via `EngineBars.AutoInstallButton` only if that CLI is missing).
- Key APIs/CLIs to call: `HttpClient.GetAsync`, `WebView2.CoreWebView2`, `HtmlAgilityPack` (or manual regex/`AngleSharp` for link rewriting), `FileDialogs.PickFolder`, `AiAgentService.RunAsync(prompt)`.

## Dependencies & risks
- SPA/JS-rendered sites won't clone faithfully via plain `HttpClient` — need WebView2 DOM dump; some sites still defeat this.
- Legal/ToS: cloning copyrighted sites — add an in-app disclaimer (en/zh) that it's for personal/learning use.
- Asset URL rewriting is error-prone (CSS `url()`, srcset, CDN/CORS, data URIs); cap download count/size to avoid runaways.
- AI reconstruction quality and token limits vary; keep native-only mode fully functional without the AI step.
- Consider adding `AngleSharp` or `HtmlAgilityPack` NuGet for robust HTML parsing.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under AI & Web; entering a URL + folder produces a browsable local `index.html` with assets; AI mode (when agent available) writes cleaned HTML/CSS/JS; all user-facing strings bilingual (English + 粵語); folder picking uses `FileDialogs` (no WinRT pickers).
