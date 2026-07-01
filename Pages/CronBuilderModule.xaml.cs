using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Cron 建構器 · Cron expression builder & explainer. Type a 5-field cron expression (or pick a
/// preset), get a bilingual plain-language description and the next 10 fire times. Pure managed —
/// no scheduler is touched, nothing is launched. Bilingual (English + 粵語).
/// </summary>
public sealed partial class CronBuilderModule : Page
{
    private bool _suppress;

    private static readonly (string TagEn, string TagZh, string Expr)[] Presets =
    {
        ("Every minute", "每分鐘", "* * * * *"),
        ("Hourly (top of the hour)", "每小時（正點）", "0 * * * *"),
        ("Daily at midnight", "每日午夜", "0 0 * * *"),
        ("Weekly on Sunday", "每週日", "0 0 * * 0"),
        ("Monthly on the 1st", "每月一號", "0 0 1 * *"),
    };

    public CronBuilderModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Cron Builder · Cron 建構器";
            HeaderBlurb.Text = P(
                "Build and understand a 5-field cron expression (minute hour day-of-month month day-of-week). See a plain-language description and the next 10 times it would fire.",
                "整同睇明一個 5 欄位嘅 cron 運算式（分 時 日 月 星期）。會有白話解釋，同埋列出之後 10 次會執行嘅時間。");

            ExprLabel.Text = P("Cron expression", "Cron 運算式");
            ExprHint.Text = P(
                "Five fields separated by spaces. Tokens: * (any), , (list), - (range), / (step). e.g. */15 9-17 * * 1-5",
                "五個欄位，用空格分開。符號：*（任何）、,（列表）、-（範圍）、/（間隔）。例如 */15 9-17 * * 1-5");
            CopyBtn.Content = P("Copy", "複製");
            PresetLabel.Text = P("Presets", "預設");
            MeaningLabel.Text = P("What it means", "咩意思");
            NextLabel.Text = P("Next 10 fire times", "之後 10 次執行時間");

            RebuildPresets();
            Evaluate();
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Error: ", "錯誤：") + ex.Message;
        }
    }

    private void RebuildPresets()
    {
        _suppress = true;
        try
        {
            object? prev = PresetBox.SelectedItem;
            string? prevExpr = prev is ComboBoxItem ci ? ci.Tag as string : null;

            PresetBox.Items.Clear();
            PresetBox.Items.Add(new ComboBoxItem { Content = P("Choose a preset…", "揀個預設…"), Tag = null });
            foreach (var (en, zh, expr) in Presets)
                PresetBox.Items.Add(new ComboBoxItem { Content = P(en, zh) + "  (" + expr + ")", Tag = expr });

            // Restore selection to the same preset expression if there was one.
            PresetBox.SelectedIndex = 0;
            if (prevExpr != null)
            {
                for (int i = 1; i < PresetBox.Items.Count; i++)
                {
                    if (PresetBox.Items[i] is ComboBoxItem item && (item.Tag as string) == prevExpr)
                    {
                        PresetBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        finally { _suppress = false; }
    }

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (PresetBox.SelectedItem is ComboBoxItem item && item.Tag is string expr)
        {
            _suppress = true;
            ExprBox.Text = expr;
            _suppress = false;
            Evaluate();
        }
    }

    private void Expr_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        // Editing by hand invalidates a chosen preset selection.
        _suppress = true;
        if (PresetBox.Items.Count > 0) PresetBox.SelectedIndex = 0;
        _suppress = false;
        Evaluate();
    }

    private void Evaluate()
    {
        try
        {
            NextList.Items.Clear();
            NextEmpty.Visibility = Visibility.Collapsed;

            var result = CronBuilderService.Parse(ExprBox.Text);
            if (!result.Ok)
            {
                StatusText.Text = P("Invalid: ", "無效：") + P(result.ErrorEn, result.ErrorZh);
                NextEmpty.Visibility = Visibility.Visible;
                NextEmpty.Text = P("No fire times — fix the expression above.", "冇執行時間 — 請修正上面嘅運算式。");
                return;
            }

            StatusText.Text = P(result.DescriptionEn, result.DescriptionZh);

            var now = DateTime.Now;
            List<DateTime> times = CronBuilderService.NextFireTimes(result, now, 10);
            if (times.Count == 0)
            {
                NextEmpty.Visibility = Visibility.Visible;
                NextEmpty.Text = P(
                    "No fire time within the next 4 years.",
                    "未來 4 年內都冇執行時間。");
                return;
            }

            foreach (var t in times)
                NextList.Items.Add(t.ToString("ddd  yyyy-MM-dd  HH:mm", CultureInfo.CurrentCulture));
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Error: ", "錯誤：") + ex.Message;
            NextList.Items.Clear();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(ExprBox.Text ?? "");
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            CopyBtn.Content = P("Copied", "已複製");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }
}
