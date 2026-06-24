using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;

namespace WinForge.Services;

/// <summary>
/// 內嵌終端機啟動器 · A tiny reusable façade for opening the powerful ConPTY-backed embedded terminal
/// from anywhere in the app. 任何模組想喺相關工作目錄／shell 開一個真正嘅終端機（支援全螢幕 TUI），
/// 只要叫 <see cref="OpenEmbeddedAsync"/> 就會彈一個對話框，入面用 <see cref="EmbeddedTerminalPanel"/>
/// 包住 <see cref="ConPtySession"/> + <see cref="Controls.TerminalView"/>。Any module can call
/// <see cref="OpenEmbeddedAsync"/> to pop a dialog hosting a real terminal at a given working directory
/// or command — the same engine the Windows Terminal and SSH modules use. Also exposes helpers to
/// resolve a default shell and to build an <c>ssh user@host -p port [-i key]</c> command line from a
/// saved profile. 全部介面文字雙語。 All strings bilingual.
/// </summary>
public static class TerminalLauncher
{
    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 喺對話框開一個內嵌 ConPTY 終端機 · Open a modal dialog hosting an embedded ConPTY terminal that
    /// runs <paramref name="commandLine"/> (default: the user's preferred shell) at
    /// <paramref name="workingDir"/>. The terminal auto-connects and is torn down when the dialog closes.
    /// </summary>
    /// <param name="xamlRoot">承載對話框嘅 XamlRoot（通常 <c>page.XamlRoot</c>）· The XamlRoot to host the dialog.</param>
    /// <param name="title">對話框標題（已雙語化）· An already-localised dialog title.</param>
    /// <param name="commandLine">要開嘅命令列 · The command line to spawn; null/empty = default shell.</param>
    /// <param name="workingDir">起始工作目錄 · The initial working directory; null = user profile.</param>
    public static async Task OpenEmbeddedAsync(XamlRoot? xamlRoot, string title,
        string? commandLine = null, string? workingDir = null)
    {
        if (xamlRoot is null) return;

        var panel = new EmbeddedTerminalPanel
        {
            MinWidth = 760,
            MinHeight = 440,
            Width = 820,
            Height = 480,
        };
        panel.Configure(string.IsNullOrWhiteSpace(commandLine) ? DefaultShell() : commandLine!,
            workingDir, autoStart: true);

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = panel,
            CloseButtonText = P("Close", "關閉"),
            DefaultButton = ContentDialogButton.Close,
        };
        // Ensure the PTY and its child process are always cleaned up when the dialog goes away.
        dlg.Closed += (_, _) => panel.Stop();
        try { await dlg.ShowAsync(); }
        finally { panel.Stop(); }
    }

    /// <summary>
    /// 預設 shell · The preferred local shell command line: PowerShell 7 (pwsh) if present, else
    /// Windows PowerShell. 用作所有「喺呢度開終端機」入口嘅預設。Used as the default for every
    /// "open terminal here" affordance.
    /// </summary>
    public static string DefaultShell()
    {
        var pwsh = ResolveOnPath("pwsh.exe");
        if (pwsh is not null) return pwsh;
        var ps = ResolveOnPath("powershell.exe");
        return ps ?? "powershell.exe";
    }

    /// <summary>cmd.exe 嘅完整路徑 · The full path to cmd.exe (System32 fallback).</summary>
    public static string Cmd()
        => ResolveOnPath("cmd.exe")
           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

    /// <summary>
    /// 由 SSH 設定檔砌出互動式 ssh 命令列 · Build an interactive <c>ssh</c> command line from a saved
    /// profile, reusing its host / user / port / identity. Returns null if ssh.exe can't be resolved
    /// or the profile is incomplete. 用 OpenSSH 客戶端（Win11 內建）跑，所以全螢幕 TUI（vim、htop、
    /// tmux）都用得。Runs via the built-in OpenSSH client so full-screen TUIs work over the real PTY.
    /// </summary>
    public static string? BuildSshCommandLine(SshProfile p)
    {
        if (p is null || string.IsNullOrWhiteSpace(p.Host) || string.IsNullOrWhiteSpace(p.User))
            return null;

        var ssh = ResolveSshExe();
        if (ssh is null) return null;

        var sb = new System.Text.StringBuilder();
        sb.Append('"').Append(ssh).Append('"');
        sb.Append(' ').Append(p.User).Append('@').Append(p.Host);
        int port = p.Port <= 0 ? 22 : p.Port;
        if (port != 22) sb.Append(" -p ").Append(port);
        if (p.Auth == SshAuthKind.PrivateKey && !string.IsNullOrWhiteSpace(p.KeyPath) && File.Exists(p.KeyPath))
            sb.Append(" -i \"").Append(p.KeyPath).Append('"');
        // -tt forces a PTY even when stdin isn't a real tty, so server-side full-screen apps render.
        sb.Append(" -tt");
        return sb.ToString();
    }

    /// <summary>ssh.exe 嘅完整路徑（或 null）· Resolve ssh.exe (PATH, then System32\OpenSSH).</summary>
    public static string? ResolveSshExe()
    {
        var onPath = ResolveOnPath("ssh.exe");
        if (onPath is not null) return onPath;
        var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        return File.Exists(sys) ? sys : null;
    }

    /// <summary>喺 PATH 同 System32 搵一個可執行檔 · Resolve an executable on PATH, with a System32 fallback.</summary>
    public static string? ResolveOnPath(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var c = Path.Combine(dir.Trim(), exe);
                if (File.Exists(c)) return c;
            }
            catch { }
        }
        try
        {
            var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
            if (File.Exists(sys)) return sys;
        }
        catch { }
        return null;
    }
}
