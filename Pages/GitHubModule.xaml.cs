using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Git 與 GitHub 模組 · The full Git &amp; GitHub workbench. Ports GitHub Desktop's core UX natively
/// over git/gh: a saved repo list (add / scan / clone), a Changes tab (file list + coloured diff +
/// commit box), a History tab (commit log + per-commit changed files + diff), a Branches tab
/// (switch / create / merge / delete, one-click PR via gh, and a vertical lane graph), and a Tools
/// tab (chunked uploader, free-form git/gh runner, and the complete operation library). Engine bars
/// auto-install git, gh and offer GitHub Desktop as a fallback. Every string is bilingual.
/// </summary>
public sealed partial class GitHubModule : Page
{
    private List<TweakDefinition>? _ops;
    private int _scope; // 0 = all, 1 = git only, 2 = GitHub only

    // currently-selected change in the Changes tab
    private GitDeskService.Change? _selectedChange;
    private bool _selectedStagedView;

    // history selection
    private GitDeskService.CommitInfo? _selectedCommit;

    private static DataTemplate? _diffTemplate;

    public GitHubModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        RepoStore.Changed += OnReposChanged;
        GitAliasStore.Changed += OnAliasesChanged;
        WorkTabs.SelectionChanged += async (_, _) => await OnTabChanged();
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            RepoStore.Changed -= OnReposChanged;
            GitAliasStore.Changed -= OnAliasesChanged;
        };
        Loaded += async (_, _) =>
        {
            Render();
            BuildScopeCombo();
            BuildQuickActions();
            BuildRepoList();
            PopulateOps(string.Empty);
            await CheckEngines();
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
        DispatcherQueue.TryEnqueue(() => { BuildRepoList(); BuildAliasList(); });

    private void OnAliasesChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(BuildAliasList);

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ===== localized text =====

    private void Render()
    {
        HeaderTitle.Text = "Git & GitHub · Git 與 GitHub";
        HeaderBlurb.Text = P(
            "GitHub Desktop, reimagined natively: pick a repo, review a colour-coded diff, stage files, commit with a summary + description, browse history, manage branches, open a pull request, and run any git or gh command.",
            "原生重做 GitHub Desktop：揀儲存庫、睇彩色 diff、暫存檔案、用摘要＋描述提交、瀏覽歷史、管理分支、開 pull request，仲可以執行任何 git 或 gh 指令。");

        ReposLabel.Text = P("Repositories", "儲存庫");
        AddRepoBtn.Content = P("Add folder…", "加資料夾…");
        ScanReposBtn.Content = P("Scan…", "掃描…");
        CloneDialogBtn.Content = P("Clone…", "複製…");
        CloneUrlBox.PlaceholderText = P("URL or owner/repo…", "網址或 owner/repo…");
        CloneRepoBtn.Content = P("Clone", "複製");

        TerminalBtn.Content = P("Terminal", "終端機");
        BrowserBtn.Content = P("Open on GitHub", "開 GitHub");

        OverviewTab.Header = P("Overview", "概覽");
        ChangesTab.Header = P("Changes", "改動");
        HistoryTab.Header = P("History", "歷史");
        BranchesTab.Header = P("Branches", "分支");
        ToolsTab.Header = P("Tools", "工具");
        WorkflowsTab.Header = P("Workflows", "工作流程");

        // Overview tab
        OverviewLabel.Text = P("Repository overview", "儲存庫概覽");
        RefreshOverviewBtn.Content = P("Refresh", "重新整理");
        PublishBranchBtn.Content = P("Publish branch", "發佈分支");
        RemotesLabel.Text = P("Remotes", "Remotes");
        RemoteNameBox.PlaceholderText = P("name", "名稱");
        RemoteUrlBox.PlaceholderText = P("fetch / push URL…", "fetch / push 網址…");
        AddRemoteBtn2.Content = P("Add remote", "新增 remote");
        StashesLabel.Text = P("Stashes", "Stashes");
        RefreshStashesBtn.Content = P("Refresh", "重新整理");
        StashMessageBox.PlaceholderText = P("Stash message…", "Stash 訊息…");
        StashIncludeUntrackedCheck.Content = P("Include untracked", "包括未追蹤");
        StashPushBtn.Content = P("Stash changes", "收藏改動");
        TagsLabel.Text = P("Tags", "Tags");
        PushTagsBtn.Content = P("Push tags", "推送 tags");
        TagNameBox.PlaceholderText = P("Tag name…", "Tag 名…");
        TagMessageBox.PlaceholderText = P("Annotation message (optional)…", "標註訊息（可選）…");
        CreateTagBtn.Content = P("Create tag", "建立 tag");

        // Changes tab
        ChangesLabel.Text = P("Changes", "改動");
        StageAllBtn.Content = P("Stage all", "暫存全部");
        RefreshChangesBtn.Content = P("Refresh", "重新整理");
        CommitBoxLabel.Text = P("Commit", "提交");
        CommitSummaryBox.PlaceholderText = P("Summary (required)…", "摘要（必填）…");
        CommitDescBox.PlaceholderText = P("Description (optional)…", "描述（可選）…");
        CommitBtn.Content = P("Commit staged", "提交已暫存");
        CommitPushBtn.Content = P("Commit & push", "提交並推送");
        DiffPathLabel.Text = P("Select a file to see its diff.", "揀一個檔案睇佢嘅 diff。");
        StageHunkBtn.Content = P("Stage file", "暫存檔案");

        // History tab
        HistoryLabel.Text = P("Commit history", "提交歷史");
        RefreshHistoryBtn.Content = P("Refresh", "重新整理");
        CommitMetaLabel.Text = P("Select a commit to inspect.", "揀一個提交去檢視。");

        // Branches tab
        BranchLabel.Text = P("Branches", "分支");
        SwitchBtn.Content = P("Switch", "切換");
        MergeBtn.Content = P("Merge into current", "合併入目前");
        DeleteBranchBtn.Content = P("Delete", "刪除");
        NewBranchBox.PlaceholderText = P("New branch name…", "新分支名…");
        CreateBranchBtn.Content = P("Create & switch", "建立並切換");
        PrLabel.Text = P("Pull request", "Pull request");
        PrBlurb.Text = P("Open a PR for the current branch via the GitHub CLI (gh). Tick auto-fill to take the title and body from your commits.",
            "用 GitHub CLI（gh）為目前分支開 PR。剔自動填就會由你嘅提交攞標題同內容。");
        PrTitleBox.PlaceholderText = P("PR title…", "PR 標題…");
        PrBodyBox.PlaceholderText = P("PR body…", "PR 內容…");
        PrDraftCheck.Content = P("Draft", "草稿");
        PrFillCheck.Content = P("Auto-fill from commits", "由提交自動填");
        PrCreateBtn.Content = P("Create PR", "建立 PR");
        PrViewBtn.Content = P("View PR in browser", "喺瀏覽器睇 PR");
        GraphLabel.Text = P("Branch graph (git log --graph)", "分支圖（git log --graph）");

        // Tools tab
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

        // Workflows tab (Gitty)
        WorkflowsBlurb.Text = P(
            "Opinionated one-click shortcuts inspired by the Gitty CLI — collapse the everyday add/commit/push, tag checkpoints, undo, and your own saved command sequences into single clicks against the selected repository.",
            "受 Gitty CLI 啟發嘅一鍵捷徑 — 將日常 add/commit/push、開檢查點 tag、撤回，同你自訂嘅指令序列，喺選中嘅儲存庫上一 click 搞掂。");
        WorkflowsLabel.Text = P("One-click workflows", "一鍵工作流程");
        UpHint.Text = P("Up · stage everything, commit, then push in one go (git add -A + commit + push).",
            "Up · 一次過暫存全部、提交、再推送（git add -A + commit + push）。");
        UpMessageBox.PlaceholderText = P("Commit message (optional)…", "提交訊息（可選）…");
        UpBtn.Content = P("Up", "Up");
        UndoBtn.Content = P("Undo last commit", "撤回上一個提交");
        ShareBtn.Content = P("Push & share link", "推送並複製連結");
        PrLinkBtn.Content = P("Copy PR link", "複製 PR 連結");

        CheckpointLabel.Text = P("Checkpoints", "檢查點");
        CheckpointHint.Text = P("Create a tag at the current branch tip and push it to origin. Restoring checks out the tag (detached HEAD).",
            "喺目前分支頂點開一個 tag 並推上 origin。還原會 checkout 個 tag（detached HEAD）。");
        CheckpointNameBox.PlaceholderText = P("Checkpoint / tag name…", "檢查點／tag 名…");
        CheckpointBtn.Content = P("Checkpoint", "建立檢查點");
        RestoreBtn.Content = P("Restore", "還原");

        AliasLabel.Text = P("Aliases", "別名");
        AliasHint.Text = P("Save a named sequence of git/gh steps (one per line) and run it with one click. Each line is \"git …\" or \"gh …\"; a bare line is treated as git. Stops on the first failing step.",
            "儲存一串具名嘅 git/gh 步驟（每行一個），一 click 執行。每行係「git …」或「gh …」；冇前綴當 git。第一步失敗就停低。");
        AliasEditorLabel.Text = P("New / edit alias", "新增／編輯別名");
        AliasNameBox.PlaceholderText = P("Alias name (button label)…", "別名（按鈕標籤）…");
        AliasStepsBox.PlaceholderText = P("add -A\ncommit -m \"wip\"\npush", "add -A\ncommit -m \"wip\"\npush");
        AliasSaveBtn.Content = P("Save alias", "儲存別名");

        BuildAliasList();
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

    // ===== engine bars =====

    private async Task CheckEngines()
    {
        bool git = await GitDeskService.GitAvailable();
        GitBar.IsOpen = !git;
        if (!git)
        {
            GitBar.Severity = InfoBarSeverity.Error;
            GitBar.Title = P("git not found", "搵唔到 git");
            GitBar.Message = P("git is the core engine here — install it automatically (Git.Git via winget), no restart needed.",
                "git 係呢度嘅核心引擎 — 自動安裝（用 winget 裝 Git.Git），唔使重啟。");
            GitBar.ActionButton = EngineBars.AutoInstallButton("Git.Git", "Install git", "安裝 git",
                async () => { GitDeskService.ResetEngineCache(); await CheckEngines(); await Refresh(); },
                rescan: GitDeskService.ResetEngineCache);
        }
        else GitBar.ActionButton = null;

        bool gh = await GitDeskService.GhAvailable();
        GhBar.IsOpen = !gh;
        if (!gh)
        {
            GhBar.Severity = InfoBarSeverity.Warning;
            GhBar.Title = P("GitHub CLI (gh) not found", "搵唔到 GitHub CLI（gh）");
            GhBar.Message = P("gh powers pull-request creation and the GitHub operations — install it automatically (GitHub.cli via winget).",
                "gh 負責建立 pull request 同 GitHub 操作 — 自動安裝（用 winget 裝 GitHub.cli）。");
            GhBar.ActionButton = EngineBars.AutoInstallButton("GitHub.cli", "Install gh", "安裝 gh",
                async () => { GitDeskService.ResetEngineCache(); await CheckEngines(); },
                rescan: GitDeskService.ResetEngineCache);
        }
        else GhBar.ActionButton = null;

        // GitHub Desktop fallback — always offered as an alternative thick client.
        DesktopBar.IsOpen = true;
        DesktopBar.Title = P("Prefer the real GitHub Desktop?", "想用真嘅 GitHub Desktop？");
        DesktopBar.Message = P("WinForge ports the workflow natively, but you can also install the official GitHub Desktop app as a fallback.",
            "WinForge 原生重做咗個工作流程，不過你都可以裝官方 GitHub Desktop 應用程式做後備。");
        DesktopBar.ActionButton = EngineBars.AutoInstallButton("GitHub.GitHubDesktop",
            "Install GitHub Desktop", "安裝 GitHub Desktop",
            async () => { Notify(InfoBarSeverity.Success, P("GitHub Desktop installed", "已安裝 GitHub Desktop"), ""); await Task.CompletedTask; });
    }

    // ===== repository list =====

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
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                Padding = new Thickness(6, 2, 6, 2),
                Background = new SolidColorBrush(Colors.Transparent),
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
            Notify(InfoBarSeverity.Success, P("Scan complete", "掃描完成"),
                P($"Added {added} repository(ies).", $"加咗 {added} 個儲存庫。"));
        }
        finally
        {
            ScanReposBtn.Content = label;
            ScanReposBtn.IsEnabled = true;
        }
    }

    // ===== clone =====

    private async void CloneRepo_Click(object sender, RoutedEventArgs e)
        => await DoClone(CloneUrlBox.Text?.Trim());

    private async void CloneDialog_Click(object sender, RoutedEventArgs e)
    {
        var urlBox = new TextBox { PlaceholderText = P("URL or owner/repo…", "網址或 owner/repo…") };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = P("Enter a clone URL (https/ssh) or an owner/repo shorthand, then choose a target folder.",
                "輸入複製網址（https/ssh）或者 owner/repo 簡寫，再揀目標資料夾。"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });
        panel.Children.Add(urlBox);

        var dlg = new ContentDialog
        {
            Title = P("Clone a repository", "複製儲存庫"),
            Content = panel,
            PrimaryButtonText = P("Choose folder & clone", "揀資料夾並複製"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        var res = await dlg.ShowAsync();
        if (res != ContentDialogResult.Primary) return;
        await DoClone(urlBox.Text?.Trim());
    }

    private async Task DoClone(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Notify(InfoBarSeverity.Warning, P("Enter a clone URL first.", "請先輸入複製網址。"), "");
            return;
        }
        var parent = await PickFolder();
        if (parent is null) return;

        CloneRepoBtn.IsEnabled = false;
        ShowConsole();
        AppendConsole($"$ git clone {GitDeskService.NormalizeCloneUrl(url)}\n");
        try
        {
            var progress = new Progress<string>(AppendConsole);
            var (r, dest) = await GitDeskService.Clone(url, parent, progress, CancellationToken.None);
            if (dest is not null)
            {
                var entry = RepoStore.Add(dest);
                if (entry is not null) RepoStore.Select(entry.Path);
                CloneUrlBox.Text = string.Empty;
                BuildRepoList();
                await Refresh();
                Notify(InfoBarSeverity.Success, P("Cloned", "已複製"), dest);
            }
            else
            {
                Notify(InfoBarSeverity.Error, P("Clone failed", "複製失敗"), Msg(r));
            }
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
        finally { CloneRepoBtn.IsEnabled = true; }
    }

    private async Task<string?> PickFolder()
    {
        try { return await FileDialogs.OpenFolderAsync(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Folder picker failed", "資料夾選擇器失敗"), ex.Message); return null; }
    }

    // ===== active repo =====

    private async Task Refresh()
    {
        RepoPathBox.Text = AppState.CurrentRepoPath;
        BuildAliasList();
        if (!GitDeskService.HasRepo)
        {
            RepoStatus.Text = P("No repository selected — add or pick one on the left.",
                "未揀儲存庫 — 喺左邊加或者揀一個。");
            ClearOverview();
            ChangesList.Children.Clear();
            BranchCombo.Items.Clear();
            return;
        }
        RepoStatus.Text = P("Checking…", "檢查緊…");
        if (!await GitDeskService.IsGitRepo())
        {
            RepoStatus.Text = P("This folder is not a git repository. Use the “git init” quick action, or pick another.",
                "呢個資料夾唔係 git 儲存庫。可以用「git init」快捷鍵，或者揀第二個。");
            ClearOverview();
            ChangesList.Children.Clear();
            BranchCombo.Items.Clear();
            return;
        }

        var branch = await GitDeskService.CurrentBranch();
        var ab = await GitDeskService.AheadBehind();
        var changes = await GitDeskService.Changes();
        string abText = ab is { } v ? P($"  ·  ↑{v.ahead} ↓{v.behind}", $"  ·  ↑{v.ahead} ↓{v.behind}") : "";
        RepoStatus.Text = P(
            $"Branch: {branch}  ·  {changes.Count} change(s){abText}",
            $"分支：{branch}  ·  {changes.Count} 項改動{abText}");

        // Refresh whichever tab is active.
        await OnTabChanged();
        await LoadBranches(branch);
    }

    private async Task OnTabChanged()
    {
        if (!GitDeskService.HasRepo) return;
        var sel = WorkTabs.SelectedItem as TabViewItem;
        if (sel == OverviewTab) await LoadOverview();
        else if (sel == ChangesTab) await LoadChanges();
        else if (sel == HistoryTab) await LoadHistory();
        else if (sel == BranchesTab) await LoadGraph();
    }

    private void BuildQuickActions()
    {
        QuickActions.Children.Clear();
        AddQuick(P("Refresh", "重新整理"), () => Task.FromResult(TweakResult.Ok("", "")));
        AddQuick(P("git init", "git init"), () => GitDeskService.RunRaw("init"));
        AddQuick(P("Fetch", "抓取"), () => GitDeskService.Fetch());
        AddQuick(P("Pull", "拉取"), () => GitDeskService.Pull());
        AddQuick(P("Push", "推送"), () => GitDeskService.Push());
        AddQuick(P("Sync", "同步"), async () =>
        {
            var pull = await GitDeskService.Pull();
            var push = await GitDeskService.Push();
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
                if (!string.IsNullOrWhiteSpace(r.Output)) { ShowConsole(); AppendConsole(r.Output! + "\n"); }
                await Refresh();
            }
            finally { btn.IsEnabled = true; }
        };
        QuickActions.Children.Add(btn);
    }

    private async void Terminal_Click(object sender, RoutedEventArgs e)
    {
        if (!GitDeskService.HasRepo)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a repository first.", "請先揀儲存庫。"), "");
            return;
        }
        // Open the embedded ConPTY terminal rooted at the selected repo folder — a real shell in-app
        // (vim, git rebase -i, etc. all work) instead of spawning an external window.
        var repoName = System.IO.Path.GetFileName(GitDeskService.Repo.TrimEnd('\\', '/'));
        await TerminalLauncher.OpenEmbeddedAsync(this.XamlRoot,
            P($"Terminal · {repoName}", $"終端機 · {repoName}"),
            commandLine: null, workingDir: GitDeskService.Repo);
    }

    private async void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (!GitDeskService.HasRepo) return;
        var r = await ShellRunner.RunIn(GitDeskService.Repo, "gh", "repo view --web", elevated: false, CancellationToken.None);
        if (!r.Success && !string.IsNullOrWhiteSpace(r.Output)) Notify(InfoBarSeverity.Warning, P("Open on GitHub", "開 GitHub"), Msg(r));
    }

    // ===== OVERVIEW tab =====

    private void ClearOverview()
    {
        OverviewSummaryPanel.Children.Clear();
        RemoteListPanel.Children.Clear();
        StashListPanel.Children.Clear();
        TagListPanel.Children.Clear();
        PublishBranchBtn.IsEnabled = false;
    }

    private async Task LoadOverview()
    {
        ClearOverview();
        if (!GitDeskService.HasRepo) return;

        var overview = await GitDeskService.Overview(CancellationToken.None);
        PublishBranchBtn.IsEnabled = !overview.Detached && string.IsNullOrWhiteSpace(overview.Upstream);

        OverviewSummaryPanel.Children.Add(InfoRow(P("Root", "根目錄"), string.IsNullOrWhiteSpace(overview.Root) ? GitDeskService.Repo : overview.Root));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Branch", "分支"),
            overview.Detached ? P($"Detached HEAD at {overview.ShortHead}", $"脫離 HEAD：{overview.ShortHead}") : overview.Branch));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Upstream", "上游"),
            string.IsNullOrWhiteSpace(overview.Upstream) ? P("Not published yet", "未發佈") : overview.Upstream));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Sync", "同步"),
            overview.Ahead is null || overview.Behind is null
                ? P("No upstream tracking information", "未有上游追蹤資料")
                : P($"Ahead {overview.Ahead}, behind {overview.Behind}", $"領先 {overview.Ahead}，落後 {overview.Behind}")));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Changes", "改動"),
            P($"{overview.TotalChanges} total · {overview.Staged} staged · {overview.Unstaged} unstaged · {overview.Untracked} untracked · {overview.Conflicted} conflicted",
              $"共 {overview.TotalChanges} 項 · {overview.Staged} 已暫存 · {overview.Unstaged} 未暫存 · {overview.Untracked} 未追蹤 · {overview.Conflicted} 衝突")));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Identity", "身份"),
            string.IsNullOrWhiteSpace(overview.UserName + overview.UserEmail)
                ? P("No repo-specific user.name / user.email", "未設定呢個 repo 專用 user.name / user.email")
                : $"{overview.UserName} <{overview.UserEmail}>"));
        OverviewSummaryPanel.Children.Add(InfoRow(P("Last commit", "最後提交"),
            string.IsNullOrWhiteSpace(overview.LastSubject)
                ? P("No commits yet", "未有提交")
                : $"{overview.ShortHead} · {overview.LastSubject} · {overview.LastAuthor} · {overview.LastDate}"));

        await LoadRemotes();
        await LoadStashes();
        await LoadTags();
    }

    private FrameworkElement InfoRow(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Top,
        });
        var val = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        return grid;
    }

    private TextBlock EmptyHint(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private async Task LoadRemotes()
    {
        RemoteListPanel.Children.Clear();
        var remotes = await GitDeskService.Remotes(CancellationToken.None);
        if (remotes.Count == 0)
        {
            RemoteListPanel.Children.Add(EmptyHint(P("No remotes configured.", "未設定 remote。")));
            return;
        }

        foreach (var remote in remotes)
            RemoteListPanel.Children.Add(BuildRemoteRow(remote));
    }

    private FrameworkElement BuildRemoteRow(GitDeskService.RemoteInfo remote)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = remote.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var fetch = string.IsNullOrWhiteSpace(remote.FetchUrl) ? remote.PushUrl : remote.FetchUrl;
        text.Children.Add(new TextBlock
        {
            Text = fetch,
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var fetchBtn = new Button { Content = P("Fetch", "抓取"), Padding = new Thickness(8, 4, 8, 4) };
        fetchBtn.Click += async (_, _) =>
        {
            var r = await GitDeskService.RunRaw($"fetch --prune \"{remote.Name.Replace("\"", "\\\"")}\"");
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Fetch remote", "抓取 remote"), Msg(r));
            await Refresh();
        };
        Grid.SetColumn(fetchBtn, 1);
        grid.Children.Add(fetchBtn);

        var removeBtn = new Button { Content = P("Remove", "移除"), Padding = new Thickness(8, 4, 8, 4) };
        removeBtn.Click += async (_, _) =>
        {
            if (!await Confirm(P("Remove remote", "移除 remote"),
                    P($"Remove remote “{remote.Name}” from this repository?", $"由呢個儲存庫移除 remote「{remote.Name}」？"),
                    P("Remove", "移除"))) return;
            var r = await GitDeskService.RemoveRemote(remote.Name);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Remove remote", "移除 remote"), Msg(r));
            await LoadOverview();
        };
        Grid.SetColumn(removeBtn, 2);
        grid.Children.Add(removeBtn);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
    }

    private async void AddRemote_Click(object sender, RoutedEventArgs e)
    {
        var r = await GitDeskService.AddRemote(RemoteNameBox.Text ?? "", RemoteUrlBox.Text ?? "");
        if (r.Success)
        {
            RemoteNameBox.Text = string.Empty;
            RemoteUrlBox.Text = string.Empty;
        }
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Add remote", "新增 remote"), Msg(r));
        await LoadOverview();
    }

    private async Task LoadStashes()
    {
        StashListPanel.Children.Clear();
        var stashes = await GitDeskService.Stashes(CancellationToken.None);
        if (stashes.Count == 0)
        {
            StashListPanel.Children.Add(EmptyHint(P("No stashes saved.", "未有 stash。")));
            return;
        }

        foreach (var stash in stashes)
            StashListPanel.Children.Add(BuildStashRow(stash));
    }

    private FrameworkElement BuildStashRow(GitDeskService.StashInfo stash)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = stash.Message, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock
        {
            Text = $"{stash.Selector} · {stash.Hash} · {stash.Age}",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var applyBtn = new Button { Content = P("Apply", "套用"), Padding = new Thickness(8, 4, 8, 4) };
        applyBtn.Click += async (_, _) =>
        {
            var r = await GitDeskService.StashApply(stash.Selector);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Apply stash", "套用 stash"), Msg(r));
            await Refresh();
        };
        Grid.SetColumn(applyBtn, 1);
        grid.Children.Add(applyBtn);

        var popBtn = new Button { Content = P("Pop", "彈出"), Padding = new Thickness(8, 4, 8, 4) };
        popBtn.Click += async (_, _) =>
        {
            var r = await GitDeskService.StashPop(stash.Selector);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Pop stash", "彈出 stash"), Msg(r));
            await Refresh();
        };
        Grid.SetColumn(popBtn, 2);
        grid.Children.Add(popBtn);

        var dropBtn = new Button { Content = P("Drop", "刪除"), Padding = new Thickness(8, 4, 8, 4) };
        dropBtn.Click += async (_, _) =>
        {
            if (!await Confirm(P("Drop stash", "刪除 stash"),
                    P($"Drop {stash.Selector}? This cannot be undone.", $"刪除 {stash.Selector}？呢個動作無法復原。"),
                    P("Drop", "刪除"))) return;
            var r = await GitDeskService.StashDrop(stash.Selector);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Drop stash", "刪除 stash"), Msg(r));
            await LoadOverview();
        };
        Grid.SetColumn(dropBtn, 3);
        grid.Children.Add(dropBtn);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
    }

    private async void StashPush_Click(object sender, RoutedEventArgs e)
    {
        var r = await GitDeskService.StashPush(StashMessageBox.Text ?? "", StashIncludeUntrackedCheck.IsChecked == true);
        if (r.Success) StashMessageBox.Text = string.Empty;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Stash changes", "收藏改動"), Msg(r));
        await Refresh();
    }

    private async Task LoadTags()
    {
        TagListPanel.Children.Clear();
        var tags = await GitDeskService.Tags(CancellationToken.None);
        if (tags.Count == 0)
        {
            TagListPanel.Children.Add(EmptyHint(P("No tags yet.", "未有 tag。")));
            return;
        }

        foreach (var tag in tags.Take(30))
            TagListPanel.Children.Add(BuildTagRow(tag));
    }

    private FrameworkElement BuildTagRow(GitDeskService.TagInfo tag)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = tag.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = string.Join(" · ", new[] { tag.Date, tag.Subject }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(sub))
        {
            text.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var deleteBtn = new Button { Content = P("Delete", "刪除"), Padding = new Thickness(8, 4, 8, 4) };
        deleteBtn.Click += async (_, _) =>
        {
            if (!await Confirm(P("Delete tag", "刪除 tag"),
                    P($"Delete local tag “{tag.Name}”?", $"刪除本機 tag「{tag.Name}」？"),
                    P("Delete", "刪除"))) return;
            var r = await GitDeskService.DeleteTag(tag.Name);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Delete tag", "刪除 tag"), Msg(r));
            await LoadOverview();
        };
        Grid.SetColumn(deleteBtn, 1);
        grid.Children.Add(deleteBtn);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
    }

    private async void CreateTag_Click(object sender, RoutedEventArgs e)
    {
        var r = await GitDeskService.CreateTag(TagNameBox.Text ?? "", TagMessageBox.Text);
        if (r.Success)
        {
            TagNameBox.Text = string.Empty;
            TagMessageBox.Text = string.Empty;
        }
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Create tag", "建立 tag"), Msg(r));
        await LoadOverview();
    }

    private async void PushTags_Click(object sender, RoutedEventArgs e)
    {
        var r = await GitDeskService.PushTags();
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Push tags", "推送 tags"), Msg(r));
        await LoadOverview();
    }

    private async void RefreshOverview_Click(object sender, RoutedEventArgs e) => await LoadOverview();

    private async void RefreshStashes_Click(object sender, RoutedEventArgs e) => await LoadStashes();

    private async void PublishBranch_Click(object sender, RoutedEventArgs e)
    {
        var r = await GitDeskService.PushSetUpstream();
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Publish branch", "發佈分支"), Msg(r));
        await Refresh();
    }

    private async Task<bool> Confirm(string title, string body, string primary)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = body,
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    // ===== CHANGES tab =====

    private async Task LoadChanges()
    {
        ChangesList.Children.Clear();
        var changes = await GitDeskService.Changes();
        if (changes.Count == 0)
        {
            ChangesList.Children.Add(new TextBlock
            {
                Text = P("Working tree clean — nothing to commit.", "工作區乾淨 — 冇嘢可以提交。"),
                FontSize = 13,
                Margin = new Thickness(8, 8, 8, 8),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            ClearDiff();
            return;
        }

        foreach (var c in changes)
        {
            var row = BuildChangeRow(c);
            ChangesList.Children.Add(row);
        }
    }

    private FrameworkElement BuildChangeRow(GitDeskService.Change c)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // staged checkbox
        var check = new CheckBox { IsChecked = c.Staged, MinWidth = 0, Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(check, P("Stage / unstage this file", "暫存／取消暫存呢個檔案"));
        check.Checked += async (_, _) => { await GitDeskService.Stage(c.Path); await Refresh(); };
        check.Unchecked += async (_, _) => { await GitDeskService.Unstage(c.Path); await Refresh(); };
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        // status badge
        var badge = new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = BadgeBrush(c.Badge),
            Child = new TextBlock { Text = c.Badge.ToString(), FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        var name = new TextBlock { Text = c.Path, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(name, c.Path);
        Grid.SetColumn(name, 2);
        grid.Children.Add(name);

        var btn = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
        };
        btn.Click += async (_, _) => await ShowDiffFor(c);

        // right-click: discard
        var flyout = new MenuFlyout();
        var discard = new MenuFlyoutItem { Text = P("Discard changes", "放棄改動") };
        discard.Click += async (_, _) =>
        {
            var r = await GitDeskService.Discard(c);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Discard", "放棄"), Msg(r));
            await Refresh();
        };
        flyout.Items.Add(discard);
        btn.ContextFlyout = flyout;

        return btn;
    }

    private static Brush BadgeBrush(char badge) => badge switch
    {
        'A' or '?' => new SolidColorBrush(Color.FromArgb(255, 0x2E, 0xA0, 0x43)),  // green: added/new
        'D' => new SolidColorBrush(Color.FromArgb(255, 0xCF, 0x22, 0x2E)),          // red: deleted
        'R' or 'C' => new SolidColorBrush(Color.FromArgb(255, 0x82, 0x50, 0xDF)),   // purple: renamed
        'U' => new SolidColorBrush(Color.FromArgb(255, 0xBC, 0x4C, 0x00)),          // orange: conflict
        _ => new SolidColorBrush(Color.FromArgb(255, 0x9A, 0x66, 0x00)),            // amber: modified
    };

    private async Task ShowDiffFor(GitDeskService.Change c)
    {
        _selectedChange = c;
        // Prefer the unstaged view; if the file is only staged, show the staged diff.
        _selectedStagedView = !c.Unstaged && c.Staged;
        DiffPathLabel.Text = c.Path;
        StageHunkBtn.Visibility = Visibility.Visible;
        StageHunkBtn.Content = _selectedStagedView ? P("Unstage file", "取消暫存") : P("Stage file", "暫存檔案");
        var lines = await GitDeskService.FileDiff(c, _selectedStagedView, CancellationToken.None);
        RenderDiff(DiffRepeater, lines);
    }

    private async void StageSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChange is null) return;
        if (_selectedStagedView)
            await GitDeskService.Unstage(_selectedChange.Path);
        else
            await GitDeskService.Stage(_selectedChange.Path);
        await Refresh();
    }

    private void ClearDiff()
    {
        _selectedChange = null;
        DiffPathLabel.Text = P("Select a file to see its diff.", "揀一個檔案睇佢嘅 diff。");
        StageHunkBtn.Visibility = Visibility.Collapsed;
        DiffRepeater.ItemsSource = null;
    }

    private async void StageAll_Click(object sender, RoutedEventArgs e)
    {
        await GitDeskService.StageAll();
        await Refresh();
    }

    private async void RefreshChanges_Click(object sender, RoutedEventArgs e) => await Refresh();

    private async void Commit_Click(object sender, RoutedEventArgs e) => await DoCommit(push: false);
    private async void CommitPush_Click(object sender, RoutedEventArgs e) => await DoCommit(push: true);

    private async Task DoCommit(bool push)
    {
        var summary = CommitSummaryBox.Text?.Trim();
        if (string.IsNullOrEmpty(summary))
        {
            Notify(InfoBarSeverity.Warning, P("Enter a commit summary first.", "請先輸入提交摘要。"), "");
            return;
        }
        CommitBtn.IsEnabled = CommitPushBtn.IsEnabled = false;
        try
        {
            var r = await GitDeskService.Commit(summary, CommitDescBox.Text?.Trim(), CancellationToken.None);
            if (!r.Success)
            {
                Notify(InfoBarSeverity.Error, P("Commit failed", "提交失敗"), Msg(r));
                return;
            }
            CommitSummaryBox.Text = string.Empty;
            CommitDescBox.Text = string.Empty;
            if (push)
            {
                var pr = await GitDeskService.Push();
                if (!pr.Success) pr = await GitDeskService.PushSetUpstream();
                Notify(pr.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                    P("Commit & push", "提交並推送"), Msg(pr));
            }
            else
            {
                Notify(InfoBarSeverity.Success, P("Committed", "已提交"), Msg(r));
            }
            await Refresh();
        }
        finally { CommitBtn.IsEnabled = CommitPushBtn.IsEnabled = true; }
    }

    // ===== HISTORY tab =====

    private async Task LoadHistory()
    {
        HistoryListPanel.Children.Clear();
        var commits = await GitDeskService.History(200, CancellationToken.None);
        if (commits.Count == 0)
        {
            HistoryListPanel.Children.Add(new TextBlock
            {
                Text = P("No commits yet.", "未有提交。"),
                FontSize = 13, Margin = new Thickness(8),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }
        foreach (var commit in commits)
            HistoryListPanel.Children.Add(BuildCommitRow(commit));
    }

    private FrameworkElement BuildCommitRow(GitDeskService.CommitInfo commit)
    {
        var sp = new StackPanel { Spacing = 1 };
        var top = new TextBlock { Text = commit.Subject, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.SemiBold };
        var sub = new TextBlock
        {
            Text = $"{commit.ShortHash}  ·  {commit.Author}  ·  {commit.Date}",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        sp.Children.Add(top);
        sp.Children.Add(sub);

        var btn = new Button
        {
            Content = sp,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
        };
        btn.Click += async (_, _) => await ShowCommit(commit);
        return btn;
    }

    private async Task ShowCommit(GitDeskService.CommitInfo commit)
    {
        _selectedCommit = commit;
        CommitMetaLabel.Text = $"{commit.ShortHash}  ·  {commit.Author}  ·  {commit.Date}\n{commit.Subject}";
        CommitFilesPanel.Children.Clear();
        var files = await GitDeskService.CommitFiles(commit.Hash, CancellationToken.None);
        foreach (var (st, path) in files)
        {
            var fbtn = new Button
            {
                Content = new TextBlock { Text = $"{st}  {path}", FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
            };
            string capturedPath = path;
            fbtn.Click += async (_, _) =>
            {
                var d = await GitDeskService.CommitFileDiff(commit.Hash, capturedPath, CancellationToken.None);
                RenderDiff(HistoryDiffRepeater, d);
            };
            CommitFilesPanel.Children.Add(fbtn);
        }
        // default: whole-commit diff
        var diff = await GitDeskService.CommitDiff(commit.Hash, CancellationToken.None);
        RenderDiff(HistoryDiffRepeater, diff);
    }

    private async void RefreshHistory_Click(object sender, RoutedEventArgs e) => await LoadHistory();

    // ===== BRANCHES tab =====

    private async Task LoadBranches(string current)
    {
        BranchCombo.Items.Clear();
        var branches = await GitDeskService.Branches(CancellationToken.None);
        foreach (var b in branches)
        {
            BranchCombo.Items.Add(b.Name);
            if (b.Current) BranchCombo.SelectedIndex = BranchCombo.Items.Count - 1;
        }
    }

    private async Task LoadGraph()
    {
        var commits = await GitDeskService.History(120, CancellationToken.None);
        var sb = new StringBuilder();
        foreach (var c in commits)
        {
            var prefix = string.IsNullOrEmpty(c.Graph) ? "*" : c.Graph;
            sb.Append(prefix).Append(' ').Append(c.ShortHash).Append("  ").Append(c.Subject)
              .Append("   (").Append(c.Author).Append(", ").Append(c.Date).Append(")\n");
        }
        GraphText.Text = sb.Length == 0 ? P("No history.", "冇歷史。") : sb.ToString();
    }

    private async void Switch_Click(object sender, RoutedEventArgs e)
    {
        if (BranchCombo.SelectedItem is not string b || string.IsNullOrEmpty(b)) return;
        var r = await GitDeskService.SwitchBranch(b);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Switch branch", "切換分支"), Msg(r));
        await Refresh();
    }

    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        if (BranchCombo.SelectedItem is not string b || string.IsNullOrEmpty(b)) return;
        var current = await GitDeskService.CurrentBranch();
        if (b == current)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a different branch", "揀另一條分支"),
                P("Choose a branch other than the current one to merge in.", "揀一條同目前唔同嘅分支去合併。"));
            return;
        }
        var r = await GitDeskService.MergeBranch(b);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Merge", "合併"), Msg(r));
        await Refresh();
    }

    private async void DeleteBranch_Click(object sender, RoutedEventArgs e)
    {
        if (BranchCombo.SelectedItem is not string b || string.IsNullOrEmpty(b)) return;
        var dlg = new ContentDialog
        {
            Title = P("Delete branch", "刪除分支"),
            Content = P($"Permanently delete branch “{b}”? This cannot be undone.", $"永久刪除分支「{b}」？呢個動作無法復原。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var r = await GitDeskService.DeleteBranch(b);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Delete branch", "刪除分支"), Msg(r));
        await Refresh();
    }

    private async void CreateBranch_Click(object sender, RoutedEventArgs e)
    {
        var name = NewBranchBox.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var r = await GitDeskService.CreateBranch(name);
        if (r.Success) NewBranchBox.Text = string.Empty;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Create branch", "建立分支"), Msg(r));
        await Refresh();
    }

    private async void CreatePr_Click(object sender, RoutedEventArgs e)
    {
        if (!await GitDeskService.GhAvailable())
        {
            Notify(InfoBarSeverity.Warning, P("gh not installed", "未裝 gh"),
                P("Install the GitHub CLI from the bar above to create pull requests.", "喺上面條 bar 裝 GitHub CLI 先可以建立 PR。"));
            return;
        }
        var auth = await GitDeskService.GhAuthStatus();
        if (!auth.ok)
        {
            Notify(InfoBarSeverity.Warning, P("gh not authenticated", "gh 未認證"),
                P("Run “gh auth login” (Tools → Command runner, gh, auth login --web) first.", "請先執行「gh auth login」（工具 → 指令執行器，gh，auth login --web）。"));
            return;
        }
        PrCreateBtn.IsEnabled = false;
        try
        {
            var r = await GitDeskService.CreatePr(PrTitleBox.Text?.Trim() ?? "", PrBodyBox.Text ?? "",
                PrDraftCheck.IsChecked == true, PrFillCheck.IsChecked == true, CancellationToken.None);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Create PR", "建立 PR"), Msg(r));
        }
        finally { PrCreateBtn.IsEnabled = true; }
    }

    private async void ViewPr_Click(object sender, RoutedEventArgs e)
    {
        if (!GitDeskService.HasRepo) return;
        var r = await ShellRunner.RunIn(GitDeskService.Repo, "gh", "pr view --web", elevated: false, CancellationToken.None);
        if (!r.Success) Notify(InfoBarSeverity.Warning, P("View PR", "睇 PR"), Msg(r));
    }

    // ===== diff rendering (virtualized) =====

    private static DataTemplate DiffTemplate => _diffTemplate ??= (DataTemplate)XamlReader.Load(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
        "<Border Background=\"{Binding Background}\" Padding=\"8,0,8,0\">" +
        "<TextBlock Text=\"{Binding Text}\" Foreground=\"{Binding Foreground}\" " +
        "FontFamily=\"Consolas\" FontSize=\"12\" IsTextSelectionEnabled=\"True\" TextWrapping=\"NoWrap\"/>" +
        "</Border></DataTemplate>");

    /// <summary>一行 diff 嘅 UI 模型 · A diff line view-model for the repeater.</summary>
    private sealed class DiffRow
    {
        public string Text { get; init; } = string.Empty;
        public Brush Foreground { get; init; } = new SolidColorBrush(Colors.Gray);
        public Brush Background { get; init; } = new SolidColorBrush(Colors.Transparent);
    }

    private void RenderDiff(ItemsRepeater repeater, List<GitDeskService.DiffLine> lines)
    {
        var added = new SolidColorBrush(Color.FromArgb(40, 0x2E, 0xA0, 0x43));
        var removed = new SolidColorBrush(Color.FromArgb(40, 0xCF, 0x22, 0x2E));
        var hunkBg = new SolidColorBrush(Color.FromArgb(40, 0x58, 0x8C, 0xE2));
        var addFg = new SolidColorBrush(Color.FromArgb(255, 0x3F, 0xB9, 0x50));
        var remFg = new SolidColorBrush(Color.FromArgb(255, 0xE5, 0x53, 0x4B));
        var hunkFg = new SolidColorBrush(Color.FromArgb(255, 0x6C, 0xA4, 0xF8));
        var metaFg = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        var ctxFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        var transparent = new SolidColorBrush(Colors.Transparent);

        var rows = new List<DiffRow>(lines.Count);
        foreach (var l in lines)
        {
            var (fg, bg) = l.Kind switch
            {
                GitDeskService.DiffKind.Added => ((Brush)addFg, (Brush)added),
                GitDeskService.DiffKind.Removed => ((Brush)remFg, (Brush)removed),
                GitDeskService.DiffKind.Hunk => ((Brush)hunkFg, (Brush)hunkBg),
                GitDeskService.DiffKind.Header => ((Brush)metaFg, (Brush)transparent),
                GitDeskService.DiffKind.Meta => ((Brush)metaFg, (Brush)transparent),
                _ => ((Brush)ctxFg, (Brush)transparent),
            };
            rows.Add(new DiffRow { Text = l.Text.Length == 0 ? " " : l.Text, Foreground = fg, Background = bg });
        }

        repeater.ItemTemplate = DiffTemplate;
        repeater.ItemsSource = rows;
    }

    // ===== console + ops library (Tools tab) =====

    private void ShowConsole() => ConsoleBorder.Visibility = Visibility.Visible;

    private void AppendConsole(string text)
    {
        ConsoleBorder.Visibility = Visibility.Visible;
        ConsoleLog.Text += text;
        if (ConsoleLog.Text.Length > 20000)
            ConsoleLog.Text = ConsoleLog.Text[^20000..];
    }

    private async void Runner_Click(object sender, RoutedEventArgs e)
    {
        var args = RunnerArgs.Text?.Trim();
        if (string.IsNullOrEmpty(args)) return;
        if (!GitDeskService.HasRepo)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a repository first.", "請先揀儲存庫。"), "");
            return;
        }
        var tool = (RunnerTool.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "git";
        RunnerBtn.IsEnabled = false;
        AppendConsole($"$ {tool} {args}\n");
        try
        {
            var r = await ShellRunner.RunIn(GitDeskService.Repo, tool, args, elevated: false, CancellationToken.None);
            AppendConsole((r.Output ?? string.Empty).Trim() + "\n");
            await Refresh();
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
        finally { RunnerBtn.IsEnabled = true; }
    }

    private async void ChunkUpload_Click(object sender, RoutedEventArgs e)
    {
        if (!GitDeskService.HasRepo)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a repository first.", "請先揀儲存庫。"), "");
            return;
        }
        long maxBytes = (long)(Math.Max(1, ChunkSizeBox.Value) * 1024 * 1024);
        var message = string.IsNullOrWhiteSpace(ChunkMessageBox.Text) ? "WinForge chunked upload" : ChunkMessageBox.Text.Trim();

        ChunkUploadBtn.IsEnabled = false;
        var label = ChunkUploadBtn.Content;
        ChunkUploadBtn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
        ShowConsole();

        var progress = new Progress<string>(AppendConsole);
        try
        {
            var r = await GitService.ChunkedUpload(maxBytes, message, progress, CancellationToken.None);
            AppendConsole("\n" + (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) + "\n");
            await Refresh();
        }
        catch (Exception ex) { AppendConsole("\n" + ex.Message + "\n"); }
        finally
        {
            ChunkUploadBtn.Content = label;
            ChunkUploadBtn.IsEnabled = true;
        }
    }

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

    // ===== WORKFLOWS / ALIASES (Gitty) tab =====

    private void WfConsole(string text)
    {
        WorkflowConsoleBorder.Visibility = Visibility.Visible;
        WorkflowConsoleLog.Text += text;
        if (WorkflowConsoleLog.Text.Length > 20000)
            WorkflowConsoleLog.Text = WorkflowConsoleLog.Text[^20000..];
    }

    private bool RequireRepo()
    {
        if (GitService.HasRepo) return true;
        Notify(InfoBarSeverity.Warning, P("Pick a repository first.", "請先揀儲存庫。"), "");
        return false;
    }

    /// <summary>Run a workflow with its button disabled, streaming to the workflow console.</summary>
    private async Task RunWorkflow(Button btn, string title, Func<IProgress<string>, Task<TweakResult>> run)
    {
        if (!RequireRepo()) return;
        btn.IsEnabled = false;
        var progress = new Progress<string>(WfConsole);
        try
        {
            var r = await run(progress);
            Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, title, Msg(r));
            await Refresh();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("GitHubModule.RunWorkflow", ex);
            WfConsole("\n" + ex.Message + "\n");
            Notify(InfoBarSeverity.Error, title, ex.Message);
        }
        finally { btn.IsEnabled = true; }
    }

    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var msg = UpMessageBox.Text ?? string.Empty;
            await RunWorkflow(UpBtn, P("Up", "Up"),
                p => GitWorkflows.Up(msg, p, CancellationToken.None));
            UpMessageBox.Text = string.Empty;
        }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.Up_Click", ex); }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!RequireRepo()) return;
            if (!await Confirm(P("Undo last commit", "撤回上一個提交"),
                    P("Soft-reset the last commit (git reset --soft HEAD~1)? Its changes stay staged so you can re-commit.",
                      "軟重設上一個提交（git reset --soft HEAD~1）？改動會留喺暫存區，可以再提交。"),
                    P("Undo", "撤回"))) return;
            await RunWorkflow(UndoBtn, P("Undo last commit", "撤回上一個提交"),
                p => GitWorkflows.Undo(p, CancellationToken.None));
        }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.Undo_Click", ex); }
    }

    private async void Share_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!RequireRepo()) return;
            ShareBtn.IsEnabled = false;
            var progress = new Progress<string>(WfConsole);
            try
            {
                var r = await GitWorkflows.PushAndShare(progress, CancellationToken.None);
                if (r.Success && !string.IsNullOrWhiteSpace(r.Output))
                    CopyToClipboard(r.Output!);
                Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                    P("Push & share link", "推送並複製連結"), Msg(r));
                await Refresh();
            }
            finally { ShareBtn.IsEnabled = true; }
        }
        catch (Exception ex)
        {
            CrashLogger.Log("GitHubModule.Share_Click", ex);
            Notify(InfoBarSeverity.Error, P("Push & share link", "推送並複製連結"), ex.Message);
        }
    }

    private async void PrLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!RequireRepo()) return;
            PrLinkBtn.IsEnabled = false;
            try
            {
                var r = await GitWorkflows.PrUrl(CancellationToken.None);
                if (r.Success && !string.IsNullOrWhiteSpace(r.Output))
                    CopyToClipboard(r.Output!);
                Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    P("Copy PR link", "複製 PR 連結"), Msg(r));
            }
            finally { PrLinkBtn.IsEnabled = true; }
        }
        catch (Exception ex)
        {
            CrashLogger.Log("GitHubModule.PrLink_Click", ex);
            Notify(InfoBarSeverity.Error, P("Copy PR link", "複製 PR 連結"), ex.Message);
        }
    }

    private async void Checkpoint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = CheckpointNameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                Notify(InfoBarSeverity.Warning, P("Enter a checkpoint name first.", "請先輸入檢查點名稱。"), "");
                return;
            }
            var branch = await GitDeskService.CurrentBranch();
            await RunWorkflow(CheckpointBtn, P("Checkpoint", "建立檢查點"),
                p => GitWorkflows.Checkpoint(name, branch, p, CancellationToken.None));
            CheckpointNameBox.Text = string.Empty;
        }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.Checkpoint_Click", ex); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = CheckpointNameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                Notify(InfoBarSeverity.Warning, P("Enter a checkpoint name first.", "請先輸入檢查點名稱。"), "");
                return;
            }
            if (!RequireRepo()) return;
            if (!await Confirm(P("Restore checkpoint", "還原檢查點"),
                    P($"Check out tag “{name}”? This leaves you in a detached HEAD — create a branch to keep working.",
                      $"Checkout tag「{name}」？呢個會令你進入 detached HEAD — 想繼續開發請開新分支。"),
                    P("Restore", "還原"))) return;
            await RunWorkflow(RestoreBtn, P("Restore checkpoint", "還原檢查點"),
                p => GitWorkflows.Restore(name, p, CancellationToken.None));
        }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.Restore_Click", ex); }
    }

    private void BuildAliasList()
    {
        if (AliasListPanel is null) return;
        AliasListPanel.Children.Clear();
        if (!GitService.HasRepo)
        {
            AliasListPanel.Children.Add(EmptyHint(P("Pick a repository to see its saved aliases.",
                "揀一個儲存庫去睇佢儲存咗嘅別名。")));
            return;
        }
        List<GitAlias> aliases;
        try { aliases = GitAliasStore.Load(GitService.Repo); }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.BuildAliasList", ex); aliases = new(); }
        if (aliases.Count == 0)
        {
            AliasListPanel.Children.Add(EmptyHint(P("No saved aliases yet — add one below.",
                "未有儲存別名 — 喺下面加一個。")));
            return;
        }
        foreach (var alias in aliases)
            AliasListPanel.Children.Add(BuildAliasRow(alias));
    }

    private FrameworkElement BuildAliasRow(GitAlias alias)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = alias.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock
        {
            Text = string.Join("  ·  ", alias.Steps),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var runBtn = new Button { Content = P("Run", "執行"), Padding = new Thickness(8, 4, 8, 4) };
        runBtn.Click += async (_, _) =>
        {
            try
            {
                await RunWorkflow(runBtn, P($"Alias · {alias.Name}", $"別名 · {alias.Name}"),
                    p => GitAliasStore.Run(alias, p, CancellationToken.None));
            }
            catch (Exception ex) { CrashLogger.Log("GitHubModule.AliasRun", ex); }
        };
        Grid.SetColumn(runBtn, 1);
        grid.Children.Add(runBtn);

        var editBtn = new Button { Content = P("Edit", "編輯"), Padding = new Thickness(8, 4, 8, 4) };
        editBtn.Click += (_, _) =>
        {
            AliasNameBox.Text = alias.Name;
            AliasStepsBox.Text = string.Join("\n", alias.Steps);
        };
        Grid.SetColumn(editBtn, 2);
        grid.Children.Add(editBtn);

        var deleteBtn = new Button { Content = P("Delete", "刪除"), Padding = new Thickness(8, 4, 8, 4) };
        deleteBtn.Click += async (_, _) =>
        {
            try
            {
                if (!await Confirm(P("Delete alias", "刪除別名"),
                        P($"Delete alias “{alias.Name}”?", $"刪除別名「{alias.Name}」？"),
                        P("Delete", "刪除"))) return;
                GitAliasStore.Remove(GitService.Repo, alias.Name);
            }
            catch (Exception ex) { CrashLogger.Log("GitHubModule.AliasDelete", ex); }
        };
        Grid.SetColumn(deleteBtn, 3);
        grid.Children.Add(deleteBtn);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
    }

    private void AliasSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!RequireRepo()) return;
            var name = AliasNameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                Notify(InfoBarSeverity.Warning, P("Enter an alias name first.", "請先輸入別名。"), "");
                return;
            }
            var steps = GitAliasStore.ParseSteps(AliasStepsBox.Text ?? string.Empty);
            if (steps.Count == 0)
            {
                Notify(InfoBarSeverity.Warning, P("Add at least one step.", "請加最少一個步驟。"), "");
                return;
            }
            GitAliasStore.Save(GitService.Repo, new GitAlias { Name = name, Steps = steps });
            AliasNameBox.Text = string.Empty;
            AliasStepsBox.Text = string.Empty;
            Notify(InfoBarSeverity.Success, P("Alias saved", "已儲存別名"),
                P($"“{name}” saved with {steps.Count} step(s).", $"「{name}」已儲存（{steps.Count} 步）。"));
        }
        catch (Exception ex)
        {
            CrashLogger.Log("GitHubModule.AliasSave_Click", ex);
            Notify(InfoBarSeverity.Error, P("Save alias", "儲存別名"), ex.Message);
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy,
            };
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        }
        catch (Exception ex) { CrashLogger.Log("GitHubModule.CopyToClipboard", ex); }
    }

    // ===== helpers =====

    private string Msg(TweakResult r)
    {
        var m = Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En;
        var outp = r.Output;
        if (!string.IsNullOrWhiteSpace(outp)) return string.IsNullOrWhiteSpace(m) ? outp! : $"{m}\n{outp}";
        return m ?? string.Empty;
    }

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
