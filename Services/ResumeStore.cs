using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 一份底稿履歷 · One stored "base resume" the user keeps and reuses.
/// 純資料、可變、可無參數建構（俾 System.Text.Json 用）。
/// Plain mutable data, parameterless-constructible for System.Text.Json round-tripping.
/// </summary>
public sealed class ResumeBase
{
    /// <summary>唯一識別碼 · Stable id (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>顯示名 · Display name, e.g. "Software Engineer — 2026".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>履歷內容（markdown／純文字）· The resume content (markdown / plain text).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>最後更新時間（ISO 8601）· Last-updated timestamp (ISO 8601).</summary>
    public string Updated { get; set; } = DateTime.Now.ToString("o");
}

/// <summary>
/// 一次生成輸出（已度身訂造嘅履歷 + 求職信）· One generated output: a tailored resume + cover letter.
/// </summary>
public sealed class ResumeOutput
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>標題（預設由職位描述／時間衍生）· Title (defaults derived from JD / timestamp).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>用咗邊個底稿 · Which base resume id was used (may be "").</summary>
    public string BaseId { get; set; } = string.Empty;

    /// <summary>用咗邊個代理 · Which agent CLI produced it, e.g. "claude".</summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>職位描述（保存以便重生成）· The job description used.</summary>
    public string JobDescription { get; set; } = string.Empty;

    /// <summary>生成嘅履歷 · Tailored resume text.</summary>
    public string Resume { get; set; } = string.Empty;

    /// <summary>生成嘅求職信 · Cover letter text.</summary>
    public string CoverLetter { get; set; } = string.Empty;

    public string Created { get; set; } = DateTime.Now.ToString("o");
}

/// <summary>
/// 履歷寫手嘅持久化儲存 · Persistent store for the Resume &amp; Cover-letter Writer.
/// 模仿 <see cref="RepoStore"/>：靜態、執行緒安全、JSON 寫入 <see cref="SettingsStore"/>
/// （keys: "resume.bases" / "resume.outputs"），任何改動都觸發 <see cref="Changed"/>。
/// Modeled on RepoStore — a static, thread-safe store persisting two JSON lists to the
/// SettingsStore under "resume.bases" and "resume.outputs", raising Changed on any mutation.
/// 全部防禦性寫法，永遠唔會擲例外。Defensive throughout; never throws.
/// </summary>
public static class ResumeStore
{
    private const string BasesKey = "resume.bases";
    private const string OutputsKey = "resume.outputs";

    private static readonly object Gate = new();
    private static readonly List<ResumeBase> _bases = new();
    private static readonly List<ResumeOutput> _outputs = new();

    /// <summary>任何清單一改就觸發 · Raised after any mutation.</summary>
    public static event EventHandler? Changed;

    /// <summary>底稿履歷清單（只讀）· The base-resume list (read-only snapshot).</summary>
    public static IReadOnlyList<ResumeBase> Bases
    {
        get { lock (Gate) return _bases.ToArray(); }
    }

    /// <summary>生成輸出歷史（最新喺前，只讀）· Output history, newest first (read-only snapshot).</summary>
    public static IReadOnlyList<ResumeOutput> Outputs
    {
        get { lock (Gate) return _outputs.OrderByDescending(o => o.Created).ToArray(); }
    }

    static ResumeStore()
    {
        LoadLocked();
    }

    // ---- persistence -------------------------------------------------------

    private static void LoadLocked()
    {
        lock (Gate)
        {
            _bases.Clear();
            _outputs.Clear();
            try
            {
                var bj = SettingsStore.Get(BasesKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(bj))
                {
                    var loaded = JsonSerializer.Deserialize<List<ResumeBase>>(bj);
                    if (loaded is not null)
                        foreach (var e in loaded)
                            if (e is not null && !string.IsNullOrWhiteSpace(e.Id))
                                _bases.Add(e);
                }
            }
            catch { /* 損壞就當空 · ignore corrupt value */ }

            try
            {
                var oj = SettingsStore.Get(OutputsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(oj))
                {
                    var loaded = JsonSerializer.Deserialize<List<ResumeOutput>>(oj);
                    if (loaded is not null)
                        foreach (var e in loaded)
                            if (e is not null && !string.IsNullOrWhiteSpace(e.Id))
                                _outputs.Add(e);
                }
            }
            catch { /* ignore corrupt value */ }
        }
    }

