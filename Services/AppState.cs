using System;

namespace WinForge.Services;

/// <summary>
/// 全域應用程式狀態 · Cross-cutting app state shared between suite modules.
/// </summary>
public static class AppState
{
    private static string _currentRepoPath = string.Empty;

    /// <summary>Git 模組目前選中嘅儲存庫資料夾 · The repo folder the Git module is operating on.</summary>
    public static string CurrentRepoPath
    {
        get => _currentRepoPath;
        set
        {
            if (_currentRepoPath == value) return;
            _currentRepoPath = value ?? string.Empty;
            SettingsStore.Set("git.repo", _currentRepoPath);
            RepoChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? RepoChanged;

    private static string _currentArchivePath = string.Empty;
    private static string _currentSourcePath = string.Empty;

    /// <summary>Archives 模組選中嘅壓縮檔 · The archive file the Archives module operates on.</summary>
    public static string CurrentArchivePath
    {
        get => _currentArchivePath;
        set { _currentArchivePath = value ?? string.Empty; SettingsStore.Set("arc.archive", _currentArchivePath); ArchiveChanged?.Invoke(null, EventArgs.Empty); }
    }

    /// <summary>Archives 模組選中嘅來源（要壓縮嘅檔案／資料夾）· The source to compress/add.</summary>
    public static string CurrentSourcePath
    {
        get => _currentSourcePath;
        set { _currentSourcePath = value ?? string.Empty; SettingsStore.Set("arc.source", _currentSourcePath); ArchiveChanged?.Invoke(null, EventArgs.Empty); }
    }

    public static event EventHandler? ArchiveChanged;

    private static string _mediaInput = string.Empty;
    private static string _mediaOutput = string.Empty;

    /// <summary>Media 模組選中嘅輸入檔 · The media module's selected input file.</summary>
    public static string CurrentMediaInput
    {
        get => _mediaInput;
        set { _mediaInput = value ?? string.Empty; SettingsStore.Set("media.in", _mediaInput); MediaChanged?.Invoke(null, EventArgs.Empty); }
    }

    /// <summary>Media 模組選中嘅輸出檔 · The media module's selected output file.</summary>
    public static string CurrentMediaOutput
    {
        get => _mediaOutput;
        set { _mediaOutput = value ?? string.Empty; SettingsStore.Set("media.out", _mediaOutput); MediaChanged?.Invoke(null, EventArgs.Empty); }
    }

    public static event EventHandler? MediaChanged;

    private static string _audioClip = string.Empty;

    /// <summary>
    /// 音訊編輯器目前嘅工作 clip（每次效果之後會換成新嘅暫存檔）·
    /// The Audio Editor's current working clip; each applied effect swaps in a new scratch file.
    /// </summary>
    public static string CurrentAudioClip
    {
        get => _audioClip;
        set
        {
            if (_audioClip == value) return;
            _audioClip = value ?? string.Empty;
            AudioClipChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>音訊編輯器選取範圍（秒）· The editor's selection range in seconds (start, end).</summary>
    public static double AudioSelStart { get; set; }
    public static double AudioSelEnd { get; set; }

    /// <summary>當 clip 換咗（效果套用後等 UI 重畫波形）· Raised when the working clip changes.</summary>
    public static event EventHandler? AudioClipChanged;

    static AppState()
    {
        _currentRepoPath = SettingsStore.Get("git.repo", string.Empty);
        _currentArchivePath = SettingsStore.Get("arc.archive", string.Empty);
        _currentSourcePath = SettingsStore.Get("arc.source", string.Empty);
        _mediaInput = SettingsStore.Get("media.in", string.Empty);
        _mediaOutput = SettingsStore.Get("media.out", string.Empty);
    }
}
