using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個實體磁碟（用嚟燒映像）· One physical disk (SD-card / USB target for imaging).</summary>
public sealed class PhysicalDisk
{
    /// <summary>磁碟編號 · The disk number, e.g. 1 → \\.\PhysicalDrive1.</summary>
    public int Number { get; init; }
    public string Model { get; init; } = "";
    public long Size { get; init; }
    public string BusType { get; init; } = "";
    public bool IsRemovable { get; init; }
    public bool IsSystem { get; init; }
    public bool IsBoot { get; init; }
    /// <summary>佔住嘅磁碟機代號 · Drive letters carved out of this disk (for dismount + warning).</summary>
    public List<string> Letters { get; init; } = new();

    public string DevicePath => $@"\\.\PhysicalDrive{Number}";
    public string HumanSize => ImagingService.HumanSize(Size);

    /// <summary>係咪可以安全當成燒錄目標（可移除、唔係系統碟）· Whether it is safe to offer as a write target.</summary>
    public bool LooksSafeTarget => IsRemovable && !IsSystem && !IsBoot;

    public string Display
    {
        get
        {
            var letters = Letters.Count > 0 ? $" [{string.Join(" ", Letters)}]" : "";
            var flags = IsSystem || IsBoot ? " ⚠SYSTEM" : (IsRemovable ? "" : " (fixed)");
            return $"Disk {Number} · {Model} · {HumanSize} · {BusType}{letters}{flags}";
        }
    }
}

/// <summary>
/// 樹莓派／SD 卡燒錄引擎 · Raspberry Pi / SD-card imaging engine. Enumerates physical disks (WMI),
/// then writes a raw OS image to <c>\\.\PhysicalDriveN</c> via CreateFile + WriteFile with a full
/// lock/dismount cycle and a strict size guard. DANGEROUS — every caller must gate the write behind a
/// strong drive picker and explicit confirmation. All in-app, no redirect.
/// </summary>
public static class ImagingService
{
    // ── Win32 ────────────────────────────────────────────────────────────────
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

