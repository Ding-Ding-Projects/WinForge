using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinForge.Services;

/// <summary>
/// 一副記憶卡牌組 · One flashcard deck.
/// </summary>
public sealed class FlashDeck
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public long CreatedUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 一張記憶卡，連同 SM-2 排程狀態 · One flashcard plus its SM-2 scheduling state.
/// </summary>
public sealed class FlashCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeckId { get; set; } = "";
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";
    public string Tags { get; set; } = "";

    // ── SM-2 scheduling state · SM-2 排程狀態 ──
    /// <summary>難度系數 · ease factor (SM-2 EF). Starts at 2.5, floored at 1.3.</summary>
    public double Ease { get; set; } = 2.5;
    /// <summary>目前間隔（日）· current interval in days.</summary>
    public int IntervalDays { get; set; } = 0;
    /// <summary>連續答啱次數 · consecutive successful repetitions.</summary>
    public int Repetitions { get; set; } = 0;
    /// <summary>下次到期（UTC 毫秒；0 = 全新未學）· next-due epoch ms; 0 means brand-new.</summary>
    public long DueUtc { get; set; } = 0;
    /// <summary>最後一次複習（UTC 毫秒；0 = 未複習）· last reviewed epoch ms; 0 = never.</summary>
    public long LastReviewedUtc { get; set; } = 0;
    public long CreatedUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore]
    public bool IsNew => Repetitions == 0 && LastReviewedUtc == 0;

    /// <summary>到期（含全新卡）· due now (new cards count as due).</summary>
    public bool IsDue(long nowMs) => IsNew || DueUtc <= nowMs;

    /// <summary>成熟卡（間隔 ≥ 21 日）· mature card per Anki convention (interval ≥ 21 days).</summary>
    [JsonIgnore]
    public bool IsMature => IntervalDays >= 21;
}

/// <summary>複習評分 · A review grade (maps to SM-2 quality).</summary>
public enum ReviewGrade { Again = 0, Hard = 1, Good = 2, Easy = 3 }

/// <summary>每副牌組嘅統計 · Per-deck counts.</summary>
public readonly record struct DeckStats(int New, int Due, int Total, int Mature);

/// <summary>整體學習統計 · Overall study statistics.</summary>
public readonly record struct StudyStats(int StudiedToday, int DueTomorrow, int Mature, int Total);

