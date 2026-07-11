using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;

namespace WinForge.Services;

public enum ClipKind { Text, Image, Files }

/// <summary>歷史入面一條剪貼簿項目 · One captured clipboard entry.</summary>
public sealed class ClipItem
{
    public ClipKind Kind { get; set; }
    public bool Pinned { get; set; }
    public string Text { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public string Time { get; set; } = "";

    public string Preview => Kind switch
    {
        ClipKind.Text => Text.Length > 200 ? Text.Substring(0, 200) + "…" : Text,
        ClipKind.Image => ImagePath,
        ClipKind.Files => string.Join("\n", Files),
        _ => "",
    };
}

/// <summary>
/// 背景剪貼簿監察 + 歷史（PowerToys/Win+V 式，包圖片同檔案）· Background clipboard monitor + history.
/// Captures text, images (saved as PNG) and copied files; survives while WinForge runs in the tray.
/// Persists to %LocalAppData%\WinForge\clipboard. No redirect.
/// </summary>
public static class ClipboardService
{
    public static ObservableCollection<ClipItem> History { get; } = new();
    public static event Action? Changed;

    private static DispatcherQueue? _dq;
    private static bool _started;
    private static bool _suppress;
    private const int MaxItems = 200;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static string Dir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "clipboard");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    private static string Manifest => Path.Combine(Dir, "history.json");

    public static void Start(DispatcherQueue dq)
    {
        if (_started) return;
        _started = true;
        _dq = dq;
        Load();
        Task.Run(GitInit);
        try { Clipboard.ContentChanged += OnContentChanged; } catch { }
    }

    // ---- local git history: commit every entry/edit; "Clear all" keeps the git log ----
    private static readonly object _gitGate = new();

    private static void GitInit()
    {
        try
        {
            if (!Directory.Exists(Path.Combine(Dir, ".git")))
            {
                RunGit("init -q");
                File.WriteAllText(Path.Combine(Dir, ".gitattributes"), "*.png binary\n");
                GitCommit("init clipboard history");
            }
        }
        catch { }
    }

    /// <summary>Commit the current history (background, serialized). Past commits are never rewritten,
    /// so "Clear all" only adds a commit — the full history stays recoverable via git log.</summary>
    private static void SaveAndGitCommitAsync(string message)
    {
        var snapshot = History.ToList();
        Task.Run(() =>
        {
            SaveSnapshot(snapshot);
            GitCommit(message);
        });
    }

