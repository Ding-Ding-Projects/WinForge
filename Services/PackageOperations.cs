using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 套件操作引擎 · Package operations engine (UniGetUI-style).
/// 由 <see cref="InstallOptions"/> 砌出每個操作（安裝／更新／解除安裝）嘅 CLI，
/// 提供即時預覽，再經 <see cref="ShellRunner"/> 連同前置／後置鈎一齊執行。
/// Builds the exact CLI for each operation from <see cref="InstallOptions"/>, exposes a live
/// preview, and runs it (with pre/post hooks and kill-before-op) through <see cref="ShellRunner"/>.
/// 永遠唔擲例外 · Never throws — everything is wrapped and returned as a TweakResult.
/// </summary>
public static class PackageOperations
{
    /// <summary>操作種類 · Which operation we are building / running.</summary>
    public enum Op
    {
        Install,
        Update,
        Uninstall,
    }

    /// <summary>引號包住 id（去掉內嵌引號）· Quote an id safely (strip embedded quotes / backticks).</summary>
    private static string Q(string id) => "\"" + (id ?? "").Replace("\"", "").Replace("`", "") + "\"";

    /// <summary>路徑用引號包住 · Quote a filesystem path.</summary>
    private static string QP(string p) => "\"" + (p ?? "").Replace("\"", "") + "\"";

    /// <summary>取得某操作對應嘅自訂參數 · Pick the custom args for this operation.</summary>
    private static string CustomArgsFor(InstallOptions o, Op op) => op switch
    {
        Op.Install => o.CustomArgsInstall ?? "",
        Op.Update => o.CustomArgsUpdate ?? "",
        Op.Uninstall => o.CustomArgsUninstall ?? "",
        _ => "",
    };

    /// <summary>
    /// 砌出將會執行嘅完整 CLI 字串（畀即時預覽用）· Build the exact CLI string that will run,
    /// for the live command preview. Maps options per-manager (see notes inline).
    /// 對於行 PowerShell 嘅引擎（psgallery / pwsh7），預覽顯示等效嘅 PowerShell cmdlet。
    /// For PowerShell-backed engines this returns the equivalent cmdlet line.
    /// </summary>
    public static string BuildCommandPreview(string managerKey, string id, Op op, InstallOptions o)
        => BuildCommandPreview(managerKey, id, "", op, o);

    /// <summary>
    /// Build the exact operation command while preserving a selected package source. The source is
    /// resolved solely by <see cref="PackageSourcePolicy"/>; callers never get to append arbitrary
    /// source text to a command line.
    /// </summary>
    public static string BuildCommandPreview(string managerKey, string id, string? source, Op op, InstallOptions o)
    {
        try
        {
            o ??= new InstallOptions();
            var key = (managerKey ?? "").ToLowerInvariant();
            if (!PackageSourcePolicy.TryResolve(key, id, source, op, out var resolvedSource, out var sourceFailure))
                return "# " + PackageSourcePolicy.MessageFor(sourceFailure).Code;
            var packageId = resolvedSource.PackageId;
            var extra = (CustomArgsFor(o, op) ?? "").Trim();
            string cmd = key switch
            {
                "winget" => Winget(packageId, op, o),
                "scoop" => Scoop(packageId, op, o),
                "choco" => Choco(packageId, op, o),
                "pip" => Pip(packageId, op, o),
                "npm" => Npm(packageId, op, o),
                "bun" => Bun(packageId, op, o),
                "dotnet" => Dotnet(packageId, op, o),
                "cargo" => Cargo(packageId, op, o),
                "vcpkg" => Vcpkg(packageId, op, o),
                "psgallery" => PsGallery(packageId, op, o),
                "pwsh7" => Pwsh7(packageId, op, o),
                _ => $"{(string.IsNullOrEmpty(key) ? "?" : key)} {op.ToString().ToLowerInvariant()} {Q(packageId)}",
            };
            // CommandSuffix can only come from PackageSourcePolicy: a fixed manager flag paired
            // with a validated source token or a trusted built-in registry endpoint.
            if (!string.IsNullOrWhiteSpace(resolvedSource.CommandSuffix)) cmd += " " + resolvedSource.CommandSuffix;
            if (extra.Length > 0) cmd += " " + extra;
            cmd = ApplyManagerSettings(key, cmd);
            return cmd.Trim();
        }
        catch (Exception ex) { return $"# {ex.Message}"; }
    }