/// <summary>
/// 原生間隔重複記憶卡儲存與排程 · Native spaced-repetition flashcard store + scheduler.
/// 純 managed C#，用 JSON 檔持久化喺 %LOCALAPPDATA%\WinForge\flashcards.json — 首次執行會自動建立，
/// 唔需要任何外部工具或安裝。實作 SM-2 演算法（難度系數／間隔／重複次數）去計下次到期日。
/// Pure managed C#. Persists to a JSON file under %LOCALAPPDATA%\WinForge — created silently on first
/// run, no external tool or installer. Implements the SM-2 algorithm (ease / interval / repetitions).
/// </summary>
public sealed class FlashcardService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string FilePath = Path.Combine(Dir, "flashcards.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private Store _store;

    private sealed class Store
    {
        public List<FlashDeck> Decks { get; set; } = new();
        public List<FlashCard> Cards { get; set; } = new();
        /// <summary>每日已複習次數（key = 本地日期 yyyy-MM-dd）· reviews per local day.</summary>
        public Dictionary<string, int> ReviewLog { get; set; } = new();
    }

    public FlashcardService()
    {
        _store = Load();
    }

    private static Store Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<Store>(json, JsonOpts);
                if (s is not null) return s;
            }
        }
        catch { /* fall through to fresh store · 失敗就用空白資料 */ }
        return new Store();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(_store, JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch { /* best-effort persistence · 盡力儲存 */ }
    }

    private static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static string TodayKey => DateTime.Now.ToString("yyyy-MM-dd");

    // ── Decks · 牌組 ────────────────────────────────────────────────────────────

    public IReadOnlyList<FlashDeck> Decks()
    {
        lock (_gate) return _store.Decks.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public FlashDeck CreateDeck(string name)
    {
        lock (_gate)
        {
            var deck = new FlashDeck { Name = name.Trim() };
            _store.Decks.Add(deck);
            Save();
            return deck;
        }
    }

    public void RenameDeck(string deckId, string newName)
    {
        lock (_gate)
        {
            var d = _store.Decks.FirstOrDefault(x => x.Id == deckId);
            if (d is null) return;
            d.Name = newName.Trim();
            Save();
        }
    }

    public void DeleteDeck(string deckId)
    {
        lock (_gate)
        {
            _store.Decks.RemoveAll(d => d.Id == deckId);
            _store.Cards.RemoveAll(c => c.DeckId == deckId);
            Save();
        }
    }

    public DeckStats StatsFor(string deckId)
    {
        lock (_gate)
        {
            long now = NowMs;
            var cards = _store.Cards.Where(c => c.DeckId == deckId).ToList();
            int total = cards.Count;
            int neu = cards.Count(c => c.IsNew);
            int due = cards.Count(c => c.IsDue(now));
            int mature = cards.Count(c => c.IsMature);
            return new DeckStats(neu, due, total, mature);
        }
    }

    // ── Cards · 卡片 ────────────────────────────────────────────────────────────

    public IReadOnlyList<FlashCard> Cards(string deckId, string? search = null)
    {
        lock (_gate)
        {
            IEnumerable<FlashCard> q = _store.Cards.Where(c => c.DeckId == deckId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(c =>
                    c.Front.Contains(s, StringComparison.CurrentCultureIgnoreCase) ||
                    c.Back.Contains(s, StringComparison.CurrentCultureIgnoreCase) ||
                    c.Tags.Contains(s, StringComparison.CurrentCultureIgnoreCase));
            }
            return q.OrderByDescending(c => c.CreatedUtc).ToList();
        }
    }

    public FlashCard AddCard(string deckId, string front, string back, string tags)
    {
        lock (_gate)
        {
            var card = new FlashCard
            {
                DeckId = deckId,
                Front = front.Trim(),
                Back = back.Trim(),
                Tags = (tags ?? "").Trim(),
            };
            _store.Cards.Add(card);
            Save();
            return card;
        }
    }

    public void UpdateCard(string cardId, string front, string back, string tags)
    {
        lock (_gate)
        {
            var c = _store.Cards.FirstOrDefault(x => x.Id == cardId);
            if (c is null) return;
            c.Front = front.Trim();
            c.Back = back.Trim();
            c.Tags = (tags ?? "").Trim();
            Save();
        }
    }

    public void DeleteCard(string cardId)
    {
        lock (_gate)
        {
            _store.Cards.RemoveAll(c => c.Id == cardId);
            Save();
        }
    }

    // ── Study · 學習 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 攞牌組內到期（含全新）嘅卡，用嚟做學習 session · The due (and new) cards for a study session.
    /// New cards last so review cards come first; capped by <paramref name="max"/>.
    /// </summary>
    public List<FlashCard> DueCards(string deckId, int max = 1000)
    {
        lock (_gate)
        {
            long now = NowMs;
            return _store.Cards
                .Where(c => c.DeckId == deckId && c.IsDue(now))
                .OrderBy(c => c.IsNew)          // review cards (false) before new (true)
                .ThenBy(c => c.DueUtc)
                .Take(max)
                .ToList();
        }
    }

    /// <summary>
    /// 用 SM-2 演算法評分一張卡，更新間隔／難度系數／重複次數同下次到期 ·
    /// Grade a card with the SM-2 algorithm; updates interval, ease, repetitions and next-due date.
    /// </summary>
    public void Grade(string cardId, ReviewGrade grade)
    {
        lock (_gate)
        {
            var c = _store.Cards.FirstOrDefault(x => x.Id == cardId);
            if (c is null) return;

            // SM-2 quality 0..5. Map our 4 buttons: Again=1 (lapse), Hard=3, Good=4, Easy=5.
            int q = grade switch
            {
                ReviewGrade.Again => 1,
                ReviewGrade.Hard => 3,
                ReviewGrade.Good => 4,
                ReviewGrade.Easy => 5,
                _ => 4,
            };

            if (q < 3)
            {
                // Failed recall: reset repetitions, relearn soon (≈10 min → next due today).
                c.Repetitions = 0;
                c.IntervalDays = 0;
                c.DueUtc = NowMs + (long)TimeSpan.FromMinutes(10).TotalMilliseconds;
            }
            else
            {
                c.Repetitions += 1;
                if (c.Repetitions == 1) c.IntervalDays = 1;
                else if (c.Repetitions == 2) c.IntervalDays = 6;
                else c.IntervalDays = (int)Math.Round(c.IntervalDays * c.Ease);

                // Easy bonus: stretch the interval a little further.
                if (grade == ReviewGrade.Easy)
                    c.IntervalDays = Math.Max(c.IntervalDays + 1, (int)Math.Round(c.IntervalDays * 1.3));

                if (c.IntervalDays < 1) c.IntervalDays = 1;
                c.DueUtc = NowMs + (long)TimeSpan.FromDays(c.IntervalDays).TotalMilliseconds;
            }

            // Update ease factor (SM-2). EF' = EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02)).
            c.Ease += 0.1 - (5 - q) * (0.08 + (5 - q) * 0.02);
            if (c.Ease < 1.3) c.Ease = 1.3;

            c.LastReviewedUtc = NowMs;

            // Tally today's review for stats.
            var key = TodayKey;
            _store.ReviewLog[key] = _store.ReviewLog.TryGetValue(key, out var n) ? n + 1 : 1;

            Save();
        }
    }

    // ── Stats · 統計 ────────────────────────────────────────────────────────────

    public int StudiedToday()
    {
        lock (_gate) return _store.ReviewLog.TryGetValue(TodayKey, out var n) ? n : 0;
    }

    public StudyStats OverallStats()
    {
        lock (_gate)
        {
            long now = NowMs;
            long tomorrowEnd = new DateTimeOffset(DateTime.Today.AddDays(2)).ToUnixTimeMilliseconds(); // end of tomorrow (local)
            int studiedToday = _store.ReviewLog.TryGetValue(TodayKey, out var n) ? n : 0;
            int dueTomorrow = _store.Cards.Count(c => !c.IsNew && c.DueUtc > now && c.DueUtc < tomorrowEnd);
            int mature = _store.Cards.Count(c => c.IsMature);
            return new StudyStats(studiedToday, dueTomorrow, mature, _store.Cards.Count);
        }
    }

    // ── CSV import / export · CSV 匯入／匯出 ─────────────────────────────────────

    /// <summary>匯出牌組做 CSV（Front,Back,Tags）· Export a deck to CSV.</summary>
    public void ExportCsv(string deckId, string path)
    {
        List<FlashCard> cards;
        lock (_gate) cards = _store.Cards.Where(c => c.DeckId == deckId).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Front,Back,Tags");
        foreach (var c in cards)
            sb.AppendLine($"{CsvEscape(c.Front)},{CsvEscape(c.Back)},{CsvEscape(c.Tags)}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>由 CSV 匯入卡片入牌組，回傳加入嘅張數 · Import CSV rows into a deck; returns count added.</summary>
    public int ImportCsv(string deckId, string path)
    {
        var text = File.ReadAllText(path);
        var rows = ParseCsv(text);
        int added = 0;
        lock (_gate)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                // Skip an optional header row.
                if (i == 0 && r.Count >= 2 &&
                    r[0].Trim().Equals("Front", StringComparison.OrdinalIgnoreCase) &&
                    r[1].Trim().Equals("Back", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (r.Count == 0) continue;
                string front = r.Count > 0 ? r[0].Trim() : "";
                string back = r.Count > 1 ? r[1].Trim() : "";
                string tags = r.Count > 2 ? r[2].Trim() : "";
                if (front.Length == 0 && back.Length == 0) continue;
                _store.Cards.Add(new FlashCard { DeckId = deckId, Front = front, Back = back, Tags = tags });
                added++;
            }
            if (added > 0) Save();
        }
        return added;
    }

    private static string CsvEscape(string s)
    {
        s ??= "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    /// <summary>輕量 RFC-4180 CSV 解析器（支援引號、逗號、換行）· Minimal RFC-4180 CSV parser.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else
            {
                switch (ch)
                {
                    case '"': inQuotes = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default: field.Append(ch); break;
                }
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
