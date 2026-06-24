using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 履歷 + 求職信寫手 · Resume &amp; Cover-letter Writer.
/// 儲存底稿履歷、貼上職位描述、用一個已安裝嘅 AI 編程代理（Claude／opencode／Codex／Pi）非互動生成
/// 度身履歷 + 求職信，可手動編輯、存入歷史、匯出 .md／.txt／.html。全程雙語、防禦性、無 WinRT picker。
/// Stores base resumes, takes a job description, drives an installed terminal AI agent non-interactively
/// to produce a tailored resume + cover letter in two editable panes, saved to history and exportable
/// to .md / .txt / .html. Bilingual, defensive, no WinRT pickers.
/// </summary>
public sealed partial class ResumeWriterModule : Page
{
    private CancellationTokenSource? _cts;
    private string _currentOutputId = "";   // history id of the loaded output, if any

    public ResumeWriterModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        ResumeStore.Changed += OnStoreChanged;
        Loaded += async (_, _) =>
        {
            Render();
            RefreshBaseCombo();
            FillToneCombo();
            await RefreshAgentsAsync();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            ResumeStore.Changed -= OnStoreChanged;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? s, EventArgs e)
    {
        Render(); FillToneCombo();
    }

    private void OnStoreChanged(object? s, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) RefreshBaseCombo();
        else DispatcherQueue.TryEnqueue(RefreshBaseCombo);
    }

    private void Render()
    {
        HeaderTitle.Text = "Resume & Cover-letter Writer · 履歷與求職信寫手";
        HeaderBlurb.Text = P(
            "Keep one or more base resumes, paste a target job description, and let an installed AI coding agent tailor a resume and matching cover letter. Edit them, save to history, and export.",
            "儲存一份或多份底稿履歷，貼上目標職位描述，由已安裝嘅 AI 編程代理幫你度身訂造履歷同埋配對嘅求職信。可以手動編輯、存入歷史、匯出。");

        BaseLabel.Text = P("Base resume", "底稿履歷");
        BaseNewBtn.Content = P("New", "新增");
        BaseImportBtn.Content = P("Import…", "匯入…");
        BaseRenameBtn.Content = P("Rename", "改名");
        BaseSaveBtn.Content = P("Save", "儲存");
        BaseDeleteBtn.Content = P("Delete", "刪除");
        BaseEditorLabel.Text = P("Base resume content (Markdown)", "底稿履歷內容（Markdown）");
        JdLabel.Text = P("Target job description", "目標職位描述");

        AgentLabel.Text = P("Agent", "代理");
        ToneLabel.Text = P("Tone", "語氣");
        GenerateBtn.Content = P("Generate", "生成");
        CancelBtn.Content = P("Cancel", "取消");
        RegenCoverBtn.Content = P("Cover letter only", "只生成求職信");

        OutputLabel.Text = P("Output title", "輸出標題");
        OutputTitleBox.PlaceholderText = P("e.g. Senior Engineer — Acme", "例如：高級工程師 — Acme");
        SaveHistoryBtn.Content = P("Save to history", "存入歷史");
        ExportResumeBtn.Content = P("Export resume…", "匯出履歷…");
        ExportCoverBtn.Content = P("Export cover…", "匯出求職信…");
        HistoryBtn.Content = P("History…", "歷史…");

        ResumePaneLabel.Text = P("Tailored resume", "度身履歷");
        CoverPaneLabel.Text = P("Cover letter", "求職信");
    }

    // ===================== Base resume library =====================

    private void RefreshBaseCombo()
    {
        var prev = (BaseCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        BaseCombo.SelectionChanged -= BaseCombo_SelectionChanged;
        BaseCombo.Items.Clear();
        foreach (var b in ResumeStore.Bases)
            BaseCombo.Items.Add(new ComboBoxItem { Content = b.Name, Tag = b.Id });
        BaseCombo.SelectionChanged += BaseCombo_SelectionChanged;

        if (BaseCombo.Items.Count == 0)
        {
            BaseEditor.Text = "";
            return;
        }
        var match = BaseCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == prev);
        BaseCombo.SelectedItem = match ?? BaseCombo.Items[0];
    }

    private string? SelectedBaseId => (BaseCombo.SelectedItem as ComboBoxItem)?.Tag as string;

    private void BaseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var id = SelectedBaseId;
        var b = id is null ? null : ResumeStore.GetBase(id);
        BaseEditor.Text = b?.Content ?? "";
    }

    private async void BaseNew_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptTextAsync(P("New base resume", "新底稿履歷"),
            P("Name", "名稱"), P("My resume", "我的履歷"));
        if (name is null) return;
        var entry = ResumeStore.AddBase(name, "");
        if (entry is not null)
        {
            RefreshBaseCombo();
            SelectBase(entry.Id);
        }
    }

    private async void BaseImport_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".md", ".txt", ".markdown", ".text");
        if (path is null) return;
        string text;
        try { text = await File.ReadAllTextAsync(path); }
        catch (Exception ex) { ShowError(ex.Message, $"出錯：{ex.Message}"); return; }

        var name = Path.GetFileNameWithoutExtension(path);
        var entry = ResumeStore.AddBase(string.IsNullOrWhiteSpace(name) ? "Imported" : name, text);
        if (entry is not null)
        {
            RefreshBaseCombo();
            SelectBase(entry.Id);
            ShowOk(P($"Imported “{entry.Name}”.", $"已匯入「{entry.Name}」。"));
        }
    }

    private async void BaseRename_Click(object sender, RoutedEventArgs e)
    {
        var id = SelectedBaseId;
        if (id is null) { ShowError("Pick a base resume first.", "請先揀一份底稿履歷。"); return; }
        var cur = ResumeStore.GetBase(id);
        var name = await PromptTextAsync(P("Rename base resume", "底稿履歷改名"),
            P("Name", "名稱"), cur?.Name ?? "");
        if (name is null) return;
        ResumeStore.UpdateBase(id, name: name);
        RefreshBaseCombo();
        SelectBase(id);
    }

    private void BaseSave_Click(object sender, RoutedEventArgs e)
    {
        var id = SelectedBaseId;
        if (id is null) { ShowError("Pick or create a base resume first.", "請先揀或建立一份底稿履歷。"); return; }
        ResumeStore.UpdateBase(id, content: BaseEditor.Text ?? "");
        ShowOk(P("Base resume saved.", "已儲存底稿履歷。"));
    }

    private async void BaseDelete_Click(object sender, RoutedEventArgs e)
    {
        var id = SelectedBaseId;
        if (id is null) return;
        var b = ResumeStore.GetBase(id);
        var ok = await ConfirmAsync(P("Delete base resume?", "刪除底稿履歷？"),
            P($"Delete “{b?.Name}”? This cannot be undone.", $"刪除「{b?.Name}」？此操作無法復原。"));
        if (!ok) return;
        ResumeStore.RemoveBase(id);
        RefreshBaseCombo();
    }

    private void SelectBase(string id)
    {
        var match = BaseCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == id);
        if (match is not null) BaseCombo.SelectedItem = match;
    }

    // ===================== Tone + agents =====================

    private void FillToneCombo()
    {
        var prev = (ToneCombo.SelectedItem as ComboBoxItem)?.Tag;
        ToneCombo.Items.Clear();
        ToneCombo.Items.Add(new ComboBoxItem { Content = P("Professional", "專業"), Tag = CoverLetterTone.Professional });
        ToneCombo.Items.Add(new ComboBoxItem { Content = P("Enthusiastic", "熱情"), Tag = CoverLetterTone.Enthusiastic });
        ToneCombo.Items.Add(new ComboBoxItem { Content = P("Concise", "精簡"), Tag = CoverLetterTone.Concise });
        ToneCombo.Items.Add(new ComboBoxItem { Content = P("Formal", "正式"), Tag = CoverLetterTone.Formal });
        var match = ToneCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, prev));
        ToneCombo.SelectedItem = match ?? ToneCombo.Items[0];
    }

    private CoverLetterTone SelectedTone =>
        (ToneCombo.SelectedItem as ComboBoxItem)?.Tag is CoverLetterTone t ? t : CoverLetterTone.Professional;

    private async Task RefreshAgentsAsync()
    {
        AgentCombo.Items.Clear();
        var installed = await ResumeWriterService.SupportedInstalledAsync();
        foreach (var a in installed)
            AgentCombo.Items.Add(new ComboBoxItem { Content = a.Name, Tag = a.Key });

        if (AgentCombo.Items.Count > 0)
        {
            AgentCombo.SelectedItem = AgentCombo.Items[0];
            AgentBar.IsOpen = false;
            GenerateBtn.IsEnabled = true;
            RegenCoverBtn.IsEnabled = true;
        }
        else
        {
            GenerateBtn.IsEnabled = false;
            RegenCoverBtn.IsEnabled = false;
            AgentBar.IsOpen = true;
            AgentBar.Severity = InfoBarSeverity.Warning;
            AgentBar.Title = P("No supported AI agent installed", "未安裝支援嘅 AI 代理");
            AgentBar.Message = P(
                "Install Claude Code, opencode, Codex or Pi from the AI Agents module, then come back here.",
                "請喺「AI 代理」模組安裝 Claude Code、opencode、Codex 或 Pi，然後返嚟呢度。");

            bool node = await AiAgentService.NodeAvailableAsync();
            AgentBar.ActionButton = node ? null : EngineBars.AutoInstallButton(
                "OpenJS.NodeJS.LTS", "Install Node.js", "安裝 Node.js",
                async () => { await RefreshAgentsAsync(); }, null);
        }
    }

    private AiAgent? SelectedAgent
    {
        get
        {
            var key = (AgentCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            return key is null ? null : AiAgentService.All.FirstOrDefault(a => a.Key == key);
        }
    }

    // ===================== Generate =====================

    private async void Generate_Click(object sender, RoutedEventArgs e) => await GenerateAsync(false);

    private async void RegenCover_Click(object sender, RoutedEventArgs e) => await GenerateAsync(true);

    private async Task GenerateAsync(bool coverOnly)
    {
        var agent = SelectedAgent;
        if (agent is null) { ShowError("No agent selected.", "未揀代理。"); return; }

        var jd = JdBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(jd))
        {
            ShowError("Paste a target job description first.", "請先貼上目標職位描述。");
            return;
        }

        // Prefer the live editor content; fall back to the stored base.
        var baseText = !string.IsNullOrWhiteSpace(BaseEditor.Text)
            ? BaseEditor.Text
            : (SelectedBaseId is { } id ? ResumeStore.GetBase(id)?.Content ?? "" : "");

        _cts = new CancellationTokenSource();
        SetBusy(true);
        ResultBar.IsOpen = false;
        try
        {
            var res = await ResumeWriterService.GenerateAsync(agent, baseText, jd, SelectedTone, _cts.Token);
            if (!res.Success)
            {
                ShowError(res.Error?.En ?? "Generation failed.", res.Error?.Zh ?? "生成失敗。");
                return;
            }

            if (coverOnly)
            {
                // Only refresh the cover-letter pane; keep the existing resume.
                if (!string.IsNullOrWhiteSpace(res.CoverLetter)) CoverEditor.Text = res.CoverLetter;
                else if (!string.IsNullOrWhiteSpace(res.Resume)) CoverEditor.Text = res.Resume; // fallback dump
                ShowOk(P("Cover letter regenerated.", "已重新生成求職信。"));
            }
            else
            {
                ResumeEditor.Text = res.Resume;
                CoverEditor.Text = res.CoverLetter;
                if (string.IsNullOrWhiteSpace(OutputTitleBox.Text))
                    OutputTitleBox.Text = DefaultTitle(jd);
                _currentOutputId = "";
                ShowOk(P("Generated. Review and edit the two panes, then Save to history.",
                         "已生成。請檢視同編輯兩個欄位，然後「存入歷史」。"));
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, $"出錯：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsActive = busy;
        GenerateBtn.IsEnabled = !busy && AgentCombo.Items.Count > 0;
        RegenCoverBtn.IsEnabled = !busy && AgentCombo.Items.Count > 0;
        CancelBtn.IsEnabled = busy;
        AgentCombo.IsEnabled = !busy;
        ToneCombo.IsEnabled = !busy;
    }

    private static string DefaultTitle(string jd)
    {
        var first = (jd ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        if (first.Length > 48) first = first.Substring(0, 48) + "…";
        return string.IsNullOrWhiteSpace(first) ? $"Resume — {DateTime.Now:yyyy-MM-dd HH:mm}" : first;
    }

    // ===================== Save / export / history =====================

    private void SaveHistory_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResumeEditor.Text) && string.IsNullOrWhiteSpace(CoverEditor.Text))
        {
            ShowError("Nothing to save yet.", "暫時冇嘢可以儲存。");
            return;
        }
        var title = string.IsNullOrWhiteSpace(OutputTitleBox.Text)
            ? DefaultTitle(JdBox.Text ?? "") : OutputTitleBox.Text.Trim();

        if (!string.IsNullOrEmpty(_currentOutputId) && ResumeStore.GetOutput(_currentOutputId) is not null)
        {
            ResumeStore.UpdateOutput(_currentOutputId, title, ResumeEditor.Text, CoverEditor.Text);
            ShowOk(P("History entry updated.", "已更新歷史紀錄。"));
        }
        else
        {
            var o = ResumeStore.AddOutput(new ResumeOutput
            {
                Title = title,
                BaseId = SelectedBaseId ?? "",
                Agent = SelectedAgent?.Key ?? "",
                JobDescription = JdBox.Text ?? "",
                Resume = ResumeEditor.Text ?? "",
                CoverLetter = CoverEditor.Text ?? "",
            });
            _currentOutputId = o.Id;
            ShowOk(P("Saved to history.", "已存入歷史。"));
        }
    }

    private async void ExportResume_Click(object sender, RoutedEventArgs e)
        => await ExportAsync(ResumeEditor.Text ?? "", P("resume", "履歷"));

    private async void ExportCover_Click(object sender, RoutedEventArgs e)
        => await ExportAsync(CoverEditor.Text ?? "", P("cover-letter", "求職信"));

    private async Task ExportAsync(string content, string what)
    {
        if (string.IsNullOrWhiteSpace(content)) { ShowError("Nothing to export.", "冇嘢可以匯出。"); return; }
        var suggested = Sanitize(string.IsNullOrWhiteSpace(OutputTitleBox.Text) ? what : OutputTitleBox.Text) + ".md";
        var path = await FileDialogs.SaveFileAsync(suggested, ".md", ".txt", ".html");
        if (path is null) return;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var text = ext == ".html" ? ToHtml(content) : content;
        var r = await ResumeWriterService.SaveTextAsync(path, text);
        ShowResult(r.Success, r);
    }

    private static string ToHtml(string md)
    {
        // 簡單包裝成可列印／轉 PDF 嘅 HTML（保留換行，HTML 轉義）· Minimal printable HTML wrapper.
        var esc = (md ?? "")
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>WinForge</title>" +
               "<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:800px;margin:40px auto;line-height:1.5;}" +
               "pre{white-space:pre-wrap;font-family:inherit;font-size:15px;}</style></head><body><pre>" +
               esc + "</pre></body></html>";
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (name ?? "resume").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "resume" : s;
    }

    private async void History_Click(object sender, RoutedEventArgs e)
    {
        var outputs = ResumeStore.Outputs;
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 420,
        };
        foreach (var o in outputs)
        {
            string when = o.Created;
            try { when = DateTime.Parse(o.Created).ToString("yyyy-MM-dd HH:mm"); } catch { }
            list.Items.Add(new ListViewItem
            {
                Content = $"{(string.IsNullOrWhiteSpace(o.Title) ? "(untitled)" : o.Title)}  ·  {when}  ·  {o.Agent}",
                Tag = o.Id,
            });
        }
        if (outputs.Count == 0)
            list.Items.Add(new ListViewItem { Content = P("(no saved outputs yet)", "（仲未有已存輸出）"), IsHitTestVisible = false });

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Saved outputs · 已存輸出", "已存輸出"),
            Content = list,
            PrimaryButtonText = P("Load", "載入"),
            SecondaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Close", "關閉"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dlg.ShowAsync();
        var id = (list.SelectedItem as ListViewItem)?.Tag as string;
        if (id is null) return;

        if (result == ContentDialogResult.Primary)
            LoadOutput(id);
        else if (result == ContentDialogResult.Secondary)
        {
            ResumeStore.RemoveOutput(id);
            if (_currentOutputId == id) _currentOutputId = "";
            ShowOk(P("Output deleted.", "已刪除輸出。"));
        }
    }

    private void LoadOutput(string id)
    {
        var o = ResumeStore.GetOutput(id);
        if (o is null) return;
        OutputTitleBox.Text = o.Title;
        ResumeEditor.Text = o.Resume;
        CoverEditor.Text = o.CoverLetter;
        if (!string.IsNullOrWhiteSpace(o.JobDescription)) JdBox.Text = o.JobDescription;
        _currentOutputId = id;
        ShowOk(P("Loaded from history.", "已由歷史載入。"));
    }

    // ===================== Dialogs + result bar =====================

    private async Task<string?> PromptTextAsync(string title, string label, string initial)
    {
        var box = new TextBox { Text = initial, PlaceholderText = label, AcceptsReturn = false };
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(box);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = P("OK", "確定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var r = await dlg.ShowAsync();
        if (r != ContentDialogResult.Primary) return null;
        var t = box.Text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = P("Yes", "係"),
            CloseButtonText = P("No", "唔係"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ShowOk(string msg)
    {
        ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成"); ResultBar.Message = msg;
    }

    private void ShowError(string en, string zh)
    {
        ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗"); ResultBar.Message = P(en, zh);
    }

    private void ShowResult(bool ok, TweakResult r)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = ok ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? (r.Output ?? "");
    }
}