    // ===================== per-manager builders =====================

    private static string Winget(string id, Op op, InstallOptions o)
    {
        var sb = new StringBuilder();
        switch (op)
        {
            case Op.Install:
                sb.Append($"winget install --id {Q(id)} -e --accept-source-agreements --accept-package-agreements");
                AppendWingetCommon(sb, o, install: true);
                break;
            case Op.Update:
                sb.Append($"winget upgrade --id {Q(id)} -e --accept-source-agreements --accept-package-agreements");
                AppendWingetCommon(sb, o, install: true);
                break;
            case Op.Uninstall:
                sb.Append($"winget uninstall --id {Q(id)} -e");
                if (!string.IsNullOrWhiteSpace(o.Scope)) sb.Append($" --scope {o.Scope}");
                sb.Append(o.Interactive ? " --interactive" : " --silent --disable-interactivity");
                break;
        }
        return sb.ToString();
    }

    private static void AppendWingetCommon(StringBuilder sb, InstallOptions o, bool install)
    {
        if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
        if (!string.IsNullOrWhiteSpace(o.Scope)) sb.Append($" --scope {o.Scope}");
        if (!string.IsNullOrWhiteSpace(o.Architecture)) sb.Append($" --architecture {o.Architecture}");
        if (install && !string.IsNullOrWhiteSpace(o.CustomInstallLocation)) sb.Append($" --location {QP(o.CustomInstallLocation)}");
        if (o.SkipHashCheck) sb.Append(" --ignore-security-hash");          // winget hash skip
        sb.Append(o.Interactive ? " --interactive" : " --silent --disable-interactivity");
        // PreRelease: winget has no equivalent — skip.
    }

