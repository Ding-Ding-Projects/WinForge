using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 媒體模組 · In-app Media module: wraps ffmpeg/ffprobe — convert, trim, GIF, grab frames, inspect, plus ~60 ops.
/// Browse uses the Win32 file dialogs so it works whether or not WinForge runs elevated.
///
/// 每個進階操作用手砌嘅控件列渲染（唔再用 TweakCard）：左邊雙語標題／說明，右邊對應控件。
/// Each advanced operation is rendered as a hand-built control row (no TweakCard): bilingual
/// title/description on the left, the matching WinUI control on the right.
/// </summary>
public sealed partial class MediaModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _busy;
    private bool _rowBusy;

    private static readonly string[] MediaExts =
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".flv", ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus" };

    public MediaModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildQuickOps();
        PopulateOps(string.Empty);
        RefreshSelection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildQuickOps();
        PopulateOps(OpsFilter?.Text ?? string.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Media · 媒體";
        HeaderBlurb.Text = P("Convert, trim, make GIFs, grab frames and inspect video/audio with ffmpeg — all in-app.",
            "用 ffmpeg 轉檔、剪裁、整 GIF、擷取畫格、檢視影片／音訊 — 全部喺 app 內。");
        SelLabel.Text = P("Files", "檔案");
        InCap.Text = P("Input", "輸入");
        OutCap.Text = P("Output", "輸出");
        InputBtn.Content = P("Open…", "開啟…");
        OutputBtn.Content = P("Save as…", "另存…");
        QuickLabel.Text = P("Quick conversions", "快速轉檔");
        TrimLabel.Text = P("Trim (start + length, HH:MM:SS)", "剪裁（開始 + 長度，HH:MM:SS）");
        TrimCopyBtn.Content = P("Trim (no re-encode)", "剪裁（唔重編碼）");
        TrimEncodeBtn.Content = P("Trim (re-encode)", "剪裁（重編碼）");
        GifLabel.Text = P("GIF / frame (fps · width)", "GIF／畫格（fps · 闊度）");
        GifBtn.Content = P("Make GIF", "整 GIF");
        FrameBtn.Content = P("Grab frame", "擷取畫格");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
        AdvancedHeader.Text = P($"Advanced operations ({(_ops ??= MediaOperations.All().ToList()).Count})",
            $"進階操作（{(_ops ??= MediaOperations.All().ToList()).Count}）");

        if (!MediaService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Click to install ffmpeg automatically (winget) — no restart needed.",
                "撳一下自動安裝 ffmpeg（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                () => { Render(); return Task.CompletedTask; }, MediaService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; }
    }

    private void RefreshSelection()
    {
        InputBox.Text = AppState.CurrentMediaInput;
        OutputBox.Text = AppState.CurrentMediaOutput;
    }

    private void BuildQuickOps()
    {
        QuickOps.Children.Clear();
        AddQuick(P("To MP4", "轉 MP4"), () => MediaService.Quick(".converted.mp4", "-i {in} -c:v libx264 -c:a aac -movflags +faststart {out}"));
        AddQuick(P("To WebM", "轉 WebM"), () => MediaService.Quick(".webm", "-i {in} -c:v libvpx-vp9 -b:v 0 -crf 32 -c:a libopus {out}"));
        AddQuick(P("To MKV", "轉 MKV"), () => MediaService.Quick(".mkv", "-i {in} -c copy {out}"));
        AddQuick(P("Extract MP3", "抽 MP3"), () => MediaService.Quick(".mp3", "-i {in} -vn -c:a libmp3lame -q:a 2 {out}"));
        AddQuick(P("Extract WAV", "抽 WAV"), () => MediaService.Quick(".wav", "-i {in} -vn -c:a pcm_s16le {out}"));
        AddQuick(P("GIF", "GIF"), () => MediaService.Quick(".gif", "-i {in} -vf \"fps=12,scale=480:-1:flags=lanczos\" {out}"));
        AddQuick(P("Compress", "壓細"), () => MediaService.Quick(".compressed.mp4", "-i {in} -c:v libx264 -crf 28 -c:a aac {out}"));
        AddQuick(P("Mute", "靜音"), () => MediaService.Quick(".muted.mp4", "-i {in} -c:v copy -an {out}"));
        AddQuick(P("Normalize audio", "正規化音量"), () => MediaService.Quick(".norm.mp4", "-i {in} -af loudnorm -c:v copy {out}"));
        AddQuick(P("Info", "資訊"), () => MediaService.Info());
    }

    private void AddQuick(string label, Func<Task<TweakResult>> run)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) => await RunAndShow(btn, run);
        QuickOps.Children.Add(btn);
    }

    private async Task RunAndShow(Button btn, Func<Task<TweakResult>> run)
    {
        if (_busy) return;
        _busy = true;
        var label = btn.Content;
        btn.IsEnabled = false;
        btn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Running ffmpeg…", "執行緊 ffmpeg…");
        try
        {
            var r = await run();
            var head = r.Success ? P("✓ Done", "✓ 完成") : P("✗ Failed", "✗ 失敗");
            var body = string.IsNullOrWhiteSpace(r.Output)
                ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
                : r.Output!;
            OutText.Text = head + "\n" + (body.Length > 4000 ? body[^4000..] : body);
        }
        catch (Exception ex) { OutText.Text = ex.Message; }
        finally { btn.Content = label; btn.IsEnabled = true; _busy = false; RefreshSelection(); }
    }

    private async void PickInput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(MediaExts);
        if (path is null) return;
        AppState.CurrentMediaInput = path;
        RefreshSelection();
        await ShowProbe();
    }

    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("output", ".mp4", ".mp3", ".gif", ".wav", ".webm", ".mkv", ".png");
        if (path is null) return;
        AppState.CurrentMediaOutput = path;
        RefreshSelection();
    }

    private async Task ShowProbe()
    {
        if (!MediaService.HasInput) { InfoBorder.Visibility = Visibility.Collapsed; return; }
        InfoBorder.Visibility = Visibility.Visible;
        InfoText.Text = P("Reading media info…", "讀取媒體資訊緊…");
        try
        {
            var r = await MediaService.Info();
            var body = (r.Output ?? "").Trim();
            InfoText.Text = body.Length == 0 ? P("No info available.", "冇資訊。")
                : (body.Length > 1600 ? body[..1600] + " …" : body);
        }
        catch (Exception ex) { InfoText.Text = ex.Message; }
    }

    private string DeriveBeside(string suffixWithExt)
    {
        var input = MediaService.Input;
        var dir = Path.GetDirectoryName(input) ?? "";
        var name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, name + suffixWithExt);
    }

    private async void TrimCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var ext = Path.GetExtension(MediaService.Input);
        var outp = DeriveBeside($".trimmed{ext}");
        var args = $"-ss {Start()} -i {{in}} -t {Dur()} -c copy {{out}}";
        await RunAndShow((Button)sender, () => MediaService.RunWith(MediaService.Input, outp, args, useProbe: false));
    }

    private async void TrimEncode_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var outp = DeriveBeside(".trimmed.mp4");
        var args = $"-ss {Start()} -i {{in}} -t {Dur()} -c:v libx264 -c:a aac -movflags +faststart {{out}}";
        await RunAndShow((Button)sender, () => MediaService.RunWith(MediaService.Input, outp, args, useProbe: false));
    }

    private async void MakeGif_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        int fps = (int)(double.IsNaN(GifFps.Value) ? 12 : GifFps.Value);
        int w = (int)(double.IsNaN(GifWidth.Value) ? 480 : GifWidth.Value);
        var args = $"-i {{in}} -vf \"fps={fps},scale={w}:-1:flags=lanczos\" {{out}}";
        await RunAndShow((Button)sender, () => MediaService.Quick(".gif", args));
    }

    private async void GrabFrame_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var args = $"-ss {Start()} -i {{in}} -frames:v 1 {{out}}";
        await RunAndShow((Button)sender, () => MediaService.Quick(".frame.png", args));
    }

    private bool Guard()
    {
        if (MediaService.HasInput) return true;
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Pick an input file first.", "請先揀輸入檔。");
        return false;
    }

    private string Start() => string.IsNullOrWhiteSpace(TrimStart.Text) ? "00:00:00" : TrimStart.Text.Trim();
    private string Dur() => string.IsNullOrWhiteSpace(TrimDuration.Text) ? "00:00:10" : TrimDuration.Text.Trim();

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= MediaOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: title + optional secondary title + description
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        var title = new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        text.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(op, result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + live value ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = op.Min,
            Maximum = op.Max,
            StepFrequency = op.Step > 0 ? op.Step : 1,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var unit = op.Unit is null ? "" : " " + op.Unit.Get(Loc.I.Language);
        var valueLabel = new TextBlock { MinWidth = 48, VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = TextAlignment.Right };

        bool suppress = true;
        try { slider.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { slider.Value = op.Min; }
        valueLabel.Text = ((int)slider.Value) + unit;
        suppress = false;

        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = ((int)slider.Value) + unit;
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(slider.Value); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { slider.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { /* ignore */ }
                valueLabel.Text = ((int)slider.Value) + unit;
                suppress = false;
                ShowError(op, ex);
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step > 0 ? op.Step : 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 130,
        };
        bool suppress = true;
        try { box.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, _) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(box.Value)) return;
            try { op.SetNumber(box.Value); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { box.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return box;
    }

    // ---------------- Info → TextBlock (+ refresh) ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Shared result / status area ----------------
    private void ShowResult(TweakDefinition op, TweakResult result)
    {
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = result.Message is null ? string.Empty : result.Message.Get(Loc.I.Language);
        ResultBar.IsOpen = true;

        // Mirror any raw output into the monospace pane (same behaviour as the quick actions).
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            OutBorder.Visibility = Visibility.Visible;
            var body = result.Output!;
            OutText.Text = body.Length > 4000 ? body[^4000..] : body;
        }
    }

    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = P(en, zh);
        ResultBar.IsOpen = true;
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message;
        ResultBar.IsOpen = true;
    }
}
