using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生間隔重複記憶卡模組 · Native Anki-style spaced-repetition flashcards.
/// 牌組 CRUD（連每副新／到期／總數）、卡片 CRUD 與搜尋、用 SM-2 演算法排程嘅學習 session
/// （顯示正面 → 揭曉背面 → 評分 Again／Hard／Good／Easy）、統計，同 CSV 匯入／匯出。
/// 全部純 managed C#，資料持久化喺 JSON 檔，首次執行自動建立，唔使任何外部工具。
/// Deck CRUD with per-deck new/due/total counts, card CRUD + search, an SM-2-scheduled study session
/// (front → show answer → grade Again/Hard/Good/Easy), stats, and CSV import/export. Pure managed C#,
/// persisted to a JSON file created silently on first run — no external tools, no installer.
/// </summary>
public sealed partial class FlashcardsModule : Page
{
    private readonly FlashcardService _svc = new();

    private readonly ObservableCollection<DeckVM> _decks = new();
    private readonly ObservableCollection<CardVM> _cards = new();
    private readonly ObservableCollection<DeckStatVM> _deckStats = new();

    private string? _cardsDeckId;
    private string _cardSearch = "";

    // Study-session state.
    private string? _studyDeckId;
    private List<FlashCard> _queue = new();
    private int _queueIndex;
    private int _studiedThisSession;
    private bool _answerShown;

    // 學習卡正面嘅富文字工具列（每實例獨立，主題只影響學習卡）· per-instance toolbar for the study card.
    private RichTextToolbar? _studyToolbar;

