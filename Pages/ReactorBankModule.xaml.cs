using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 反應堆銀行 · Reactor Bank — the wallet for WinForge's reactor-backed currency, Watts (⚡). Watts are
/// MINTED live from the flagship reactor's electrical output (while it is generating) and EARNED from the
/// reactor-powered industrial loads, then SPENT in a small store of perks that unlock features across those
/// modules. Reads the live reactor via ReactorStatusApiService (a value struct) and the shared
/// ReactorEconomyService. Fully bilingual, leak-safe, never throws.
/// </summary>
public sealed partial class ReactorBankModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick = DateTime.UtcNow;

    private sealed record Perk(string Id, string En, string Zh, string DescEn, string DescZh, double Cost);

    private static readonly Perk[] Perks =
    {
        new("perk.goldreactor", "Golden Reactor badge", "黃金反應堆徽章",
            "A cosmetic prestige badge — proof you ran the plant hard.", "純裝飾的榮譽徽章 — 證明你狠狠操過部機。", 500),
        new("perk.mine.turbo", "Compute Mine: turbo rigs (+25% hashrate)", "運算礦場：渦輪礦機（算力 +25%）",
            "Unlocks a permanent +25% hashrate boost in the Compute Mine.", "喺運算礦場永久解鎖 +25% 算力。", 1200),
        new("perk.collider.priority", "Collider: priority beam time", "對撞機：優先束流時間",
            "Unlocks faster energy ramp + higher luminosity in the Particle Collider.", "喺粒子對撞機解鎖更快升能同更高亮度。", 1500),
        new("perk.smelter.preheat", "Smelter: pre-heaters (freeze-proof)", "冶煉廠：預熱器（防凍結）",
            "Unlocks freeze-resistant pots in the Aluminium Smelter.", "喺鋁冶煉廠解鎖抗凍結電解槽。", 1800),
        new("perk.aicluster.overclock", "AI Cluster: overclock (+30% throughput)", "AI 叢集：超頻（吞吐 +30%）",
            "Unlocks a permanent +30% training throughput in the AI Training Cluster.", "喺 AI 訓練叢集永久解鎖 +30% 訓練吞吐。", 2000),
    };

    public ReactorBankModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        ReactorEconomyService.I.Changed += OnEconomyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        Render();
        BuildStore();
        RefreshBalance();
        RefreshLedger();
        _lastTick = DateTime.UtcNow;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        Loc.I.LanguageChanged -= OnLang;
        ReactorEconomyService.I.Changed -= OnEconomyChanged;
    }

    private void OnLang(object? s, EventArgs e) { Render(); BuildStore(); RefreshBalance(); RefreshLedger(); }
    private void OnEconomyChanged() { try { DispatcherQueue.TryEnqueue(() => { RefreshBalance(); RefreshLedger(); RefreshStoreStates(); }); } catch { } }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Reactor Bank · 反應堆銀行", "反應堆銀行 · Reactor Bank");
        HeaderBlurb.Text = P(
            "Watts (⚡) are minted from the reactor's live electrical output and earned from the reactor-powered loads (grid, mining, smelting, hydrogen, compute…). Spend them below to unlock features across those modules.",
            "瓦特幣（⚡）由反應堆嘅實時電力鑄造，亦可從各個核電負載（電網、挖礦、冶煉、制氫、運算…）賺取。喺下面用嚟解鎖各模組嘅功能。");
        BalanceCaption.Text = P("Balance", "餘額");
        BalanceUnit.Text = P("Watts", "瓦特幣");
        StoreTitle.Text = P("Store — features that require Watts", "商店 — 需要瓦特幣嘅功能");
        StoreBlurb.Text = P("Each unlock is one-time and persists. The powered modules read these to enable their boosts.",
            "每個解鎖係一次性並會保存。相關模組會讀取呢啲解鎖去啟用加成。");
        LedgerTitle.Text = P("Ledger", "賬簿");
        LedgerEmpty.Text = P("No transactions yet. Open the reactor and a load module to start minting Watts.",
            "未有交易。開啟反應堆同一個負載模組就會開始鑄造瓦特幣。");
        FooterNote.Text = P("Tip: Watts mint only while the reactor is actually generating (not in MODE 5, scram or meltdown).",
            "提示：只有喺反應堆真正發電時先會鑄造瓦特幣（MODE 5、急停或熔毀時唔會）。");
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            var now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 2);
            _lastTick = now;

            var snap = ReactorStatusApiService.I.LastSnapshot; // value struct — always present
            double mw = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && mw > 1 && !snap.IsScrammed && !snap.IsMeltdown && !coldMode;

            ReactorMeter.Value = Math.Clamp(mw, 0, 1150);
            ReactorMeterCaption.Text = P($"Reactor output: {mw:N0} MWe · {mode}", $"反應堆輸出：{mw:N0} MWe · {mode}");

            double minted = ReactorEconomyService.I.MintFromPower(mw, dt, generating);
            OfflineBar.IsOpen = !generating;
            if (!generating)
                OfflineBar.Message = P("Reactor not generating — no Watts are being minted. Start it up to mint.",
                    "反應堆冇發電 — 冇鑄造瓦特幣。啟動反應堆先會鑄幣。");

            double perSec = generating ? mw * ReactorEconomyService.MintPerMWSecond : 0;
            MintRateText.Text = generating
                ? P($"Minting ≈ {perSec:N2} ⚡/s from {mw:N0} MWe", $"正以 {mw:N0} MWe 鑄造 ≈ {perSec:N2} ⚡/秒")
                : P("Minting paused — reactor offline.", "鑄幣暫停 — 反應堆離線。");
        }
        catch { /* never throw from the tick */ }
    }

    private void RefreshBalance()
    {
        try { BalanceText.Text = ReactorEconomyService.I.Balance.ToString("N1", CultureInfo.InvariantCulture); } catch { }
    }

    private void RefreshLedger()
    {
        try
        {
            var items = ReactorEconomyService.I.Ledger;
            LedgerList.ItemsSource = items;
            LedgerEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }

    // ─────────────── store ───────────────

    private readonly Dictionary<string, Button> _buyButtons = new();

    private void BuildStore()
    {
        StoreHost.Children.Clear();
        _buyButtons.Clear();
        foreach (var perk in Perks)
        {
            var card = new StackPanel { Spacing = 4 };
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Child = card,
            };

            var top = new Grid { ColumnSpacing = 12 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Spacing = 2 };
            titleStack.Children.Add(new TextBlock { Text = P(perk.En, perk.Zh), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
            titleStack.Children.Add(new TextBlock { Text = P(perk.DescEn, perk.DescZh), FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            Grid.SetColumn(titleStack, 0);
            top.Children.Add(titleStack);

            var buy = new Button { VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
            var perkLocal = perk;
            buy.Click += (_, _) => BuyPerk(perkLocal);
            Grid.SetColumn(buy, 1);
            top.Children.Add(buy);

            card.Children.Add(top);
            _buyButtons[perk.Id] = buy;
            StoreHost.Children.Add(border);
        }
        RefreshStoreStates();
    }

    private void RefreshStoreStates()
    {
        foreach (var perk in Perks)
        {
            if (!_buyButtons.TryGetValue(perk.Id, out var buy)) continue;
            bool owned = ReactorEconomyService.I.IsUnlocked(perk.Id);
            bool afford = ReactorEconomyService.I.CanAfford(perk.Cost);
            if (owned)
            {
                buy.Content = P("Owned ✓", "已擁有 ✓");
                buy.IsEnabled = false;
            }
            else
            {
                buy.Content = P($"Buy · {perk.Cost:N0} ⚡", $"購買 · {perk.Cost:N0} ⚡");
                buy.IsEnabled = afford;
            }
        }
    }

    private void BuyPerk(Perk perk)
    {
        try
        {
            var reason = P($"Unlocked: {perk.En}", $"已解鎖：{perk.Zh}");
            bool ok = ReactorEconomyService.I.Unlock(perk.Id, perk.Cost, reason);
            if (!ok)
                OfflineBar.Message = P("Not enough Watts yet — keep the reactor generating.", "瓦特幣唔夠 — 繼續發電賺取。");
            RefreshStoreStates();
        }
        catch { }
    }
}
