using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Git 與 GitHub 模組 · The full Git &amp; GitHub workbench: a saved repository list (add / scan / clone),
/// stage / commit / branch / sync, the chunked uploader, a free-form git &amp; gh command runner, and the
/// complete operation library (every git CLI command + everything GitHub exposes through gh / gh api).
/// </summary>
public sealed partial class GitHubModule : Page
{
    private List<TweakDefinition>? _ops;
    private int _scope; // 0 = all, 1 = git only, 2 = GitHub only

    public GitHubModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        RepoStore.Changed += OnReposChanged;
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            RepoStore.Changed -= OnReposChanged;
        };
        Loaded += async (_, _) =>
        {
            Render();
            BuildScopeCombo();
            BuildQuickActions();
            BuildRepoList();
            PopulateOps(string.Empty);
            await Refresh();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildScopeCombo();
        BuildQuickActions();
        BuildRepoList();
        PopulateOps(OpsFilter.Text ?? string.Empty);
    }

    private void OnReposChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(BuildRepoList);

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Git & GitHub · Git 與 GitHub";
        HeaderBlurb.Text = P(
            "Manage many repositories, stage/commit/branch/sync, chunk-upload, run any git or gh command, and reach every git CLI command and everything GitHub offers.",
            "管理多個儲存庫、暫存／提交／分支／同步、分批上載、執行任何 git 或 gh 指令，仲覆蓋晒成個 git CLI 同 GitHub 嘅功能。");

        ReposLabel.Text = P("Repositories", "儲存庫");
        AddRepoBtn.Content = P("Add folder…", "加資料夾…");
        ScanReposBtn.Content = P("Scan…", "掃描…");
        CloneUrlBox.PlaceholderText = P("Clone URL…", "複製網址…");
        CloneRepoBtn.Content = P("Clone", "複製");

        TerminalBtn.Content = P("Terminal", "終端機");
        BrowserBtn.Content = P("Open on GitHub", "開 GitHub");
        CommitLabel.Text = P("Stage all & commit", "暫存全部並提交");
        CommitMessageBox.PlaceholderText = P("Commit message…", "提交訊息…");
        CommitBtn.Content = P("Commit", "提交");

        BranchLabel.Text = P("Branches", "分支");
        SwitchBtn.Content = P("Switch", "切換");
        NewBranchBox.PlaceholderText = P("New branch name…", "新分支名…");
        CreateBranchBtn.Content = P("Create", "建立");

        ChunkLabel.Text = P("Chunked upload (size per commit, push one at a time)",
            "分批上載（每個 commit 大細，逐個 push）");
        ChunkBlurb.Text = P(
            "Splits everything that needs uploading into commits no larger than the chosen size (MB), then pushes them one commit at a time.",
            "將所有要上載嘅嘢切成唔超過指定大細（MB）嘅 commit，然後逐個 commit push 上去。");
        ChunkMessageBox.PlaceholderText = P("Commit message prefix…", "提交訊息前綴…");
        ChunkUploadBtn.Content = P("Chunk & push", "分批推送");

        RunnerLabel.Text = P("Command runner", "指令執行器");
        RunnerBtn.Content = P("Run", "執行");

        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
        AdvancedHeader.Text = P($"Operation library ({GitCatalog.Count})", $"操作庫（{GitCatalog.Count}）");
    }

    private void BuildScopeCombo()
    {
        int sel = ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex;
        ScopeCombo.Items.Clear();
        ScopeCombo.Items.Add(P("All", "全部"));
        ScopeCombo.Items.Add(P("Git", "Git"));
        ScopeCombo.Items.Add(P("GitHub", "GitHub"));
        ScopeCombo.SelectedIndex = sel;
    }

    // ===== Repository list =====

    private void BuildRepoList()
    {
        RepoListPanel.Children.Clear();
        var active = AppState.CurrentRepoPath;
        var repos = RepoStore.All;
        if (repos.Count == 0)
        {
            RepoListPanel.Children.Add(new TextBlock
            {
                Text = P("No repositories yet — add a folder, scan, or clone.",
                    "未有儲存庫 — 加資料夾、掃描或者複製一個。"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }

        foreach (var repo in repos)
        {
            bool isActive = string.Equals(repo.Path, active, StringComparison.OrdinalIgnoreCase);
            var grid = new Grid { ColumnSpacing = 6 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel { Spacing = 1 };
            texts.Children.Add(new TextBlock
            {
                Text = repo.Name,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var sub = string.IsNullOrEmpty(repo.Branch) ? repo.Path : $"{repo.Path}  ·  {repo.Branch}";
            texts.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(texts, 0);
            grid.Children.Add(texts);

            string capturedPath = repo.Path;
            var remove = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(6, 2, 6, 2),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            ToolTipService.SetToolTip(remove, P("Remove from list", "由清單移除"));
            remove.Click += (_, _) => { RepoStore.Remove(capturedPath); };
            Grid.SetColumn(remove, 1);
            grid.Children.Add(remove);

            var row = new Button
            {
                Content = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8, 8, 8),
                BorderThickness = new Thickness(isActive ? 2 : 1),
                BorderBrush = isActive
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };
            row.Click += async (_, _) =>
            {
                RepoStore.Select(capturedPath);
                BuildRepoList();
                await Refresh();
            };
            RepoListPanel.Children.Add(row);
        }
    }

    private async void AddRepo_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolder();
        if (folder is null) return;
        var entry = RepoStore.Add(folder);
        if (entry is not null)
        {
            RepoStore.Select(entry.Path);
            BuildRepoList();
            await Refresh();
        }
    }

    private async void ScanRepos_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolder();
        if (folder is null) return;
        ScanReposBtn.IsEnabled = false;
        var label = ScanReposBtn.Content;
        ScanReposBtn.Content = P("Scanning…", "掃描緊…");
        try
        {
            int added = await RepoStore.ScanFolderAsync(folder, 3, CancellationToken.None);
            BuildRepoList();
            RepoStatus.Text = P($"Added {added} repository(ies) from the scan.", $"由掃描加咗 {added} 個儲存庫。");
        }
        finally
        {
            ScanReposBtn.Content = label;
            ScanReposBtn.IsEnabled = true;
        }
    }

    private async void CloneRepo_Click(object sender, RoutedEventArgs e)
    {
        var url = CloneUrlBox.Text?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            RepoStatus.Text = P("Enter a clone URL first.", "請先輸入複製網址。");
            return;
        }
        var parent = await PickFolder();
        if (parent is null) return;

        CloneRepoBtn.IsEnabled = false;
        AppendConsole($"$ git clone {url}\n");
        try
        {
            var r = await ShellRunner.RunIn(parent, "git", $"clone {url}", elevated: false, CancellationToken.None);
            AppendConsole((r.Output ?? string.Empty) + "\n");
            var name = RepoNameFromUrl(url);
            var dest = System.IO.Path.Combine(parent, name);
            if (System.IO.Directory.Exists(dest))
            {
                var entry = RepoStore.Add(dest);
                if (entry is not null) RepoStore.Select(entry.Path);
                CloneUrlBox.Text = string.Empty;
                BuildRepoList();
                await Refresh();
            }
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
        finally { CloneRepoBtn.IsEnabled = true; }
    }

    private static string RepoNameFromUrl(string url)
    {
        var s = url.TrimEnd('/');
        var slash = s.LastIndexOf('/');
        var name = slash >= 0 ? s[(slash + 1)..] : s;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return string.IsNullOrEmpty(name) ? "repo" : name;
    }

    private async Task<string?> PickFolder()
    {
        // Win32 COM folder dialog (works elevated, unlike the WinRT FolderPicker).
        try { return await FileDialogs.OpenFolderAsync(); }
        catch (Exception ex) { RepoStatus.Text = ex.Message; return null; }
    }

    // ===== Active repo =====

    private async Task Refresh()
    {
        RepoPathBox.Text = AppState.CurrentRepoPath;
        if (!GitService.HasRepo)
        {
            RepoStatus.Text = P("No repository selected — add or pick one on the left.",
                "未揀儲存庫 — 喺左邊加或者揀一個。");
            BranchCombo.Items.Clear();
            return;
        }
        RepoStatus.Text = P("Checking…", "檢查緊…");
        bool isRepo = await GitService.IsGitRepo();
        if (!isRepo)
        {
            RepoStatus.Text = P("This folder is not a git repository. Use the “git init” quick action, or pick another.",
                "呢個資料夾唔係 git 儲存庫。可以用「git init」快捷鍵，或者揀第二個。");
            BranchCombo.Items.Clear();
            return;
        }
        var branch = await GitService.CurrentBranch();
        var pending = await GitService.PendingFiles();
        long totalBytes = pending.Sum(p => p.size);
        RepoStatus.Text = P(
            $"Branch: {branch}  ·  {pending.Count} pending change(s)  ·  {Mb(totalBytes)} MB",
            $"分支：{branch}  ·  {pending.Count} 項待處理改動  ·  {Mb(totalBytes)} MB");
        await LoadBranches(branch);
    }

    private async Task LoadBranches(string current)
    {
        BranchCombo.Items.Clear();
        var r = await GitService.RunRaw("branch --format=%(refname:short)");
        if (!r.Success) return;
        foreach (var raw in (r.Output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var b = raw.Trim();
            if (b.Length == 0) continue;
            BranchCombo.Items.Add(b);
            if (b == current) BranchCombo.SelectedIndex = BranchCombo.Items.Count - 1;
        }
    }

    private static string Mb(long bytes) => Math.Round(bytes / 1024.0 / 1024.0, 1).ToString("0.0");

    private void BuildQuickActions()
    {
        QuickActions.Children.Clear();
        AddQuick(P("Refresh", "重新整理"), () => Task.FromResult(TweakResult.Ok("", "")));
        AddQuick(P("git init", "git init"), () => GitService.RunRaw("init"));
        AddQuick(P("Stage all", "暫存全部"), () => GitService.RunRaw("add -A"));
        AddQuick(P("Pull", "拉取"), () => GitService.RunRaw("pull"));
        AddQuick(P("Fetch", "抓取"), () => GitService.RunRaw("fetch --all --prune"));
        AddQuick(P("Push", "推送"), () => GitService.RunRaw("push"));
        AddQuick(P("Sync", "同步"), async () =>
        {
            var pull = await GitService.RunRaw("pull");
            var push = await GitService.RunRaw("push");
            return TweakResult.Ok((pull.Output ?? "") + "\n" + (push.Output ?? ""), "");
        });
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
                if (!string.IsNullOrWhiteSpace(r.Output)) AppendConsole(r.Output! + "\n");
                await Refresh();
            }
            finally { btn.IsEnabled = true; }
        };
        QuickActions.Children.Add(btn);
    }

    private void Terminal_Click(object sender, RoutedEventArgs e)
    {
        if (!GitService.HasRepo) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = $"-d \"{GitService.Repo}\"", UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/K cd /d \"{GitService.Repo}\"", UseShellExecute = true }); }
            catch { /* ignore */ }
        }
    }

    private async void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (!GitService.HasRepo) return;
        var r = await ShellRunner.RunIn(GitService.Repo, "gh", "repo view --web", elevated: false, CancellationToken.None);
        if (!r.Success && !string.IsNullOrWhiteSpace(r.Output)) AppendConsole(r.Output! + "\n");
    }

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        var msg = CommitMessageBox.Text?.Trim();
        if (string.IsNullOrEmpty(msg))
        {
            RepoStatus.Text = P("Enter a commit message first.", "請先輸入提交訊息。");
            return;
        }
        CommitBtn.IsEnabled = false;
        try
        {
            await GitService.RunRaw("add -A");
            var r = await GitService.RunRaw($"commit -m \"{msg.Replace("\"", "'")}\"");
            AppendConsole((r.Output ?? string.Empty).Trim() + "\n");
            if (r.Success) CommitMessageBox.Text = string.Empty;
            await Refresh();
        }
        finally { CommitBtn.IsEnabled = true; }
    }

    private async void Switch_Click(object sender, RoutedEventArgs e)
    {
        if (BranchCombo.SelectedItem is not string b || string.IsNullOrEmpty(b)) return;
        var r = await GitService.RunRaw($"switch \"{b}\"");
        AppendConsole((r.Output ?? string.Empty).Trim() + "\n");
        await Refresh();
    }

    private async void CreateBranch_Click(object sender, RoutedEventArgs e)
    {
        var name = NewBranchBox.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var r = await GitService.RunRaw($"switch -c \"{name}\"");
        AppendConsole((r.Output ?? string.Empty).Trim() + "\n");
        if (r.Success) NewBranchBox.Text = string.Empty;
        await Refresh();
    }

    private async void Runner_Click(object sender, RoutedEventArgs e)
    {
        var args = RunnerArgs.Text?.Trim();
        if (string.IsNullOrEmpty(args)) return;
        if (!GitService.HasRepo)
        {
            RepoStatus.Text = P("Pick a repository first.", "請先揀儲存庫。");
            return;
        }
        var tool = (RunnerTool.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "git";
        RunnerBtn.IsEnabled = false;
        AppendConsole($"$ {tool} {args}\n");
        try
        {
            var r = await ShellRunner.RunIn(GitService.Repo, tool, args, elevated: false, CancellationToken.None);
            AppendConsole((r.Output ?? string.Empty).Trim() + "\n");
            await Refresh();
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
        finally { RunnerBtn.IsEnabled = true; }
    }

    private void AppendConsole(string text)
    {
        ConsoleBorder.Visibility = Visibility.Visible;
        ConsoleLog.Text += text;
        if (ConsoleLog.Text.Length > 20000)
            ConsoleLog.Text = ConsoleLog.Text[^20000..];
    }

    private async void ChunkUpload_Click(object sender, RoutedEventArgs e)
    {
        if (!GitService.HasRepo)
        {
            RepoStatus.Text = P("Pick a repository first.", "請先揀儲存庫。");
            return;
        }
        long maxBytes = (long)(Math.Max(1, ChunkSizeBox.Value) * 1024 * 1024);
        var message = string.IsNullOrWhiteSpace(ChunkMessageBox.Text) ? "WinForge chunked upload" : ChunkMessageBox.Text.Trim();

        ChunkUploadBtn.IsEnabled = false;
        var label = ChunkUploadBtn.Content;
        ChunkUploadBtn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };

        var progress = new Progress<string>(s => AppendConsole(s));
        try
        {
            var r = await GitService.ChunkedUpload(maxBytes, message, progress, CancellationToken.None);
            AppendConsole("\n" + (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) + "\n");
            await Refresh();
        }
        catch (Exception ex)
        {
            AppendConsole("\n" + ex.Message + "\n");
        }
        finally
        {
            ChunkUploadBtn.Content = label;
            ChunkUploadBtn.IsEnabled = true;
        }
    }

    // ===== Operation library =====

    private void Scope_Changed(object sender, SelectionChangedEventArgs e)
    {
        _scope = ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex;
        PopulateOps(OpsFilter.Text ?? string.Empty);
    }

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= GitCatalog.All.ToList();
        OpsPanel.Children.Clear();

        IEnumerable<TweakDefinition> shown = _scope switch
        {
            1 => GitCatalog.GitOnly,
            2 => GitCatalog.GitHubOnly,
            _ => _ops,
        };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = shown.Where(t => t.SearchHaystack.Contains(f));
        }

        foreach (var op in shown.Take(400))
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }
}
