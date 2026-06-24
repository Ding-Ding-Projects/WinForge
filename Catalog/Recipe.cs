using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 一鍵流程 · "Recipes": one button that runs several real steps in sequence and reports each.
/// </summary>
public static class Recipe
{
    public delegate Task<TweakResult> Step(CancellationToken ct);

    public static (string label, Step run) Cmd(string label, string command, bool admin = false)
        => (label, ct => ShellRunner.RunCmd(command, admin, ct));

    public static (string label, Step run) Ps(string label, string script, bool admin = false)
        => (label, ct => ShellRunner.RunPowershell(script, admin, ct));

    public static (string label, Step run) Reg(string label, Action act)
        => (label, ct =>
        {
            act();
            return Task.FromResult(TweakResult.Ok("set", "已設定"));
        });

    /// <summary>
    /// 套用一批調校 · Apply a set of tweaks: toggles get SetIsOn(on); actions are run only when on
    /// (an action can't be "undone"). Used by "Calm Windows" etc.
    /// </summary>
    public static (string label, Step run) Apply(string label, Func<IEnumerable<TweakDefinition>> source, bool on = true)
        => (label, async ct =>
        {
            int ok = 0, fail = 0;
            foreach (var t in source())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (t.Kind == TweakKind.Toggle && t.SetIsOn is not null) { t.SetIsOn(on); ok++; }
                    else if (on && t.Kind == TweakKind.Action && t.RunAsync is not null)
                    {
                        var r = await t.RunAsync(ct);
                        if (r.Success) ok++; else fail++;
                    }
                }
                catch { fail++; }
            }
            return new TweakResult(fail == 0, new LocalizedText($"{ok} applied, {fail} failed", $"套用咗 {ok} 項，{fail} 項失敗"), null);
        });

    /// <summary>停用名稱包含關鍵字嘅開機項目 · Disable startup entries whose name contains any keyword.</summary>
    public static (string label, Step run) DisableStartup(string label, params string[] keywords)
        => (label, ct =>
        {
            int n = 0;
            foreach (var item in StartupManager.List())
            {
                if (!item.Enabled) continue;
                if (keywords.Any(k => item.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    try { StartupManager.SetEnabled(item, false); n++; } catch { /* HKLM needs admin */ }
                }
            }
            return Task.FromResult(TweakResult.Ok($"Disabled {n} startup item(s)", $"停用咗 {n} 個開機項目"));
        });

    public static TweakDefinition Make(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, bool admin, bool destructive, params (string label, Step run)[] steps)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            async ct =>
            {
                var sb = new StringBuilder();
                bool ok = true;
                int i = 0;
                foreach (var s in steps)
                {
                    ct.ThrowIfCancellationRequested();
                    i++;
                    try
                    {
                        var r = await s.run(ct);
                        sb.AppendLine($"{(r.Success ? "✓" : "✗")} [{i}/{steps.Length}] {s.label}");
                        if (!r.Success) ok = false;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"✗ [{i}/{steps.Length}] {s.label}: {ex.Message}");
                        ok = false;
                    }
                }
                return new TweakResult(ok,
                    new LocalizedText(ok ? "Recipe complete." : "Recipe finished with some failures.",
                        ok ? "流程完成。" : "流程做完，但有部分失敗。"),
                    sb.ToString().TrimEnd());
            },
            requiresAdmin: admin, destructive: destructive);
}
