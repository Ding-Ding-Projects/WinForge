using System;
using System.Collections.Generic;

namespace WinForge.Models;

/// <summary>
/// 工作區入面一個應用程式視窗嘅紀錄 · One captured application window inside a workspace.
/// 純資料、可變、可無參數建構（俾 System.Text.Json 用）。
/// Plain mutable data, parameterless-constructible for System.Text.Json round-tripping.
/// </summary>
public sealed class WorkspaceApp
{
    /// <summary>程式可執行檔嘅絕對路徑 · Absolute path to the application executable.</summary>
    public string ExePath { get; set; } = string.Empty;

    /// <summary>命令列引數（盡力擷取，未必有）· Command-line arguments (best-effort; may be empty).</summary>
    public string Args { get; set; } = string.Empty;

    /// <summary>擷取時嘅視窗標題 · Window title at capture time.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>程序名（唔含 .exe）· Process name (without .exe), for best-effort matching.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>視窗左上角 X · Window left (X) in virtual-desktop pixels.</summary>
    public int X { get; set; }

    /// <summary>視窗左上角 Y · Window top (Y) in virtual-desktop pixels.</summary>
    public int Y { get; set; }

    /// <summary>視窗闊度 · Window width in pixels.</summary>
    public int W { get; set; }

    /// <summary>視窗高度 · Window height in pixels.</summary>
    public int H { get; set; }

    /// <summary>顯示器索引（0 起）· Monitor index (0-based), best-effort.</summary>
    public int Monitor { get; set; }

    /// <summary>視窗狀態 · Window state: "normal", "maximized" or "minimized".</summary>
    public string State { get; set; } = "normal";

    /// <summary>啟動時係咪包括呢個 app · Whether to include this app when launching.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>顯示用名（預設係檔名）· Display name (defaults to the exe leaf name).</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// 一個已命名嘅工作區（一組應用程式視窗）· A named workspace — a captured set of app windows.
/// 對應 PowerToys Workspaces 嘅 Project 概念。
/// Mirrors the "Project" concept from PowerToys Workspaces.
/// </summary>
public sealed class Workspace
{
    /// <summary>唯一識別碼 · Stable unique id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>工作區名 · Workspace display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>建立時間（UTC ticks）· Created timestamp (UTC ticks).</summary>
    public long CreatedTicks { get; set; } = DateTime.UtcNow.Ticks;

    /// <summary>最後啟動時間（UTC ticks，0 = 未啟動過）· Last-launched timestamp (UTC ticks; 0 = never).</summary>
    public long LastLaunchedTicks { get; set; }

    /// <summary>工作區入面嘅應用程式 · The applications captured in this workspace.</summary>
    public List<WorkspaceApp> Apps { get; set; } = new();
}
