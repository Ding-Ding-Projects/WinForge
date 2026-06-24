using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace WinForge.Services;

/// <summary>
/// 一個遠端目錄項目（檔案或資料夾）· One remote directory entry shown in the remote pane.
/// Mirrors the shape of a local <see cref="System.IO.FileSystemInfo"/> so both panes share a row type.
/// </summary>
public sealed class RemoteEntry
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public bool IsSymlink { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public string Permissions { get; init; } = "";
}

/// <summary>
/// 信任決定（TOFU）· The result of a trust-on-first-use prompt for an unknown host-key / cert.
/// </summary>
public enum TrustDecision { Reject, Once, Always }

/// <summary>未知主機指紋提示 · Details handed to the UI when an unknown host key / cert appears.</summary>
public sealed class TrustPrompt
{
    public string Kind { get; init; } = "";        // "SFTP host key" / "FTPS certificate"
    public string Host { get; init; } = "";
    public string Fingerprint { get; init; } = ""; // SHA-256
    public string Detail { get; init; } = "";
}

/// <summary>
/// 統一 FTP／FTPS／SFTP 客戶端 · Unified FTP/FTPS/SFTP client wrapping FluentFTP's
/// <see cref="AsyncFtpClient"/> and SSH.NET's <see cref="SftpClient"/> behind one async surface:
/// Connect / List / Download / Upload (with resume) / Rename / Delete / Mkdir. All I/O is async
/// with cancellation and <see cref="IProgress{T}"/>; host-key / cert trust is delegated to the UI
/// via <see cref="TrustCallback"/> (trust-on-first-use). One connection at a time per instance.
/// </summary>
public sealed class FtpService : IDisposable
{
    private readonly FtpSite _site;
    private readonly string _password;

    private AsyncFtpClient? _ftp;
    private SftpClient? _sftp;

    /// <summary>UI 提供嘅 TOFU 提示回呼 · UI-supplied trust prompt; null ⇒ reject unknown keys/certs.</summary>
    public Func<TrustPrompt, Task<TrustDecision>>? TrustCallback { get; set; }

    /// <summary>當使用者揀「永遠信任」· Raised (fingerprint) when the user chose "always trust".</summary>
    public event Action<string>? FingerprintTrusted;

    public FtpService(FtpSite site, string password)
    {
        _site = site;
        _password = password ?? "";
    }

    public bool IsSftp => _site.Protocol == FtpProtocol.Sftp;
    public bool IsConnected => IsSftp ? _sftp?.IsConnected == true : _ftp?.IsConnected == true;

