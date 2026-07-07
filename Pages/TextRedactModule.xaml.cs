using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字遮蔽 / 個資遮罩 · Text redactor / PII masker — paste text, tick which categories to
/// detect (email, phone, credit card, IPv4, long digit runs), pick a mask style, and get
/// masked output live with a per-category count. Pure-managed regex (1s timeout). Bilingual,
/// never throws. Detection is best-effort, not a guarantee.
/// </summary>
public sealed partial class TextRedactModule : Page
{
    private bool _suppress;

    public TextRedactModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Apply();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Text Redactor", "文字遮蔽");
        HeaderBlurb.Text = P("Paste any text and mask the personal info inside it — emails, phone numbers, card numbers, IP addresses and long ID digits — before you share a log or screenshot.",
            "貼上任何文字，遮蔽入面嘅個人資料 — 電郵、電話、卡號、IP 位址同埋一長串 ID 數字 — 分享 log 或者截圖之前先用。");

        DetectLabel.Text = P("Detect and mask", "偵測並遮蔽");
        EmailChk.Content = P("Email addresses", "電郵地址");
        PhoneChk.Content = P("Phone numbers", "電話號碼");
        CardChk.Content = P("Credit-card numbers (13–16 digits)", "信用卡號碼（13–16 位數）");
        IpChk.Content = P("IPv4 addresses", "IPv4 位址");
        DigitsChk.Content = P("Long digit runs (IDs, 7+ digits)", "一長串數字（ID，7 位以上）");

        StyleLabel.Text = P("Mask style", "遮蔽方式");
        InputLabel.Text = P("Input text", "輸入文字");
        OutputLabel.Text = P("Masked output", "遮蔽結果");
        CopyButton.Content = P("Copy output", "複製結果");

        DisclaimerText.Text = P("Note: detection is best-effort using patterns — it is not guaranteed to catch every piece of sensitive data. Review the result before sharing.",
            "備註：偵測係用規則盡力而為，唔保證捉到每一項敏感資料。分享之前請自行檢查結果。");

        RebuildStyleCombo();
        Apply();
    }

    private void RebuildStyleCombo()
    {
        _suppress = true;
        int idx = StyleCombo.SelectedIndex;
        if (idx < 0) idx = 0;
        StyleCombo.Items.Clear();
        StyleCombo.Items.Add(P("Full asterisks (****)", "全部星號（****）"));
        StyleCombo.Items.Add(P("[REDACTED] label", "[REDACTED] 標籤"));
        StyleCombo.Items.Add(P("Keep last 4 characters", "保留最後 4 個字元"));
        StyleCombo.SelectedIndex = Math.Min(idx, StyleCombo.Items.Count - 1);
        _suppress = false;
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        Apply();
    }

    private void Style_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Apply();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Apply();
    }

    private TextRedactService.MaskStyle CurrentStyle() => StyleCombo.SelectedIndex switch
    {
        1 => TextRedactService.MaskStyle.Redacted,
        2 => TextRedactService.MaskStyle.KeepLast4,
        _ => TextRedactService.MaskStyle.Asterisks,
    };

    private void Apply()
    {
        // Guard against calls before controls are ready (e.g. very early Render()).
        if (InputBox is null || OutputBox is null || StatusText is null) return;

        try
        {
            var enabled = new List<TextRedactService.Category>();
            if (EmailChk.IsChecked == true) enabled.Add(TextRedactService.Category.Email);
            if (PhoneChk.IsChecked == true) enabled.Add(TextRedactService.Category.Phone);
            if (CardChk.IsChecked == true) enabled.Add(TextRedactService.Category.CreditCard);
            if (IpChk.IsChecked == true) enabled.Add(TextRedactService.Category.Ipv4);
            if (DigitsChk.IsChecked == true) enabled.Add(TextRedactService.Category.LongDigits);

            var result = TextRedactService.Redact(InputBox.Text, enabled, CurrentStyle());
            OutputBox.Text = result.Output;
            UpdateStatus(result);
        }
        catch
        {
            // Absolute belt-and-braces: the UI must never crash from a redaction pass.
            StatusText.Text = P("Something went wrong while masking — output may be incomplete.",
                "遮蔽途中出咗問題 — 結果可能唔完整。");
        }
    }

    private void UpdateStatus(TextRedactService.Result r)
    {
        if (r.TimedOut)
        {
            StatusText.Text = P("Matching timed out on some text (over 1 second) — those parts were left unmasked. Try shorter input.",
                "部分文字比對超時（超過 1 秒）— 嗰啲部分無遮到。試下短啲嘅輸入。");
            return;
        }
        if (r.Failed)
        {
            StatusText.Text = P("A detection pattern failed — some categories may be incomplete.",
                "有偵測規則失敗 — 部分類別可能唔完整。");
            return;
        }
        if (string.IsNullOrEmpty(InputBox.Text))
        {
            StatusText.Text = P("Paste some text above to begin.", "喺上面貼啲文字先。");
            return;
        }
        if (r.Total == 0)
        {
            StatusText.Text = P("No matches found for the selected categories.", "揀咗嘅類別搵唔到任何符合項目。");
            return;
        }

        int email = Count(r, TextRedactService.Category.Email);
        int phone = Count(r, TextRedactService.Category.Phone);
        int card = Count(r, TextRedactService.Category.CreditCard);
        int ip = Count(r, TextRedactService.Category.Ipv4);
        int digits = Count(r, TextRedactService.Category.LongDigits);

        StatusText.Text = P(
            $"Masked {r.Total} item(s) — email {email}, phone {phone}, card {card}, IPv4 {ip}, digits {digits}.",
            $"已遮蔽 {r.Total} 項 — 電郵 {email}、電話 {phone}、卡號 {card}、IPv4 {ip}、數字 {digits}。");
    }

    private static int Count(TextRedactService.Result r, TextRedactService.Category c)
        => r.Counts.TryGetValue(c, out int n) ? n : 0;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(OutputBox.Text ?? string.Empty);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied masked output to the clipboard.", "已複製遮蔽結果到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Couldn't copy to the clipboard.", "無法複製到剪貼簿。");
        }
    }
}