    private static void SaveLocked()
    {
        try
        {
            SettingsStore.Set(BasesKey, JsonSerializer.Serialize(_bases,
                new JsonSerializerOptions { WriteIndented = false }));
            SettingsStore.Set(OutputsKey, JsonSerializer.Serialize(_outputs,
                new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { /* best effort */ }
    }

    private static void RaiseChanged() => Changed?.Invoke(null, EventArgs.Empty);

    // ---- base resumes ------------------------------------------------------

    /// <summary>新增一份底稿履歷 · Add a base resume; returns the new entry (or null on empty name).</summary>
    public static ResumeBase? AddBase(string name, string content = "")
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(n)) n = "Resume";
        lock (Gate)
        {
            var entry = new ResumeBase
            {
                Name = n,
                Content = content ?? string.Empty,
                Updated = DateTime.Now.ToString("o"),
            };
            _bases.Add(entry);
            SaveLocked();
            RaiseChanged();
            return entry;
        }
    }

    /// <summary>更新底稿內容／名稱 · Update a base resume's name and/or content.</summary>
    public static void UpdateBase(string id, string? name = null, string? content = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        bool changed = false;
        lock (Gate)
        {
            var e = _bases.FirstOrDefault(b => b.Id == id);
            if (e is null) return;
            if (name is not null) { e.Name = name.Trim(); changed = true; }
            if (content is not null) { e.Content = content; changed = true; }
            if (changed) { e.Updated = DateTime.Now.ToString("o"); SaveLocked(); }
        }
        if (changed) RaiseChanged();
    }

    /// <summary>移除一份底稿 · Remove a base resume by id.</summary>
    public static void RemoveBase(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        bool removed;
        lock (Gate)
        {
            removed = _bases.RemoveAll(b => b.Id == id) > 0;
            if (removed) SaveLocked();
        }
        if (removed) RaiseChanged();
    }

    public static ResumeBase? GetBase(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (Gate) return _bases.FirstOrDefault(b => b.Id == id);
    }

    // ---- generated outputs -------------------------------------------------

    /// <summary>儲存一次生成輸出到歷史 · Save a generated output to history; returns it.</summary>
    public static ResumeOutput AddOutput(ResumeOutput o)
    {
        if (o is null) o = new ResumeOutput();
        if (string.IsNullOrWhiteSpace(o.Id)) o.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(o.Created)) o.Created = DateTime.Now.ToString("o");
        lock (Gate)
        {
            _outputs.Add(o);
            SaveLocked();
        }
        RaiseChanged();
        return o;
    }

    /// <summary>更新已存輸出（手動編輯後）· Update a saved output after manual edits.</summary>
    public static void UpdateOutput(string id, string? title = null, string? resume = null,
        string? coverLetter = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        bool changed = false;
        lock (Gate)
        {
            var e = _outputs.FirstOrDefault(o => o.Id == id);
            if (e is null) return;
            if (title is not null) { e.Title = title.Trim(); changed = true; }
            if (resume is not null) { e.Resume = resume; changed = true; }
            if (coverLetter is not null) { e.CoverLetter = coverLetter; changed = true; }
            if (changed) SaveLocked();
        }
        if (changed) RaiseChanged();
    }

    /// <summary>移除一次輸出 · Remove an output from history by id.</summary>
    public static void RemoveOutput(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        bool removed;
        lock (Gate)
        {
            removed = _outputs.RemoveAll(o => o.Id == id) > 0;
            if (removed) SaveLocked();
        }
        if (removed) RaiseChanged();
    }

    public static ResumeOutput? GetOutput(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (Gate) return _outputs.FirstOrDefault(o => o.Id == id);
    }
}