    // ============================ Connect ============================

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (IsSftp) await ConnectSftpAsync(ct);
        else await ConnectFtpAsync(ct);
    }

    private async Task ConnectFtpAsync(CancellationToken ct)
    {
        var config = new FtpConfig
        {
            EncryptionMode = _site.Protocol == FtpProtocol.Ftps ? FtpEncryptionMode.Auto : FtpEncryptionMode.None,
            ValidateAnyCertificate = false,
            DataConnectionType = FtpDataConnectionType.AutoPassive,
            ConnectTimeout = 15000,
            ReadTimeout = 30000,
        };

        var client = new AsyncFtpClient(_site.Host, _site.User, _password,
            _site.Port > 0 ? _site.Port : 21, config);

        // FTPS 憑證信任（TOFU）· FTPS certificate trust-on-first-use.
        client.ValidateCertificate += (control, e) =>
        {
            if (e.PolicyErrors == SslPolicyErrors.None) { e.Accept = true; return; }
            var fp = e.Certificate is null ? "" : e.Certificate.GetCertHashString();
            if (!string.IsNullOrEmpty(_site.TrustedFingerprint) &&
                string.Equals(fp, _site.TrustedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                e.Accept = true; return;
            }
            // 同步回呼入面唔可以 await；用阻塞式等 UI 決定 · synchronous callback: block on the UI prompt.
            var decision = AskTrust(new TrustPrompt
            {
                Kind = "FTPS certificate · FTPS 憑證",
                Host = _site.Host,
                Fingerprint = fp,
                Detail = e.PolicyErrorMessage ?? e.PolicyErrors.ToString(),
            });
            if (decision == TrustDecision.Always) FingerprintTrusted?.Invoke(fp);
            e.Accept = decision != TrustDecision.Reject;
        };

        await client.Connect(ct);
        _ftp = client;

        if (!string.IsNullOrWhiteSpace(_site.RemoteDir))
        {
            try { await client.SetWorkingDirectory(_site.RemoteDir, ct); } catch { /* fall back to default */ }
        }
    }

    private async Task ConnectSftpAsync(CancellationToken ct)
    {
        ConnectionInfo info;
        int port = _site.Port > 0 ? _site.Port : 22;

        if (_site.Auth == SftpAuth.KeyFile && !string.IsNullOrWhiteSpace(_site.KeyFilePath))
        {
            var key = string.IsNullOrEmpty(_password)
                ? new PrivateKeyFile(_site.KeyFilePath)
                : new PrivateKeyFile(_site.KeyFilePath, _password);
            var method = new PrivateKeyAuthenticationMethod(_site.User, key);
            info = new ConnectionInfo(_site.Host, port, _site.User, method);
        }
        else
        {
            var method = new PasswordAuthenticationMethod(_site.User, _password);
            info = new ConnectionInfo(_site.Host, port, _site.User, method);
        }

        var client = new SftpClient(info);

        // SFTP host-key TOFU · trust-on-first-use for the server's host key.
        client.HostKeyReceived += (s, e) =>
        {
            var fp = e.FingerPrintSHA256 ?? "";
            if (!string.IsNullOrEmpty(_site.TrustedFingerprint) &&
                string.Equals(fp, _site.TrustedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                e.CanTrust = true; return;
            }
            var decision = AskTrust(new TrustPrompt
            {
                Kind = "SFTP host key · SFTP 主機金鑰",
                Host = _site.Host,
                Fingerprint = fp,
                Detail = $"{e.HostKeyName}, {e.KeyLength}-bit",
            });
            if (decision == TrustDecision.Always) FingerprintTrusted?.Invoke(fp);
            e.CanTrust = decision != TrustDecision.Reject;
        };

        await client.ConnectAsync(ct);
        _sftp = client;

        if (!string.IsNullOrWhiteSpace(_site.RemoteDir))
        {
            try { client.ChangeDirectory(_site.RemoteDir); } catch { /* fall back to home */ }
        }
    }

    /// <summary>喺非同步回呼度阻塞式問 UI · Synchronously resolve the async UI trust prompt.</summary>
    private TrustDecision AskTrust(TrustPrompt prompt)
    {
        if (TrustCallback is null) return TrustDecision.Reject;
        try { return TrustCallback(prompt).GetAwaiter().GetResult(); }
        catch { return TrustDecision.Reject; }
    }

    // ============================ List ============================

    public async Task<string> GetHomeDirectoryAsync(CancellationToken ct)
    {
        if (IsSftp) return _sftp!.WorkingDirectory;
        return await _ftp!.GetWorkingDirectory(ct);
    }

    public async Task<List<RemoteEntry>> ListAsync(string path, CancellationToken ct)
    {
        var list = new List<RemoteEntry>();
        if (IsSftp)
        {
            await foreach (var f in _sftp!.ListDirectoryAsync(path, ct))
            {
                if (f.Name is "." or "..") continue;
                list.Add(new RemoteEntry
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    IsDirectory = f.IsDirectory,
                    IsSymlink = f.IsSymbolicLink,
                    Size = f.Length,
                    Modified = f.LastWriteTime,
                    Permissions = PermString(f.Attributes),
                });
            }
        }
        else
        {
            var items = await _ftp!.GetListing(path, ct);
            foreach (var it in items)
            {
                if (it.Name is "." or "..") continue;
                list.Add(new RemoteEntry
                {
                    Name = it.Name,
                    FullPath = it.FullName,
                    IsDirectory = it.Type == FtpObjectType.Directory,
                    IsSymlink = it.Type == FtpObjectType.Link,
                    Size = it.Size,
                    Modified = it.Modified,
                    Permissions = it.RawPermissions ?? "",
                });
            }
        }
        return list.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string PermString(Renci.SshNet.Sftp.SftpFileAttributes a)
    {
        char R(bool b) => b ? 'r' : '-';
        char W(bool b) => b ? 'w' : '-';
        char X(bool b) => b ? 'x' : '-';
        return $"{R(a.OwnerCanRead)}{W(a.OwnerCanWrite)}{X(a.OwnerCanExecute)}" +
               $"{R(a.GroupCanRead)}{W(a.GroupCanWrite)}{X(a.GroupCanExecute)}" +
               $"{R(a.OthersCanRead)}{W(a.OthersCanWrite)}{X(a.OthersCanExecute)}";
    }

    // ============================ Download ============================

    /// <summary>下載一個遠端檔案到本機（支援續傳）· Download a remote file, resuming if a partial local file exists.</summary>
    public async Task DownloadAsync(string remotePath, string localPath, bool resume,
        IProgress<double> progress, CancellationToken ct)
    {
        if (IsSftp)
        {
            long start = 0;
            var fi = new FileInfo(localPath);
            if (resume && fi.Exists) start = fi.Length;
            long total = await GetSizeAsync(remotePath, ct);

            using var input = _sftp!.OpenRead(remotePath);
            if (start > 0 && start < total) input.Seek(start, SeekOrigin.Begin);
            else start = 0;

            using var output = new FileStream(localPath, start > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None);

            await CopyAsync(input, output, start, total, progress, ct);
        }
        else
        {
            var exists = resume ? FtpLocalExists.Resume : FtpLocalExists.Overwrite;
            var p = new Progress<FtpProgress>(fp => progress.Report(fp.Progress / 100.0));
            await _ftp!.DownloadFile(localPath, remotePath, exists, FtpVerify.None, p, ct);
            progress.Report(1.0);
        }
    }

    // ============================ Upload ============================

    /// <summary>上載一個本機檔案到遠端（支援續傳）· Upload a local file, resuming from the remote size if it exists.</summary>
    public async Task UploadAsync(string localPath, string remotePath, bool resume,
        IProgress<double> progress, CancellationToken ct)
    {
        if (IsSftp)
        {
            long total = new FileInfo(localPath).Length;
            long start = 0;
            if (resume)
            {
                try { var rs = await GetSizeAsync(remotePath, ct); if (rs > 0 && rs < total) start = rs; }
                catch { start = 0; }
            }

            using var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (start > 0) input.Seek(start, SeekOrigin.Begin);

            // append 模式續傳；否則由頭覆寫 · append for resume, otherwise overwrite from byte 0.
            using var output = _sftp!.Open(remotePath, start > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write);
            await CopyAsync(input, output, start, total, progress, ct);
        }
        else
        {
            var exists = resume ? FtpRemoteExists.Resume : FtpRemoteExists.Overwrite;
            var p = new Progress<FtpProgress>(fp => progress.Report(fp.Progress / 100.0));
            await _ftp!.UploadFile(localPath, remotePath, exists, true, FtpVerify.None, p, ct);
            progress.Report(1.0);
        }
    }

    private static async Task CopyAsync(Stream input, Stream output, long start, long total,
        IProgress<double> progress, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long done = start;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (total > 0) progress.Report(Math.Clamp((double)done / total, 0, 1));
        }
        await output.FlushAsync(ct);
        progress.Report(1.0);
    }

    // ============================ Size ============================

    public async Task<long> GetSizeAsync(string remotePath, CancellationToken ct)
    {
        if (IsSftp)
        {
            try { var f = await _sftp!.GetAsync(remotePath, ct); return f.Length; }
            catch { return -1; }
        }
        return await _ftp!.GetFileSize(remotePath, -1, ct);
    }

    // ============================ Mutations ============================

    public async Task<bool> ExistsAsync(string remotePath, CancellationToken ct)
    {
        if (IsSftp) return await _sftp!.ExistsAsync(remotePath, ct);
        try { return (await _ftp!.GetFileSize(remotePath, -1, ct)) >= 0; }
        catch { return false; }
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken ct)
    {
        if (IsSftp) await _sftp!.RenameFileAsync(oldPath, newPath, ct);
        else await _ftp!.Rename(oldPath, newPath, ct);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct)
    {
        if (IsSftp) await _sftp!.DeleteFileAsync(remotePath, ct);
        else await _ftp!.DeleteFile(remotePath, ct);
    }

    public async Task DeleteDirectoryAsync(string remotePath, CancellationToken ct)
    {
        if (IsSftp)
        {
            // SFTP rmdir 要求空目錄；遞迴清乾淨先 · SFTP rmdir needs an empty dir, so recurse first.
            await DeleteSftpTreeAsync(remotePath, ct);
        }
        else
        {
            await _ftp!.DeleteDirectory(remotePath, ct); // FluentFTP deletes recursively
        }
    }

    private async Task DeleteSftpTreeAsync(string path, CancellationToken ct)
    {
        await foreach (var f in _sftp!.ListDirectoryAsync(path, ct))
        {
            if (f.Name is "." or "..") continue;
            if (f.IsDirectory) await DeleteSftpTreeAsync(f.FullName, ct);
            else await _sftp.DeleteFileAsync(f.FullName, ct);
        }
        await _sftp.DeleteDirectoryAsync(path, ct);
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken ct)
    {
        if (IsSftp) await _sftp!.CreateDirectoryAsync(remotePath, ct);
        else await _ftp!.CreateDirectory(remotePath, true, ct);
    }

    // ============================ Path helpers ============================

    /// <summary>合併遠端路徑（永遠用「/」）· Join a remote path using forward slashes.</summary>
    public static string CombineRemote(string dir, string name)
    {
        if (string.IsNullOrEmpty(dir) || dir == "/") return "/" + name.TrimStart('/');
        return dir.TrimEnd('/') + "/" + name.TrimStart('/');
    }

    /// <summary>遠端父目錄 · The parent of a remote path.</summary>
    public static string ParentRemote(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return "/";
        var trimmed = path.TrimEnd('/');
        int i = trimmed.LastIndexOf('/');
        return i <= 0 ? "/" : trimmed[..i];
    }

    // ============================ Dispose ============================

    public void Dispose()
    {
        try { _ftp?.Dispose(); } catch { }
        try { _sftp?.Dispose(); } catch { }
        _ftp = null;
        _sftp = null;
    }
}
