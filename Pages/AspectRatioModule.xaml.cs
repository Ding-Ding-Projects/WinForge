using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 長寬比計算 · Aspect-ratio calculator — simplify a resolution to its GCD ratio (16:9),
/// preset-seeded ratio locking, and ratio-preserving scaling. Shows decimal + megapixels.
/// Pure managed, robust (guards zero/negative). Bilingual. No redirect.
/// </summary>
public sealed partial class AspectRatioModule : Page
{
    private bool _suppress;
    // Current locked ratio (seeded by simplify / preset), used by the scale section.
    private double _ratioW = 16;
    private double _ratioH = 9;

    // Common presets (label, w, h).
    private static readonly (string Label, double W, double H)[] Presets =
    {
        ("16:9", 16, 9),
        ("16:10", 16, 10),
        ("4:3", 4, 3),
        ("21:9", 21, 9),
        ("32:9", 32, 9),
        ("1:1", 1, 1),
        ("3:2", 3, 2),
        ("2:3", 2, 3),
        ("9:16", 9, 16),
    };

    public AspectRatioModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SeedPresets();
        Render();
        RecomputeSimplify();
        RecomputeScale();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void SeedPresets()
    {
        _suppress = true;
        try
        {
            PresetCombo.Items.Clear();
            foreach (var p in Presets) PresetCombo.Items.Add(p.Label);
            PresetCombo.SelectedIndex = 0;
        }
        catch { /* never throw */ }
        _suppress = false;
    }

    private void Render()
    {
        try
        {
            Header.Title = "Aspect Ratio · 長寬比計算";
            HeaderBlurb.Text = P("Simplify a resolution to its aspect ratio, or scale a size while keeping the ratio locked. Shows the decimal ratio and megapixels.",
                "將解析度化簡做長寬比，或者鎖住比例嚟縮放尺寸。仲會顯示小數比同埋百萬像素。");

            SimplifyTitle.Text = P("Simplify a resolution", "化簡解析度");
            WidthLabel.Text = P("Width", "闊度");
            HeightLabel.Text = P("Height", "高度");
            CopyBtn.Content = P("Copy result", "複製結果");

            PresetTitle.Text = P("Common presets", "常用預設");
            PresetLabel.Text = P("Seed a ratio", "載入比例");

            ScaleTitle.Text = P("Scale (ratio locked)", "縮放（鎖定比例）");
            ScaleBlurb.Text = P("Type a target width to get the matching height, or a target height to get the width — using the ratio above.",
                "輸入目標闊度會計出對應高度，輸入目標高度會計出闊度 — 用上面嘅比例。");
            TargetWidthLabel.Text = P("Target width", "目標闊度");
            TargetHeightLabel.Text = P("Target height", "目標高度");

            RecomputeSimplify();
            RecomputeScale();
        }
        catch { /* never throw */ }
    }

    // ── Simplify section ──────────────────────────────────────────────
    private void Simplify_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        RecomputeSimplify();
    }

    private void RecomputeSimplify()
    {
        try
        {
            double w = Val(WidthBox);
            double h = Val(HeightBox);

            if (AspectRatioService.Simplify(w, h, out long rw, out long rh))
            {
                RatioText.Text = $"{rw}:{rh}";
                // Adopt this as the locked ratio for scaling & keep the preset in sync.
                _ratioW = rw;
                _ratioH = rh;
                SyncPresetToRatio(rw, rh);

                double dec = AspectRatioService.DecimalRatio(w, h);
                double mp = AspectRatioService.Megapixels(w, h);
                DetailText.Text = P(
                    $"Decimal {dec:0.####} · {mp:0.##} MP ({w:0}×{h:0})",
                    $"小數 {dec:0.####} · {mp:0.##} 百萬像素（{w:0}×{h:0}）");
                Status(P("Ratio simplified.", "比例已化簡。"));
            }
            else
            {
                RatioText.Text = "—";
                DetailText.Text = P("Enter a positive width and height.", "請輸入正數嘅闊度同高度。");
                Status(P("Waiting for a valid width and height.", "等緊有效嘅闊度同高度。"));
            }
        }
        catch { /* never throw */ }
    }

    // ── Preset section ────────────────────────────────────────────────
    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            int i = PresetCombo.SelectedIndex;
            if (i < 0 || i >= Presets.Length) return;
            var p = Presets[i];
            _ratioW = p.W;
            _ratioH = p.H;
            RecomputeScale();
            Status(P($"Ratio seeded to {p.Label}.", $"已載入比例 {p.Label}。"));
        }
        catch { /* never throw */ }
    }

    private void SyncPresetToRatio(long rw, long rh)
    {
        try
        {
            int match = -1;
            for (int i = 0; i < Presets.Length; i++)
            {
                if ((long)Presets[i].W == rw && (long)Presets[i].H == rh) { match = i; break; }
            }
            _suppress = true;
            PresetCombo.SelectedIndex = match; // -1 clears selection when no preset matches
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    // ── Scale section ─────────────────────────────────────────────────
    private void TargetWidth_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        RecomputeScale();
    }

    private void TargetHeight_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        RecomputeScaleFromHeight();
    }

    private void RecomputeScale()
    {
        try
        {
            double tw = Val(TargetWidthBox);
            double h = AspectRatioService.HeightForWidth(_ratioW, _ratioH, tw);
            ScaledHeightText.Text = double.IsNaN(h)
                ? "—"
                : P($"→ height {h:0.##}", $"→ 高度 {h:0.##}");

            double th = Val(TargetHeightBox);
            double w = AspectRatioService.WidthForHeight(_ratioW, _ratioH, th);
            ScaledWidthText.Text = double.IsNaN(w)
                ? "—"
                : P($"→ width {w:0.##}", $"→ 闊度 {w:0.##}");
        }
        catch { /* never throw */ }
    }

    private void RecomputeScaleFromHeight()
    {
        // Same math; kept as a distinct handler so height edits update independently.
        RecomputeScale();
    }

    // ── Copy ──────────────────────────────────────────────────────────
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double w = Val(WidthBox);
            double h = Val(HeightBox);
            if (!AspectRatioService.Simplify(w, h, out long rw, out long rh))
            {
                Status(P("Nothing to copy — enter a valid resolution.", "冇嘢可以複製 — 請輸入有效解析度。"));
                return;
            }
            double dec = AspectRatioService.DecimalRatio(w, h);
            double mp = AspectRatioService.Megapixels(w, h);
            string text = $"{w:0}×{h:0}  =  {rw}:{rh}  ({dec:0.####}, {mp:0.##} MP)";

            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            Status(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch
        {
            Status(P("Couldn't copy to the clipboard.", "複製到剪貼簿失敗。"));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static double Val(NumberBox box)
    {
        double v = box.Value;
        return double.IsNaN(v) ? 0 : v;
    }

    private void Status(string msg)
    {
        try { StatusText.Text = msg; } catch { /* never throw */ }
    }
}
