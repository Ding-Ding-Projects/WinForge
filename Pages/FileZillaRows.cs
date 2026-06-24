using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>站台清單一行 · One Site-Manager row (wraps an <see cref="FtpSite"/>).</summary>
public sealed class SiteRow
{
    public FtpSite Site { get; }
    public SiteRow(FtpSite site) { Site = site; }
    public string Name => string.IsNullOrWhiteSpace(Site.Name) ? Site.Host : Site.Name;
    public string Summary => $"{Site.Protocol} · {Site.User}@{Site.Host}:{(Site.Port > 0 ? Site.Port : Site.DefaultPort)}";
}

/// <summary>共用嘅大小／時間格式 · Shared size + date formatting for both panes.</summary>
internal static class RowFormat
{
    public static string Size(long bytes)
    {
        if (bytes < 0) return "";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{v:0.#} {u[i]}";
    }

    public static string Date(DateTime dt) => dt == default ? "" : dt.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>本機窗一行 · One local-pane row (file or folder).</summary>
public sealed class LocalRow
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }

    public string Glyph => IsDirectory ? "" : "";
    public string SizeText => IsDirectory ? "" : RowFormat.Size(Size);
    public string ModifiedText => RowFormat.Date(Modified);

    public static LocalRow ForDir(DirectoryInfo d) => new()
    {
        Name = d.Name, FullPath = d.FullName, IsDirectory = true, Size = -1, Modified = SafeTime(() => d.LastWriteTime),
    };

    public static LocalRow ForFile(FileInfo f) => new()
    {
        Name = f.Name, FullPath = f.FullName, IsDirectory = false, Size = SafeLen(f), Modified = SafeTime(() => f.LastWriteTime),
    };

    private static long SafeLen(FileInfo f) { try { return f.Length; } catch { return 0; } }
    private static DateTime SafeTime(Func<DateTime> g) { try { return g(); } catch { return default; } }
}

/// <summary>遠端窗一行 · One remote-pane row (wraps a <see cref="RemoteEntry"/>).</summary>
public sealed class RemoteRow
{
    public RemoteEntry Entry { get; }
    public RemoteRow(RemoteEntry entry) { Entry = entry; }

    public string Name => Entry.Name;
    public bool IsDirectory => Entry.IsDirectory;
    public string Glyph => Entry.IsDirectory ? "" : (Entry.IsSymlink ? "" : "");
    public string SizeText => Entry.IsDirectory ? "" : RowFormat.Size(Entry.Size);
    public string ModifiedText => RowFormat.Date(Entry.Modified);
}

/// <summary>傳輸佇列項目狀態 · State of one transfer-queue item.</summary>
public enum QueueStatus { Pending, Active, Done, Failed, Cancelled }

/// <summary>傳輸佇列一行 · One transfer-queue row with live progress (INotifyPropertyChanged).</summary>
public sealed class QueueRow : INotifyPropertyChanged
{
    public bool IsUpload { get; private init; }
    public string LocalPath { get; private init; } = "";
    public string RemotePath { get; private init; } = "";
    public string FileName { get; private init; } = "";

    private double _progress;
    public double Progress { get => _progress; set { _progress = value; OnChanged(); } }

    private QueueStatus _status = QueueStatus.Pending;
    public QueueStatus Status { get => _status; private set { _status = value; OnChanged(); } }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set { _statusText = value; OnChanged(); } }

    public string DirectionGlyph => IsUpload ? "" : ""; // up / down chevrons
    public string PathText => IsUpload ? $"{LocalPath}  →  {RemotePath}" : $"{RemotePath}  →  {LocalPath}";

    public void SetStatus(QueueStatus s, string text) { Status = s; StatusText = text; }

    public static QueueRow Upload(string local, string remote, string name) =>
        new() { IsUpload = true, LocalPath = local, RemotePath = remote, FileName = name, _statusText = "queued · 排隊" };

    public static QueueRow Download(string remote, string local, string name) =>
        new() { IsUpload = false, LocalPath = local, RemotePath = remote, FileName = name, _statusText = "queued · 排隊" };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
