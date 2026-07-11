using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 隨機工具箱 · Randomizer toolkit — cryptographically strong integers, coin flip (with tally),
/// dice roller (NdM±k), and a list picker/shuffle (Fisher–Yates). All randomness via
/// System.Security.Cryptography.RandomNumberGenerator; never System.Random. Bilingual, robust.
/// </summary>
public sealed partial class RandomizerModule : Page
{
    private int _heads;
    private int _tails;

    public RandomizerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Randomizer · 隨機工具箱";
            HeaderBlurb.Text = P(
                "Cryptographically strong, unbiased randomness — integers, coin flips, dice and list picking. Everything uses a secure RNG, not System.Random.",
                "用加密級、無偏差嘅隨機數 — 整數、擲銀仔、擲骰同抽名單。全部行安全 RNG，唔用 System.Random。");
            StatusText.Text = P("Ready.", "準備就緒。");

            IntTitle.Text = P("Random integers", "隨機整數");
            IntMinLabel.Text = P("Min", "最細");
            IntMaxLabel.Text = P("Max", "最大");
            IntCountLabel.Text = P("Count", "數量");
            IntUniqueChk.Content = P("No duplicates (unique)", "唔重複（唯一）");
            IntGenBtn.Content = P("Generate", "產生");
            IntCopyBtn.Content = P("Copy", "複製");

            CoinTitle.Text = P("Coin flip", "擲銀仔");
            CoinFlipBtn.Content = P("Flip", "擲");
            CoinResetBtn.Content = P("Reset tally", "重設計數");
            UpdateCoinTally();

            DiceTitle.Text = P("Dice roller", "擲骰");
            DiceRollBtn.Content = P("Roll", "擲");

            ListTitle.Text = P("List picker", "名單抽籤");
            ListHint.Text = P("One item per line.", "每行一項。");
            ListPickBtn.Content = P("Pick one", "抽一個");
            ListShuffleBtn.Content = P("Shuffle all", "全部打亂");
            ListCopyBtn.Content = P("Copy", "複製");
        }
        catch { /* never throw from UI render */ }
    }

    private void SetStatus(string en, string zh) => StatusText.Text = P(en, zh);

    private static void Copy(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text ?? string.Empty);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch { /* clipboard may be busy; ignore */ }
    }

    // ---- Random integers ----
    private void IntGen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int min = (int)(double.IsNaN(IntMinBox.Value) ? 0 : IntMinBox.Value);
            int max = (int)(double.IsNaN(IntMaxBox.Value) ? 0 : IntMaxBox.Value);
            int count = (int)(double.IsNaN(IntCountBox.Value) ? 1 : IntCountBox.Value);
            bool unique = IntUniqueChk.IsChecked == true;
            if (count <= 0) { SetStatus("Count must be at least 1.", "數量最少要 1。"); return; }

            if (min > max) (min, max) = (max, min);
            long span = (long)max - min + 1;
            if (unique && count > span)
            {
                SetStatus("Not enough unique values in that range — turn off ‘unique’ or widen the range.",
                    "個範圍冇咁多個唯一值 — 關咗『唯一』或者擴闊範圍。");
                return;
            }

            var values = RandomizerService.Integers(min, max, count, unique);
            IntResult.Text = RandomizerService.Join(values);
            SetStatus($"Generated {values.Count} integer(s).", $"產生咗 {values.Count} 個整數。");
        }
        catch { SetStatus("Could not generate integers.", "產生整數失敗。"); }
    }

    private void IntCopy_Click(object sender, RoutedEventArgs e)
    {
        Copy(IntResult.Text);
        SetStatus("Copied.", "已複製。");
    }

    // ---- Coin flip ----
    private void CoinFlip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool heads = RandomizerService.CoinFlip();
            if (heads) _heads++; else _tails++;
            CoinResult.Text = heads ? P("Heads", "公") : P("Tails", "字");
            UpdateCoinTally();
        }
        catch { SetStatus("Coin flip failed.", "擲銀仔失敗。"); }
    }

    private void CoinReset_Click(object sender, RoutedEventArgs e)
    {
        _heads = 0; _tails = 0;
        CoinResult.Text = string.Empty;
        UpdateCoinTally();
    }

    private void UpdateCoinTally()
    {
        try
        {
            int total = _heads + _tails;
            CoinTally.Text = P(
                $"Heads: {_heads}   Tails: {_tails}   (of {total})",
                $"公：{_heads}   字：{_tails}   （共 {total} 次）");
        }
        catch { }
    }

    // ---- Dice roller ----
    private void DiceRoll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = RandomizerService.RollDice(DiceBox.Text);
            if (!r.Ok)
            {
                DiceResult.Text = string.Empty;
                if (r.Error == "empty")
                    SetStatus("Enter dice notation, e.g. 2d6 or 1d20+3.", "輸入骰仔寫法，例如 2d6 或 1d20+3。");
                else
                    SetStatus("Bad dice notation — try like 2d6, 1d20+3 or 4d8-2.", "骰仔寫法有錯 — 試下 2d6、1d20+3 或 4d8-2。");
                return;
            }

            string rolls = RandomizerService.Join(r.Rolls);
            string mod = r.Modifier == 0 ? "" : (r.Modifier > 0 ? $" + {r.Modifier}" : $" - {-(long)r.Modifier}");
            DiceResult.Text = P(
                $"Rolls: {rolls}{mod}\nTotal: {r.Total}",
                $"每粒：{rolls}{mod}\n總和：{r.Total}");
            SetStatus($"Rolled {r.Count}d{r.Sides}.", $"擲咗 {r.Count}d{r.Sides}。");
        }
        catch { SetStatus("Could not roll the dice.", "擲骰失敗。"); }
    }

    // ---- List picker ----
    private void ListPick_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = RandomizerService.SplitLines(ListInput.Text);
            if (items.Count == 0) { SetStatus("Add at least one item (one per line).", "最少加一項（每行一個）。"); return; }
            string? pick = RandomizerService.PickOne(items);
            ListResult.Text = pick ?? string.Empty;
            SetStatus($"Picked 1 of {items.Count}.", $"喺 {items.Count} 個入面抽咗 1 個。");
        }
        catch { SetStatus("Could not pick an item.", "抽籤失敗。"); }
    }

    private void ListShuffle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = RandomizerService.SplitLines(ListInput.Text);
            if (items.Count == 0) { SetStatus("Add at least one item (one per line).", "最少加一項（每行一個）。"); return; }
            List<string> shuffled = RandomizerService.Shuffle(items);
            ListResult.Text = string.Join(Environment.NewLine, shuffled);
            SetStatus($"Shuffled {shuffled.Count} item(s).", $"打亂咗 {shuffled.Count} 項。");
        }
        catch { SetStatus("Could not shuffle the list.", "打亂失敗。"); }
    }

    private void ListCopy_Click(object sender, RoutedEventArgs e)
    {
        Copy(ListResult.Text);
        SetStatus("Copied.", "已複製。");
    }
}
