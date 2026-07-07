# WinForge Reactor Status Client SDK · WinForge 反應堆狀態用戶端 SDK

A **drop-in, dependency-free** C# client for the WinForge reactor's public local status API. Copy
**one file** — [`ReactorStatusClient.cs`](./ReactorStatusClient.cs) — into your own project and read the
simulated reactor's power/status in real time, or make your app **depend on the reactor** (only run
while it is generating).

只需複製 **一個檔案** — [`ReactorStatusClient.cs`](./ReactorStatusClient.cs) — 入你嘅 C# 專案，
即可即時讀取模擬反應堆嘅功率／狀態，或者令你嘅 app **依賴反應堆**（只喺發電時運行）。

---

## What it talks to · 連接乜嘢

The WinForge app publishes the **same** versioned snapshot over two local transports:

| Transport · 傳輸 | Name · 名稱 | Use · 用途 |
| --- | --- | --- |
| Memory-mapped file · 記憶體映射檔 | `Local\WinForge.Reactor.Status` (mutex `Local\WinForge.Reactor.Status.Mutex`) | Zero-latency fast read · 零延遲快速讀取 |
| Named pipe · 具名管道 | `\\.\pipe\WinForge.Reactor.Status` | `GET` one snapshot / `SUBSCRIBE` stream · 取一個／訂閱串流 |

No NuGet packages. Uses only `System.IO.Pipes`, `System.IO.MemoryMappedFiles`, `System.Text.Json`.
無需 NuGet，只用 BCL。Recommended target: **net6.0 or later** (Windows).

### Snapshot schema (schemaVersion 1) · 快照結構

```jsonc
{
  "schemaVersion": 1,
  "sequence": 12345,            // monotonically increasing; bumps every publish · 單調遞增
  "timestampUtc": "2026-06-25T...Z",
  "mode": "Run",               // Shutdown | Startup | Run | Tripped | Meltdown | Offline
  "powerPercent": 100.0,
  "thermalMW": 3411.0,
  "electricMW": 1150.0,
  "isGenerating": true,        // synchronized & delivering power to the grid · 已併網供電
  "isScrammed": false,
  "isMeltdown": false,
  "primaryPressureMPa": 15.5,
  "coolantAvgC": 305.0,
  "reactorPeriodS": 1000000000.0,
  "activeAlarms": ["HighPower"]
}
```

When the reactor page is closed the API serves an **offline** snapshot (`mode: "Offline"`,
`isGenerating: false`). 反應堆頁面關閉時，API 會回傳「離線」快照。

---

## Quick start · 快速開始

```csharp
using WinForge.Reactor.Sdk;

var reactor = new ReactorStatusClient();

// 1) Fast path: read the latest snapshot straight from shared memory (no connection).
//    快路徑：直接由共享記憶體讀取最新快照（無需連線）。
if (reactor.TryReadShared(out ReactorStatus s))
    Console.WriteLine($"{s.Mode}  {s.PowerPercent:F1}%  {s.ElectricMW:F0} MWe  gen={s.IsGenerating}");

// 2) Request a single fresh snapshot over the named pipe (GET).
//    透過具名管道請求一個新快照（GET）。
ReactorStatus? one = await reactor.RequestAsync(timeoutMs: 2000);

// 3) Stream snapshots every ~500 ms (SUBSCRIBE) until cancelled.
//    每約 500 毫秒串流一個快照（SUBSCRIBE），直至取消。
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await foreach (ReactorStatus snap in reactor.SubscribeAsync(cts.Token))
    Console.WriteLine($"seq {snap.Sequence}: {snap.PowerPercent:F1}%");
```

---

## Full example: gate an app on the reactor · 完整範例：依賴反應堆

A console app that **refuses to run unless the reactor is generating**, then keeps working only while
it stays generating. 一個 **只有反應堆發電先肯運行** 嘅 console app，並只喺持續發電期間繼續工作。

```csharp
// Program.cs — copy ReactorStatusClient.cs next to this file.
using WinForge.Reactor.Sdk;

var reactor = new ReactorStatusClient();

// Refuse to run unless the reactor is generating right now.
// 反應堆未發電就拒絕運行。
if (!reactor.IsReactorGenerating())
{
    Console.WriteLine("Reactor is not generating — waiting for grid power…");
    Console.WriteLine("反應堆未發電 — 等待電網供電…");

    using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    try
    {
        // Block until the reactor starts generating (the "depend on the reactor" helper).
        // 等到反應堆開始發電。
        await reactor.WaitForGeneratingAsync(startupCts.Token, pollMs: 1000);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Gave up waiting. Exiting. · 等唔到，結束。");
        return;
    }
}

Console.WriteLine("✅ Reactor is generating — starting work. · 反應堆發電中 — 開始工作。");

// Keep working only while the reactor keeps generating; stop the moment it stops.
// 只喺反應堆持續發電期間工作；一停就退出。
using var runCts = new CancellationTokenSource();
await foreach (ReactorStatus s in reactor.SubscribeAsync(runCts.Token))
{
    if (!s.IsGenerating)
    {
        Console.WriteLine($"⚠ Reactor stopped generating (mode={s.Mode}). Shutting down. · 反應堆停止發電，結束。");
        break;
    }
    Console.WriteLine($"working… {s.ElectricMW:F0} MWe on the grid · 供電中");
    await Task.Delay(1000);
}
```

`csproj` for the sample · 範例專案檔：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <!-- Just drop ReactorStatusClient.cs into this folder — no package references needed. -->
</Project>
```

---

## API surface · API 介面

| Member · 成員 | Description · 說明 |
| --- | --- |
| `bool TryReadShared(out ReactorStatus)` | MMF fast path; `false` if the reactor never started · 記憶體映射快路徑 |
| `Task<ReactorStatus?> RequestAsync(int timeoutMs, CancellationToken)` | Named-pipe `GET`; `null` if offline · 具名管道 GET |
| `IAsyncEnumerable<ReactorStatus> SubscribeAsync(CancellationToken)` | Named-pipe `SUBSCRIBE` stream (~500 ms) · 訂閱串流 |
| `bool IsReactorGenerating()` | One-shot generating check (MMF→pipe fallback) · 一次性發電檢查 |
| `Task WaitForGeneratingAsync(CancellationToken, int pollMs)` | Await until generating · 等到發電 |

All methods are robust: they never throw on the reactor being offline — they return `false` / `null`
/ yield nothing. 所有方法都健壯：反應堆離線時唔會擲例外。
