using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 全域例外處理 · Global last-resort exception handling. Logs every unhandled exception to
/// %LOCALAPPDATA%\WinForge\crash.log and (where possible) keeps the app alive instead of letting it die.
/// 一個模組出錯唔應該拖冧成個 app。A single module's error should never take the whole app down.
/// </summary>
public static class CrashLogger
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "crash.log");

    /// <summary>掛上所有例外來源 · Hook every unhandled-exception source. Call once at startup.</summary>
    public static void Install(Microsoft.UI.Xaml.Application app)
    {
        app.UnhandledException += (_, e) =>
        {
            Log("XAML", e.Exception);
            // Keep running — a page/handler fault should not terminate the whole suite.
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("Task", e.Exception);
            e.SetObserved();
        };
    }

    private static bool _traceStarted;
    private static readonly string TracePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "startup-trace.log");

    /// <summary>啟動追蹤標記 · Append a startup-step marker (truncates on the first call of the process).</summary>
    public static void Mark(string step)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TracePath)!);
                if (!_traceStarted) { File.WriteAllText(TracePath, "=== launch ===\n"); _traceStarted = true; }
                File.AppendAllText(TracePath, $"{DateTime.Now:HH:mm:ss.fff} {step}\n");
            }
        }
        catch { }
    }

    /// <summary>記錄一個例外 · Append one exception to the crash log (best effort).</summary>
    public static void Log(string source, Exception? ex)
    {
        if (ex is null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath,
                    $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] ====\n{ex}\n\n");
            }
        }
        catch { /* never throw from the logger */ }
    }

    /// <summary>包住一個動作，唔畀佢拋出 · Run an action, swallowing + logging any exception.</summary>
    public static void Guard(string source, Action body)
    {
        try { body(); }
        catch (Exception ex) { Log(source, ex); }
    }
}
