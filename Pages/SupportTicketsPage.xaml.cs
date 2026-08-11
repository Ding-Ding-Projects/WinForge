using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

public sealed partial class SupportTicketsPage : Page
{
    private readonly SupportTicketService _service = new();
    private bool _initialized;

    public SupportTicketsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            TicketSearchBox.PatternChanged += TicketSearchBox_PatternChanged;
            _initialized = true;
        }
        PopulateChoices();
        RefreshCopy();
        RefreshTickets();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        TicketSearchBox.PatternChanged -= TicketSearchBox_PatternChanged;
        _initialized = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PopulateChoices();
        RefreshCopy();
        RefreshTickets();
    }

    private void TicketSearchBox_PatternChanged(object? sender, EventArgs e) => RefreshTickets();

    private void PopulateChoices()
    {
        SupportTicketCategory? category = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag is SupportTicketCategory selectedCategory
            ? selectedCategory : null;
        SupportTicketSeverity? severity = (SeverityBox.SelectedItem as ComboBoxItem)?.Tag is SupportTicketSeverity selectedSeverity
            ? selectedSeverity : null;
        CategoryBox.Items.Clear();
        SeverityBox.Items.Clear();

        foreach (SupportTicketCategory value in Enum.GetValues<SupportTicketCategory>())
            CategoryBox.Items.Add(new ComboBoxItem { Content = CategoryLabel(value), Tag = value });
        foreach (SupportTicketSeverity value in Enum.GetValues<SupportTicketSeverity>())
            SeverityBox.Items.Add(new ComboBoxItem { Content = SeverityLabel(value), Tag = value });

        CategoryBox.SelectedIndex = category is not null ? (int)category.Value : 0;
        SeverityBox.SelectedIndex = severity is not null ? (int)severity.Value : 1;
        AutomationProperties.SetName(CategoryBox, Loc.I.Pick("Ticket category", "工單類別"));
        AutomationProperties.SetName(SeverityBox, Loc.I.Pick("Ticket severity", "工單嚴重程度"));
        AutomationProperties.SetName(DescriptionBox, Loc.I.Pick("Ticket description", "工單描述"));
    }

    private void RefreshCopy()
    {
        TitleText.Text = Loc.I.Pick("Support Tickets", "支援工單");
        SubtitleText.Text = Loc.I.Pick(
            "A local fictional support desk for recording a problem and showing the only recovery action this app can offer.",
            "呢個係本機虛構支援台，用嚟記錄問題，同展示呢個 app 可以提供嘅唯一處理方法。");
        DisclosureText.Text = Loc.I.Pick(SupportTicketService.LocalOnlyDisclosure, SupportTicketService.LocalOnlyDisclosureZh);
        RecoveryText.Text = Loc.I.Pick(SupportTicketService.RecoveryInstructions, SupportTicketService.RecoveryInstructionsZh);
        CreateHeading.Text = Loc.I.Pick("Create a local ticket", "建立本機工單");
        DescriptionBox.Header = Loc.I.Pick("Description", "描述");
        DescriptionBox.PlaceholderText = Loc.I.Pick("Describe what happened and what you need to recover.", "描述發生咗咩事，同你想點樣處理。");
        CreateButton.Content = Loc.I.Pick("Create ticket", "建立工單");
        ListHeading.Text = Loc.I.Pick("Ticket history", "工單紀錄");
        BulkScopeText.Text = Loc.I.Pick(
            "Select visible tickets with click, Shift+click, or the keyboard. Bulk actions apply to the visible selection only.",
            "可以用 click、Shift+click 或鍵盤揀選顯示緊嘅工單；批量操作只會套用喺目前揀選嘅顯示項目。");
        SelectAllButton.Content = Loc.I.Pick("Select all visible tickets", "揀晒所有顯示緊嘅工單");
        InvertSelectionButton.Content = Loc.I.Pick("Invert visible selection", "反轉顯示緊嘅揀選");
        AutomationProperties.SetName(TicketsList, Loc.I.Pick("Support ticket list", "支援工單清單"));
        AdvanceStatusButton.Content = Loc.I.Pick("Advance selected tickets", "推進所選工單狀態");
        DeleteSelectedButton.Content = Loc.I.Pick("Delete selected tickets", "刪除所選工單");
        ExportHeading.Text = Loc.I.Pick("Export selected tickets", "匯出所選工單");
        ExportJsonButton.Content = "JSON · JSON";
        ExportCsvButton.Content = "CSV · CSV";
        ExportMarkdownButton.Content = "Markdown · Markdown";
        ExportHtmlButton.Content = "HTML · HTML";
        ResolutionHeading.Text = Loc.I.Pick("Resolution", "處理方法");
        FolderPathBox.Header = Loc.I.Pick("Application-data folder", "Application-data 資料夾");
        OpenFolderButton.Content = Loc.I.Pick("Open folder without deleting anything", "開啟資料夾（唔會刪除任何嘢）");
        AutomationProperties.SetName(CreateButton, Loc.I.Pick("Create local support ticket", "建立本機支援工單"));
        AutomationProperties.SetName(AdvanceStatusButton, Loc.I.Pick("Advance selected ticket status", "推進所選工單狀態"));
        AutomationProperties.SetName(DeleteSelectedButton, Loc.I.Pick("Delete selected support tickets", "刪除所選支援工單"));
        AutomationProperties.SetName(SelectAllButton, Loc.I.Pick("Select all visible support tickets", "揀晒所有顯示緊嘅支援工單"));
        AutomationProperties.SetName(InvertSelectionButton, Loc.I.Pick("Invert visible support ticket selection", "反轉顯示緊嘅支援工單揀選"));
        AutomationProperties.SetName(ExportJsonButton, "Export selected support tickets as JSON");
        AutomationProperties.SetName(ExportCsvButton, "Export selected support tickets as CSV");
        AutomationProperties.SetName(ExportMarkdownButton, "Export selected support tickets as Markdown");
        AutomationProperties.SetName(ExportHtmlButton, "Export selected support tickets as HTML");
        AutomationProperties.SetName(OpenFolderButton, Loc.I.Pick("Open application-data folder without deleting anything", "開啟 application-data 資料夾但唔刪除任何嘢"));
    }

    private void RefreshTickets()
    {
        IReadOnlyList<SupportTicket> tickets = _service.Tickets;
        var rows = tickets.Select(ticket => new TicketRow(
            ticket.Id,
            $"{ticket.TicketNumber} · {CategoryLabel(ticket.Category)} · {StatusLabel(ticket.Status)}",
            $"{SeverityLabel(ticket.Severity)} · {ticket.CreatedAt.LocalDateTime:g} · {ticket.Description}"))
            .Where(row => TicketSearchBox.Match($"{row.Summary} {row.Details}").IsMatch)
            .ToArray();
        TicketsList.ItemsSource = rows;
        TicketCountText.Text = Loc.I.Pick(
            $"Showing {rows.Length} of {tickets.Count} local ticket(s).",
            $"顯示緊 {tickets.Count} 張本機工單入面嘅 {rows.Length} 張。");
        EmptyStateText.Text = TicketSearchBox.ValidationError is string validation
            ? validation
            : tickets.Count == 0
                ? Loc.I.Pick("No local tickets yet. Create one above when you need the fictional desk.", "暫時冇本機工單；有需要時可以喺上面建立呢個虛構支援台工單。")
                : Loc.I.Pick("No tickets match this search.", "冇工單符合呢個搜尋。");
        EmptyStateText.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(TicketsList,
            Loc.I.Pick($"Support ticket list, {rows.Length} visible", $"支援工單清單，顯示 {rows.Length} 張"));
        UpdateBulkState();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SupportTicketCategory category = ((ComboBoxItem)CategoryBox.SelectedItem).Tag is SupportTicketCategory selectedCategory
                ? selectedCategory : SupportTicketCategory.General;
            SupportTicketSeverity severity = ((ComboBoxItem)SeverityBox.SelectedItem).Tag is SupportTicketSeverity selectedSeverity
                ? selectedSeverity : SupportTicketSeverity.Normal;
            SupportTicket ticket = _service.CreateTicket(category, DescriptionBox.Text, severity);
            DescriptionBox.Text = string.Empty;
            RefreshTickets();
            TicketsList.SelectedIndex = 0;
            ShowStatus(InfoBarSeverity.Success,
                "Ticket created", "工單已建立",
                $"{ticket.TicketNumber}: {ticket.FirstResponse}",
                $"{ticket.TicketNumber}：{ticket.FirstResponseZh}");
        }
        catch (ArgumentException exception)
        {
            ShowStatus(InfoBarSeverity.Error,
                "Ticket was not created", "工單未建立",
                exception.Message, exception.Message);
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error,
                "Ticket storage failed", "工單儲存失敗",
                exception.Message, exception.Message);
        }
    }

    private void TicketsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateBulkState();

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        TicketsList.SelectedItems.Clear();
        foreach (object item in TicketsList.Items) TicketsList.SelectedItems.Add(item);
        UpdateBulkState();
    }

    private void InvertSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = TicketsList.SelectedItems.OfType<TicketRow>().Select(row => row.Id).ToHashSet();
        var all = TicketsList.Items.OfType<TicketRow>().ToArray();
        TicketsList.SelectedItems.Clear();
        foreach (TicketRow row in all)
            if (!selected.Contains(row.Id)) TicketsList.SelectedItems.Add(row);
        UpdateBulkState();
    }

    private void AdvanceStatusButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TicketRow[] selected = SelectedRows();
            if (selected.Length == 0) return;
            int advanced = _service.AdvanceTickets(selected.Select(row => row.Id), out int alreadyResolved, out string error);
            if (!string.IsNullOrWhiteSpace(error))
            {
                ShowStatus(InfoBarSeverity.Error, "Ticket storage failed", "工單儲存失敗", error, error);
                return;
            }
            if (advanced == 0)
            {
                ShowStatus(InfoBarSeverity.Informational,
                    "Ticket is already resolved", "工單已經處理完成",
                    "No selected ticket needed a status change.", "所選工單冇一張需要改變狀態。");
                return;
            }
            RefreshTickets();
            ShowStatus(InfoBarSeverity.Success,
                "Ticket status advanced", "工單狀態已推進",
                $"{advanced} selected ticket(s) advanced; {alreadyResolved} were already resolved.",
                $"{advanced} 張所選工單已推進；其中 {alreadyResolved} 張本身已完成。");
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Ticket storage failed", "工單儲存失敗", exception.Message, exception.Message);
        }
    }

    private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        TicketRow[] selected = SelectedRows();
        if (selected.Length == 0) return;
        string keyOne = "DELETE";
        string keyTwo = $"{selected.Length} TICKETS";
        var first = new TextBox { Header = Loc.I.Pick("Key 1", "第一條匙"), PlaceholderText = keyOne, MaxLength = keyOne.Length };
        var second = new TextBox { Header = Loc.I.Pick("Key 2", "第二條匙"), PlaceholderText = keyTwo, MaxLength = keyTwo.Length };
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false };
        void UpdateSlider(object? _, TextChangedEventArgs __)
            => slider.IsEnabled = string.Equals(first.Text, keyOne, StringComparison.Ordinal) &&
                                  string.Equals(second.Text, keyTwo, StringComparison.Ordinal);
        first.TextChanged += UpdateSlider;
        second.TextChanged += UpdateSlider;
        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = Loc.I.Pick(
                        $"This permanently deletes {selected.Length} selected local ticket(s). Nothing outside those tickets is affected.",
                        $"呢個動作會永久刪除 {selected.Length} 張所選本機工單；其他內容唔受影響。"),
                    TextWrapping = TextWrapping.Wrap,
                },
                first, second, slider,
            },
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.I.Pick("Confirm ticket deletion", "確認刪除工單"),
            Content = panel,
            PrimaryButtonText = Loc.I.Pick("Delete selected tickets", "刪除所選工單"),
            CloseButtonText = Loc.I.Pick("Emergency exit", "緊急離開"),
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!slider.IsEnabled || slider.Value < 100) args.Cancel = true;
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int deleted = _service.DeleteTickets(selected.Select(row => row.Id), out string error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            ShowStatus(InfoBarSeverity.Error, "Ticket deletion failed", "刪除工單失敗", error, error);
            return;
        }
        RefreshTickets();
        ShowStatus(InfoBarSeverity.Success, "Tickets deleted", "工單已刪除",
            $"Deleted {deleted} selected local ticket(s).", $"已刪除 {deleted} 張所選本機工單。");
    }

    private TicketRow[] SelectedRows() => TicketsList.SelectedItems.OfType<TicketRow>().ToArray();

    private IReadOnlyList<SupportTicket> SelectedTickets()
    {
        HashSet<Guid> ids = SelectedRows().Select(row => row.Id).ToHashSet();
        return _service.Tickets.Where(ticket => ids.Contains(ticket.Id)).ToArray();
    }

    private void UpdateBulkState()
    {
        bool hasSelection = TicketsList.SelectedItems.Count > 0;
        AdvanceStatusButton.IsEnabled = hasSelection;
        DeleteSelectedButton.IsEnabled = hasSelection;
        ExportJsonButton.IsEnabled = hasSelection;
        ExportCsvButton.IsEnabled = hasSelection;
        ExportMarkdownButton.IsEnabled = hasSelection;
        ExportHtmlButton.IsEnabled = hasSelection;
        int selected = TicketsList.SelectedItems.Count;
        BulkScopeText.Text = Loc.I.Pick(
            $"{selected} visible ticket(s) selected. Select all means this filtered list only.",
            $"已揀選 {selected} 張顯示緊嘅工單。揀晒只代表目前篩選清單。");
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
        => await ExportSelectedAsync("json", "winforge-support-tickets", SupportTicketService.ExportJson);

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        => await ExportSelectedAsync("csv", "winforge-support-tickets", SupportTicketService.ExportCsv);

    private async void ExportMarkdownButton_Click(object sender, RoutedEventArgs e)
        => await ExportSelectedAsync("md", "winforge-support-tickets", SupportTicketService.ExportMarkdown);

    private async void ExportHtmlButton_Click(object sender, RoutedEventArgs e)
        => await ExportSelectedAsync("html", "winforge-support-tickets", SupportTicketService.ExportHtml);

    private async Task ExportSelectedAsync(
        string extension,
        string baseName,
        TicketExporter exporter)
    {
        IReadOnlyList<SupportTicket> selected = SelectedTickets();
        if (selected.Count == 0) return;
        string? path = await FileDialogs.SaveFileAsync(baseName, "." + extension);
        if (string.IsNullOrWhiteSpace(path)) return;
        bool ok = exporter(path, selected, out string error);
        ShowStatus(ok ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            ok ? "Tickets exported" : "Ticket export failed",
            ok ? "工單已匯出" : "匯出工單失敗",
            ok ? $"Exported {selected.Count} selected ticket(s); the export reflects the active filtered selection." : error,
            ok ? $"已匯出 {selected.Count} 張所選工單；匯出內容跟目前篩選揀選一致。" : error);
    }

    private void TicketsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem item && args.Item is TicketRow row)
        {
            AutomationProperties.SetName(item, $"{row.Summary}. {row.Details}");
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SupportTicketFolderRequest request = _service.RequestOpenApplicationDataFolder();
            FolderPathBox.Text = request.Path;
            ShowStatus(
                request.OpenRequested ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                request.OpenRequested ? "Folder opening requested" : "Folder could not be opened",
                request.OpenRequested ? "已要求開啟資料夾" : "開唔到資料夾",
                request.OpenRequested
                    ? "The file manager was asked to show this path. Nothing was deleted."
                    : request.Error ?? "The file manager did not accept the request. Nothing was deleted.",
                request.OpenRequested
                    ? "已要求檔案管理員顯示呢個路徑。冇刪除任何嘢。"
                    : request.Error ?? "檔案管理員冇接受要求。冇刪除任何嘢。");
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Folder opening failed", "開啟資料夾失敗", exception.Message, exception.Message);
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string titleEn, string titleZh, string messageEn, string messageZh)
    {
        AppNoticeSeverity noticeSeverity = severity switch
        {
            InfoBarSeverity.Success => AppNoticeSeverity.Success,
            InfoBarSeverity.Warning => AppNoticeSeverity.Warning,
            InfoBarSeverity.Error => AppNoticeSeverity.Error,
            _ => AppNoticeSeverity.Informational,
        };
        AppNotificationService.Publish(new AppNoticeDraft(
            FunnyLevelSettings.I.StyleEnglish(titleEn),
            FunnyLevelSettings.I.StyleCantonese(titleZh),
            FunnyLevelSettings.I.StyleEnglish(messageEn),
            FunnyLevelSettings.I.StyleCantonese(messageZh),
            noticeSeverity,
            Key: "support-tickets.status",
            AutoDismissMs: noticeSeverity is AppNoticeSeverity.Error or AppNoticeSeverity.Warning ? null : 7_000));
    }

    private static string CategoryLabel(SupportTicketCategory category) => Loc.I.Pick(category switch
    {
        SupportTicketCategory.Installation => "Installation",
        SupportTicketCategory.Update => "Update",
        SupportTicketCategory.Accessibility => "Accessibility",
        SupportTicketCategory.DataRecovery => "Data recovery",
        _ => "General",
    }, category switch
    {
        SupportTicketCategory.Installation => "安裝",
        SupportTicketCategory.Update => "更新",
        SupportTicketCategory.Accessibility => "無障礙",
        SupportTicketCategory.DataRecovery => "資料復原",
        _ => "一般",
    });

    private static string SeverityLabel(SupportTicketSeverity severity) => Loc.I.Pick(severity.ToString(), severity switch
    {
        SupportTicketSeverity.High => "高",
        SupportTicketSeverity.Low => "低",
        _ => "普通",
    });

    private static string StatusLabel(SupportTicketStatus status) => Loc.I.Pick(status switch
    {
        SupportTicketStatus.Acknowledged => "Acknowledged",
        SupportTicketStatus.InProgress => "In progress",
        SupportTicketStatus.Resolved => "Resolved",
        _ => "New",
    }, status switch
    {
        SupportTicketStatus.Acknowledged => "已確認",
        SupportTicketStatus.InProgress => "處理中",
        SupportTicketStatus.Resolved => "已完成",
        _ => "新建",
    });

    private sealed record TicketRow(Guid Id, string Summary, string Details);
    private delegate bool TicketExporter(string path, IEnumerable<SupportTicket> tickets, out string error);
}
