using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Cloudflare 與 Cloudflare Tunnel · cloudflared, named &amp; quick Tunnels, Cloudflare Access,
/// DNS-over-HTTPS and WARP — all in-app via the cloudflared / warp-cli CLIs. Bilingual.
/// </summary>
public sealed partial class CloudflareModule : Page
{
    private List<TweakDefinition>? _ops;

    public CloudflareModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) => { Render(); BuildQuickActions(); PopulateOps(string.Empty); await CheckEngine(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); BuildQuickActions(); PopulateOps(OpsFilter.Text ?? string.Empty); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Cloudflare & Tunnel · Cloudflare 與 Tunnel";
        HeaderBlurb.Text = P(
            "Run cloudflared from inside WinTune: named tunnels, free quick tunnels, route DNS, Cloudflare Access, DNS-over-HTTPS and WARP.",
            "喺 WinTune 直接用 cloudflared：具名 tunnel、免費快速 tunnel、DNS 路由、Cloudflare Access、DNS-over-HTTPS 同 WARP。");
        _ops ??= CloudflareOperations.All().ToList();
        AdvancedHeader.Text = P($"Operations ({_ops.Count})", $"操作（{_ops.Count}）");
        PlaceholderHint.Text = P(
            "Tip: many operations use placeholders — MYTUNNEL (tunnel name), app.example.com (hostname), http://localhost:8080 (local service). Edit them to your values; long-running ones (run, quick tunnel, DoH) open in a terminal.",
            "提示：好多操作用咗佔位符 — MYTUNNEL（tunnel 名）、app.example.com（主機名）、http://localhost:8080（本機服務）。改成你嘅值；長時間運行嘅（run、快速 tunnel、DoH）會喺終端機開出嚟。");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
    }

    private async Task CheckEngine()
    {
        bool ok = await CloudflareService.IsInstalledAsync();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("cloudflared not found", "搵唔到 cloudflared");
        EngineBar.Message = P("Click to install cloudflared automatically (winget) — no restart needed.",
            "撳一下自動安裝 cloudflared（winget）— 唔使重開。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            CloudflareService.WingetId, "Install cloudflared automatically", "自動安裝 cloudflared",
            async () => { await CheckEngine(); }, null);
    }

    private void BuildQuickActions()
    {
        QuickActions.Children.Clear();
        AddQuick(P("Version", "版本"), () => CloudflareService.RunRaw("--version"));
        AddQuick(P("List tunnels", "列出 tunnel"), () => CloudflareService.RunRaw("tunnel list"));
        AddQuick(P("Login", "登入"), () => Task.FromResult(CloudflareService.LaunchInTerminal("cloudflared", "tunnel login")));
        AddQuick(P("WARP status", "WARP 狀態"), () => CloudflareService.Warp("status"));
        AddQuick(P("Update", "更新"), () => CloudflareService.RunRaw("update"));
    }

    private void AddQuick(string label, Func<Task<TweakResult>> run)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) =>
        {
            btn.IsEnabled = false;
            try
            {
                var r = await run();
                OutBorder.Visibility = Visibility.Visible;
                var body = string.IsNullOrWhiteSpace(r.Output)
                    ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
                    : r.Output!;
                OutText.Text = body.Length > 4000 ? body[^4000..] : body;
            }
            catch (Exception ex) { OutBorder.Visibility = Visibility.Visible; OutText.Text = ex.Message; }
            finally { btn.IsEnabled = true; }
        };
        QuickActions.Children.Add(btn);
    }

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= CloudflareOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }
}
