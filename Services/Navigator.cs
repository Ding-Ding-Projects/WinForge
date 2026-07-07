using System;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 由 MainWindow 設定嘅導覽鈎子 · Navigation hooks wired up by MainWindow,
/// so pages (e.g. the dashboard tiles) can drive the NavigationView.
/// </summary>
public static class Navigator
{
    public static Action<AppCategory>? GoToCategory { get; set; }
    public static Action? GoToSettings { get; set; }
    public static Action<string>? GoToPage { get; set; }

    /// <summary>導覽去一個套件模組（用 nav tag，例如 "module.git"）· Navigate to a suite module by nav tag.</summary>
    public static Action<string>? GoToModule { get; set; }
}
