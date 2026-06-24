using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 .ssh 入面嘅金鑰 · One key pair discovered under %USERPROFILE%\.ssh.</summary>
public sealed class SshKeyInfo
{
    public string PublicKeyPath { get; init; } = string.Empty;
    public string PrivateKeyPath { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;       // ed25519 / rsa / ecdsa …
    public string Comment { get; init; } = string.Empty;
    public string PublicKeyText { get; init; } = string.Empty;
}

/// <summary>一個遠端 SFTP 條目 · One remote directory entry from SFTP.</summary>
public sealed class SftpEntry
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Length { get; init; }
    public DateTime Modified { get; init; }
    public string DisplaySize => IsDirectory ? "—" : HumanSize(Length);

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{v:0.#} {u[i]}";
    }
}

/// <summary>
/// SSH 工具核心引擎 · The core SSH engine, backed by SSH.NET (pure-managed) plus ssh-keygen.exe
/// (ships with Win11) for key generation. Provides: build a connected client from a profile,
/// run a remote command, open an interactive ShellStream, list/read local keys, generate a new
/// key pair, one-click passwordless deploy of a public key, and SFTP browse/transfer.
/// 全部在進程內完成，連線唔需要外部 binary（除咗產生金鑰用 ssh-keygen）。
/// </summary>
public static class SshService
{
    public static string SshDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

    private static string KnownHostsPath => Path.Combine(SshDir, "winforge_known_hosts");

    // ---- connection --------------------------------------------------------

    /// <summary>
    /// 由設定檔砌一個已連線嘅 SshClient · Build and connect an <see cref="SshClient"/> from a profile.
    /// 會實施 TOFU 主機金鑰檢查（第一次見到就記住，之後改變就拒絕連線以防中間人攻擊）。
    /// Enforces a TOFU host-key check (trust-on-first-use, then refuse on change to block MITM).
    /// </summary>
    public static SshClient Connect(SshProfile p, string secret, TimeSpan? timeout = null)
    {
        var info = BuildConnectionInfo(p, secret);
        info.Timeout = timeout ?? TimeSpan.FromSeconds(20);
        var client = new SshClient(info);
        client.HostKeyReceived += OnHostKeyReceived;
        client.Connect();
        return client;
    }

    public static SftpClient ConnectSftp(SshProfile p, string secret, TimeSpan? timeout = null)
    {
        var info = BuildConnectionInfo(p, secret);
        info.Timeout = timeout ?? TimeSpan.FromSeconds(20);
        var client = new SftpClient(info);
        client.HostKeyReceived += OnHostKeyReceived;
        client.Connect();
        return client;
    }

    private static ConnectionInfo BuildConnectionInfo(SshProfile p, string secret)
    {
        if (string.IsNullOrWhiteSpace(p.Host)) throw new InvalidOperationException("Host is empty.");
        if (string.IsNullOrWhiteSpace(p.User)) throw new InvalidOperationException("User is empty.");
        int port = p.Port <= 0 ? 22 : p.Port;

        if (p.Auth == SshAuthKind.PrivateKey)
        {
            if (string.IsNullOrWhiteSpace(p.KeyPath) || !File.Exists(p.KeyPath))
                throw new InvalidOperationException("Private key file not found.");
            var keyFile = string.IsNullOrEmpty(secret)
                ? new PrivateKeyFile(p.KeyPath)
                : new PrivateKeyFile(p.KeyPath, secret);
            var pkMethod = new PrivateKeyAuthenticationMethod(p.User, keyFile);
            return new ConnectionInfo(p.Host, port, p.User, pkMethod);
        }

        // Password (also wire keyboard-interactive so MFA-style password prompts work).
        var pwMethod = new PasswordAuthenticationMethod(p.User, secret ?? string.Empty);
        var kiMethod = new KeyboardInteractiveAuthenticationMethod(p.User);
        kiMethod.AuthenticationPrompt += (_, e) =>
        {
            foreach (var prompt in e.Prompts) prompt.Response = secret ?? string.Empty;
        };
        return new ConnectionInfo(p.Host, port, p.User, pwMethod, kiMethod);
    }