    private static string Scoop(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"scoop install {id}");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($"@{o.Version}");
                if (o.SkipHashCheck) sb.Append(" --skip");                  // scoop skip-hash
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder($"scoop update {id}");
                if (o.SkipHashCheck) sb.Append(" --skip");
                return sb.ToString();
            }
            case Op.Uninstall:
            {
                var sb = new StringBuilder($"scoop uninstall {id}");
                if (o.RemoveDataOnUninstall) sb.Append(" --purge");         // scoop remove persisted data
                return sb.ToString();
            }
        }
        return $"scoop {id}";
    }

    private static string Choco(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"choco install {id} -y");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (o.PreRelease) sb.Append(" --pre");
                if (o.SkipHashCheck) sb.Append(" --ignore-checksums");      // choco skip-hash
                if (o.Interactive) sb.Append(" --notsilent");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder($"choco upgrade {id} -y");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (o.PreRelease) sb.Append(" --pre");
                if (o.SkipHashCheck) sb.Append(" --ignore-checksums");
                if (o.Interactive) sb.Append(" --notsilent");
                return sb.ToString();
            }
            case Op.Uninstall:
            {
                var sb = new StringBuilder($"choco uninstall {id} -y");
                if (o.RemoveDataOnUninstall) sb.Append(" --remove-dependencies");
                return sb.ToString();
            }
        }
        return $"choco {id} -y";
    }

    private static string Pip(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder("pip install");
                if (o.PreRelease) sb.Append(" --pre");
                sb.Append($" {id}");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($"=={o.Version}");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder("pip install --upgrade");
                if (o.PreRelease) sb.Append(" --pre");
                sb.Append($" {id}");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($"=={o.Version}");
                return sb.ToString();
            }
            case Op.Uninstall:
                return $"pip uninstall -y {id}";
        }
        return $"pip {id}";
    }

    private static string Npm(string id, Op op, InstallOptions o)
    {
        // npm: PreRelease maps to the '@next' dist-tag, but only when no explicit version is pinned.
        string Tag()
        {
            if (!string.IsNullOrWhiteSpace(o.Version)) return $"@{o.Version}";
            if (o.PreRelease) return "@next";
            return "";
        }
        switch (op)
        {
            case Op.Install: return $"npm install -g {id}{Tag()}";
            case Op.Update: return $"npm install -g {id}{(string.IsNullOrWhiteSpace(o.Version) ? (o.PreRelease ? "@next" : "@latest") : "@" + o.Version)}";
            case Op.Uninstall: return $"npm uninstall -g {id}";
        }
        return $"npm {id}";
    }

    private static string Bun(string id, Op op, InstallOptions o)
    {
        string Tag()
        {
            if (!string.IsNullOrWhiteSpace(o.Version)) return $"@{o.Version}";
            if (o.PreRelease) return "@next";
            return "";
        }
        switch (op)
        {
            case Op.Install: return $"bun add -g {id}{Tag()}";
            case Op.Update: return $"bun add -g {id}{(string.IsNullOrWhiteSpace(o.Version) ? (o.PreRelease ? "@next" : "@latest") : "@" + o.Version)}";
            case Op.Uninstall: return $"bun remove -g {id}";
        }
        return $"bun {id}";
    }

    private static string Dotnet(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"dotnet tool install {id}");
                AppendDotnetTarget(sb, o);
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (o.PreRelease) sb.Append(" --prerelease");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder($"dotnet tool update {id}");
                AppendDotnetTarget(sb, o);
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (o.PreRelease) sb.Append(" --prerelease");
                return sb.ToString();
            }
            case Op.Uninstall:
            {
                var sb = new StringBuilder($"dotnet tool uninstall {id}");
                AppendDotnetTarget(sb, o);
                return sb.ToString();
            }
        }
        return $"dotnet tool {id}";
    }

    /// <summary>
    /// .NET tool scope flags are mutually exclusive: a custom tool path must never be combined with
    /// --global/-g. Keep the choice in one helper so install, update and uninstall stay symmetrical.
    /// </summary>
    private static void AppendDotnetTarget(StringBuilder sb, InstallOptions o)
    {
        if (!string.IsNullOrWhiteSpace(o.CustomInstallLocation))
            sb.Append($" --tool-path {QP(o.CustomInstallLocation)}");
        else
            sb.Append(" --global");
    }

    private static string Cargo(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"cargo install {id}");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (!string.IsNullOrWhiteSpace(o.CustomInstallLocation)) sb.Append($" --root {QP(o.CustomInstallLocation)}");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder($"cargo install {id} --force");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" --version {o.Version}");
                if (!string.IsNullOrWhiteSpace(o.CustomInstallLocation)) sb.Append($" --root {QP(o.CustomInstallLocation)}");
                return sb.ToString();
            }
            case Op.Uninstall:
            {
                var sb = new StringBuilder($"cargo uninstall {id}");
                if (!string.IsNullOrWhiteSpace(o.CustomInstallLocation)) sb.Append($" --root {QP(o.CustomInstallLocation)}");
                return sb.ToString();
            }
        }
        return $"cargo {id}";
    }

    private static string Vcpkg(string id, Op op, InstallOptions o) => op switch
    {
        Op.Install => $"vcpkg install {id}",
        Op.Update => $"vcpkg upgrade {id} --no-dry-run",
        Op.Uninstall => $"vcpkg remove {id}",
        _ => $"vcpkg {id}",
    };

    private static string PsGallery(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"Install-Module -Name {Q(id)} -Force -Scope CurrentUser");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" -RequiredVersion {o.Version}");
                if (o.PreRelease) sb.Append(" -AllowPrerelease");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder($"Update-Module -Name {Q(id)}");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" -RequiredVersion {o.Version}");
                if (o.PreRelease) sb.Append(" -AllowPrerelease");
                return sb.ToString();
            }
            case Op.Uninstall:
                return $"Uninstall-Module -Name {Q(id)}";
        }
        return $"Get-Module {Q(id)}";
    }

    private static string Pwsh7(string id, Op op, InstallOptions o)
    {
        switch (op)
        {
            case Op.Install:
            {
                var sb = new StringBuilder($"Install-PSResource -Name {Q(id)} -TrustRepository -Scope CurrentUser");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" -Version {o.Version}");
                if (o.PreRelease) sb.Append(" -Prerelease");
                return sb.ToString();
            }
            case Op.Update:
            {
                var sb = new StringBuilder(
                    $"$ErrorActionPreference = 'Stop'; Update-PSResource -Name {Q(id)} -TrustRepository -Force -ErrorAction Stop");
                if (!string.IsNullOrWhiteSpace(o.Version)) sb.Append($" -Version {Q(o.Version)}");
                if (o.PreRelease) sb.Append(" -Prerelease");
                if (o.UninstallPreviousOnUpdate)
                {
                    // Query the real post-update state and retain its newest version. This avoids a placeholder
                    // range and never removes anything unless Update-PSResource completed successfully.
                    sb.Append($"; $installed = @(Get-InstalledPSResource -Name {Q(id)} -ErrorAction SilentlyContinue | Sort-Object Version -Descending)");
                    sb.Append("; if ($installed.Count -gt 1) { $keepVersion = $installed[0].Version.ToString()");
                    sb.Append($"; $installed | Select-Object -Skip 1 | ForEach-Object {{ $oldVersion = $_.Version.ToString(); if ($oldVersion -ne $keepVersion) {{ Uninstall-PSResource -Name {Q(id)} -Version $oldVersion -Confirm:$false -ErrorAction Stop }} }} }}");
                }
                return sb.ToString();
            }
            case Op.Uninstall:
                return $"Uninstall-PSResource -Name {Q(id)}";
        }
        return $"Get-PSResource {Q(id)}";
    }

    /// <summary>邊啲引擎係經 PowerShell 執行 · Which engines run as PowerShell cmdlets rather than cmd.exe.</summary>
    private static bool IsPowershellEngine(string key)
        => key is "scoop" or "psgallery" or "pwsh7";

    /// <summary>Apply global per-manager arguments, proxy flags, and vcpkg settings.</summary>
    private static string ApplyManagerSettings(string key, string cmd)
    {
        try
        {
            if (key == "vcpkg")
            {
                var triplet = PackageManagerSettings.VcpkgTriplet.Trim();
                if (triplet.Length > 0 && !cmd.Contains("--triplet", StringComparison.OrdinalIgnoreCase))
                    cmd += $" --triplet {triplet.Replace("\"", "").Replace("`", "")}";
            }

            var managerArgs = PackageManagerSettings.GetManagerExecutableArgs(key).Trim();
            if (managerArgs.Length > 0) cmd += " " + managerArgs;

            var proxyArgs = PackageManagerSettings.ProxyArgsFor(key).Trim();
            if (proxyArgs.Length > 0) cmd += " " + proxyArgs;
        }
        catch { /* settings are best-effort; preserve the base command */ }
        return cmd;
    }

    // ===================== runner =====================

    /// <summary>
    /// 執行一個操作（連同前置／後置鈎、操作前關閉程序）。
    /// Run one operation including kill-before-op, pre-hook (with optional abort), the main command,
    /// and a best-effort post-hook. Elevation follows <see cref="InstallOptions.RunAsAdministrator"/>.
    /// 永遠唔擲例外 · Never throws — wrapped and returned as TweakResult.
    /// </summary>
    public static Task<TweakResult> RunAsync(string managerKey, string id, Op op, InstallOptions o, CancellationToken ct)
        => RunAsync(managerKey, id, "", op, o, null, ct);

    /// <summary>Run an operation with a selected source retained from its package row/bundle.</summary>
    public static Task<TweakResult> RunAsync(string managerKey, string id, string? source, Op op,
        InstallOptions o, CancellationToken ct)
        => RunAsync(managerKey, id, source, op, o, null, ct);

    /// <summary>
    /// 同上，但逐行串流輸出畀進度／狀態 UI · Same as <see cref="RunAsync(string,string,Op,InstallOptions,CancellationToken)"/>
    /// but reports each output line via <paramref name="onLine"/> so a progress / status UI can show live output.
    /// 永遠唔擲例外 · Never throws — wrapped and returned as a TweakResult carrying the real exit code + output.
    /// </summary>
    public static Task<TweakResult> RunAsync(string managerKey, string id, Op op, InstallOptions o,
        IProgress<string>? onLine, CancellationToken ct)
        => RunAsync(managerKey, id, "", op, o, onLine, ct);

    /// <summary>
    /// Same as the streaming overload above, with an explicit package source validated before any
    /// hook or shell process can run.
    /// </summary>
    public static async Task<TweakResult> RunAsync(string managerKey, string id, string? source, Op op, InstallOptions o,
        IProgress<string>? onLine, CancellationToken ct)
    {
        try
        {
            o ??= new InstallOptions();
            var key = (managerKey ?? "").ToLowerInvariant();
            if (!PackageSourcePolicy.TryResolve(key, id, source, op, out _, out var sourceFailure))
            {
                var message = PackageSourcePolicy.MessageFor(sourceFailure);
                return TweakResult.Fail(message.En, message.Zh) with { Code = message.Code };
            }
            bool elevated = o.RunAsAdministrator;

            // (a) 操作前關閉程序 · Kill processes before the operation.
            if (o.KillBeforeOperation is { Count: > 0 })
            {
                var sb = new StringBuilder();
                foreach (var raw in o.KillBeforeOperation)
                {
                    var name = (raw ?? "").Trim();
                    if (name.Length == 0) continue;
                    name = name.Replace("'", "''");
                    // 去掉 .exe 因為 Stop-Process -Name 唔要副檔名 · Stop-Process -Name wants the bare name.
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                    if (o.ForceKill) sb.Append($"Stop-Process -Name '{name}' -Force -ErrorAction SilentlyContinue; ");
                    else sb.Append($"Stop-Process -Name '{name}' -ErrorAction SilentlyContinue; ");
                }
                var killScript = sb.ToString().Trim();
                if (killScript.Length > 0)
                {
                    try { await ShellRunner.RunPowershell(killScript, elevated, ct); } catch { /* best effort */ }
                }
            }

            // (b) 前置鈎 · Pre-hook; abort if requested and it fails.
            var (preCmd, abortOnPreFail) = PreHookFor(o, op);
            if (!string.IsNullOrWhiteSpace(preCmd))
            {
                TweakResult preRes;
                try { preRes = await ShellRunner.RunPowershell(preCmd, elevated, ct); }
                catch (Exception ex) { preRes = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
                if (abortOnPreFail && !preRes.Success)
                {
                    return TweakResult.Fail(
                        "Aborted: pre-operation command failed.",
                        "已中止：前置指令失敗。",
                        preRes.Output);
                }
            }

            // (c) 主要操作 · The main command (with absolute-path resolution for the CLI token).
            var cmd = ResolveCliToken(key, BuildCommandPreview(key, id, source, op, o));
            TweakResult main;
            try
            {
                main = IsPowershellEngine(key)
                    ? await RunPowerShellEngineAsync(key, cmd, onLine, elevated, ct)
                    : await ShellRunner.RunCmdStreaming(cmd, onLine, elevated, ct);
            }
            catch (Exception ex) { main = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }

            // (d) 後置鈎（盡力而為，唔會中止）· Post-hook (best-effort, never aborts).
            var postCmd = PostHookFor(o, op);
            if (!string.IsNullOrWhiteSpace(postCmd))
            {
                try { await ShellRunner.RunPowershell(postCmd, elevated, ct); } catch { /* best effort */ }
            }

            // (e) 回傳主要操作結果 · Return the main op's result.
            return main;
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 將命令開頭嘅裸 CLI token（winget/choco/scoop）換成解析到嘅絕對路徑，避免提權後 PATH 唔同而「唔識」。
    /// Replace the leading bare CLI token (winget/choco/scoop) with its resolved absolute path so a
    /// different (elevated / refreshed) PATH can't cause a silent "not recognized" (9009) failure.
    /// PowerShell 引擎唔改（scoop 係 shim，PowerShell 會自己搵）· Leaves PowerShell engines untouched.
    /// </summary>
    private static string ResolveCliToken(string key, string cmd)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cmd)) return cmd;
            string? tool = key switch
            {
                "winget" => "winget",
                "scoop" => "scoop",
                "choco" => "choco",
                "pip" => "pip",
                "npm" => "npm",
                "bun" => "bun",
                "dotnet" => "dotnet",
                "cargo" => "cargo",
                "vcpkg" => "vcpkg",
                _ => null,
            };
            if (tool is null) return cmd;

            var trimmed = cmd.TrimStart();
            if (!trimmed.StartsWith(tool + " ", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals(tool, StringComparison.OrdinalIgnoreCase))
                return cmd;

            var resolved = PackageManagerSettings.GetManagerExecutablePath(key).Trim();
            if (resolved.Length == 0 && key == "vcpkg")
            {
                var root = PackageManagerSettings.VcpkgRoot.Trim();
                var candidate = root.Length == 0 ? "" : Path.Combine(root, "vcpkg.exe");
                if (File.Exists(candidate)) resolved = candidate;
            }
            if (resolved.Length == 0) resolved = ShellRunner.ResolveExe(tool);
            if (string.IsNullOrEmpty(resolved) || string.Equals(resolved, tool, StringComparison.OrdinalIgnoreCase))
                return cmd;                    // nothing better than the bare token — leave as-is

            var rest = trimmed.Length > tool.Length ? trimmed[tool.Length..] : "";
            var quoted = resolved.Contains(' ') ? $"\"{resolved}\"" : resolved;
            if (key == "scoop" && quoted.StartsWith('"')) quoted = "& " + quoted;
            return quoted + rest;
        }
        catch { return cmd; }
    }

    /// <summary>Run package-manager scripts in the correct PowerShell host. Windows PowerShell owns
    /// PowerShellGet, while PSResourceGet requires PowerShell 7. A configured executable overrides it.</summary>
    private static Task<TweakResult> RunPowerShellEngineAsync(string key, string script,
        IProgress<string>? onLine, bool elevated, CancellationToken ct)
    {
        if (key == "scoop")
            return ShellRunner.RunPowershellStreaming(script, onLine, elevated, ct);

        var configured = PackageManagerSettings.GetManagerExecutablePath(key).Trim();
        var host = configured.Length > 0
            ? configured
            : key == "pwsh7" ? ShellRunner.ResolveExe("pwsh") : "powershell.exe";
        if (string.IsNullOrWhiteSpace(host)) host = key == "pwsh7" ? "pwsh.exe" : "powershell.exe";

        var bytes = Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        return ShellRunner.RunStreaming(host,
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            onLine, elevated, workingDirectory: null, ct);
    }

    private static (string cmd, bool abort) PreHookFor(InstallOptions o, Op op) => op switch
    {
        Op.Install => (o.PreInstallCommand ?? "", o.AbortOnPreInstallFail),
        Op.Update => (o.PreUpdateCommand ?? "", o.AbortOnPreUpdateFail),
        Op.Uninstall => (o.PreUninstallCommand ?? "", o.AbortOnPreUninstallFail),
        _ => ("", false),
    };

    private static string PostHookFor(InstallOptions o, Op op) => op switch
    {
        Op.Install => o.PostInstallCommand ?? "",
        Op.Update => o.PostUpdateCommand ?? "",
        Op.Uninstall => o.PostUninstallCommand ?? "",
        _ => "",
    };
}