    private const uint FSCTL_LOCK_VOLUME = 0x00090018;
    private const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
    private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    private const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        IntPtr inBuffer, uint inBufferSize, IntPtr outBuffer, uint outBufferSize,
        out uint bytesReturned, IntPtr overlapped);

    /// <summary>
    /// 列出實體磁碟 · Enumerate physical disks with size/bus/removable/system flags and the drive
    /// letters they host (so we can dismount + warn). Read-only.
    /// </summary>
    public static async Task<List<PhysicalDisk>> ListDisks(CancellationToken ct = default)
    {
        // One PowerShell round-trip: disks + their partitions' drive letters + which disk holds C:/boot.
        const string script = @"
$sys = (Get-CimInstance Win32_OperatingSystem).SystemDrive
$disks = Get-Disk | ForEach-Object {
  $d = $_
  $letters = @()
  try { $letters = Get-Partition -DiskNumber $d.Number -ErrorAction SilentlyContinue |
        Where-Object { $_.DriveLetter } | ForEach-Object { ""$($_.DriveLetter):"" } } catch {}
  [pscustomobject]@{
    Number     = $d.Number
    Model      = ($d.FriendlyName  -as [string])
    Size       = [int64]$d.Size
    BusType    = ($d.BusType -as [string])
    Removable  = [bool]($d.BusType -eq 'USB' -or $d.BusType -eq 'SD' -or $d.BusType -eq 'MMC')
    IsBoot     = [bool]$d.IsBoot
    IsSystem   = [bool]$d.IsSystem
    Letters    = $letters
    HasSysDrive= [bool]($letters -contains $sys)
  }
}
$disks | ConvertTo-Json -Depth 4";
        var json = await ShellRunner.CapturePowershellJson(script, ct);
        var list = new List<PhysicalDisk>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var items = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root.EnumerateArray().ToList()
                : new List<System.Text.Json.JsonElement> { root };
            foreach (var el in items)
            {
                var letters = new List<string>();
                if (el.TryGetProperty("Letters", out var le))
                {
                    if (le.ValueKind == System.Text.Json.JsonValueKind.Array)
                        foreach (var l in le.EnumerateArray()) { var s = l.GetString(); if (!string.IsNullOrEmpty(s)) letters.Add(s); }
                    else if (le.ValueKind == System.Text.Json.JsonValueKind.String)
                    { var s = le.GetString(); if (!string.IsNullOrEmpty(s)) letters.Add(s); }
                }
                bool isSystem = GetBool(el, "IsSystem") || GetBool(el, "HasSysDrive");
                list.Add(new PhysicalDisk
                {
                    Number = GetInt(el, "Number"),
                    Model = GetStr(el, "Model"),
                    Size = GetLong(el, "Size"),
                    BusType = GetStr(el, "BusType"),
                    IsRemovable = GetBool(el, "Removable"),
                    IsBoot = GetBool(el, "IsBoot"),
                    IsSystem = isSystem,
                    Letters = letters,
                });
            }
        }
        catch { /* return whatever parsed */ }
        return list.OrderBy(d => d.Number).ToList();
    }

    private static int GetInt(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var i) ? i : 0;
    private static long GetLong(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt64(out var i) ? i : 0;
    private static string GetStr(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? (v.GetString() ?? "") : "";
    private static bool GetBool(System.Text.Json.JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && (v.ValueKind == System.Text.Json.JsonValueKind.True
            || (v.ValueKind == System.Text.Json.JsonValueKind.String && string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase)));

    /// <summary>進度回報 · Progress callback: (bytesWritten, totalBytes).</summary>
    public delegate void ProgressHandler(long written, long total);

    /// <summary>
    /// 將映像原始寫入實體磁碟 · Write a raw image to a physical disk. Performs: size guard (image must
    /// fit), lock+dismount of every hosted volume, raw WriteFile in 4 MiB blocks, then unlock. MUST be
    /// elevated. Returns a bilingual result.
    /// </summary>
    public static Task<TweakResult> WriteImage(PhysicalDisk disk, string imagePath, ProgressHandler? progress,
        CancellationToken ct = default)
        => Task.Run(() => WriteImageCore(disk, imagePath, progress, ct), ct);

    private static TweakResult WriteImageCore(PhysicalDisk disk, string imagePath, ProgressHandler? progress, CancellationToken ct)
    {
        if (!AdminHelper.IsElevated)
            return TweakResult.Fail("Writing to a raw disk needs administrator rights.", "原始寫入磁碟需要管理員權限。");
        if (!File.Exists(imagePath))
            return TweakResult.Fail("Image file not found.", "搵唔到映像檔。");

        long imageSize = new FileInfo(imagePath).Length;
        if (imageSize <= 0)
            return TweakResult.Fail("Image file is empty.", "映像檔係空嘅。");

        // ── HARD SAFETY GUARDS ────────────────────────────────────────────────
        if (disk.IsSystem || disk.IsBoot)
            return TweakResult.Fail("Refusing to write to the system/boot disk.", "拒絕寫入系統／開機磁碟。");
        if (disk.Size <= 0)
            return TweakResult.Fail("Could not read the target disk size.", "讀唔到目標磁碟大細。");
        if (imageSize > disk.Size)
            return TweakResult.Fail(
                $"Image ({HumanSize(imageSize)}) is larger than the target disk ({HumanSize(disk.Size)}).",
                $"映像（{HumanSize(imageSize)}）大過目標磁碟（{HumanSize(disk.Size)}）。");

        // Lock + dismount each hosted volume so the write isn't fighting the filesystem.
        var volumeHandles = new List<SafeFileHandle>();
        try
        {
            foreach (var letter in disk.Letters)
            {
                ct.ThrowIfCancellationRequested();
                var volPath = $@"\\.\{letter.TrimEnd('\\')}"; // e.g. \\.\E:
                var vh = CreateFile(volPath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (vh.IsInvalid) { vh.Dispose(); continue; }
                DeviceIoControl(vh, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                DeviceIoControl(vh, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                volumeHandles.Add(vh); // keep locked for the duration of the write
            }

            using var diskHandle = CreateFile(disk.DevicePath, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
                FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH, IntPtr.Zero);
            if (diskHandle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                return TweakResult.Fail($"Could not open {disk.DevicePath} (error {err}). Run as administrator.",
                    $"開唔到 {disk.DevicePath}（錯誤 {err}）。請以管理員身分執行。");
            }

            // Verify the real device length once more at the metal (defence in depth).
            long deviceLen = QueryDiskLength(diskHandle);
            if (deviceLen > 0 && imageSize > deviceLen)
                return TweakResult.Fail(
                    $"Image ({HumanSize(imageSize)}) is larger than the device ({HumanSize(deviceLen)}).",
                    $"映像（{HumanSize(imageSize)}）大過裝置（{HumanSize(deviceLen)}）。");

            // FILE_FLAG_NO_BUFFERING requires sector-aligned buffers + sizes. Use a 4 MiB block.
            const int block = 4 * 1024 * 1024;
            const int sector = 512;
            var buffer = new byte[block];
            long written = 0;

            using var diskStream = new FileStream(diskHandle, FileAccess.Write, block);
            using var src = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, block);

            int read;
            while ((read = src.Read(buffer, 0, block)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                // No-buffering writes must be a multiple of the sector size; pad the final block with zeros.
                int toWrite = read;
                if (toWrite % sector != 0)
                {
                    int padded = ((toWrite / sector) + 1) * sector;
                    Array.Clear(buffer, toWrite, padded - toWrite);
                    toWrite = padded;
                }
                diskStream.Write(buffer, 0, toWrite);
                written += read;
                progress?.Invoke(Math.Min(written, imageSize), imageSize);
            }
            diskStream.Flush();
            progress?.Invoke(imageSize, imageSize);

            return TweakResult.Ok(
                $"Wrote {HumanSize(imageSize)} to {disk.DevicePath} ({disk.Model}).",
                $"已將 {HumanSize(imageSize)} 寫入 {disk.DevicePath}（{disk.Model}）。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Write cancelled — the card may be left in an unusable state.",
                "已取消寫入 — 張卡可能會處於不可用狀態。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
        finally
        {
            foreach (var vh in volumeHandles)
            {
                try { DeviceIoControl(vh, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero); } catch { }
                vh.Dispose();
            }
        }
    }

    private static long QueryDiskLength(SafeFileHandle h)
    {
        try
        {
            var buf = Marshal.AllocHGlobal(8);
            try
            {
                if (DeviceIoControl(h, IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, buf, 8, out _, IntPtr.Zero))
                    return Marshal.ReadInt64(buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// 寫入後讀回校驗 · After a write, read the disk back and compare it byte-for-byte against the source
    /// image (only the image's length is compared; trailing disk bytes are ignored). Streams in 4 MiB
    /// blocks with live progress and cancellation. Also computes the SHA-256 of the image bytes read back
    /// from the device, returned in the result's Output. MUST be elevated (raw read of the device).
    /// </summary>
    public static Task<TweakResult> VerifyImage(PhysicalDisk disk, string imagePath, ProgressHandler? progress,
        CancellationToken ct = default)
        => Task.Run(() => VerifyImageCore(disk, imagePath, progress, ct), ct);

    private static TweakResult VerifyImageCore(PhysicalDisk disk, string imagePath, ProgressHandler? progress, CancellationToken ct)
    {
        if (!AdminHelper.IsElevated)
            return TweakResult.Fail("Reading back a raw disk needs administrator rights.", "讀回原始磁碟需要管理員權限。");
        if (!File.Exists(imagePath))
            return TweakResult.Fail("Image file not found.", "搵唔到映像檔。");

        long imageSize = new FileInfo(imagePath).Length;
        if (imageSize <= 0)
            return TweakResult.Fail("Image file is empty.", "映像檔係空嘅。");

        try
        {
            using var diskHandle = CreateFile(disk.DevicePath, GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
                FILE_FLAG_NO_BUFFERING, IntPtr.Zero);
            if (diskHandle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                return TweakResult.Fail($"Could not open {disk.DevicePath} for verify (error {err}). Run as administrator.",
                    $"開唔到 {disk.DevicePath} 做校驗（錯誤 {err}）。請以管理員身分執行。");
            }

            const int block = 4 * 1024 * 1024;
            const int sector = 512;
            var diskBuf = new byte[block];
            var imgBuf = new byte[block];
            long compared = 0;

            using var sha = System.Security.Cryptography.SHA256.Create();
            using var diskStream = new FileStream(diskHandle, FileAccess.Read, block);
            using var src = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, block);

            while (compared < imageSize)
            {
                ct.ThrowIfCancellationRequested();

                long remaining = imageSize - compared;
                // No-buffering reads must be sector-aligned in size; read a full (padded) block.
                int want = (int)Math.Min(block, ((remaining + sector - 1) / sector) * sector);

                int diskRead = ReadFull(diskStream, diskBuf, want);
                int imgRead = ReadFull(src, imgBuf, (int)Math.Min(block, remaining));

                if (imgRead <= 0) break;
                int cmp = (int)Math.Min(imgRead, remaining);

                for (int i = 0; i < cmp; i++)
                {
                    if (i >= diskRead || diskBuf[i] != imgBuf[i])
                        return TweakResult.Fail(
                            $"Verify FAILED at byte {compared + i:N0}: the disk does not match the image. Re-write the disk.",
                            $"校驗失敗，喺第 {compared + i:N0} 個位元組：磁碟同映像唔一致。請重新燒錄。");
                }

                sha.TransformBlock(imgBuf, 0, cmp, null, 0);
                compared += cmp;
                progress?.Invoke(Math.Min(compared, imageSize), imageSize);
            }

            if (compared < imageSize)
                return TweakResult.Fail(
                    $"Verify incomplete: only {HumanSize(compared)} of {HumanSize(imageSize)} could be read back.",
                    $"校驗未完成：只讀返到 {HumanSize(compared)}／{HumanSize(imageSize)}。");

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = sha.Hash is { } h ? Convert.ToHexString(h).ToLowerInvariant() : "";
            progress?.Invoke(imageSize, imageSize);

            return TweakResult.Ok(
                $"Verify PASSED — {HumanSize(imageSize)} on {disk.DevicePath} matches the image. SHA-256: {hash}",
                $"校驗通過 — {disk.DevicePath} 上嘅 {HumanSize(imageSize)} 同映像一致。SHA-256：{hash}",
                hash);
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Verify cancelled.", "已取消校驗。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>Read exactly <paramref name="count"/> bytes (or until EOF) into the buffer.</summary>
    private static int ReadFull(Stream s, byte[] buf, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = s.Read(buf, total, count - total);
            if (n <= 0) break;
            total += n;
        }
        return total;
    }

    // ── Boot-partition pre-seed (ssh / wifi / user) ──────────────────────────

    /// <summary>
    /// 喺 boot 分割區預設 Pi 啟動設定 · Pre-seed Raspberry Pi OS boot config onto the FAT boot partition
    /// after flashing: an empty <c>ssh</c> file to enable SSH, <c>wpa_supplicant.conf</c> for Wi-Fi, and
    /// <c>userconf.txt</c> for the first user (password is SHA-512 crypt hashed). Pass the boot drive
    /// letter (e.g. "E:"). Returns the list of files written.
    /// </summary>
    public static TweakResult SeedBootConfig(string bootDriveLetter, bool enableSsh,
        string? wifiSsid, string? wifiPassword, string wifiCountry,
        string? userName, string? userPassword)
    {
        try
        {
            var root = bootDriveLetter.TrimEnd('\\');
            if (!root.EndsWith(":")) root += ":";
            root += "\\";
            if (!Directory.Exists(root))
                return TweakResult.Fail($"Boot partition {bootDriveLetter} not found. Re-insert the card after flashing.",
                    $"搵唔到 boot 分割區 {bootDriveLetter}。燒完之後請重新插卡。");

            var written = new List<string>();

            if (enableSsh)
            {
                File.WriteAllText(Path.Combine(root, "ssh"), "");
                written.Add("ssh");
            }

            if (!string.IsNullOrWhiteSpace(wifiSsid))
            {
                var country = string.IsNullOrWhiteSpace(wifiCountry) ? "GB" : wifiCountry.Trim().ToUpperInvariant();
                var sb = new StringBuilder();
                sb.Append("ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev\n");
                sb.Append($"country={country}\n");
                sb.Append("update_config=1\n\n");
                sb.Append("network={\n");
                sb.Append($"\tssid=\"{Escape(wifiSsid!)}\"\n");
                if (!string.IsNullOrEmpty(wifiPassword))
                    sb.Append($"\tpsk=\"{Escape(wifiPassword)}\"\n");
                else
                    sb.Append("\tkey_mgmt=NONE\n");
                sb.Append("}\n");
                File.WriteAllText(Path.Combine(root, "wpa_supplicant.conf"), sb.ToString().Replace("\r\n", "\n"));
                written.Add("wpa_supplicant.conf");
            }

            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrEmpty(userPassword))
            {
                var hash = CryptSha512(userPassword!);
                File.WriteAllText(Path.Combine(root, "userconf.txt"), $"{userName.Trim()}:{hash}\n");
                written.Add("userconf.txt");
            }

            if (written.Count == 0)
                return TweakResult.Fail("Nothing selected to pre-seed.", "冇揀任何要預設嘅嘢。");

            return TweakResult.Ok(
                $"Wrote {string.Join(", ", written)} to {bootDriveLetter}.",
                $"已將 {string.Join("、", written)} 寫入 {bootDriveLetter}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// SHA-512 crypt (the <c>$6$</c> format Raspberry Pi OS' userconf.txt expects). Uses a random salt.
    /// </summary>
    private static string CryptSha512(string password)
    {
        var saltBytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(saltBytes);
        const string saltAlphabet = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var saltSb = new StringBuilder(16);
        foreach (var b in saltBytes) saltSb.Append(saltAlphabet[b % 64]);
        return Sha512Crypt.Crypt(password, saltSb.ToString());
    }

    public static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes;
        int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{Math.Round(s, 1)} {u[i]}";
    }
}
