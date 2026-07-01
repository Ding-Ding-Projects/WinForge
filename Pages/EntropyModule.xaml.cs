using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 熵值分析（隨機度分析）· Entropy / randomness analyzer. Type text, choose how to interpret it
/// (UTF-8 bytes / raw chars / hex bytes), and see Shannon entropy, total information, theoretical
/// max, % of max, symbol counts, a chi-square uniformity stat and a top-N frequency histogram.
/// Live on change, copyable report. Pure managed, robust — never throws. Bilingual (粵語).
/// </summary>
public sealed partial class EntropyModule : Page
{
    private bool _suppress;

    public EntropyModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Entropy Analyzer · 熵值分析";
        HeaderBlurb.Text = P("Measure the randomness of any text or byte stream. Computes Shannon entropy (bits per symbol), total information, the theoretical maximum, and a chi-square uniformity check — with a live frequency histogram.",
            "量度任何文字或者位元組串嘅隨機度。計 Shannon 熵（每個符號幾多 bit）、總資訊量、理論上限，仲有卡方均勻度檢定 — 附即時頻率直方圖。");

        ModeLabel.Text = P("Interpret input as", "點樣解讀輸入");
        InputBox.PlaceholderText = P("Type or paste text here…", "喺度打字或者貼上文字…");
        MetricsTitle.Text = P("Metrics", "指標");
        HistoTitle.Text = P("Frequency histogram (top symbols)", "頻率直方圖（最常見符號）");
        CopyButton.Content = P("Copy report", "複製報告");

        // Rebuild the ComboBox items with localized labels, preserving the selected index.
        _suppress = true;
        int idx = ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(P("UTF-8 bytes", "UTF-8 位元組"));
        ModeBox.Items.Add(P("Raw characters", "原始字元"));
        ModeBox.Items.Add(P("Hex bytes", "十六進位位元組"));
        ModeBox.SelectedIndex = idx;
        _suppress = false;
    }

    private EntropyService.Interpretation CurrentMode()
        => ModeBox.SelectedIndex switch
        {
            1 => EntropyService.Interpretation.RawChars,
            2 => EntropyService.Interpretation.HexBytes,
            _ => EntropyService.Interpretation.Utf8Bytes,
        };

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private EntropyService.Report? _last;

    private void Recompute()
    {
        try
        {
            var report = EntropyService.Analyze(InputBox.Text, CurrentMode());
            _last = report;

            if (!report.Ok)
            {
                HistoList.ItemsSource = null;
                MetricsText.Text = "—";
                StatusText.Text = report.Error switch
                {
                    "__EMPTY__" => P("Enter some text to analyze.", "輸入啲文字先可以分析。"),
                    "__BADHEX__" => P("Invalid hex — use only 0-9 and A-F (spaces allowed).", "十六進位無效 — 只可以用 0-9 同 A-F（可以有空格）。"),
                    "__ODDHEX__" => P("Hex needs an even number of digits (two per byte).", "十六進位要雙數位（每個位元組兩位）。"),
                    _ => P("Could not analyze the input.", "無法分析輸入。"),
                };
                return;
            }

            MetricsText.Text = BuildMetrics(report);
            HistoList.ItemsSource = report.Top;
            StatusText.Text = P($"{report.Count:N0} symbols · {report.Unique:N0} unique · {report.PercentOfMax:0.0}% of maximum randomness.",
                $"{report.Count:N0} 個符號 · {report.Unique:N0} 個唔同 · 達到理論最大隨機度嘅 {report.PercentOfMax:0.0}%。");
        }
        catch (Exception ex)
        {
            // Defensive: the service never throws, but the UI layer must never crash either.
            HistoList.ItemsSource = null;
            MetricsText.Text = "—";
            StatusText.Text = P("Something went wrong: ", "出咗問題：") + ex.Message;
        }
    }

    private string BuildMetrics(EntropyService.Report r)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(P("Shannon entropy   : ", "Shannon 熵        ：")).AppendLine(r.Entropy.ToString("0.0000", inv) + P(" bits/symbol", " bit/符號"));
        sb.Append(P("Total information : ", "總資訊量          ：")).AppendLine(r.TotalInfo.ToString("0.0", inv) + P(" bits", " bit"));
        sb.Append(P("Max entropy       : ", "最大熵            ：")).AppendLine(r.MaxEntropy.ToString("0.0000", inv) + P($" bits (alphabet {r.AlphabetSize})", $" bit（字母表 {r.AlphabetSize}）"));
        sb.Append(P("% of maximum      : ", "佔最大百分比      ：")).AppendLine(r.PercentOfMax.ToString("0.00", inv) + "%");
        sb.Append(P("Symbols / unique  : ", "符號 / 唔同        ：")).AppendLine($"{r.Count:N0} / {r.Unique:N0}");
        sb.Append(P("Chi-square (unif.): ", "卡方（均勻度）    ：")).Append(r.ChiSquare.ToString("0.00", inv));
        return sb.ToString();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _last;
            if (r == null || !r.Ok)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時無嘢可以複製。");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(P("WinForge — Entropy report", "WinForge — 熵值報告"));
            sb.AppendLine(BuildMetrics(r));
            sb.AppendLine();
            sb.AppendLine(P("Symbol\tCount\tPercent", "符號\t數量\t百分比"));
            foreach (var row in r.Top)
                sb.AppendLine($"{row.Symbol}\t{row.Count}\t{row.PercentText}");

            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy,
            };
            dp.SetText(sb.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            StatusText.Text = P("Report copied to the clipboard.", "報告已經複製咗去剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }
}
