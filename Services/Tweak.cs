using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 建立調校項目嘅工廠 · Factory helpers for building <see cref="TweakDefinition"/>s declaratively.
///
/// 設計目標：分類目錄只需要提供「資料」（真實 registry 路徑／指令 + 雙語文字），
/// 唔使寫行為邏輯。Goal: catalog files supply data only (real registry paths/commands +
/// bilingual text); behaviour lives here.
/// </summary>
public static class Tweak
{
    private static string[] Keys(string? kw) => string.IsNullOrWhiteSpace(kw)
        ? Array.Empty<string>()
        : kw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// 由登錄檔值支援嘅開關 · A toggle backed by a registry value.
    /// offValue 為 null 表示「關」時刪除個值 · a null offValue deletes the value when turned off.
    /// </summary>
    public static TweakDefinition RegToggle(
        string id, string enT, string zhT, string enD, string zhD,
        RegRoot root, string path, string name,
        object onValue, object? offValue,
        RegistryValueKind kind = RegistryValueKind.DWord,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Toggle,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            GetIsOn = () => RegistryHelper.ValueEquals(root, path, name, onValue),
            SetIsOn = on =>
            {
                if (on) RegistryHelper.SetValue(root, path, name, onValue, kind);
                else if (offValue is null) RegistryHelper.DeleteValue(root, path, name);
                else RegistryHelper.SetValue(root, path, name, offValue, kind);
            },
        };

    /// <summary>
    /// 自訂讀寫邏輯嘅開關 · A toggle with custom read/write logic (e.g. multiple registry values).
    /// </summary>
    public static TweakDefinition CustomToggle(
        string id, string enT, string zhT, string enD, string zhD,
        Func<bool> getIsOn, Action<bool> setIsOn,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Toggle,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            GetIsOn = getIsOn,
            SetIsOn = setIsOn,
        };

    /// <summary>由登錄檔值支援嘅多選一 · A choice backed by a single registry value.</summary>
    public static TweakDefinition RegChoice(
        string id, string enT, string zhT, string enD, string zhD,
        RegRoot root, string path, string name, RegistryValueKind kind,
        (string en, string zh, object value)[] options,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
    {
        var choices = new List<TweakChoice>();
        foreach (var o in options)
            choices.Add(new TweakChoice(new LocalizedText(o.en, o.zh), o.value.ToString()!));

        return new TweakDefinition
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Choice,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            Choices = choices,
            GetCurrentChoice = () =>
            {
                foreach (var o in options)
                    if (RegistryHelper.ValueEquals(root, path, name, o.value))
                        return o.value.ToString();
                return null;
            },
            SetChoice = val =>
            {
                foreach (var o in options)
                    if (string.Equals(o.value.ToString(), val, StringComparison.OrdinalIgnoreCase))
                    {
                        RegistryHelper.SetValue(root, path, name, o.value, kind);
                        return;
                    }
            },
        };
    }

    /// <summary>一次性動作 · A one-shot action with a custom async body.</summary>
    public static TweakDefinition Action(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn,
        Func<CancellationToken, Task<TweakResult>> run,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            RequiresAdmin = requiresAdmin,
            Destructive = destructive,
            Restart = restart,
            Keywords = Keys(keywords),
            ActionLabel = new(enBtn, zhBtn),
            RunAsync = run,
        };

    /// <summary>執行外部程序嘅動作 · An action that runs an external process.</summary>
    public static TweakDefinition Shell(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string fileName, string arguments,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ShellRunner.Run(fileName, arguments, requiresAdmin, ct),
            requiresAdmin, destructive, restart, keywords);

