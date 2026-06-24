# Handoff: Ollama (local LLM runner)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/ollama/ollama (Go; local REST API on `http://localhost:11434`) |
| **License** | MIT (open source). WinForge talks to it only over its local REST API and installs it via winget — no source vendoring, so the license imposes no constraints on WinForge. |
| **Proposed module** | Ollama · "AI Agents" (or "AI / Tools") nav group · Tag `module.ollama` |
| **Effort** | M — no engine to build; effort is a native model-manager UI plus a streaming chat client over a stable, well-documented HTTP API. |

## What the user asked for
Bring Ollama — a local LLM runner — into WinForge as a native module: list installed models, pull/delete models, show running models, run a streaming chat with adjustable parameters, and (later) offer Ollama as a local provider for WinForge's existing AI tools (Resume Writer / AI Agents).

## Recommended approach
**Hybrid (native C# WinUI front-end over the local REST API).** Ollama itself is a Go binary with embedded llama.cpp / GPU runners — not realistically reimplementable in C#, so per the global strategy we install the binary via winget and build a rich native front-end around its local HTTP API. Everything the user sees (model list, pull progress, chat, parameter panel, running-models view) is native WinUI calling `http://localhost:11434` with `HttpClient`. No WebView, no CLI scraping. v1 scope: detect/start Ollama, manage models, and a working streaming chat. Embedding it as a provider for other modules is "later."

## Features to implement (v1 → later)
- v1: Detect Ollama (probe `GET /api/version`); if missing, `EngineBars.AutoInstallButton("Ollama.Ollama", …)`; if installed but not serving, offer to start `ollama serve` via `ShellRunner`.
- v1: List installed models (`GET /api/tags`) with size/quantization/modified date; show running models (`GET /api/ps`).
- v1: Pull a model by name (`POST /api/pull`, stream → progress bar) and delete a model (`DELETE /api/delete`), each with a bilingual confirm/InfoBar.
- v1: Chat pane — pick a model, stream tokens via `POST /api/chat` (`"stream": true`, parse newline-delimited JSON); Stop button cancels the request; keep conversation history in memory.
- v1: Parameter panel — temperature, top_p, top_k, num_ctx, seed, system prompt — sent in the `options` object.
- later: Save/load chat sessions; expose Ollama as a local provider in `AiAgentService` so Resume Writer / AI tools can target it; `/api/embeddings`; image/multimodal input; custom Modelfile creation; remote-host URL setting.

## Integration plan (WinForge specifics)
- New files: `Services/OllamaService.cs` (typed `HttpClient` wrapper: `GetVersionAsync`, `ListModelsAsync`, `ListRunningAsync`, `PullModelAsync` + delete, `ChatStreamAsync` yielding chunks via `IAsyncEnumerable` with `CancellationToken`; never throw on network errors — return results/empty), `Pages/OllamaModule.xaml(.cs)` (model list + pull/delete + running view + chat + params). Optionally `Catalog/OllamaOperations.cs` for a few `Tweak.Action`/`Cmd` ops (start/stop serve, open models folder).
- Nav wiring: add `NavigationViewItem Tag="module.ollama"` in `MainWindow.xaml` under the AI group; add a `ModuleRegistry.All` entry (keywords: `ollama llm local ai model chat gguf llama mistral 本地 模型 聊天 人工智能`); add `"module.ollama" => typeof(OllamaModule)` to `MapType`, a `case "module.ollama":` in `NavView_SelectionChanged`, and optionally `--page ollama` in `ApplyStartPage`.
- Engine/install: winget id `Ollama.Ollama` via `EngineBars.AutoInstallButton(..., recheck, rescan)` — recheck should re-probe `/api/version`.
- Key APIs/CLIs to call: REST on `http://localhost:11434` — `/api/version`, `/api/tags`, `/api/ps`, `/api/pull`, `/api/delete`, `/api/chat`. CLI only for lifecycle: `ollama serve` via `ShellRunner`. Use `Services/FileDialogs.cs` for any file picking (never WinRT pickers).

## Dependencies & risks
- Ollama may be installed but not running, or on a non-default port — make the base URL a setting and surface a clear "not reachable" bilingual InfoBar with a Start action.
- Pulls are large (GBs) and long — stream progress, allow cancel, and handle partial/failed downloads gracefully.
- Streaming chat returns newline-delimited JSON, not SSE — parse line-by-line and stop on `"done": true`.
- First inference on a cold model is slow and GPU/RAM dependent — show a spinner; don't block the UI thread.
- Deleting a model is destructive — require confirmation.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; missing Ollama is offered for winget install; installed models list and running models show; pull/delete work with progress; chat streams tokens from a real model and Stop cancels; parameters affect output; every user-facing string is bilingual (English + 粵語); no WinRT pickers; no unhandled exceptions when Ollama is absent or offline.
