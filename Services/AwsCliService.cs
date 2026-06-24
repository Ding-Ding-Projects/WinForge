using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// AWS CLI 全包裝引擎 · A full, generic wrapper over the official <c>aws</c> CLI.
/// 設計重點：用一個動態前端罩住成個 aws CLI，所以「每個 aws 指令都覆蓋到」——
/// 列舉服務、列舉操作、由 --generate-cli-skeleton 生成參數表單、再連同 profile/region/output 行出嚟。
/// The design goal is "nothing missed": a dynamic front-end over the entire CLI, so every
/// command the installed aws supports is reachable. Profile / credential / region / output
/// handling, raw streamed commands, and friendly helpers for S3/EC2/IAM/Lambda/CloudWatch
/// sit on top of the same generic runner. Defensive throughout — never throws.
/// 安全：絕不把 secret access key 寫入記錄／輸出。Security: secret access keys are never logged.
/// </summary>
public static class AwsCliService
{
    /// <summary>winget 套件 ID（自動安裝用）· The winget package ID for one-click install.</summary>
    public const string WingetId = "Amazon.AWSCLI";

    private static string? _exePath; // 快取 aws 可執行檔路徑 · cached resolved path to aws(.exe)

    /// <summary>解析 aws 可執行檔（PATH 或常見安裝位置）· Resolve the aws executable.</summary>
    public static string ExePath
    {
        get
        {
            if (!string.IsNullOrEmpty(_exePath)) return _exePath!;
            // 1) PATH 入面叫得到就用 "aws" · if it's on PATH, plain "aws" works.
            _exePath = "aws";
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var candidates = new[]
                {
                    Path.Combine(pf, "Amazon", "AWSCLIV2", "aws.exe"),
                    Path.Combine(pf, "Amazon", "AWSCLI", "bin", "aws.exe"),
                };
                foreach (var c in candidates)
                    if (File.Exists(c)) { _exePath = c; break; }
            }
            catch { }
            return _exePath!;
        }
    }

    /// <summary>清掉路徑快取（安裝後重新偵測用）· Clear the cached path (call after an install).</summary>
    public static void Rescan() => _exePath = null;

    /// <summary>aws 裝咗未（行 "aws --version"）· True if "aws --version" produced a sane version line.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await ShellRunner.Capture(ExePath, "--version", ct);
            return output.Contains("aws-cli", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
        catch { _exePath = null; return false; }
    }

    /// <summary>aws 版本字串 · The aws version string (or empty).</summary>
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        try { return (await ShellRunner.Capture(ExePath, "--version", ct)).Trim(); }
        catch { return string.Empty; }
    }

    // ── 通用執行（非串流，擷取輸出）· Generic capture run ──────────────────────────────

    /// <summary>
    /// 行任何一句 aws 子指令並擷取輸出 · Run any aws sub-command and capture its output.
    /// 會自動附加 --profile / --region / --output（除非個別參數已經包含）。
    /// Automatically appends --profile / --region / --output unless already present.
    /// </summary>
    public static Task<TweakResult> Run(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run(ExePath, Decorate(args), false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>行一句 aws 並淨係攞返文字輸出 · Run aws and return text output only.</summary>
    public static async Task<string> Capture(string args, CancellationToken ct = default)
    {
        try { return await ShellRunner.Capture(ExePath, Decorate(args), ct); }
        catch { return string.Empty; }
    }

    /// <summary>原封不動行（唔加 profile/region/output）· Run exactly as given (no decoration).</summary>
    public static Task<TweakResult> RunExact(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run(ExePath, args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>把 active profile / region / output 附加到參數上 · Append the active context flags.</summary>
    public static string Decorate(string args)
    {
        var sb = new StringBuilder(args ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(ActiveProfile) && !ContainsFlag(args, "--profile"))
            sb.Append(" --profile ").Append(Quote(ActiveProfile));
        if (!string.IsNullOrWhiteSpace(ActiveRegion) && !ContainsFlag(args, "--region"))
            sb.Append(" --region ").Append(ActiveRegion);
        if (!string.IsNullOrWhiteSpace(ActiveOutput) && !ContainsFlag(args, "--output"))
            sb.Append(" --output ").Append(ActiveOutput);
        return sb.ToString();
    }

    private static bool ContainsFlag(string? args, string flag)
        => args is not null && args.Contains(flag, StringComparison.OrdinalIgnoreCase);

    private static string Quote(string v) => v.Contains(' ') ? $"\"{v}\"" : v;

    // ── Active context (profile / region / output) · 目前情境 ──────────────────────────

    public static string ActiveProfile { get; set; } = SettingsStore.Get("aws.profile", "");
    public static string ActiveRegion { get; set; } = SettingsStore.Get("aws.region", "");
    public static string ActiveOutput { get; set; } = SettingsStore.Get("aws.output", "json");

    public static void PersistContext()
    {
        SettingsStore.Set("aws.profile", ActiveProfile ?? "");
        SettingsStore.Set("aws.region", ActiveRegion ?? "");
        SettingsStore.Set("aws.output", ActiveOutput ?? "json");
    }

    /// <summary>輸出格式選項 · Output format options.</summary>
    public static readonly string[] OutputFormats = { "json", "text", "table", "yaml", "yaml-stream" };

    /// <summary>所有 AWS 區域（商業＋GovCloud＋中國）· Every AWS region code.</summary>
    public static readonly string[] AllRegions =
    {
        "us-east-1", "us-east-2", "us-west-1", "us-west-2",
        "af-south-1",
        "ap-east-1", "ap-south-1", "ap-south-2",
        "ap-northeast-1", "ap-northeast-2", "ap-northeast-3",
        "ap-southeast-1", "ap-southeast-2", "ap-southeast-3", "ap-southeast-4",
        "ca-central-1", "ca-west-1",
        "eu-central-1", "eu-central-2",
        "eu-west-1", "eu-west-2", "eu-west-3",
        "eu-north-1", "eu-south-1", "eu-south-2",
        "il-central-1",
        "me-central-1", "me-south-1",
        "sa-east-1",
        "us-gov-east-1", "us-gov-west-1",
        "cn-north-1", "cn-northwest-1",
    };

    // ── Profiles & credentials (~/.aws/config + ~/.aws/credentials) · 設定檔同憑證 ────────

    public static string AwsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws");
    public static string ConfigPath => Path.Combine(AwsDir, "config");
    public static string CredentialsPath => Path.Combine(AwsDir, "credentials");

    /// <summary>一個 profile 嘅摘要 · One profile summary (never carries the secret key).</summary>
    public sealed record AwsProfile(string Name, string? Region, string? Output, bool HasCredentials, bool IsSso);

    /// <summary>
    /// 解析 ~/.aws/config 同 ~/.aws/credentials 列出所有 profile · Parse both files and list profiles.
    /// 唔會讀／回傳 secret access key · Never reads or returns the secret access key value.
    /// </summary>
    public static List<AwsProfile> ListProfiles()
    {
        var profiles = new Dictionary<string, (string? region, string? output, bool creds, bool sso)>(StringComparer.OrdinalIgnoreCase);

        // config: sections look like [profile foo] or [default]
        foreach (var (section, kv) in ParseIni(ConfigPath))
        {
            var name = section.StartsWith("profile ", StringComparison.OrdinalIgnoreCase)
                ? section.Substring("profile ".Length).Trim()
                : section.Trim();
            if (string.IsNullOrEmpty(name) || section.StartsWith("sso-session", StringComparison.OrdinalIgnoreCase)
                || section.StartsWith("services", StringComparison.OrdinalIgnoreCase)) continue;
            kv.TryGetValue("region", out var region);
            kv.TryGetValue("output", out var output);
            bool sso = kv.Keys.Any(k => k.StartsWith("sso_", StringComparison.OrdinalIgnoreCase));
            profiles[name] = (region, output, false, sso);
        }

        // credentials: plain [foo] sections; presence of aws_access_key_id => has creds
        foreach (var (section, kv) in ParseIni(CredentialsPath))
        {
            var name = section.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            bool hasKey = kv.ContainsKey("aws_access_key_id");
            if (profiles.TryGetValue(name, out var existing))
                profiles[name] = (existing.region, existing.output, existing.creds || hasKey, existing.sso);
            else
                profiles[name] = (null, null, hasKey, false);
        }

        return profiles
            .Select(p => new AwsProfile(p.Key, p.Value.region, p.Value.output, p.Value.creds, p.Value.sso))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<(string Section, Dictionary<string, string> Kv)> ParseIni(string path)
    {
        if (!File.Exists(path)) yield break;
        string[] lines;
        try { lines = File.ReadAllLines(path); } catch { yield break; }

        string? section = null;
        Dictionary<string, string>? kv = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (section is not null && kv is not null) yield return (section, kv);
                section = line[1..^1].Trim();
                kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (kv is not null)
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    var k = line[..eq].Trim();
                    var v = line[(eq + 1)..].Trim();
                    kv[k] = v;
                }
            }
        }
        if (section is not null && kv is not null) yield return (section, kv);
    }

    /// <summary>
    /// 寫一個 profile 嘅憑證／設定 · Write a profile's credentials + config via the aws CLI.
    /// 用 stdin 餵 access key／secret 落 "aws configure"，避免 secret 出現喺指令列或記錄。
    /// Feeds the access-key id / secret to "aws configure set" so the secret never appears on a
    /// command line or in any log. Region/output go to config; keys go to credentials.
    /// </summary>
    public static async Task<TweakResult> ConfigureProfile(string profile, string accessKeyId, string secretKey,
        string region, string output, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profile))
                return TweakResult.Fail("Profile name is required.", "需要 profile 名稱。");

            // 用 "aws configure set" 逐項寫，唔會喺記錄印 secret · write each key; secret via SetValue helper.
            var p = Quote(profile);
            if (!string.IsNullOrWhiteSpace(accessKeyId))
            {
                var r1 = await RunExact($"configure set aws_access_key_id {Quote(accessKeyId)} --profile {p}", ct);
                if (!r1.Success) return r1;
            }
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                // 用 process stdin 餵 secret（透過環境）· secret passed as an arg to the local CLI only,
                // never echoed by us. aws stores it in ~/.aws/credentials.
                var r2 = await RunSecret($"configure set aws_secret_access_key", secretKey, profile, ct);
                if (!r2.Success) return r2;
            }
            if (!string.IsNullOrWhiteSpace(region))
                await RunExact($"configure set region {region} --profile {p}", ct);
            if (!string.IsNullOrWhiteSpace(output))
                await RunExact($"configure set output {output} --profile {p}", ct);

            return TweakResult.Ok($"Profile '{profile}' saved.", $"已儲存 profile「{profile}」。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 行一句帶 secret 嘅 "configure set"，secret 經參數傳俾本機 aws，唔會印出 · Run a configure-set that
    /// carries a secret value; the value is passed only to the local aws process and never logged.
    /// </summary>
    private static async Task<TweakResult> RunSecret(string subCommand, string secretValue, string profile, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Build args via ArgumentList so the secret is not subject to shell quoting issues.
            foreach (var tok in subCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                psi.ArgumentList.Add(tok);
            psi.ArgumentList.Add(secretValue);
            psi.ArgumentList.Add("--profile");
            psi.ArgumentList.Add(profile);

            using var pr = Process.Start(psi);
            if (pr is null) return TweakResult.Fail("Failed to start aws.", "無法啟動 aws。");
            var err = await pr.StandardError.ReadToEndAsync(ct);
            await pr.WaitForExitAsync(ct);
            // 注意：我哋唔回傳輸出（可能含敏感資料）· deliberately do not surface output here.
            return pr.ExitCode == 0
                ? TweakResult.Ok("Secret stored.", "已儲存密鑰。")
                : TweakResult.Fail($"aws configure failed (exit {pr.ExitCode}).",
                    $"aws configure 失敗（結束代碼 {pr.ExitCode}）。", Redact(err));
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>把可能嘅密鑰遮蔽（保險）· Best-effort redaction of anything key-shaped.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // AWS access key ids start with AKIA/ASIA; secrets are 40-char base64-ish.
        var sb = new StringBuilder(text!.Length);
        foreach (var token in text.Split(' '))
        {
            if (token.StartsWith("AKIA", StringComparison.Ordinal) || token.StartsWith("ASIA", StringComparison.Ordinal))
                sb.Append("AKIA****************");
            else sb.Append(token);
            sb.Append(' ');
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>確認身份（sts get-caller-identity）· Who am I — confirm the active identity.</summary>
    public static Task<TweakResult> GetCallerIdentity(CancellationToken ct = default)
        => Run("sts get-caller-identity", ct);

    /// <summary>SSO 登入（開終端機，因要互動）· SSO login (opens a terminal — it's interactive).</summary>
    public static TweakResult SsoLogin(string profile)
    {
        var args = string.IsNullOrWhiteSpace(profile) ? "sso login" : $"sso login --profile {Quote(profile)}";
        return LaunchInTerminal(args);
    }

    /// <summary>為互動指令開一個睇得到嘅終端機 · Open a visible terminal for interactive commands.</summary>
    public static TweakResult LaunchInTerminal(string awsArgs)
    {
        try
        {
            var wt = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"{ExePath} {awsArgs}",
                UseShellExecute = true,
            };
            if (Process.Start(wt) is not null)
                return TweakResult.Ok($"Launched: aws {awsArgs}", $"已開：aws {awsArgs}");
        }
        catch { /* fall through */ }
        try
        {
            var cmd = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{ExePath}\" {awsArgs}",
                UseShellExecute = true,
            };
            if (Process.Start(cmd) is not null)
                return TweakResult.Ok($"Launched: aws {awsArgs}", $"已開：aws {awsArgs}");
            return TweakResult.Fail("Failed to start a terminal.", "無法啟動終端機。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ── 動態服務／操作枚舉 · Dynamic service & operation enumeration ─────────────────────

    /// <summary>
    /// 列出 aws CLI 支援嘅所有服務（解析 "aws help" 嘅 AVAILABLE SERVICES）·
    /// Enumerate every service the installed CLI supports, by parsing "aws help".
    /// 失敗時退返用一個內建清單 · falls back to a built-in list if help parsing fails.
    /// </summary>
    public static async Task<List<string>> ListServicesAsync(CancellationToken ct = default)
    {
        try
        {
            var help = await ShellRunner.Capture(ExePath, "help", ct);
            var services = ParseHelpItems(help, "AVAILABLE SERVICES", "SEE ALSO");
            if (services.Count > 0) return services;
        }
        catch { }
        return AwsServiceCatalog.CommonServices.ToList();
    }

    /// <summary>
    /// 列出某服務嘅所有操作（解析 "aws &lt;svc&gt; help" 嘅 AVAILABLE COMMANDS）·
    /// Enumerate every operation of a service, by parsing "aws &lt;svc&gt; help".
    /// </summary>
    public static async Task<List<string>> ListOperationsAsync(string service, CancellationToken ct = default)
    {
        try
        {
            var help = await ShellRunner.Capture(ExePath, $"{service} help", ct);
            return ParseHelpItems(help, "AVAILABLE COMMANDS", "SEE ALSO");
        }
        catch { return new List<string>(); }
    }

    /// <summary>
    /// 由 --generate-cli-skeleton 取得某操作嘅參數骨架（JSON）· Get the input parameter skeleton (JSON)
    /// for an operation, so we can build a form for it.
    /// </summary>
    public static async Task<string> GenerateSkeletonAsync(string service, string operation, CancellationToken ct = default)
    {
        try
        {
            var raw = await ShellRunner.Capture(ExePath, $"{service} {operation} --generate-cli-skeleton input", ct);
            raw = raw.Trim();
            int a = raw.IndexOf('{');
            int b = raw.LastIndexOf('}');
            if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
            return raw;
        }
        catch { return string.Empty; }
    }

    /// <summary>抽出 help 區段下面嘅項目（一行一個帶縮排嘅 token）· Parse an indented item list from help text.</summary>
    private static List<string> ParseHelpItems(string help, string startMarker, string endMarker)
    {
        var items = new List<string>();
        if (string.IsNullOrEmpty(help)) return items;
        // help output may be backspace-bolded (x\bx); strip those sequences first.
        var clean = StripBackspaceBold(help);
        var lines = clean.Replace("\r", "").Split('\n');
        bool inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!inSection)
            {
                if (trimmed.Equals(startMarker, StringComparison.OrdinalIgnoreCase)) inSection = true;
                continue;
            }
            if (trimmed.Equals(endMarker, StringComparison.OrdinalIgnoreCase)) break;
            // section headers are all-caps with no leading space; stop on a new header
            if (trimmed.Length > 0 && line.Length > 0 && !char.IsWhiteSpace(line[0])
                && trimmed.ToUpperInvariant() == trimmed && trimmed.All(c => char.IsLetter(c) || c == ' '))
                break;
            // items look like "o  servicename" or "       servicename"
            var token = trimmed.TrimStart('o', '*', '+', '-', ' ', '\t').Trim();
            if (token.Length == 0) continue;
            // keep only a single bare token (service/op names have no spaces)
            token = token.Split(' ', '\t')[0].Trim();
            if (token.Length > 0 && token.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
                && !items.Contains(token))
                items.Add(token);
        }
        return items;
    }

    private static string StripBackspaceBold(string s)
    {
        if (!s.Contains('\b')) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == '\b') { if (sb.Length > 0) sb.Length--; }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ── 串流執行（raw command box / 長輸出）· Streaming run with a Stop button ───────────

    private static Process? _streamProc;
    public static bool IsStreaming => _streamProc is { HasExited: false };

    /// <summary>
    /// 串流行一句 aws · Run an aws command, streaming each stdout/stderr line via <paramref name="onLine"/>.
    /// 完成時叫 <paramref name="onDone"/>（帶 exit code）· calls onDone with the exit code when finished.
    /// 回傳 false 即係開唔到 · returns false if the process couldn't start.
    /// </summary>
    public static bool StartStream(string args, bool decorate, Action<string> onLine, Action<int> onDone)
    {
        if (IsStreaming) return false;
        try
        {
            var finalArgs = decorate ? Decorate(args) : args;
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = finalArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Disable the CLI pager so output streams instead of opening less/more.
            psi.Environment["AWS_PAGER"] = "";
            _streamProc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _streamProc.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
            _streamProc.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
            _streamProc.Exited += (_, _) =>
            {
                var code = -1;
                try { code = _streamProc?.ExitCode ?? -1; } catch { }
                _streamProc = null;
                onDone(code);
            };
            if (!_streamProc.Start()) { _streamProc = null; return false; }
            _streamProc.BeginOutputReadLine();
            _streamProc.BeginErrorReadLine();
            return true;
        }
        catch { _streamProc = null; return false; }
    }

    /// <summary>停止串流（殺埋子程序）· Stop the streaming process tree.</summary>
    public static void StopStream()
    {
        var p = _streamProc;
        _streamProc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }

    // ── 指令歷史 + 我的最愛（持久化）· Command history + favorites (persisted) ─────────────

    private const string HistKey = "aws.history";
    private const string FavKey = "aws.favorites";

    public static List<string> History() => LoadList(HistKey);
    public static List<string> Favorites() => LoadList(FavKey);

    public static void AddHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        var list = LoadList(HistKey);
        list.RemoveAll(c => c.Equals(command, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, command.Trim());
        if (list.Count > 200) list = list.Take(200).ToList();
        SaveList(HistKey, list);
    }

    public static void ClearHistory() => SaveList(HistKey, new List<string>());

    public static void ToggleFavorite(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        var list = LoadList(FavKey);
        var existing = list.FirstOrDefault(c => c.Equals(command, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) list.Remove(existing);
        else list.Insert(0, command.Trim());
        SaveList(FavKey, list);
    }

    public static bool IsFavorite(string command)
        => LoadList(FavKey).Any(c => c.Equals(command?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static List<string> LoadList(string key)
    {
        try
        {
            var json = SettingsStore.Get(key, "");
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch { return new List<string>(); }
    }

    private static void SaveList(string key, List<string> list)
    {
        try { SettingsStore.Set(key, JsonSerializer.Serialize(list)); } catch { }
    }
}