    /// <summary>同上，但用預設按鈕「Run · 執行」· Same, with a default "Run · 執行" button.</summary>
    public static TweakDefinition Shell(
        string id, string enT, string zhT, string enD, string zhD,
        string fileName, string arguments,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => Shell(id, enT, zhT, enD, zhD, "Run", "執行", fileName, arguments,
            requiresAdmin, destructive, restart, keywords);

    /// <summary>執行 PowerShell 嘅動作 · An action that runs a PowerShell snippet.</summary>
    public static TweakDefinition Powershell(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string script,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ShellRunner.RunPowershell(script, requiresAdmin, ct),
            requiresAdmin, destructive, restart, keywords);

    /// <summary>
    /// 表格輸出動作 · An action whose PowerShell pipeline is rendered as a native grid.
    /// 傳入會輸出物件嘅 pipeline（唔使加 Format-Table）· pass a pipeline that emits objects
    /// (no Format-Table needed); this appends Select/ConvertTo-Csv and the UI draws a table.
    /// </summary>
    public static TweakDefinition Table(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string pipeline,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
    {
        var p = NormalizeForCsv(pipeline);
        return new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            TabularOutput = true,
            ActionLabel = new(enBtn, zhBtn),
            // Silence the progress stream so its CLIXML serialization never pollutes the CSV on stderr.
            RunAsync = ct => ShellRunner.RunPowershell(
                $"$ProgressPreference='SilentlyContinue'; {p} | ConvertTo-Csv -NoTypeInformation", requiresAdmin, ct),
        };
    }

    /// <summary>
    /// 將末端嘅 Format-Table／Format-List 改寫成 Select-Object，等 ConvertTo-Csv 出到乾淨 CSV ·
    /// Rewrite a trailing Format-Table/Format-List into Select-Object so ConvertTo-Csv yields clean CSV.
    /// "| Format-Table A, B -AutoSize" → "| Select-Object A, B"; a bare "| Format-Table -AutoSize" is dropped
    /// (an upstream Select-Object, if any, already chose the columns).
    /// </summary>
    private static string NormalizeForCsv(string pipeline)
    {
        var m = Regex.Match(pipeline, @"\|\s*Format-(Table|List)\b(?<rest>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return pipeline;

        var before = pipeline.Substring(0, m.Index).TrimEnd();
        var rest = Regex.Replace(m.Groups["rest"].Value, @"-AutoSize|-Wrap|-HideTableHeaders|-GroupBy\s+\S+",
            "", RegexOptions.IgnoreCase).Trim();
        rest = rest.TrimStart('|').Trim();
        return (rest.Length > 0 && !rest.StartsWith("-")) ? $"{before} | Select-Object {rest}" : before;
    }

    /// <summary>執行 cmd 指令嘅動作 · An action that runs a cmd.exe command line.</summary>
    public static TweakDefinition Cmd(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string command,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ShellRunner.RunCmd(command, requiresAdmin, ct),
            requiresAdmin, destructive, restart, keywords);

    /// <summary>唯讀資訊項目 · A read-only information row.</summary>
    public static TweakDefinition Info(
        string id, string enT, string zhT, string enD, string zhD,
        Func<string> getter, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Info,
            Keywords = Keys(keywords),
            GetInfo = getter,
        };

    // ======================================================================
    //  Rich interactive factories (foundation upgrade) · 進階互動工廠
    // ======================================================================

    /// <summary>數值滑桿 · A numeric Slider with min/max/step and optional unit.</summary>
    public static TweakDefinition Slider(
        string id, string enT, string zhT, string enD, string zhD,
        double min, double max, double step, Func<double> get, Action<double> set,
        string? unitEn = null, string? unitZh = null,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Slider,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            Min = min, Max = max, Step = step,
            Unit = unitEn is null ? null : new LocalizedText(unitEn, unitZh ?? unitEn),
            GetNumber = get, SetNumber = set,
        };

    /// <summary>數字輸入框 · A NumberBox numeric entry with min/max/step.</summary>
    public static TweakDefinition Number(
        string id, string enT, string zhT, string enD, string zhD,
        double min, double max, double step, Func<double> get, Action<double> set,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Number,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            Min = min, Max = max, Step = step,
            GetNumber = get, SetNumber = set,
        };

    /// <summary>單選按鈕組 · A RadioButtons group backed by the same choice plumbing as a Choice.</summary>
    public static TweakDefinition RadioGroup(
        string id, string enT, string zhT, string enD, string zhD,
        (string en, string zh, string value)[] options,
        Func<string?> getCurrent, Action<string> setChoice,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
    {
        var choices = new List<TweakChoice>();
        foreach (var o in options)
            choices.Add(new TweakChoice(new LocalizedText(o.en, o.zh), o.value));
        return new TweakDefinition
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.RadioGroup,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            Choices = choices,
            GetCurrentChoice = getCurrent,
            SetChoice = setChoice,
        };
    }

    /// <summary>多重勾選清單 · A vertical list of independent CheckBoxes.</summary>
    public static TweakDefinition MultiCheck(
        string id, string enT, string zhT, string enD, string zhD,
        IReadOnlyList<TweakToggleItem> items,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.MultiCheck,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            CheckItems = items,
        };

    /// <summary>顏色揀選 · A colour swatch + ColorPicker flyout + hex box (#RRGGBB).</summary>
    public static TweakDefinition Color(
        string id, string enT, string zhT, string enD, string zhD,
        Func<string> getHex, Action<string> setHex,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Color,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            GetHex = getHex, SetHex = setHex,
        };

    /// <summary>日期（可選時間）揀選 · A DatePicker, optionally with a TimePicker.</summary>
    public static TweakDefinition Date(
        string id, string enT, string zhT, string enD, string zhD,
        Func<DateTimeOffset?> get, Action<DateTimeOffset?> set, bool includeTime = false,
        bool requiresAdmin = false, RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.DateKind,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = Keys(keywords),
            GetDate = get, SetDate = set, IncludeTime = includeTime,
        };

    /// <summary>多步驟精靈 · A multi-step ContentDialog wizard; values collected per step feed the finish handler.</summary>
    public static TweakDefinition Wizard(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn,
        IReadOnlyList<WizardStep> steps,
        Func<IReadOnlyDictionary<string, string>, CancellationToken, Task<TweakResult>> finish,
        bool requiresAdmin = false, bool destructive = false,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Wizard,
            RequiresAdmin = requiresAdmin,
            Destructive = destructive,
            Restart = restart,
            Keywords = Keys(keywords),
            ActionLabel = new(enBtn, zhBtn),
            WizardSteps = steps,
            WizardFinish = finish,
        };
}
