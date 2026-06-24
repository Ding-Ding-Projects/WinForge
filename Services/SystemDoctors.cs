using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個診斷項目（解析自指令輸出）· One parsed diagnostic row from command output.</summary>
public sealed class DoctorRow
{
    public string Primary { get; set; } = "";   // 主要文字 · main text (usually a real name/value)
    public string Secondary { get; set; } = ""; // 次要文字 · detail line
    public string Glyph { get; set; } = "";      // 圖示 · optional FontIcon glyph
    public string? Tag { get; set; }             // 內部識別（例如裝置名、job id）· internal id for follow-up actions
}

/// <summary>診斷結果：一句雙語摘要 + 解析好嘅清單 · A diagnose result: bilingual summary + parsed rows.</summary>
public sealed class DoctorReport
{
    public LocalizedText Summary { get; }
    public List<DoctorRow> Rows { get; }
    public string? RawOutput { get; }
    public bool Ok { get; }

    public DoctorReport(LocalizedText summary, List<DoctorRow>? rows = null, string? raw = null, bool ok = true)
    {
        Summary = summary;
        Rows = rows ?? new List<DoctorRow>();
        RawOutput = raw;
        Ok = ok;
    }
}

/// <summary>
/// 系統醫生 · System Doctors — guided, in-app rescue routines for common Windows 11 breakages.
/// 每個「醫生」都做真嘢（powercfg、ipconfig、netsh、sc、檔案操作…），唔係 dump 原始輸出，
/// 而係解析成原生雙語清單。Each doctor runs real commands and parses output into native bilingual lists.
/// </summary>
public static class SystemDoctors
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ============================================================================================
    // 1) Print Spooler & queue rescue · 列印多工緩衝與佇列救援
    // ============================================================================================

    /// <summary>列出等緊嘅列印工作 · List pending print jobs, parsed.</summary>
    public static async Task<DoctorReport> ListPrintJobsAsync(CancellationToken ct = default)
    {
        var json = await ShellRunner.CapturePowershellJson(
            "@(Get-CimInstance Win32_PrintJob | Select-Object @{n='Document';e={$_.Document}},@{n='Owner';e={$_.Owner}},@{n='Status';e={$_.JobStatus}},@{n='Pages';e={$_.TotalPages}},@{n='Name';e={$_.Name}}) | ConvertTo-Json -Compress",
            ct);
        var rows = new List<DoctorRow>();
        try
        {
            var jobs = JsonSerializer.Deserialize<List<PrintJob>>(json, JsonOpts);
            if (jobs is null && json.StartsWith("{"))
            {
                var one = JsonSerializer.Deserialize<PrintJob>(json, JsonOpts);
                if (one is not null) jobs = new List<PrintJob> { one };
            }
            if (jobs is not null)
            {
                foreach (var j in jobs)
                {
                    rows.Add(new DoctorRow
                    {
                        Primary = string.IsNullOrWhiteSpace(j.Document) ? (j.Name ?? "(job)") : j.Document!,
                        Secondary = $"{Loc.I.Pick("Owner", "擁有者")}: {j.Owner ?? "?"}  ·  {Loc.I.Pick("Status", "狀態")}: {j.Status ?? "?"}  ·  {j.Pages} {Loc.I.Pick("pages", "頁")}",
                        Glyph = ((char)0xE749).ToString(),
                        Tag = j.Name,
                    });
                }
            }
        }
        catch { /* leave rows empty */ }

        var summary = rows.Count == 0
            ? new LocalizedText("The print queue is empty.", "列印佇列係空嘅。")
            : new LocalizedText($"{rows.Count} job(s) waiting in the print queue.", $"列印佇列有 {rows.Count} 個工作等緊。");
        return new DoctorReport(summary, rows, json);
    }

    /// <summary>停 Spooler → 清 PRINTERS 資料夾 → 重啟 · Stop spooler, purge queue files, restart.</summary>
    public static Task<TweakResult> RescueSpoolerAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Service -Name Spooler -Force; " +
            "Remove-Item \"$env:SystemRoot\\System32\\spool\\PRINTERS\\*\" -Force -Recurse -ErrorAction SilentlyContinue; " +
            "Start-Service -Name Spooler; " +
            "'Spooler stopped, queue purged and restarted.'",
            elevated: false, ct);

    /// <summary>只重啟 Spooler（唔清檔）· Restart the spooler without purging files.</summary>
    public static Task<TweakResult> RestartSpoolerAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell("Restart-Service -Name Spooler -Force; 'Spooler restarted.'", elevated: false, ct);

    /// <summary>清走一個指定工作 · Cancel a single print job by its CIM Name key.</summary>
    public static Task<TweakResult> CancelPrintJobAsync(string jobName, CancellationToken ct = default)
    {
        var safe = jobName.Replace("'", "''");
        return ShellRunner.RunPowershell(
            $"Get-CimInstance Win32_PrintJob | Where-Object {{ $_.Name -eq '{safe}' }} | Remove-CimInstance; 'Job cancelled.'",
            elevated: false, ct);
    }

    private sealed class PrintJob
    {
        public string? Document { get; set; }
        public string? Owner { get; set; }
        public string? Status { get; set; }
        public int Pages { get; set; }
        public string? Name { get; set; }
    }

    // ============================================================================================
    // 2) Network / DNS doctor · 網絡 / DNS 醫生
    // ============================================================================================

    /// <summary>列出網絡介面卡同 IP · List adapters with status + IPv4, parsed.</summary>
    public static async Task<DoctorReport> ListAdaptersAsync(CancellationToken ct = default)
    {
        var json = await ShellRunner.CapturePowershellJson(
            "@(Get-NetAdapter | Select-Object Name,InterfaceDescription,Status,LinkSpeed,MacAddress) | ConvertTo-Json -Compress", ct);
        var rows = new List<DoctorRow>();
        try
        {
            var list = JsonSerializer.Deserialize<List<NetAdapter>>(json, JsonOpts);
            if (list is null && json.StartsWith("{"))
            {
                var one = JsonSerializer.Deserialize<NetAdapter>(json, JsonOpts);
                if (one is not null) list = new List<NetAdapter> { one };
            }
            if (list is not null)
            {
                foreach (var a in list)
                {
                    bool up = string.Equals(a.Status, "Up", StringComparison.OrdinalIgnoreCase);
                    rows.Add(new DoctorRow
                    {
                        Primary = a.Name ?? "(adapter)",
                        Secondary = $"{a.InterfaceDescription}  ·  {a.Status}  ·  {a.LinkSpeed}",
                        Glyph = up ? ((char)0xE839).ToString() : ((char)0xEB5E).ToString(),
                        Tag = a.Name,
                    });
                }
            }
        }
        catch { }

        var summary = rows.Count == 0
            ? new LocalizedText("No network adapters found.", "搵唔到網絡介面卡。")
            : new LocalizedText($"{rows.Count} network adapter(s).", $"{rows.Count} 個網絡介面卡。");
        return new DoctorReport(summary, rows, json);
    }

    public static Task<TweakResult> FlushDnsAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd("ipconfig /flushdns", elevated: false, ct);

    public static Task<TweakResult> ResetWinsockAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd("netsh winsock reset", elevated: false, ct);

    public static Task<TweakResult> ResetTcpIpAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd("netsh int ip reset", elevated: false, ct);

    public static Task<TweakResult> ReleaseRenewAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd("ipconfig /release & ipconfig /renew", elevated: false, ct);

    /// <summary>停用再啟用一張介面卡（要管理員）· Disable then re-enable an adapter (needs admin).</summary>
    public static Task<TweakResult> BounceAdapterAsync(string adapterName, CancellationToken ct = default)
    {
        var safe = adapterName.Replace("'", "''");
        return ShellRunner.RunPowershell(
            $"Disable-NetAdapter -Name '{safe}' -Confirm:$false; Start-Sleep -Seconds 2; Enable-NetAdapter -Name '{safe}' -Confirm:$false; \"Adapter '{safe}' bounced.\"",
            elevated: false, ct);
    }

    /// <summary>「一鍵修復連線」：flushdns + winsock + ip reset + release/renew · One-click repair.</summary>
    public static Task<TweakResult> RepairConnectionAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd(
            "ipconfig /flushdns & netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & netsh advfirewall reset",
            elevated: false, ct);

    private sealed class NetAdapter
    {
        public string? Name { get; set; }
        public string? InterfaceDescription { get; set; }
        public string? Status { get; set; }
        public string? LinkSpeed { get; set; }
        public string? MacAddress { get; set; }
    }

    // ============================================================================================
    // 3) Sleep / Wake doctor · 睡眠 / 喚醒醫生
    // ============================================================================================

    /// <summary>乜嘢阻住部機瞓覺（powercfg /requests）· What blocks sleep, parsed.</summary>
    public static async Task<DoctorReport> SleepBlockersAsync(CancellationToken ct = default)
    {
        var raw = await ShellRunner.Capture("powercfg.exe", "/requests", ct);
        var rows = ParsePowercfgRequests(raw);
        var summary = rows.Count == 0
            ? new LocalizedText("Nothing is currently blocking sleep.", "而家冇嘢阻住部機瞓覺。")
            : new LocalizedText($"{rows.Count} active power request(s) blocking sleep.", $"{rows.Count} 個電源要求正阻住睡眠。");
        return new DoctorReport(summary, rows, raw, ok: !string.IsNullOrWhiteSpace(raw));
    }

    private static List<DoctorRow> ParsePowercfgRequests(string raw)
    {
        var rows = new List<DoctorRow>();
        if (string.IsNullOrWhiteSpace(raw)) return rows;
        string? category = null;
        foreach (var lineRaw in raw.Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0) continue;
            // Category headers are ALL-CAPS tokens like DISPLAY:, SYSTEM:, AWAYMODE:, EXECUTION:, PERFBOOST:, ACTIVELOCKSCREEN:
            if (line.EndsWith(":") && line.ToUpperInvariant() == line && line.Length <= 24)
            {
                category = line.TrimEnd(':');
                continue;
            }
            if (line.Equals("None.", StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add(new DoctorRow
            {
                Primary = line,
                Secondary = category ?? "",
                Glyph = ((char)0xE708).ToString(),
            });
        }
        return rows;
    }

    /// <summary>最近係乜嘢整醒部機 · The last thing that woke the PC.</summary>
    public static async Task<DoctorReport> LastWakeAsync(CancellationToken ct = default)
    {
        var raw = (await ShellRunner.Capture("powercfg.exe", "/lastwake", ct)).Trim();
        return new DoctorReport(new LocalizedText("Last wake source.", "最近一次喚醒來源。"), raw: raw,
            ok: !string.IsNullOrWhiteSpace(raw));
    }

    /// <summary>有排程嘅喚醒計時器 · Scheduled wake timers, parsed.</summary>
    public static async Task<DoctorReport> WakeTimersAsync(CancellationToken ct = default)
    {
        var raw = await ShellRunner.Capture("powercfg.exe", "/waketimers", ct);
        var rows = new List<DoctorRow>();
        foreach (var lineRaw in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("There are no active wake timers", StringComparison.OrdinalIgnoreCase)) break;
            if (line.StartsWith("[", StringComparison.Ordinal))
                rows.Add(new DoctorRow { Primary = line, Glyph = ((char)0xE823).ToString() });
            else if (rows.Count > 0)
                rows[^1].Secondary = string.IsNullOrEmpty(rows[^1].Secondary) ? line : rows[^1].Secondary + "  " + line;
        }
        var summary = rows.Count == 0
            ? new LocalizedText("No active wake timers.", "冇啟用中嘅喚醒計時器。")
            : new LocalizedText($"{rows.Count} active wake timer(s).", $"{rows.Count} 個啟用中嘅喚醒計時器。");
        return new DoctorReport(summary, rows, raw);
    }

    /// <summary>邊啲裝置可以整醒部機 · Devices armed to wake the PC, parsed.</summary>
    public static async Task<DoctorReport> WakeArmedDevicesAsync(CancellationToken ct = default)
    {
        var raw = await ShellRunner.Capture("powercfg.exe", "/devicequery wake_armed", ct);
        var rows = new List<DoctorRow>();
        foreach (var lineRaw in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("NONE", StringComparison.OrdinalIgnoreCase)) break;
            rows.Add(new DoctorRow { Primary = line, Glyph = ((char)0xE975).ToString(), Tag = line });
        }
        var summary = rows.Count == 0
            ? new LocalizedText("No devices are armed to wake the PC.", "冇裝置可以整醒部機。")
            : new LocalizedText($"{rows.Count} device(s) can wake the PC.", $"{rows.Count} 個裝置可以整醒部機。");
        return new DoctorReport(summary, rows, raw);
    }

    /// <summary>解除一個裝置嘅喚醒能力（要管理員）· Disarm a device from waking the PC (needs admin).</summary>
    public static Task<TweakResult> DisarmWakeDeviceAsync(string deviceName, CancellationToken ct = default)
    {
        var safe = deviceName.Replace("\"", "");
        return ShellRunner.Run("powercfg.exe", $"/devicedisablewake \"{safe}\"", elevated: false, ct);
    }

    /// <summary>解除全部喚醒計時器（停用喚醒計時器）· Disable all wake timers via the active power scheme.</summary>
    public static Task<TweakResult> DisableWakeTimersAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd(
            "powercfg /setacvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 0 & powercfg /setdcvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 0 & powercfg /setactive SCHEME_CURRENT & echo Wake timers disabled.",
            elevated: false, ct);

    /// <summary>讀取快速啟動 (HiberbootEnabled) 狀態 · Read fast-startup (hybrid boot) state.</summary>
    public static async Task<DoctorReport> FastStartupStateAsync(CancellationToken ct = default)
    {
        var raw = (await ShellRunner.CapturePowershell(
            "(Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -ErrorAction SilentlyContinue).HiberbootEnabled",
            ct)).Trim();
        bool on = raw.StartsWith("1");
        var summary = on
            ? new LocalizedText("Fast startup (hybrid boot) is ON — can cause wake/shutdown quirks.",
                "快速啟動（混合開機）開咗 — 可能引致喚醒／關機怪問題。")
            : new LocalizedText("Fast startup is OFF.", "快速啟動已關閉。");
        return new DoctorReport(summary, raw: $"HiberbootEnabled = {(string.IsNullOrEmpty(raw) ? "(unset)" : raw)}");
    }

    /// <summary>開／關快速啟動（要管理員）· Toggle fast startup (needs admin).</summary>
    public static Task<TweakResult> SetFastStartupAsync(bool enabled, CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            $"Set-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -Value {(enabled ? 1 : 0)} -Type DWord; 'HiberbootEnabled set to {(enabled ? 1 : 0)}.'",
            elevated: false, ct);

    /// <summary>解鎖並啟用「終極效能」電源計劃 · Unlock and activate the Ultimate Performance power scheme.</summary>
    public static Task<TweakResult> UnlockUltimatePerformanceAsync(CancellationToken ct = default)
        => ShellRunner.RunCmd(
            "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 & powercfg /setactive e9a42b02-d5df-448d-aa00-03f14749eb61 & echo Ultimate Performance unlocked and active.",
            elevated: false, ct);

    // ============================================================================================
    // 4) Shell recovery — Fix taskbar & Start · 外殼修復 — 修復工作列與開始功能表
    // ============================================================================================

    /// <summary>清 IrisService 快取、重新註冊外殼套件、重啟 explorer · Clear cache, re-register shell, restart explorer.</summary>
    public static Task<TweakResult> FixTaskbarAndStartAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; " +
            // Clear the IrisService / cached content-delivery data that corrupts the Start menu.
            "reg delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\IrisService\" /f 2>$null; " +
            // Re-register the Start menu / shell experience host packages for the current user.
            "Get-AppxPackage Microsoft.Windows.ShellExperienceHost | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" -ErrorAction SilentlyContinue }; " +
            "Get-AppxPackage Microsoft.Windows.StartMenuExperienceHost | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" -ErrorAction SilentlyContinue }; " +
            "Start-Sleep -Seconds 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Taskbar & Start repaired: cache cleared, shell packages re-registered, Explorer restarted.'",
            elevated: false, ct);

    // ============================================================================================
    // 5) Search index governor · 搜尋索引管理
    // ============================================================================================

    /// <summary>Windows 搜尋服務狀態 · Windows Search service state.</summary>
    public static async Task<DoctorReport> SearchStateAsync(CancellationToken ct = default)
    {
        var raw = (await ShellRunner.CapturePowershell(
            "$s = Get-Service WSearch -ErrorAction SilentlyContinue; if ($s) { \"$($s.Status) / $($s.StartType)\" } else { 'not found' }", ct)).Trim();
        var web = (await ShellRunner.CapturePowershell(
            "(Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name DisableSearchBoxSuggestions -ErrorAction SilentlyContinue).DisableSearchBoxSuggestions", ct)).Trim();
        bool webOff = web.StartsWith("1");
        var summary = new LocalizedText(
            $"Windows Search: {raw}. Web results in Start: {(webOff ? "disabled" : "enabled")}.",
            $"Windows 搜尋：{raw}。開始功能表網頁結果：{(webOff ? "已停用" : "啟用中")}。");
        return new DoctorReport(summary, raw: $"WSearch = {raw}\nDisableSearchBoxSuggestions = {(string.IsNullOrEmpty(web) ? "(unset)" : web)}");
    }

    public static Task<TweakResult> PauseSearchAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell("Stop-Service WSearch -Force; 'Windows Search paused (service stopped).'", elevated: false, ct);

    public static Task<TweakResult> ResumeSearchAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell("Set-Service WSearch -StartupType Automatic; Start-Service WSearch; 'Windows Search resumed.'", elevated: false, ct);

    /// <summary>重建搜尋索引（停服務、刪 Windows.edb、重啟）· Rebuild the search index.</summary>
    public static Task<TweakResult> RebuildSearchIndexAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Service WSearch -Force; " +
            "Remove-Item \"$env:ProgramData\\Microsoft\\Search\\Data\\Applications\\Windows\\Windows.edb\" -Force -ErrorAction SilentlyContinue; " +
            "Set-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows Search' -Name SetupCompletedSuccessfully -Value 0 -Type DWord -ErrorAction SilentlyContinue; " +
            "Start-Service WSearch; 'Search index reset — Windows will rebuild it in the background.'",
            elevated: false, ct);

    /// <summary>關閉開始功能表嘅網頁結果（Bing）· Kill web/Bing results in Start search.</summary>
    public static Task<TweakResult> DisableWebResultsAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "New-Item 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Force | Out-Null; " +
            "Set-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name DisableSearchBoxSuggestions -Value 1 -Type DWord; " +
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Web results disabled in Start search.'",
            elevated: false, ct);

    public static Task<TweakResult> EnableWebResultsAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Remove-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name DisableSearchBoxSuggestions -ErrorAction SilentlyContinue; " +
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Web results re-enabled in Start search.'",
            elevated: false, ct);

    // ============================================================================================
    // 6) Explorer perf tuner · 檔案總管效能調校
    // ============================================================================================

    /// <summary>檢視 Explorer 設定 + 重複 explorer 程序 · Explorer settings + ghost explorer count.</summary>
    public static async Task<DoctorReport> ExplorerStateAsync(CancellationToken ct = default)
    {
        var sep = (await ShellRunner.CapturePowershell(
            "(Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name SeparateProcess -ErrorAction SilentlyContinue).SeparateProcess", ct)).Trim();
        var count = (await ShellRunner.CapturePowershell("(Get-Process explorer -ErrorAction SilentlyContinue | Measure-Object).Count", ct)).Trim();
        bool sepOn = sep.StartsWith("1");
        int.TryParse(count, out var n);
        var rows = new List<DoctorRow>
        {
            new() { Primary = Loc.I.Pick("Launch folder windows in a separate process", "用獨立程序開啟資料夾視窗"),
                    Secondary = sepOn ? Loc.I.Pick("ON", "開") : Loc.I.Pick("OFF", "關"), Glyph = ((char)0xE8B7).ToString() },
            new() { Primary = Loc.I.Pick("Running explorer.exe processes", "運行中嘅 explorer.exe 程序"),
                    Secondary = n.ToString(), Glyph = ((char)0xE9D9).ToString() },
        };
        return new DoctorReport(new LocalizedText(
            $"SeparateProcess = {(sepOn ? "ON" : "OFF")}, {n} explorer process(es) running.",
            $"獨立程序 = {(sepOn ? "開" : "關")}，{n} 個 explorer 程序運行中。"), rows,
            $"SeparateProcess = {(string.IsNullOrEmpty(sep) ? "(unset)" : sep)}\nexplorer.exe count = {count}");
    }

    public static Task<TweakResult> SetSeparateProcessAsync(bool enabled, CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            $"Set-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name SeparateProcess -Value {(enabled ? 1 : 0)} -Type DWord; 'SeparateProcess set to {(enabled ? 1 : 0)} — new windows apply it.'",
            elevated: false, ct);

    /// <summary>結束多餘／鬼影 explorer 程序（保留外殼）· Kill ghost explorer processes, keep the shell.</summary>
    public static Task<TweakResult> KillGhostExplorersAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; " +
            "if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Ghost Explorer processes cleared; the shell was restarted once.'",
            elevated: false, ct);

    // ============================================================================================
    // 7) Icon / thumbnail cache rebuilder · 圖示 / 縮圖快取重建
    // ============================================================================================

    /// <summary>重建圖示快取（ie4uinit + 刪 IconCache.db）· Rebuild the icon cache.</summary>
    public static Task<TweakResult> RebuildIconCacheAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; " +
            "ie4uinit.exe -show; " +
            "Remove-Item \"$env:LocalAppData\\IconCache.db\" -Force -ErrorAction SilentlyContinue; " +
            "Remove-Item \"$env:LocalAppData\\Microsoft\\Windows\\Explorer\\iconcache*\" -Force -ErrorAction SilentlyContinue; " +
            "Start-Sleep -Seconds 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Icon cache rebuilt.'",
            elevated: false, ct);

    /// <summary>重建縮圖快取（刪 thumbcache*.db）· Rebuild the thumbnail cache.</summary>
    public static Task<TweakResult> RebuildThumbnailCacheAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; " +
            "Remove-Item \"$env:LocalAppData\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\" -Force -ErrorAction SilentlyContinue; " +
            "Start-Sleep -Seconds 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }; " +
            "'Thumbnail cache rebuilt.'",
            elevated: false, ct);

    // ============================================================================================
    // 8) Take ownership / reset permissions · 取得擁有權 / 重設權限
    // ============================================================================================

    /// <summary>對一個路徑取得擁有權 + 賦予完整控制（要管理員）· Take ownership + grant FullControl (needs admin).</summary>
    public static Task<TweakResult> TakeOwnershipAsync(string path, bool recurse, CancellationToken ct = default)
    {
        var safe = path.Replace("\"", "");
        var rec = recurse ? " /r /d y" : "";
        var icaclsRec = recurse ? " /t /c" : "";
        var user = Environment.UserDomainName + "\\" + Environment.UserName;
        return ShellRunner.RunCmd(
            $"takeown /f \"{safe}\"{rec} & icacls \"{safe}\" /grant \"{user}\":F{icaclsRec}",
            elevated: false, ct);
    }

    /// <summary>還原預設權限：重設繼承嘅 ACL（要管理員）· Reset ACLs to inherited defaults (undo, needs admin).</summary>
    public static Task<TweakResult> ResetPermissionsAsync(string path, bool recurse, CancellationToken ct = default)
    {
        var safe = path.Replace("\"", "");
        var rec = recurse ? " /t /c" : "";
        return ShellRunner.RunCmd(
            $"icacls \"{safe}\" /reset{rec}",
            elevated: false, ct);
    }
}
