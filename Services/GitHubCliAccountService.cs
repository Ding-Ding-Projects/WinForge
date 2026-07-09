using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

public sealed record GitHubCliAccount(
    string Login,
    string Host,
    bool Active,
    string State,
    string Scopes,
    string TokenSource,
    string GitProtocol,
    string Error);

public sealed record GitHubCliStatus(
    bool Installed,
    string Version,
    bool EnvironmentOverride,
    IReadOnlyList<GitHubCliAccount> Accounts,
    string Error);

/// <summary>
/// Safe manager for GitHub CLI's native multi-account authentication model.
/// It never invokes token/show-token commands and never logs gh output. Auth
/// mutations are serialized across WinForge processes with a named semaphore.
/// </summary>
public static partial class GitHubCliAccountService
{
    private const string Host = "github.com";
    private static readonly Semaphore AuthSemaphore = new(1, 1, "Local\\WinForge.GitHubCliAuth");

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$")]
    private static partial Regex LoginPattern();

    [GeneratedRegex(@"(?i)\b(?:gh[a-z]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})")]
    private static partial Regex TokenPattern();

    public static async Task<GitHubCliStatus> GetStatusAsync(CancellationToken ct = default)
    {
        string? gh = FindExecutable();
        if (AdminHelper.IsElevated)
            return new GitHubCliStatus(gh is not null, "", HasEnvironmentOverride(), [],
                "Restart WinForge normally before running GitHub CLI.");

        if (gh is null)
            return new GitHubCliStatus(false, "", HasEnvironmentOverride(), [], "GitHub CLI is not installed.");

        if (!await WaitForAuthSemaphoreAsync(ct))
            return new GitHubCliStatus(true, "", HasEnvironmentOverride(), [], "Another GitHub CLI account operation is still running.");
        try
        {
            var versionRun = await RunAsync(gh, ["--version"], ct);
            string version = FirstLine(versionRun.Output);
            var statusRun = await RunAsync(gh, ["auth", "status", "--hostname", Host, "--json", "hosts"], ct);
            if (!statusRun.Success)
                return new GitHubCliStatus(true, version, HasEnvironmentOverride(), [], SafeError(statusRun));

            var accounts = ParseAccounts(statusRun.Output);
            return new GitHubCliStatus(true, version, HasEnvironmentOverride(), accounts, "");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new GitHubCliStatus(true, "", HasEnvironmentOverride(), [], SafeText(ex.Message));
        }
        finally
        {
            try { AuthSemaphore.Release(); } catch { }
        }
    }

    public static async Task<TweakResult> SwitchAsync(string login, CancellationToken ct = default)
    {
        var validation = Validate(login);
        if (validation is not null) return validation;
        if (HasEnvironmentOverride())
            return TweakResult.Fail(
                "GH_TOKEN or GITHUB_TOKEN overrides stored accounts. Clear that environment variable before switching.",
                "GH_TOKEN 或 GITHUB_TOKEN 會蓋過已儲存帳戶；請先清除環境變數再切換。");
        return await MutateAsync(
            ["auth", "switch", "--hostname", Host, "--user", login],
            $"GitHub CLI switched to {login}.", $"GitHub CLI 已切換到 {login}。", ct);
    }

    public static async Task<TweakResult> LogoutAsync(string login, CancellationToken ct = default)
    {
        var validation = Validate(login);
        if (validation is not null) return validation;
        if (HasEnvironmentOverride())
            return TweakResult.Fail(
                "GH_TOKEN or GITHUB_TOKEN overrides stored accounts. Clear that environment variable before logging out.",
                "GH_TOKEN 或 GITHUB_TOKEN 會蓋過已儲存帳戶；請先清除環境變數再登出。");
        return await MutateAsync(
            ["auth", "logout", "--hostname", Host, "--user", login],
            $"Removed the local GitHub CLI login for {login}. The OAuth grant was not revoked on GitHub.",
            $"已移除 {login} 嘅本機 GitHub CLI 登入；GitHub 上面嘅 OAuth 授權未被撤銷。", ct);
    }

