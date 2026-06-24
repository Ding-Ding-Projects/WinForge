using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WinForgeLauncher;

/// <summary>
/// WinForge 可靠啟動器 · Reliable WinForge launcher.
/// 啟動 WinForge.exe；若佢喺開機頭幾秒以 0xC000027B（WinUI 框架偶發閃退）退出，就重試（最多 5 次）。
/// Launches WinForge.exe; if it exits with 0xC000027B (the intermittent WinUI startup fail-fast) within
/// the first few seconds, it relaunches — up to 5 attempts. ~10% per-launch failure → ~0.001% after 5.
/// </summary>
internal static class Program
{
    private const int StowedException = unchecked((int)0xC000027B);
    private const int MaxAttempts = 5;
    private const int EarlyWindowMs = 8000;

    [STAThread]
    private static int Main(string[] args)
    {
        string dir = AppContext.BaseDirectory;
        string app = Path.Combine(dir, "WinForge.exe");
        if (!File.Exists(app))
        {
            // 退而求其次：喺 PATH／同層搵唔到就照試 · fall back to a bare name if not found next to us.
            app = "WinForge.exe";
        }

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Process child;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = app,
                    UseShellExecute = false,
                    WorkingDirectory = dir,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);
                child = Process.Start(psi)!;
            }
            catch
            {
                return 1;
            }

            if (child.WaitForExit(EarlyWindowMs))
            {
                // 喺早期視窗內就退出 · exited within the early window.
                if (child.ExitCode == StowedException && attempt < MaxAttempts)
                {
                    Thread.Sleep(400); // 畀框架完全釋放資源 · let the framework fully release before retrying.
                    continue;
                }
                return child.ExitCode; // 正常退出，或最後一次嘗試 · normal exit, or final attempt.
            }

            // 捱過早期視窗 → 已成功啟動；等佢正常結束（收入系統匣都繼續行）。
            // Survived the early window → launched fine; wait for it to finish (tray-resident is fine).
            child.WaitForExit();
            return child.ExitCode;
        }
        return StowedException;
    }
}