    private static void GitCommit(string message)
    {
        lock (_gitGate)
        {
            try
            {
                RunGit("add -A");
                var msg = AiCommitMessage(message);
                RunGit($"-c user.name=WinForge -c user.email=clipboard@winforge.local commit -q --allow-empty -m \"{msg.Replace("\"", "'")}\"");
            }
            catch { }
        }
    }

    // ---- opencode: AI-written commit messages (background, no window); auto-install opencode + nodejs if absent ----
    private static bool _ocChecked, _ocOk, _installTried;

    private static string AiCommitMessage(string fallback)
    {
        try
        {
            if (!_ocChecked)
            {
                _ocChecked = true;
                _ocOk = RunCapture("opencode", "--version", 6000).Trim().Length > 0;
            }
            if (!_ocOk)
            {
                if (!_installTried) { _installTried = true; Task.Run(EnsureOpenCode); }  // fully automatic, one-shot
                return fallback;
            }
            var hint = History.FirstOrDefault()?.Preview ?? fallback;
            if (hint.Length > 300) hint = hint.Substring(0, 300);
            hint = hint.Replace("\"", "'").Replace('\r', ' ').Replace('\n', ' ');
            var outp = RunCapture("opencode", $"run \"In 8 words or fewer write a git commit message summarising this copied clipboard content: {hint}\"", 25000);
            var line = outp.Split('\n').Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0) ?? "";
            return line.Length is > 0 and <= 120 ? line : fallback;
        }
        catch { return fallback; }
    }

    private static void EnsureOpenCode()
    {
        try
        {
            if (RunCapture("node", "--version", 6000).Trim().Length == 0)
                RunCapture("winget", "install --id OpenJS.NodeJS.LTS -e --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", 600000);
            PackageService.RefreshProcessPath();
            RunCapture("cmd.exe", "/c npm install -g opencode-ai", 600000);
            PackageService.RefreshProcessPath();
            _ocChecked = false; // re-detect on the next commit
        }
        catch { }
    }

    private static string RunCapture(string file, string args, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                WorkingDirectory = Dir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return "";
            // Drain BOTH pipes concurrently and make the timeout real. The old code read stdout to
            // EOF before WaitForExit with stderr also redirected — a chatty child (npm/winget/opencode
            // spew progress to stderr) fills the 64 KB stderr pipe, blocks on write, never exits, and
            // ReadToEnd() then hangs this thread FOREVER (with _gitGate held → every later clipboard
            // commit piles onto the pool → app-wide freeze).
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }
            try { Task.WaitAll(new Task[] { so, se }, 3000); } catch { }
            return so.IsCompletedSuccessfully ? so.Result : "";
        }
        catch { return ""; }
    }

    private static void RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = Dir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return;
            // Redirected pipes MUST be drained: a git that prints more than the pipe buffer blocks on
            // write and never exits; the old WaitForExit(8000) then timed out and LEAKED a live git
            // still holding the repo lock, wedging every later commit.
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(8000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }
            try { Task.WaitAll(new Task[] { so, se }, 2000); } catch { }
        }
        catch { }
    }

    private static void OnContentChanged(object? sender, object e)
    {
        if (_suppress) { _suppress = false; return; }
        _dq?.TryEnqueue(async () => await Capture());
    }

    private static async Task Capture()
    {
        try
        {
            var view = Clipboard.GetContent();

            if (view.Contains(StandardDataFormats.Bitmap))
            {
                var bmpRef = await view.GetBitmapAsync();
                using var ras = await bmpRef.OpenReadAsync();
                var decoder = await BitmapDecoder.CreateAsync(ras);
                var sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var path = Path.Combine(Dir, $"clip-{Stamp()}.png");
                using (var fs = File.Open(path, FileMode.Create))
                using (var outStream = fs.AsRandomAccessStream())
                {
                    var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
                    enc.SetSoftwareBitmap(sb);
                    await enc.FlushAsync();
                }
                Add(new ClipItem { Kind = ClipKind.Image, ImagePath = path, Time = Now() });
            }
            else if (view.Contains(StandardDataFormats.StorageItems))
            {
                var items = await view.GetStorageItemsAsync();
                var files = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                if (files.Count > 0)
                    Add(new ClipItem { Kind = ClipKind.Files, Files = files, Time = Now() });
            }
            else if (view.Contains(StandardDataFormats.Text))
            {
                var text = await view.GetTextAsync();
                if (!string.IsNullOrEmpty(text) &&
                    !(History.FirstOrDefault() is { Kind: ClipKind.Text } last && last.Text == text))
                    Add(new ClipItem { Kind = ClipKind.Text, Text = text, Time = Now() });
            }
        }
        catch { }
    }

    private static void Add(ClipItem item)
    {
        History.Insert(History.Count(i => i.Pinned), item);
        TrimToLimit();
        SaveAndGitCommitAsync($"capture {item.Kind} {item.Time}");
        Changed?.Invoke();
    }

    private static void TrimToLimit()
    {
        while (History.Count > MaxItems)
        {
            var drop = History.LastOrDefault(i => !i.Pinned);
            if (drop is null) break;
            History.Remove(drop);
            TryDeleteImage(drop);
        }
    }

    /// <summary>Put an item back on the clipboard.</summary>
    public static void CopyBack(ClipItem item)
    {
        try
        {
            var dp = new DataPackage();
            if (item.Kind == ClipKind.Text) dp.SetText(item.Text);
            else if (item.Kind == ClipKind.Image && File.Exists(item.ImagePath))
                dp.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(new Uri(item.ImagePath)));
            else if (item.Kind == ClipKind.Files) dp.SetText(string.Join(Environment.NewLine, item.Files));
            else return;
            _suppress = true;
            Clipboard.SetContent(dp);
        }
        catch { _suppress = false; }
    }

    /// <summary>把純文字（剝走任何格式）放返落剪貼簿 · Put plain text (rich formatting stripped) on the clipboard.</summary>
    public static void CopyPlainText(string text)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text ?? "");
            _suppress = true;
            Clipboard.SetContent(dp);
            Clipboard.Flush();
        }
        catch { _suppress = false; }
    }

    // ---- QR codes: encode locally with QRCoder, fully offline ----

    /// <summary>產生 QR 碼 PNG bytes（純本地，唔上網）· Generate QR-code PNG bytes locally (no network).
    /// pixelsPerModule controls the size; larger = sharper / bigger image.</summary>
    public static byte[] GenerateQrPng(string text, int pixelsPerModule = 20)
    {
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("empty text");
        using var gen = new QRCoder.QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var png = new QRCoder.PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>把 text 編成 QR 碼，存做 PNG，回傳檔案路徑 · Encode text as a QR PNG, save it, return the path.</summary>
    public static string SaveQrPng(string text)
    {
        var bytes = GenerateQrPng(text);
        var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"WinForge-QR-{Stamp()}.png");
        File.WriteAllBytes(outPath, bytes);
        return outPath;
    }

    /// <summary>產生 QR 碼，存到暫存 PNG 再放上剪貼簿 · Generate the QR, save a temp PNG, then put it on the clipboard.
    /// Returns the temp PNG path so the caller can preview it.</summary>
    public static string CopyQrToClipboard(string text)
    {
        var bytes = GenerateQrPng(text);
        var tmp = Path.Combine(Dir, $"qr-{Stamp()}.png");
        File.WriteAllBytes(tmp, bytes);
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(new Uri(tmp)));
            _suppress = true;
            Clipboard.SetContent(dp);
            Clipboard.Flush();
        }
        catch { _suppress = false; }
        return tmp;
    }

    public static void Remove(ClipItem item)
    {
        History.Remove(item);
        TryDeleteImage(item);
        SaveAndGitCommitAsync("remove item");
        Changed?.Invoke();
    }

    public static void TogglePin(ClipItem item)
    {
        if (item is null || !History.Contains(item)) return;
        try
        {
            var makePinned = !item.Pinned;
            History.Remove(item);
            item.Pinned = makePinned;
            History.Insert(makePinned ? 0 : History.Count(i => i.Pinned), item);
            SaveAndGitCommitAsync(makePinned ? "pin item" : "unpin item");
            Changed?.Invoke();
        }
        catch { }
    }

    public static void Clear()
    {
        foreach (var i in History) TryDeleteImage(i);
        History.Clear();
        // Commit the cleared state — this does NOT touch .git, so every past entry stays in the git log.
        SaveAndGitCommitAsync("clear all (history preserved in git log)");
        Changed?.Invoke();
    }

    /// <summary>Convert an image item to another format; returns the saved path.</summary>
    public static async Task<string> SaveImageAs(ClipItem item, string ext)
    {
        if (item.Kind != ClipKind.Image || !File.Exists(item.ImagePath)) throw new InvalidOperationException("not an image");
        var encoderId = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
            ".bmp" => BitmapEncoder.BmpEncoderId,
            ".gif" => BitmapEncoder.GifEncoderId,
            ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
            _ => BitmapEncoder.PngEncoderId,
        };
        var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"WinForge-{Stamp()}{ext}");

        using var inFs = File.OpenRead(item.ImagePath);
        using var inRas = inFs.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(inRas);
        var sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        using (var outFs = File.Open(outPath, FileMode.Create))
        using (var outRas = outFs.AsRandomAccessStream())
        {
            var enc = await BitmapEncoder.CreateAsync(encoderId, outRas);
            enc.SetSoftwareBitmap(sb);
            await enc.FlushAsync();
        }
        return outPath;
    }

    private static void TryDeleteImage(ClipItem item)
    {
        if (item.Kind == ClipKind.Image && File.Exists(item.ImagePath))
            try { File.Delete(item.ImagePath); } catch { }
    }

    private static void SaveSnapshot(List<ClipItem> snapshot)
    {
        try { File.WriteAllText(Manifest, JsonSerializer.Serialize(snapshot, JsonOpts)); } catch { }
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(Manifest)) return;
            var list = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(Manifest), JsonOpts);
            if (list is null) return;
            History.Clear();
            foreach (var i in list) History.Add(i);
        }
        catch { }
    }

    private static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