    public static TweakResult OpenLoginTerminal()
    {
        if (AdminHelper.IsElevated)
            return TweakResult.Fail(
                "Restart WinForge normally before opening GitHub CLI login.",
                "請用一般權限重開 WinForge，再開 GitHub CLI 登入。");
        string? gh = FindExecutable();
        if (gh is null) return TweakResult.Fail("GitHub CLI is not installed.", "GitHub CLI 仲未安裝。");
        if (HasEnvironmentOverride())
            return TweakResult.Fail(
                "GH_TOKEN or GITHUB_TOKEN overrides stored accounts. Clear that environment variable before logging in.",
                "GH_TOKEN 或 GITHUB_TOKEN 會蓋過已儲存帳戶；請先清除環境變數再登入。");

        string wtArgs = $"new-tab --title \"GitHub CLI login\" \"{gh.Replace("\"", "")}\" auth login --hostname {Host} --web --git-protocol https --clipboard --skip-ssh-key";
        if (UserProcessLauncher.TryStart("wt.exe", wtArgs, null, out _))
            return TweakResult.Ok(
                "Opened an interactive GitHub CLI web login terminal.",
                "已開啟 GitHub CLI 網頁登入終端機。");

        string command = $"& '{gh.Replace("'", "''")}' auth login --hostname {Host} --web --git-protocol https --clipboard --skip-ssh-key";
        string psArgs = $"-NoLogo -NoProfile -NoExit -Command \"{command.Replace("\"", "`\"")}\"";
        return UserProcessLauncher.TryStart("powershell.exe", psArgs, null, out string error)
            ? TweakResult.Ok("Opened an interactive GitHub CLI web login terminal.", "已開啟 GitHub CLI 網頁登入終端機。")
            : TweakResult.Fail($"Could not open the login terminal: {error}", $"開唔到登入終端機：{error}");
    }

    public static string? FindExecutable()
    {
        try
        {
            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GitHub CLI", "gh.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GitHub CLI", "gh.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "gh.exe"),
            };
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try { candidates.Add(Path.Combine(directory.Trim().Trim('"'), "gh.exe")); } catch { }
            }
            return candidates.FirstOrDefault(File.Exists);
        }
        catch { return null; }
    }

    private static async Task<TweakResult> MutateAsync(
        IReadOnlyList<string> args, string okEn, string okZh, CancellationToken ct)
    {
        if (AdminHelper.IsElevated)
            return TweakResult.Fail("Restart WinForge normally before managing GitHub CLI.", "請用一般權限重開 WinForge，再管理 GitHub CLI。");
        string? gh = FindExecutable();
        if (gh is null) return TweakResult.Fail("GitHub CLI is not installed.", "GitHub CLI 仲未安裝。");
        if (!await WaitForAuthSemaphoreAsync(ct))
            return TweakResult.Fail("Another GitHub CLI account operation is still running.", "另一個 GitHub CLI 帳戶操作仲運行緊。");
        try
        {
            var run = await RunAsync(gh, args, ct);
            return run.Success
                ? TweakResult.Ok(okEn, okZh)
                : TweakResult.Fail($"GitHub CLI operation failed: {SafeError(run)}", $"GitHub CLI 操作失敗：{SafeError(run)}");
        }
        finally
        {
            try { AuthSemaphore.Release(); } catch { }
        }
    }

    private static TweakResult? Validate(string login)
    {
        if (string.IsNullOrWhiteSpace(login) || !LoginPattern().IsMatch(login))
            return TweakResult.Fail("Choose a valid GitHub login first.", "請先揀一個有效嘅 GitHub 登入名稱。");
        return null;
    }

    private static IReadOnlyList<GitHubCliAccount> ParseAccounts(string json)
    {
        var accounts = new List<GitHubCliAccount>();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("hosts", out var hosts) || hosts.ValueKind != JsonValueKind.Object)
            return accounts;

        foreach (var host in hosts.EnumerateObject())
        {
            if (host.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in host.Value.EnumerateArray())
            {
                accounts.Add(new GitHubCliAccount(
                    ReadString(item, "login"),
                    ReadString(item, "host", host.Name),
                    ReadBool(item, "active"),
                    ReadString(item, "state"),
                    ReadString(item, "scopes"),
                    ReadString(item, "tokenSource"),
                    ReadString(item, "gitProtocol"),
                    ReadString(item, "error")));
            }
        }
        return accounts.OrderByDescending(a => a.Active).ThenBy(a => a.Login, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ReadString(JsonElement item, string name, string fallback = "") =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;

    private static bool ReadBool(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;

    private static bool HasEnvironmentOverride() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GH_TOKEN"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));

    private static Task<bool> WaitForAuthSemaphoreAsync(CancellationToken ct) => Task.Run(() =>
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 10.0);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (AuthSemaphore.WaitOne(TimeSpan.FromMilliseconds(100))) return true;
        }
        ct.ThrowIfCancellationRequested();
        return false;
    });

    private static string FirstLine(string value) =>
        value.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";

    private static string SafeError(ProcessResult result)
    {
        string error = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        error = SafeText(error);
        return string.IsNullOrWhiteSpace(error) ? $"exit code {result.ExitCode}" : error;
    }

    private static string SafeText(string value)
    {
        string line = FirstLine(value);
        line = TokenPattern().Replace(line, "[credential redacted]");
        return line[..Math.Min(line.Length, 500)];
    }

    private static async Task<ProcessResult> RunAsync(
        string executable, IReadOnlyList<string> args, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) return new ProcessResult(false, 1, "", "Could not start GitHub CLI.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string output = (await stdout).Trim();
            string error = (await stderr).Trim();
            return new ProcessResult(process.ExitCode == 0, process.ExitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            if (ct.IsCancellationRequested) throw;
            return new ProcessResult(false, 1, "", "GitHub CLI timed out.");
        }
    }

    private sealed record ProcessResult(bool Success, int ExitCode, string Output, string Error);
}
