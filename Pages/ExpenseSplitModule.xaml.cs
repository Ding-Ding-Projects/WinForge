using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 夾錢分帳 · Expense Splitter — add people, log who paid for what, and get a MINIMAL greedy
/// settle-up plan ("X pays Y $Z"). Pure managed; no I/O beyond the clipboard. Bilingual, never throws.
/// </summary>
public sealed partial class ExpenseSplitModule : Page
{
    // People bound to the People ListView; also the ComboBox source for each expense row.
    private readonly ObservableCollection<PersonRow> _people = new();
    private readonly ObservableCollection<ExpenseRow> _expenses = new();
    private int _nextExpenseId = 1;
    private bool _suppress;

    public ExpenseSplitModule()
    {
        InitializeComponent();
        PeopleList.ItemsSource = _people;
        ExpensesList.ItemsSource = _expenses;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        // Detach row change hooks so nothing dangles after we leave the page.
        foreach (var ex in _expenses) ex.Changed -= OnExpenseChanged;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Recompute();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Expense Splitter · 夾錢分帳", "夾錢分帳 · Expense Splitter");
            HeaderBlurb.Text = P("Add everyone, log who paid for what, and get the fewest payments needed to settle up. Great for trips, dinners and shared bills.",
                "加齊人、記低邊個俾咗邊樣，就會算出用最少嘅轉帳搞掂條數。旅行、食飯、夾份埋單啱晒。");
            CurrencyLabel.Text = P("Currency symbol", "貨幣符號");
            PeopleTitle.Text = P("People", "人員");
            NewPersonBox.PlaceholderText = P("Name…", "名…");
            AddPersonBtn.Content = P("Add person", "加人");
            ExpensesTitle.Text = P("Expenses", "支出");
            AddExpenseBtn.Content = P("Add expense", "加一項支出");
            SummaryTitle.Text = P("Summary & settle-up", "總結同找數");
            CopyBtn.Content = P("Copy plan", "複製方案");

            // Refresh per-row placeholders (localized).
            foreach (var ex in _expenses)
                ex.DescPlaceholder = P("What was it for?", "買咗乜？");
        }
        catch { /* never throw from UI text */ }
    }

    // ---- People ----

    private void NewPerson_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) AddPerson_Click(sender, e);
    }

    private void AddPerson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = (NewPersonBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) { Status(P("Type a name first.", "先輸入個名。")); return; }
            if (_people.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            { Status(P("That name is already in the list.", "個名已經喺清單度。")); return; }

            _people.Add(new PersonRow { Name = name });
            NewPersonBox.Text = "";
            SyncPeopleIntoExpenses();
            Recompute();
        }
        catch { Status(P("Could not add that person.", "加唔到呢個人。")); }
    }

    private void RemovePerson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is string name)
            {
                var row = _people.FirstOrDefault(p => p.Name == name);
                if (row != null) _people.Remove(row);
                // Any expense that was paid by this person loses its payer.
                foreach (var ex in _expenses)
                    if (ex.Payer == name) ex.Payer = null;
                SyncPeopleIntoExpenses();
                Recompute();
            }
        }
        catch { Status(P("Could not remove that person.", "移除唔到。")); }
    }

    // ---- Expenses ----

    private void AddExpense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var row = new ExpenseRow
            {
                Id = _nextExpenseId++,
                DescPlaceholder = P("What was it for?", "買咗乜？"),
                Payer = _people.FirstOrDefault()?.Name,
            };
            row.SetPeople(_people.Select(p => p.Name).ToList());
            row.Changed += OnExpenseChanged;
            _expenses.Add(row);
            Recompute();
        }
        catch { Status(P("Could not add an expense.", "加唔到支出。")); }
    }

    private void RemoveExpense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is int id)
            {
                var row = _expenses.FirstOrDefault(x => x.Id == id);
                if (row != null)
                {
                    row.Changed -= OnExpenseChanged;
                    _expenses.Remove(row);
                    Recompute();
                }
            }
        }
        catch { Status(P("Could not remove that expense.", "移除唔到支出。")); }
    }

    private void OnExpenseChanged(object? sender, EventArgs e) => Recompute();

    private void Currency_Changed(object sender, TextChangedEventArgs e) => Recompute();

    // Push the current people names into every expense row's ComboBox source, keeping selection if possible.
    private void SyncPeopleIntoExpenses()
    {
        var names = _people.Select(p => p.Name).ToList();
        foreach (var ex in _expenses) ex.SetPeople(names);
    }

    // ---- Compute + render summary ----

    private ExpenseSplitService.SplitResult _last = new();

    private void Recompute()
    {
        try
        {
            string symbol = string.IsNullOrEmpty(CurrencyBox?.Text) ? "$" : CurrencyBox!.Text.Trim();
            if (string.IsNullOrEmpty(symbol)) symbol = "$";

            var people = _people.Select(p => p.Name).ToList();

            if (people.Count == 0)
            {
                _last = new ExpenseSplitService.SplitResult();
                SummaryBody.Text = "";
                Status(P("Add at least one person to begin.", "先加最少一個人開始。"));
                return;
            }

            // Aggregate what each person paid across all expenses with a valid payer & amount.
            var paid = new Dictionary<string, double>(StringComparer.Ordinal);
            bool anyAmount = false;
            bool missingPayer = false;
            foreach (var ex in _expenses)
            {
                double amt = double.IsNaN(ex.Amount) || ex.Amount < 0 ? 0 : ex.Amount;
                if (amt <= 0) continue;
                if (string.IsNullOrEmpty(ex.Payer)) { missingPayer = true; continue; }
                anyAmount = true;
                if (!paid.ContainsKey(ex.Payer)) paid[ex.Payer] = 0;
                paid[ex.Payer] += amt;
            }

            _last = ExpenseSplitService.Compute(people, paid);
            RenderSummary(symbol);

            if (!anyAmount)
                Status(P("Add an expense with a payer and amount to see the split.", "加一項有付款人同金額嘅支出就會計數。"));
            else if (missingPayer)
                Status(P("Some expenses have no payer — pick one for each.", "有啲支出未揀付款人 — 逐項揀返。"));
            else if (_last.Transfers.Count == 0)
                Status(P("All settled — no transfers needed.", "全部找清，唔使轉帳。"));
            else
                Status(P($"{_last.Transfers.Count} transfer(s) to settle up.", $"要 {_last.Transfers.Count} 次轉帳搞掂。"));
        }
        catch
        {
            Status(P("Something went wrong computing the split.", "計數時出咗少少問題。"));
        }
    }

    private void RenderSummary(string symbol)
    {
        try
        {
            var lines = new List<string>
            {
                P($"People: {_last.PeopleCount}   Total: {ExpenseSplitService.Money(symbol, _last.GrandTotal)}   Fair share: {ExpenseSplitService.Money(symbol, _last.FairShare)}",
                  $"人數：{_last.PeopleCount}   總數：{ExpenseSplitService.Money(symbol, _last.GrandTotal)}   人均：{ExpenseSplitService.Money(symbol, _last.FairShare)}"),
                "",
                P("Balances:", "結餘："),
            };

            foreach (var b in _last.Balances)
            {
                string tag = b.Net > 0.005 ? P("is owed", "應收")
                           : b.Net < -0.005 ? P("owes", "應付")
                           : P("settled", "已平");
                lines.Add($"  {b.Name}: {P("paid", "已付")} {ExpenseSplitService.Money(symbol, b.Paid)}, {tag} {ExpenseSplitService.Money(symbol, Math.Abs(b.Net))}");
            }

            lines.Add("");
            lines.Add(P("Transfers:", "轉帳："));
            if (_last.Transfers.Count == 0)
                lines.Add(P("  Everyone is settled — nothing to transfer.", "  大家已經找清，唔使轉帳。"));
            else
                foreach (var t in _last.Transfers)
                    lines.Add("  " + P($"{t.From} pays {t.To} {ExpenseSplitService.Money(symbol, t.Amount)}",
                                        $"{t.From} 俾 {t.To} {ExpenseSplitService.Money(symbol, t.Amount)}"));

            SummaryBody.Text = string.Join(Environment.NewLine, lines);
        }
        catch { SummaryBody.Text = ""; }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_people.Count == 0) { Status(P("Nothing to copy yet.", "暫時冇嘢可以複製。")); return; }
            string symbol = string.IsNullOrEmpty(CurrencyBox?.Text) ? "$" : CurrencyBox!.Text.Trim();
            if (string.IsNullOrEmpty(symbol)) symbol = "$";

            string plan = ExpenseSplitService.BuildPlanText(_last, symbol, P);
            var pkg = new DataPackage();
            pkg.SetText(plan);
            Clipboard.SetContent(pkg);
            Status(P("Settle-up plan copied to the clipboard.", "找數方案已複製到剪貼簿。"));
        }
        catch { Status(P("Could not copy the plan.", "複製唔到方案。")); }
    }

    private void Status(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }

    // ---------- Row view-models (classic {Binding}) ----------

    private sealed class PersonRow
    {
        public string Name { get; set; } = "";
    }

    private sealed class ExpenseRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Changed;

        public int Id { get; set; }

        private string _description = "";
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnChanged(); } }
        }

        private string _descPlaceholder = "";
        public string DescPlaceholder
        {
            get => _descPlaceholder;
            set { if (_descPlaceholder != value) { _descPlaceholder = value; Raise(nameof(DescPlaceholder)); } }
        }

        private string? _payer;
        public string? Payer
        {
            get => _payer;
            set { if (_payer != value) { _payer = value; OnChanged(); } }
        }

        private double _amount;
        public double Amount
        {
            get => _amount;
            set { if (_amount != value) { _amount = value; OnChanged(); } }
        }

        // ComboBox source of people names. Kept in sync by the page.
        private ObservableCollection<string> _people = new();
        public ObservableCollection<string> People => _people;

        public void SetPeople(List<string> names)
        {
            _people.Clear();
            foreach (var n in names) _people.Add(n);
            // Drop a stale payer that's no longer a person.
            if (_payer != null && !names.Contains(_payer)) Payer = null;
            Raise(nameof(People));
        }

        private void OnChanged([CallerMemberName] string? name = null)
        {
            Raise(name);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