    public FlashcardsModule()
    {
        InitializeComponent();
        DeckList.ItemsSource = _decks;
        CardList.ItemsSource = _cards;
        StatsList.ItemsSource = _deckStats;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Loaded += (_, _) => EnsureStudyToolbar();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>建立學習卡正面嘅富文字工具列；主題只影響學習卡容器 · build the study card's toolbar once (theme scoped to the card).</summary>
    private void EnsureStudyToolbar()
    {
        if (_studyToolbar is not null) return;
        try
        {
            _studyToolbar = new RichTextToolbar(StudyFront, RichTextToolbar.Mode.ReadOnly, themeScope: StudyCard);
            StudyToolbarHost.Content = _studyToolbar;
        }
        catch (Exception ex) { CrashLogger.Log("flashcards:toolbar", ex); }
    }

    private void OnLoaded(object? s, RoutedEventArgs e)
    {
        Render();
        RefreshDecks();
        RefreshDeckCombos();
        RefreshStats();
    }

    // ── Bilingual chrome · 雙語介面文字 ─────────────────────────────────────────

    private void Render()
    {
        Header.Title = "Flashcards · 間隔重複記憶卡";
        HeaderBlurb.Text = P(
            "Native Anki-style spaced repetition. Build decks, add cards, then study: see the front, reveal the answer, and grade your recall — the SM-2 algorithm schedules each card's next review. All offline, stored locally.",
            "原生 Anki 式間隔重複記憶。建立牌組、加入卡片，然後學習：睇正面、揭曉答案、再為自己嘅記憶評分 — SM-2 演算法會安排每張卡下次幾時複習。全程離線，資料存喺本機。");

        DecksTab.Header = P("Decks · 牌組", "牌組");
        CardsTab.Header = P("Cards · 卡片", "卡片");
        StudyTab.Header = P("Study · 學習", "學習");
        StatsTab.Header = P("Stats · 統計", "統計");

        NewDeckLbl.Text = P("New deck · 新牌組", "新牌組");
        ImportDeckLbl.Text = P("Import CSV · 匯入 CSV", "匯入 CSV");
        DecksEmptyText.Text = P("No decks yet. Create your first deck to get started.", "未有牌組。建立你嘅第一副牌組就可以開始。");

        AddCardLbl.Text = P("Add card · 加卡片", "加卡片");
        CardSearchBox.PlaceholderText = P("Search front / back / tags · 搜尋正面／背面／標籤", "搜尋正面／背面／標籤");
        CardsEmptyText.Text = P("No cards. Pick a deck and add some cards.", "未有卡片。揀一副牌組再加卡片。");
        CardsDeckHint.Text = P("Double-click a card to edit · 雙擊卡片可編輯", "雙擊卡片可編輯");

        StartStudyLbl.Text = P("Start studying · 開始學習", "開始學習");
        ShowAnswerBtn.Content = P("Show answer · 顯示答案", "顯示答案");
        StudyFrontLbl.Text = P("Front · 正面", "正面");
        StudyBackLbl.Text = P("Back · 背面", "背面");
        AgainBtn.Content = P("Again · 重來", "重來");
        HardBtn.Content = P("Hard · 難", "難");
        GoodBtn.Content = P("Good · 良好", "良好");
        EasyBtn.Content = P("Easy · 容易", "容易");
        if (_queue.Count == 0)
            StudyIdleText.Text = P("Pick a deck above and press Start studying. Only due and new cards appear.",
                "喺上面揀一副牌組再撳「開始學習」。只會出現到期同全新嘅卡片。");

        StatStudiedLbl.Text = P("Studied today · 今日已學", "今日已學");
        StatTomorrowLbl.Text = P("Due tomorrow · 明日到期", "明日到期");
        StatMatureLbl.Text = P("Mature cards · 成熟卡片", "成熟卡片");
        StatTotalLbl.Text = P("Total cards · 卡片總數", "卡片總數");
        StatsDeckHeader.Text = P("Deck maturity · 牌組成熟度", "牌組成熟度");

        // Rebuild VM-held labels so menu/captions follow the language.
        RefreshDecks();
        RefreshStats();
        UpdateStudyProgress();
    }

    // ── Decks · 牌組 ────────────────────────────────────────────────────────────

    private void RefreshDecks()
    {
        _decks.Clear();
        foreach (var d in _svc.Decks())
            _decks.Add(new DeckVM(d, _svc.StatsFor(d.Id)));
        DecksEmpty.Visibility = _decks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DeckList.Visibility = _decks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshDeckCombos()
    {
        var decks = _svc.Decks();

        string? prevCards = _cardsDeckId;
        CardsDeckBox.Items.Clear();
        foreach (var d in decks) CardsDeckBox.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Id });
        SelectComboById(CardsDeckBox, prevCards);

        string? prevStudy = _studyDeckId;
        StudyDeckBox.Items.Clear();
        foreach (var d in decks) StudyDeckBox.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Id });
        SelectComboById(StudyDeckBox, prevStudy);
    }

    private static void SelectComboById(ComboBox box, string? id)
    {
        if (box.Items.Count == 0) { box.SelectedIndex = -1; return; }
        for (int i = 0; i < box.Items.Count; i++)
            if ((box.Items[i] as ComboBoxItem)?.Tag as string == id) { box.SelectedIndex = i; return; }
        box.SelectedIndex = 0;
    }

    private void RefreshDecks_Click(object sender, RoutedEventArgs e)
    {
        RefreshDecks();
        RefreshDeckCombos();
        RefreshStats();
    }

    private async void NewDeck_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptText(P("New deck", "新牌組"), P("Deck name · 牌組名稱", "牌組名稱"), "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var deck = _svc.CreateDeck(name);
        RefreshDecks();
        RefreshDeckCombos();
        RefreshStats();
    }

    private async void DeckRename_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var current = _svc.Decks().FirstOrDefault(d => d.Id == id);
        var name = await PromptText(P("Rename deck", "重新命名牌組"), P("Deck name · 牌組名稱", "牌組名稱"), current?.Name ?? "");
        if (string.IsNullOrWhiteSpace(name)) return;
        _svc.RenameDeck(id, name);
        RefreshDecks();
        RefreshDeckCombos();
        RefreshStats();
    }

    private async void DeckDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var d = _svc.Decks().FirstOrDefault(x => x.Id == id);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete deck?", "刪除牌組？"),
            Content = P($"Delete \"{d?.Name}\" and all its cards? This cannot be undone.",
                        $"刪除「{d?.Name}」同埋入面所有卡片？此動作無法復原。"),
            PrimaryButtonText = P("Delete · 刪除", "刪除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        _svc.DeleteDeck(id);
        if (_cardsDeckId == id) _cardsDeckId = null;
        if (_studyDeckId == id) { _studyDeckId = null; EndSession(); }
        RefreshDecks();
        RefreshDeckCombos();
        RefreshCards();
        RefreshStats();
    }

    private void DeckList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (DeckList.SelectedItem is DeckVM vm) GoToCards(vm.Id);
    }

    private void DeckCards_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string id) GoToCards(id);
    }

    private void DeckStudy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        _studyDeckId = id;
        RefreshDeckCombos();
        MainPivot.SelectedItem = StudyTab;
        BeginSession(id);
    }

    private void GoToCards(string deckId)
    {
        _cardsDeckId = deckId;
        RefreshDeckCombos();
        MainPivot.SelectedItem = CardsTab;
        RefreshCards();
    }

    // ── CSV import / export · CSV 匯入／匯出 ─────────────────────────────────────

    private async void ImportDeck_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".csv");
        if (path is null) return;

        // Default the new deck name to the file name.
        var suggested = System.IO.Path.GetFileNameWithoutExtension(path);
        var name = await PromptText(P("Import deck from CSV", "由 CSV 匯入牌組"),
            P("New deck name · 新牌組名稱", "新牌組名稱"), suggested);
        if (string.IsNullOrWhiteSpace(name)) return;

        var deck = _svc.CreateDeck(name);
        int added;
        try { added = _svc.ImportCsv(deck.Id, path); }
        catch (Exception ex)
        {
            _svc.DeleteDeck(deck.Id);
            await Info(P("Import failed", "匯入失敗"), ex.Message);
            return;
        }
        RefreshDecks();
        RefreshDeckCombos();
        RefreshStats();
        await Info(P("Import complete", "匯入完成"),
            P($"Imported {added} card(s) into \"{name}\".", $"已匯入 {added} 張卡片到「{name}」。"));
    }

    private async void DeckExport_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var d = _svc.Decks().FirstOrDefault(x => x.Id == id);
        var path = await FileDialogs.SaveFileAsync((d?.Name ?? "deck") + ".csv", ".csv");
        if (path is null) return;
        try
        {
            _svc.ExportCsv(id, path);
            await Info(P("Export complete", "匯出完成"), P($"Saved to {path}", $"已儲存到 {path}"));
        }
        catch (Exception ex) { await Info(P("Export failed", "匯出失敗"), ex.Message); }
    }

    // ── Cards · 卡片 ────────────────────────────────────────────────────────────

    private void CardsDeck_Changed(object sender, SelectionChangedEventArgs e)
    {
        _cardsDeckId = (CardsDeckBox.SelectedItem as ComboBoxItem)?.Tag as string;
        RefreshCards();
    }

    private void CardSearch_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _cardSearch = sender.Text ?? "";
        RefreshCards();
    }

    private void RefreshCards()
    {
        _cards.Clear();
        if (_cardsDeckId is not null)
            foreach (var c in _svc.Cards(_cardsDeckId, _cardSearch))
                _cards.Add(new CardVM(c));
        bool empty = _cards.Count == 0;
        CardsEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        CardList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        AddCardBtn.IsEnabled = _cardsDeckId is not null;
    }

    private async void AddCard_Click(object sender, RoutedEventArgs e)
    {
        if (_cardsDeckId is null)
        {
            await Info(P("No deck selected", "未揀牌組"), P("Pick a deck first.", "請先揀一副牌組。"));
            return;
        }
        var res = await PromptCard(P("Add card", "加卡片"), "", "", "");
        if (res is null) return;
        _svc.AddCard(_cardsDeckId, res.Value.front, res.Value.back, res.Value.tags);
        RefreshCards();
        RefreshDecks();
        RefreshStats();
    }

    private void CardList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (CardList.SelectedItem is CardVM vm) EditCard(vm.Id);
    }

    private void CardEdit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string id) EditCard(id);
    }

    private async void EditCard(string cardId)
    {
        if (_cardsDeckId is null) return;
        var c = _svc.Cards(_cardsDeckId).FirstOrDefault(x => x.Id == cardId);
        if (c is null) return;
        var res = await PromptCard(P("Edit card", "編輯卡片"), c.Front, c.Back, c.Tags);
        if (res is null) return;
        _svc.UpdateCard(cardId, res.Value.front, res.Value.back, res.Value.tags);
        RefreshCards();
    }

    private async void CardDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete card?", "刪除卡片？"),
            Content = P("This card will be removed permanently.", "此卡片會被永久移除。"),
            PrimaryButtonText = P("Delete · 刪除", "刪除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        _svc.DeleteCard(id);
        RefreshCards();
        RefreshDecks();
        RefreshStats();
    }

    // ── Study session · 學習階段 ────────────────────────────────────────────────

    private void StudyDeck_Changed(object sender, SelectionChangedEventArgs e)
    {
        var id = (StudyDeckBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (id != _studyDeckId) { _studyDeckId = id; EndSession(); }
    }

    private void StartStudy_Click(object sender, RoutedEventArgs e)
    {
        var id = (StudyDeckBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (id is null) return;
        _studyDeckId = id;
        BeginSession(id);
    }

    private void BeginSession(string deckId)
    {
        _queue = _svc.DueCards(deckId);
        _queueIndex = 0;
        _studiedThisSession = 0;
        if (_queue.Count == 0)
        {
            EndSession();
            StudyIdleText.Text = P("Nothing due in this deck right now. Great job — come back later!",
                "呢副牌組暫時冇到期嘅卡片。做得好 — 遲啲再嚟啦！");
            return;
        }
        ShowCurrentCard();
    }

    private void ShowCurrentCard()
    {
        if (_queueIndex >= _queue.Count) { FinishSession(); return; }
        var card = _queue[_queueIndex];
        _answerShown = false;

        StudyIdle.Visibility = Visibility.Collapsed;
        StudyCard.Visibility = Visibility.Visible;

        StudyFront.Text = card.Front;
        StudyBack.Text = card.Back;
        StudyTags.Text = string.IsNullOrWhiteSpace(card.Tags) ? "" : P("Tags: ", "標籤：") + card.Tags;
        StudyTags.Visibility = string.IsNullOrWhiteSpace(card.Tags) ? Visibility.Collapsed : Visibility.Visible;

        StudyBackPanel.Visibility = Visibility.Collapsed;
        StudyDivider.Visibility = Visibility.Collapsed;
        ShowAnswerBtn.Visibility = Visibility.Visible;
        GradeRow.Visibility = Visibility.Collapsed;

        UpdateStudyProgress();
    }

    private void ShowAnswer_Click(object sender, RoutedEventArgs e)
    {
        if (_queueIndex >= _queue.Count) return;
        _answerShown = true;
        StudyBackPanel.Visibility = Visibility.Visible;
        StudyDivider.Visibility = Visibility.Visible;
        ShowAnswerBtn.Visibility = Visibility.Collapsed;
        GradeRow.Visibility = Visibility.Visible;
    }

    private void Grade_Again(object sender, RoutedEventArgs e) => GradeCurrent(ReviewGrade.Again);
    private void Grade_Hard(object sender, RoutedEventArgs e) => GradeCurrent(ReviewGrade.Hard);
    private void Grade_Good(object sender, RoutedEventArgs e) => GradeCurrent(ReviewGrade.Good);
    private void Grade_Easy(object sender, RoutedEventArgs e) => GradeCurrent(ReviewGrade.Easy);

    private void GradeCurrent(ReviewGrade grade)
    {
        if (!_answerShown || _queueIndex >= _queue.Count) return;
        var card = _queue[_queueIndex];
        _svc.Grade(card.Id, grade);
        _studiedThisSession++;

        // "Again" requeues the card later in this session so it gets re-seen.
        if (grade == ReviewGrade.Again)
            _queue.Add(card);

        _queueIndex++;
        ShowCurrentCard();

        // Keep counts/stats live.
        RefreshDecks();
        RefreshStats();
    }

    private void FinishSession()
    {
        EndSession();
        StudyIdleText.Text = P($"Session complete — you reviewed {_studiedThisSession} card(s). Nice work!",
            $"學習完成 — 你複習咗 {_studiedThisSession} 張卡片。做得好！");
        RefreshDecks();
        RefreshStats();
    }

    private void EndSession()
    {
        _queue = new List<FlashCard>();
        _queueIndex = 0;
        StudyCard.Visibility = Visibility.Collapsed;
        StudyIdle.Visibility = Visibility.Visible;
        UpdateStudyProgress();
    }

    private void UpdateStudyProgress()
    {
        if (_queue.Count == 0) { StudyProgress.Text = ""; return; }
        int done = Math.Min(_queueIndex, _queue.Count);
        StudyProgress.Text = P($"Card {Math.Min(_queueIndex + 1, _queue.Count)} of {_queue.Count}  ·  {_studiedThisSession} reviewed",
            $"第 {Math.Min(_queueIndex + 1, _queue.Count)} / {_queue.Count} 張  ·  已複習 {_studiedThisSession}");
    }

    // ── Stats · 統計 ────────────────────────────────────────────────────────────

    private void RefreshStats()
    {
        var s = _svc.OverallStats();
        StatStudiedNum.Text = s.StudiedToday.ToString();
        StatTomorrowNum.Text = s.DueTomorrow.ToString();
        StatMatureNum.Text = s.Mature.ToString();
        StatTotalNum.Text = s.Total.ToString();

        _deckStats.Clear();
        foreach (var d in _svc.Decks())
            _deckStats.Add(new DeckStatVM(d, _svc.StatsFor(d.Id)));
    }

    // ── Small dialog helpers · 對話框小幫手 ─────────────────────────────────────

    private async System.Threading.Tasks.Task<string?> PromptText(string title, string header, string initial)
    {
        var box = new TextBox { Header = header, Text = initial, AcceptsReturn = false };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = P("OK · 確定", "確定"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    private async System.Threading.Tasks.Task<(string front, string back, string tags)?> PromptCard(
        string title, string front, string back, string tags)
    {
        var frontBox = new TextBox { Header = P("Front · 正面", "正面"), Text = front,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 };
        var backBox = new TextBox { Header = P("Back · 背面", "背面"), Text = back,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 };
        var tagsBox = new TextBox { Header = P("Tags (optional, comma-separated) · 標籤（可選，逗號分隔）", "標籤（可選，逗號分隔）"), Text = tags };
        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(frontBox);
        panel.Children.Add(backBox);
        panel.Children.Add(tagsBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = P("Save · 儲存", "儲存"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(frontBox.Text) && string.IsNullOrWhiteSpace(backBox.Text)) return null;
        return (frontBox.Text, backBox.Text, tagsBox.Text);
    }

    private async System.Threading.Tasks.Task Info(string title, string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = P("Close · 關閉", "關閉"),
        };
        await dlg.ShowAsync();
    }
}

// ── View models · 檢視模型 ──────────────────────────────────────────────────────

/// <summary>牌組清單一行 · One deck row in the deck list.</summary>
public sealed class DeckVM
{
    public string Id { get; }
    public string Name { get; }
    public string CountsLine { get; }
    public string DuePill { get; }

    // Bilingual menu labels (computed at build time so they follow the current language).
    public string StudyText { get; } = Loc.I.Pick("Study · 學習", "學習");
    public string CardsText { get; } = Loc.I.Pick("Cards · 卡片", "卡片");
    public string RenameText { get; } = Loc.I.Pick("Rename · 重新命名", "重新命名");
    public string ExportText { get; } = Loc.I.Pick("Export CSV · 匯出 CSV", "匯出 CSV");
    public string DeleteText { get; } = Loc.I.Pick("Delete · 刪除", "刪除");

    public DeckVM(FlashDeck d, DeckStats s)
    {
        Id = d.Id;
        Name = d.Name;
        CountsLine = Loc.I.Pick(
            $"New {s.New}  ·  Due {s.Due}  ·  Total {s.Total}",
            $"新 {s.New}  ·  到期 {s.Due}  ·  共 {s.Total}");
        DuePill = Loc.I.Pick($"{s.Due} due", $"到期 {s.Due}");
    }
}

/// <summary>卡片清單一行 · One card row in the card list.</summary>
public sealed class CardVM
{
    public string Id { get; }
    public string Front { get; }
    public string Back { get; }
    public string SchedLine { get; }
    public string EditText { get; } = Loc.I.Pick("Edit · 編輯", "編輯");
    public string DeleteText { get; } = Loc.I.Pick("Delete · 刪除", "刪除");

    public CardVM(FlashCard c)
    {
        Id = c.Id;
        Front = c.Front;
        Back = c.Back;
        if (c.IsNew)
            SchedLine = Loc.I.Pick("new", "全新");
        else
        {
            var due = DateTimeOffset.FromUnixTimeMilliseconds(c.DueUtc).LocalDateTime;
            SchedLine = Loc.I.Pick($"due {due:yyyy-MM-dd}", $"到期 {due:yyyy-MM-dd}");
        }
    }
}

/// <summary>統計頁牌組一行 · One deck row on the stats page.</summary>
public sealed class DeckStatVM
{
    public string Name { get; }
    public string CountsLine { get; }
    public string MaturityPill { get; }

    public DeckStatVM(FlashDeck d, DeckStats s)
    {
        Name = d.Name;
        CountsLine = Loc.I.Pick(
            $"New {s.New}  ·  Due {s.Due}  ·  Mature {s.Mature}  ·  Total {s.Total}",
            $"新 {s.New}  ·  到期 {s.Due}  ·  成熟 {s.Mature}  ·  共 {s.Total}");
        int pct = s.Total == 0 ? 0 : (int)Math.Round(100.0 * s.Mature / s.Total);
        MaturityPill = Loc.I.Pick($"{pct}% mature", $"成熟 {pct}%");
    }
}