    // ---- TOFU host-key store ----------------------------------------------

    private static void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        try
        {
            var fp = e.FingerPrintSHA256;
            var known = ReadKnownHosts();
            if (known.TryGetValue("default", out var stored))
            {
                // Trust only if it matches what we saw the first time.
                e.CanTrust = string.Equals(stored, fp, StringComparison.Ordinal);
            }
            else
            {
                // First sight: trust and remember (TOFU).
                e.CanTrust = true;
                WriteKnownHost(fp);
            }
        }
        catch { e.CanTrust = true; /* never harden into a lock-out on store errors */ }
    }

    private static Dictionary<string, string> ReadKnownHosts()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (File.Exists(KnownHostsPath))
                foreach (var line in File.ReadAllLines(KnownHostsPath))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2) d[parts[0]] = parts[1];
                }
        }
        catch { }
        return d;
    }

    private static void WriteKnownHost(string fingerprint)
    {
        try
        {
            Directory.CreateDirectory(SshDir);
            File.AppendAllText(KnownHostsPath, $"default\t{fingerprint}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>清除已記住嘅主機金鑰指紋 · Forget the remembered host-key fingerprint (re-TOFU next time).</summary>
    public static void ResetKnownHosts()
    {
        try { if (File.Exists(KnownHostsPath)) File.Delete(KnownHostsPath); } catch { }
    }

    // ---- exec --------------------------------------------------------------

    /// <summary>連線、跑一句指令、回傳輸出 · Connect, run one command, return its combined output.</summary>
    public static Task<TweakResult> RunCommandAsync(SshProfile p, string secret, string command,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                using var client = Connect(p, secret);
                using var cmd = client.CreateCommand(command);
                var stdout = cmd.Execute();
                var stderr = cmd.Error ?? string.Empty;
                client.Disconnect();
                var body = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
                int code = cmd.ExitStatus ?? 0;
                return code == 0
                    ? TweakResult.Ok("Command finished.", "指令完成。", body.Trim())
                    : TweakResult.Fail($"Exit status {code}.", $"結束狀態 {code}。", body.Trim());
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"連線失敗：{ex.Message}");
            }
        }, ct);

    /// <summary>開一個互動式 shell 串流 · Open an interactive ShellStream (caller owns the client).</summary>
    public static (SshClient client, ShellStream stream) OpenShell(SshProfile p, string secret)
    {
        var client = Connect(p, secret);
        var stream = client.CreateShellStream("xterm-256color", 120, 40, 800, 600, 4096);
        return (client, stream);
    }

    // ---- key management ----------------------------------------------------

    /// <summary>列出 .ssh 入面嘅金鑰 · List key pairs found under ~/.ssh (by their *.pub files).</summary>
    public static List<SshKeyInfo> ListKeys()
    {
        var list = new List<SshKeyInfo>();
        try
        {
            if (!Directory.Exists(SshDir)) return list;
            foreach (var pub in Directory.EnumerateFiles(SshDir, "*.pub"))
            {
                string text = string.Empty;
                try { text = File.ReadAllText(pub).Trim(); } catch { }
                var bits = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                string type = bits.Length > 0 ? PrettyType(bits[0]) : "";
                string comment = bits.Length > 2 ? bits[2] : "";
                var priv = pub[..^4]; // strip ".pub"
                list.Add(new SshKeyInfo
                {
                    PublicKeyPath = pub,
                    PrivateKeyPath = File.Exists(priv) ? priv : string.Empty,
                    Type = type,
                    Comment = comment,
                    PublicKeyText = text,
                });
            }
        }
        catch { }
        return list;
    }

    private static string PrettyType(string algo) => algo switch
    {
        "ssh-ed25519" => "ed25519",
        "ssh-rsa" => "rsa",
        _ when algo.StartsWith("ecdsa") => "ecdsa",
        _ when algo.StartsWith("sk-") => algo.Replace("sk-", "sk-"),
        _ => algo,
    };

    /// <summary>
    /// 用 ssh-keygen 產生新金鑰 · Generate a new key pair via ssh-keygen.exe (ships with Win11).
    /// <paramref name="type"/> = "ed25519" or "rsa"; <paramref name="passphrase"/> may be empty.
    /// </summary>
    public static async Task<TweakResult> GenerateKeyAsync(string type, string fileName, string comment,
        string passphrase, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(SshDir);
            var path = Path.Combine(SshDir, fileName);
            if (File.Exists(path) || File.Exists(path + ".pub"))
                return TweakResult.Fail("A key with that name already exists.", "已經有同名嘅金鑰。");

            string t = type.Equals("rsa", StringComparison.OrdinalIgnoreCase) ? "rsa" : "ed25519";
            var bits = t == "rsa" ? " -b 4096" : "";
            var cmt = string.IsNullOrWhiteSpace(comment) ? $"{Environment.UserName}@winforge" : comment;
            // -N "" empty passphrase; quote everything; -f target path.
            var args = $"-t {t}{bits} -f \"{path}\" -N \"{passphrase}\" -C \"{cmt}\" -q";
            var r = await ShellRunner.Run("ssh-keygen.exe", args, elevated: false, ct);
            if (r.Success && File.Exists(path + ".pub"))
                return TweakResult.Ok($"Generated {fileName} ({t}).", $"已產生 {fileName}（{t}）。",
                    File.ReadAllText(path + ".pub").Trim());
            return TweakResult.Fail("ssh-keygen failed.", "ssh-keygen 失敗。", r.Output);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>讀取一個公鑰檔嘅文字 · Read a public-key file's text, or "".</summary>
    public static string ReadPublicKey(string pubPath)
    {
        try { return File.Exists(pubPath) ? File.ReadAllText(pubPath).Trim() : string.Empty; }
        catch { return string.Empty; }
    }

    // ---- passwordless deploy ----------------------------------------------

    /// <summary>
    /// 一鍵免密碼部署 · One-click passwordless deploy: open a (password) session to the remote,
    /// append <paramref name="publicKeyText"/> to ~/.ssh/authorized_keys (de-duplicating), and
    /// lock down permissions (chmod 700 ~/.ssh && chmod 600 authorized_keys). After this, key
    /// auth works and the password is no longer needed.
    /// </summary>
    public static Task<TweakResult> DeployPublicKeyAsync(SshProfile p, string secret, string publicKeyText,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                var key = publicKeyText.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    return TweakResult.Fail("No public key text to deploy.", "冇公鑰內容可部署。");

                using var client = Connect(p, secret);
                // POSIX one-liner: make ~/.ssh, append the key only if absent, fix permissions.
                // Single-quote the key so the remote shell treats it literally.
                var safeKey = key.Replace("'", "'\\''");
                var script =
                    "mkdir -p ~/.ssh && chmod 700 ~/.ssh && " +
                    "touch ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && " +
                    $"grep -qxF '{safeKey}' ~/.ssh/authorized_keys || echo '{safeKey}' >> ~/.ssh/authorized_keys";
                using var cmd = client.CreateCommand(script);
                cmd.Execute();
                int code = cmd.ExitStatus ?? 0;
                var err = cmd.Error ?? string.Empty;
                client.Disconnect();

                return code == 0
                    ? TweakResult.Ok(
                        "Public key deployed. Future logins should not need a password.",
                        "公鑰已部署。之後登入應該唔再需要密碼。")
                    : TweakResult.Fail($"Remote returned exit {code}.", $"遠端結束代碼 {code}。", err);
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"部署失敗：{ex.Message}");
            }
        }, ct);

    // ---- SFTP --------------------------------------------------------------

    /// <summary>列出遠端目錄 · List a remote directory over SFTP.</summary>
    public static Task<(bool ok, string error, List<SftpEntry> entries)> ListRemoteAsync(
        SshProfile p, string secret, string path, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var entries = new List<SftpEntry>();
            try
            {
                using var sftp = ConnectSftp(p, secret);
                var dir = string.IsNullOrWhiteSpace(path) ? sftp.WorkingDirectory : path;
                foreach (var f in sftp.ListDirectory(dir))
                {
                    if (f.Name == ".") continue;
                    entries.Add(new SftpEntry
                    {
                        Name = f.Name,
                        FullPath = f.FullName,
                        IsDirectory = f.IsDirectory,
                        Length = f.Length,
                        Modified = f.LastWriteTime,
                    });
                }
                sftp.Disconnect();
                entries = entries
                    .OrderByDescending(e => e.IsDirectory)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return (true, string.Empty, entries);
            }
            catch (Exception ex) { return (false, ex.Message, entries); }
        }, ct);

    public static Task<TweakResult> UploadAsync(SshProfile p, string secret, string localPath, string remoteDir,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                using var sftp = ConnectSftp(p, secret);
                var remote = CombineRemote(remoteDir, Path.GetFileName(localPath));
                using var fs = File.OpenRead(localPath);
                sftp.UploadFile(fs, remote, true, null);
                sftp.Disconnect();
                return TweakResult.Ok($"Uploaded {Path.GetFileName(localPath)}.", $"已上載 {Path.GetFileName(localPath)}。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"上載失敗：{ex.Message}"); }
        }, ct);

    public static Task<TweakResult> DownloadAsync(SshProfile p, string secret, string remotePath, string localPath,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                using var sftp = ConnectSftp(p, secret);
                using var fs = File.Create(localPath);
                sftp.DownloadFile(remotePath, fs, null);
                sftp.Disconnect();
                return TweakResult.Ok($"Downloaded to {localPath}.", $"已下載到 {localPath}。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"下載失敗：{ex.Message}"); }
        }, ct);

    public static Task<TweakResult> MakeDirAsync(SshProfile p, string secret, string remoteDir, string newName,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                using var sftp = ConnectSftp(p, secret);
                sftp.CreateDirectory(CombineRemote(remoteDir, newName));
                sftp.Disconnect();
                return TweakResult.Ok($"Created folder {newName}.", $"已建立資料夾 {newName}。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"建立失敗：{ex.Message}"); }
        }, ct);

    public static Task<TweakResult> DeleteRemoteAsync(SshProfile p, string secret, SftpEntry entry,
        CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                using var sftp = ConnectSftp(p, secret);
                if (entry.IsDirectory) sftp.DeleteDirectory(entry.FullPath);
                else sftp.DeleteFile(entry.FullPath);
                sftp.Disconnect();
                return TweakResult.Ok($"Deleted {entry.Name}.", $"已刪除 {entry.Name}。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"刪除失敗：{ex.Message}"); }
        }, ct);

    private static string CombineRemote(string dir, string name)
    {
        dir = string.IsNullOrWhiteSpace(dir) ? "." : dir.TrimEnd('/');
        return $"{dir}/{name}";
    }

    /// <summary>父目錄路徑（用喺 SFTP「向上」按鈕）· The parent of a remote POSIX path.</summary>
    public static string ParentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return "/";
        path = path.TrimEnd('/');
        int i = path.LastIndexOf('/');
        if (i <= 0) return "/";
        return path[..i];
    }

    // ---- OpenSSH client feature check -------------------------------------

    /// <summary>ssh.exe / ssh-keygen.exe 喺咪存在 · Whether the Win11 OpenSSH client is present.</summary>
    public static bool OpenSshClientPresent()
    {
        try
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try { if (File.Exists(Path.Combine(dir.Trim(), "ssh-keygen.exe"))) return true; } catch { }
            }
            var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh-keygen.exe");
            return File.Exists(sys);
        }
        catch { return false; }
    }

    /// <summary>啟用 OpenSSH 客戶端可選功能 · Enable the OpenSSH.Client optional Windows feature.</summary>
    public static Task<TweakResult> EnableOpenSshClientAsync(CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            "Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0", elevated: true, ct);
}
