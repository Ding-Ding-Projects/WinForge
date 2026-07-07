using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinForge.Models;

namespace WinForge.Services;

public sealed class CakeCreditSnapshot
{
    public long BalanceUnits { get; init; }
    public long LifetimeDepositedUnits { get; init; }
    public long LifetimeSpentUnits { get; init; }
    public int CakesFed { get; init; }
    public int CakeFilesAvailable { get; init; }
    public LocalizedText LastMessage { get; init; } = new("", "");
}

public sealed class CakeCreditChargeResult
{
    public bool Success { get; init; }
    public long UnitsRequested { get; init; }
    public long UnitsCharged { get; init; }
    public long BalanceUnits { get; init; }
    public int CakesFedNow { get; init; }
    public LocalizedText Message { get; init; } = new("", "");
}

/// <summary>
/// Shared AI generation credit meter. One signed .cake file can be eaten into 1,000,000 generated
/// units, then communication AI, Ollama, and terminal agents spend those units as usage credits.
/// </summary>
public sealed class CakeCreditService
{
    public const long UnitsPerCake = 1_000_000;

    public static CakeCreditService I { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _stateFile;
    private readonly CakeFileService _cakes;
    private State _state;

    public CakeCreditService(string? cakeRoot = null, CakeFileService? cakes = null)
    {
        var root = string.IsNullOrWhiteSpace(cakeRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "cake-factory")
            : cakeRoot;
        Directory.CreateDirectory(root);
        _stateFile = Path.Combine(root, "cake-credits.json");
        _cakes = cakes ?? new CakeFileService(root);
        _state = LoadState();
    }

    public CakeCreditSnapshot Snapshot
    {
        get { lock (_gate) return MakeSnapshotLocked(); }
    }

    public bool CanStartGeneration
    {
        get
        {
            lock (_gate)
                return _state.BalanceUnits > 0 || CountAvailableCakeFilesLocked() > 0;
        }
    }

    public CakeCreditChargeResult CheckCanStartGeneration(string meterNameEn, string meterNameZh)
    {
        lock (_gate)
        {
            if (_state.BalanceUnits > 0 || CountAvailableCakeFilesLocked() > 0)
            {
                var ok = new LocalizedText(
                    $"{meterNameEn} has cake credits ready.",
                    $"{meterNameZh} 已有蛋糕額度可用。");
                return new CakeCreditChargeResult
                {
                    Success = true,
                    BalanceUnits = _state.BalanceUnits,
                    Message = ok,
                };
            }

            var msg = NeedCakeMessage(meterNameEn, meterNameZh, 1, _state.BalanceUnits);
            _state.LastMessageEn = msg.En;
            _state.LastMessageZh = msg.Zh;
            SaveStateLocked();
            return new CakeCreditChargeResult
            {
                Success = false,
                UnitsRequested = 1,
                BalanceUnits = _state.BalanceUnits,
                Message = msg,
            };
        }
    }

    public CakeCreditChargeResult FeedOneCake(string reasonEn = "Cake credits", string reasonZh = "蛋糕額度")
    {
        lock (_gate)
        {
            if (!TryFeedOneCakeLocked(out _, out var refused))
            {
                _state.LastMessageEn = refused.En;
                _state.LastMessageZh = refused.Zh;
                SaveStateLocked();
                return new CakeCreditChargeResult
                {
                    Success = false,
                    BalanceUnits = _state.BalanceUnits,
                    Message = refused,
                };
            }

            var msg = new LocalizedText(
                $"{reasonEn}: fed 1 cake into AI credits. Balance: {FormatUnits(_state.BalanceUnits)}.",
                $"{reasonZh}：已餵入 1 個蛋糕做 AI 額度。餘額：{FormatUnits(_state.BalanceUnits)}。");
            _state.LastMessageEn = msg.En;
            _state.LastMessageZh = msg.Zh;
            SaveStateLocked();
            return new CakeCreditChargeResult
            {
                Success = true,
                BalanceUnits = _state.BalanceUnits,
                CakesFedNow = 1,
                Message = msg,
            };
        }
    }

    public CakeCreditChargeResult TryChargeGeneratedUnits(string meterNameEn, string meterNameZh, long units)
    {
        units = Math.Max(0, units);
        lock (_gate)
        {
            if (units == 0)
            {
                var none = new LocalizedText(
                    $"{meterNameEn} produced no generated units, so no cake credit was spent.",
                    $"{meterNameZh} 未產生生成單位，所以無消耗蛋糕額度。");
                return new CakeCreditChargeResult
                {
                    Success = true,
                    BalanceUnits = _state.BalanceUnits,
                    Message = none,
                };
            }

            int fed = 0;
            while (_state.BalanceUnits < units)
            {
                if (!TryFeedOneCakeLocked(out _, out _))
                    break;
                fed++;
            }

            if (_state.BalanceUnits < units)
            {
                var msg = NeedCakeMessage(meterNameEn, meterNameZh, units, _state.BalanceUnits);
                _state.LastMessageEn = msg.En;
                _state.LastMessageZh = msg.Zh;
                SaveStateLocked();
                return new CakeCreditChargeResult
                {
                    Success = false,
                    UnitsRequested = units,
                    BalanceUnits = _state.BalanceUnits,
                    CakesFedNow = fed,
                    Message = msg,
                };
            }

            _state.BalanceUnits -= units;
            _state.LifetimeSpentUnits = SafeAdd(_state.LifetimeSpentUnits, units);

            var fedPartEn = fed > 0 ? $" Fed {fed} cake{(fed == 1 ? "" : "s")} first." : "";
            var fedPartZh = fed > 0 ? $" 已先餵入 {fed} 個蛋糕。" : "";
            var msgOk = new LocalizedText(
                $"{meterNameEn} used {FormatUnits(units)}.{fedPartEn} Balance: {FormatUnits(_state.BalanceUnits)}.",
                $"{meterNameZh} 已使用 {FormatUnits(units)}。{fedPartZh}餘額：{FormatUnits(_state.BalanceUnits)}。");
            _state.LastMessageEn = msgOk.En;
            _state.LastMessageZh = msgOk.Zh;
            SaveStateLocked();
            return new CakeCreditChargeResult
            {
                Success = true,
                UnitsRequested = units,
                UnitsCharged = units,
                BalanceUnits = _state.BalanceUnits,
                CakesFedNow = fed,
                Message = msgOk,
            };
        }
    }

    public static long GeneratedUnitsFrom(int? completionTokens, string? generatedText)
        => completionTokens is > 0 ? completionTokens.Value : EstimateGeneratedUnits(generatedText);

    public static long EstimateGeneratedUnits(string? generatedText)
    {
        var text = (generatedText ?? "").Trim();
        if (text.Length == 0) return 0;
        return Math.Max(1, (text.Length + 3L) / 4L);
    }

    public static string FormatUnits(long units)
        => units.ToString("N0", CultureInfo.CurrentCulture) + " gen";

    private bool TryFeedOneCakeLocked(out CakeEatResult eat, out LocalizedText refused)
    {
        eat = _cakes.EatLatest();
        if (!eat.Eaten)
        {
            refused = new LocalizedText(
                "No edible .cake file is available. Bake and pack a cake in Cake Factory first; 1 cake buys 1,000,000 generated units.",
                "暫時無可食用 .cake 檔。請先喺蛋糕工廠烘焙並包裝蛋糕；1 個蛋糕等於 1,000,000 個生成單位。");
            return false;
        }

        _state.BalanceUnits = SafeAdd(_state.BalanceUnits, UnitsPerCake);
        _state.LifetimeDepositedUnits = SafeAdd(_state.LifetimeDepositedUnits, UnitsPerCake);
        _state.CakesFed = Math.Max(0, _state.CakesFed) + 1;
        refused = new LocalizedText("", "");
        return true;
    }

    private CakeCreditSnapshot MakeSnapshotLocked()
        => new()
        {
            BalanceUnits = _state.BalanceUnits,
            LifetimeDepositedUnits = _state.LifetimeDepositedUnits,
            LifetimeSpentUnits = _state.LifetimeSpentUnits,
            CakesFed = _state.CakesFed,
            CakeFilesAvailable = CountAvailableCakeFilesLocked(),
            LastMessage = new LocalizedText(_state.LastMessageEn, _state.LastMessageZh),
        };

    private int CountAvailableCakeFilesLocked()
    {
        try { return _cakes.ListFresh().Count(c => c.SignatureValid); }
        catch { return 0; }
    }

    private State LoadState()
    {
        try
        {
            if (File.Exists(_stateFile))
                return JsonSerializer.Deserialize<State>(File.ReadAllText(_stateFile)) ?? new State();
        }
        catch { }
        return new State();
    }

    private void SaveStateLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
            File.WriteAllText(_stateFile, JsonSerializer.Serialize(_state, JsonOptions));
        }
        catch { }
    }

    private static LocalizedText NeedCakeMessage(string meterNameEn, string meterNameZh, long units, long balance)
        => new(
            $"{meterNameEn} needs {FormatUnits(units)} but only {FormatUnits(balance)} is available. Bake and pack a cake in Cake Factory, then try again. 1 cake = 1,000,000 generated units.",
            $"{meterNameZh} 需要 {FormatUnits(units)}，但目前只有 {FormatUnits(balance)}。請先喺蛋糕工廠烘焙並包裝蛋糕，再重試。1 個蛋糕 = 1,000,000 個生成單位。");

    private static long SafeAdd(long a, long b)
        => a > long.MaxValue - b ? long.MaxValue : a + b;

    private sealed class State
    {
        public long BalanceUnits { get; set; }
        public long LifetimeDepositedUnits { get; set; }
        public long LifetimeSpentUnits { get; set; }
        public int CakesFed { get; set; }
        public string LastMessageEn { get; set; } = "";
        public string LastMessageZh { get; set; } = "";
    }
}
