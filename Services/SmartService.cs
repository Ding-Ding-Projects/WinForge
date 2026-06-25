using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 一個 SMART 屬性 · A single parsed SMART attribute (ATA) row.
/// </summary>
public sealed class SmartAttribute
{
    public byte Id { get; init; }
    public string NameEn { get; init; } = "";
    public string NameZh { get; init; } = "";
    public int Current { get; init; }   // normalised value 1..253 (higher usually better)
    public int Worst { get; init; }
    public int Threshold { get; init; }
    public long Raw { get; init; }      // 48-bit raw value
}

/// <summary>
/// 一個實體磁碟嘅快照 · A snapshot of one physical disk's identity + SMART health.
/// </summary>
public sealed class DiskHealth
{
    public int Index { get; init; }                 // \\.\PhysicalDriveN
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Firmware { get; set; } = "";
    public string InterfaceType { get; set; } = ""; // SCSI / IDE / USB / NVMe …
    public long CapacityBytes { get; set; }
    public string MediaType { get; set; } = "";     // SSD / HDD / NVMe / Unknown
    public bool IsNvme { get; set; }

    // Headline health figures (filled when SMART reads succeed)
    public int? TemperatureC { get; set; }
    public long? PowerOnHours { get; set; }
    public long? PowerCycles { get; set; }
    public long? ReallocatedSectors { get; set; }
    public long? PendingSectors { get; set; }
    public long? UncorrectableSectors { get; set; }
    public int? PercentageUsed { get; set; }        // NVMe only
    public long? DataUnitsRead { get; set; }        // NVMe only (in 512KB units already converted to bytes)
    public long? DataUnitsWritten { get; set; }     // NVMe only

    public List<SmartAttribute> Attributes { get; } = new();

    /// <summary>SMART 讀取係咪成功 · Whether any SMART payload was read (vs. WMI-only fallback).</summary>
    public bool SmartRead { get; set; }
    public string? ErrorEn { get; set; }
    public string? ErrorZh { get; set; }

    /// <summary>計算健康等級 · Computed health bucket from thresholds + critical raw counts.</summary>
    public Models.StatusColor Health
    {
        get
        {
            // Bad: any reallocated/pending/uncorrectable sectors, NVMe near end of life,
            // or any attribute at/below its failure threshold.
            bool bad =
                (ReallocatedSectors is > 0) ||
                (UncorrectableSectors is > 0) ||
                (PercentageUsed is >= 100) ||
                Attributes.Any(a => a.Threshold > 0 && a.Current > 0 && a.Current <= a.Threshold);
            if (bad) return Models.StatusColor.Bad;

            // Caution: pending sectors, high wear, hot, or close to threshold.
            bool caution =
                (PendingSectors is > 0) ||
                (PercentageUsed is >= 80) ||
                (TemperatureC is >= 60) ||
                Attributes.Any(a => a.Threshold > 0 && a.Current > 0 && a.Current <= a.Threshold + 10);
            if (caution) return Models.StatusColor.Warn;

            return Models.StatusColor.Good;
        }
    }

    public (string En, string Zh) HealthText() => Health switch
    {
        Models.StatusColor.Good => ("Good", "良好"),
        Models.StatusColor.Warn => ("Caution", "注意"),
        Models.StatusColor.Bad => ("Bad", "不良"),
        _ => ("Unknown", "未知"),
    };
}

/// <summary>
/// 原生 SMART 硬碟健康讀取 · Native SMART disk-health reader.
/// 純 WMI + PInvoke（DeviceIoControl / IOCTL_STORAGE_QUERY_PROPERTY），唔會啟動任何外部程式。
/// Pure WMI + PInvoke; never launches CrystalDiskInfo or any external tool.
/// </summary>
public static class SmartService
{
    // ---------------- Public API ----------------

    /// <summary>列舉所有實體磁碟並讀取 SMART · Enumerate physical disks and read SMART for each.</summary>
    public static List<DiskHealth> Enumerate()
    {
        var disks = EnumerateWmi();
        foreach (var d in disks)
        {
            try
            {
                if (d.IsNvme)
                {
                    if (!ReadNvme(d)) TryReadAtaThenFlag(d);
                }
                else
                {
                    if (!ReadAta(d) && !ReadNvme(d))
                        FlagReadFailed(d);
                }
            }
            catch (Exception ex)
            {
                FlagReadFailed(d, ex.Message);
            }
        }
        return disks;
    }

    /// <summary>只刷新溫度（畀計時器用）· Cheap temperature-only refresh for the auto-refresh timer.</summary>
    public static void RefreshTemperature(DiskHealth d)
    {
        try
        {
            if (d.IsNvme) ReadNvme(d);
            else ReadAta(d);
        }
        catch { /* keep last good value */ }
    }

    private static void TryReadAtaThenFlag(DiskHealth d)
    {
        if (!ReadAta(d)) FlagReadFailed(d);
    }

    private static void FlagReadFailed(DiskHealth d, string? detail = null)
    {
        if (d.SmartRead) return;
        d.ErrorEn = AdminHelper.IsElevated
            ? "SMART data could not be read from this drive (the controller may not expose it)."
            : "SMART data needs administrator rights — showing identity from WMI only.";
        d.ErrorZh = AdminHelper.IsElevated
            ? "無法從呢個磁碟讀取 SMART 資料（控制器可能唔提供）。"
            : "讀取 SMART 需要管理員權限 — 只顯示 WMI 提供嘅基本資料。";
    }

    // ---------------- WMI identity ----------------

    private static List<DiskHealth> EnumerateWmi()
    {
        var list = new List<DiskHealth>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Index, Model, SerialNumber, FirmwareRevision, InterfaceType, Size, MediaType, PNPDeviceID FROM Win32_DiskDrive");
            foreach (ManagementObject mo in searcher.Get())
            {
                int idx = ToInt(mo["Index"], -1);
                if (idx < 0) continue;
                var d = new DiskHealth
                {
                    Index = idx,
                    Model = (mo["Model"]?.ToString() ?? "").Trim(),
                    Serial = (mo["SerialNumber"]?.ToString() ?? "").Trim(),
                    Firmware = (mo["FirmwareRevision"]?.ToString() ?? "").Trim(),
                    InterfaceType = (mo["InterfaceType"]?.ToString() ?? "").Trim(),
                    CapacityBytes = ToLong(mo["Size"], 0),
                    MediaType = (mo["MediaType"]?.ToString() ?? "").Trim(),
                };
                string pnp = (mo["PNPDeviceID"]?.ToString() ?? "").ToUpperInvariant();
                d.IsNvme = pnp.Contains("NVME") || d.InterfaceType.Equals("NVMe", StringComparison.OrdinalIgnoreCase)
                           || d.Model.ToUpperInvariant().Contains("NVME");
                list.Add(d);
            }
        }
        catch { /* fall through with whatever we have */ }

        // Enrich SSD/HDD media type + NVMe bus from MSFT_PhysicalDisk (Storage namespace).
        try
        {
            var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            scope.Connect();
            using var q = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT DeviceId, MediaType, BusType, Size FROM MSFT_PhysicalDisk"));
            foreach (ManagementObject mo in q.Get())
            {
                int idx = ToInt(mo["DeviceId"], -1);
                var d = list.FirstOrDefault(x => x.Index == idx);
                if (d is null) continue;
                ushort media = ToUShort(mo["MediaType"]);
                ushort bus = ToUShort(mo["BusType"]);
                if (string.IsNullOrEmpty(d.MediaType) || d.MediaType == "Fixed hard disk media")
                    d.MediaType = media switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => d.MediaType };
                if (bus == 17) d.IsNvme = true; // BusType 17 == NVMe
                if (d.CapacityBytes == 0) d.CapacityBytes = ToLong(mo["Size"], 0);
            }
        }
        catch { /* Storage namespace may be unavailable; ignore */ }

        foreach (var d in list)
            if (string.IsNullOrEmpty(d.MediaType))
                d.MediaType = d.IsNvme ? "NVMe" : "Unknown";

        return list.OrderBy(d => d.Index).ToList();
    }

    // ---------------- ATA / SATA via SMART_RCV_DRIVE_DATA ----------------

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private static readonly IntPtr INVALID_HANDLE = new(-1);

    private const uint SMART_GET_VERSION = 0x074080;
    private const uint SMART_RCV_DRIVE_DATA = 0x07C088;
    private const byte SMART_CMD = 0xB0;
    private const byte READ_ATTRIBUTES = 0xD0;
    private const byte READ_THRESHOLDS = 0xD1;
    private const byte SMART_CYL_LOW = 0x4F;
    private const byte SMART_CYL_HI = 0xC2;

    [StructLayout(LayoutKind.Sequential)]
    private struct IDEREGS
    {
        public byte bFeaturesReg;
        public byte bSectorCountReg;
        public byte bSectorNumberReg;
        public byte bCylLowReg;
        public byte bCylHighReg;
        public byte bDriveHeadReg;
        public byte bCommandReg;
        public byte bReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SENDCMDINPARAMS
    {
        public uint cBufferSize;
        public IDEREGS irDriveRegs;
        public byte bDriveNumber;
        public byte bReserved1;
        public byte bReserved2;
        public byte bReserved3;
        public uint dwReserved0;
        public uint dwReserved1;
        public uint dwReserved2;
        public uint dwReserved3;
        public byte bBuffer; // first byte of the in-buffer
    }

    private static bool ReadAta(DiskHealth d)
    {
        IntPtr h = OpenDrive(d.Index);
        if (h == INVALID_HANDLE) return false;
        try
        {
            var attrs = AtaReadTable(h, d.Index, READ_ATTRIBUTES);
            if (attrs is null) return false;
            var thr = AtaReadTable(h, d.Index, READ_THRESHOLDS);
            ParseAtaAttributes(d, attrs, thr);
            d.SmartRead = true;
            d.ErrorEn = d.ErrorZh = null;
            return true;
        }
        finally { CloseHandle(h); }
    }

    private static byte[]? AtaReadTable(IntPtr h, int driveIndex, byte feature)
    {
        // Output: SENDCMDOUTPARAMS = 8-byte header (cBufferSize + DRIVERSTATUS) + 512 data bytes.
        const int dataLen = 512;
        int outSize = 16 + dataLen; // generous header allowance
        var inParams = new SENDCMDINPARAMS
        {
            cBufferSize = dataLen,
            bDriveNumber = (byte)driveIndex,
            irDriveRegs = new IDEREGS
            {
                bFeaturesReg = feature,
                bSectorCountReg = 1,
                bSectorNumberReg = 1,
                bCylLowReg = SMART_CYL_LOW,
                bCylHighReg = SMART_CYL_HI,
                bDriveHeadReg = 0xA0,
                bCommandReg = SMART_CMD,
            },
        };

        int inSize = Marshal.SizeOf<SENDCMDINPARAMS>();
        IntPtr inBuf = Marshal.AllocHGlobal(inSize);
        IntPtr outBuf = Marshal.AllocHGlobal(outSize);
        try
        {
            Marshal.StructureToPtr(inParams, inBuf, false);
            if (!DeviceIoControl(h, SMART_RCV_DRIVE_DATA, inBuf, (uint)inSize, outBuf, (uint)outSize, out _, IntPtr.Zero))
                return null;

            // Data begins after cBufferSize(4) + DRIVERSTATUS(4) = 8 bytes.
            var data = new byte[dataLen];
            Marshal.Copy(outBuf + 8, data, 0, dataLen);
            return data;
        }
        finally
        {
            Marshal.FreeHGlobal(inBuf);
            Marshal.FreeHGlobal(outBuf);
        }
    }

    private static void ParseAtaAttributes(DiskHealth d, byte[] attrData, byte[]? thrData)
    {
        d.Attributes.Clear();
        // Layout: byte0 = revision; then up to 30 entries of 12 bytes starting at offset 2.
        var thresholds = new Dictionary<byte, int>();
        if (thrData is not null)
        {
            for (int i = 0; i < 30; i++)
            {
                int off = 2 + i * 12;
                if (off + 1 >= thrData.Length) break;
                byte id = thrData[off];
                if (id == 0) continue;
                thresholds[id] = thrData[off + 1];
            }
        }

        for (int i = 0; i < 30; i++)
        {
            int off = 2 + i * 12;
            if (off + 11 >= attrData.Length) break;
            byte id = attrData[off];
            if (id == 0) continue;
            int current = attrData[off + 3];
            int worst = attrData[off + 4];
            long raw = 0;
            for (int b = 0; b < 6; b++) raw |= (long)attrData[off + 5 + b] << (8 * b);
            int threshold = thresholds.TryGetValue(id, out var t) ? t : 0;

            var (en, zh) = AttrName(id);
            d.Attributes.Add(new SmartAttribute
            {
                Id = id,
                NameEn = en,
                NameZh = zh,
                Current = current,
                Worst = worst,
                Threshold = threshold,
                Raw = raw,
            });

            switch (id)
            {
                case 0x05: d.ReallocatedSectors = raw; break;
                case 0x09: d.PowerOnHours = raw & 0xFFFFFFFFFFFF; break;
                case 0x0C: d.PowerCycles = raw; break;
                case 0xC2: d.TemperatureC = (int)(raw & 0xFF); break; // low byte = current temp
                case 0xC5: d.PendingSectors = raw; break;
                case 0xC6: d.UncorrectableSectors = raw; break;
                case 0xE7: if (d.PercentageUsed is null && raw is >= 0 and <= 100) d.PercentageUsed = (int)(100 - raw); break;
            }
        }
    }

    // ---------------- NVMe via IOCTL_STORAGE_QUERY_PROPERTY ----------------

    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

    // STORAGE_PROPERTY_ID
    private const uint StorageDeviceProtocolSpecificProperty = 50;
    // STORAGE_QUERY_TYPE
    private const uint PropertyStandardQuery = 0;
    // STORAGE_PROTOCOL_TYPE
    private const uint ProtocolTypeNvme = 3;
    // STORAGE_PROTOCOL_NVME_DATA_TYPE
    private const uint NVMeDataTypeLogPage = 2;
    private const uint NVME_LOG_PAGE_HEALTH_INFO = 0x02;

    private static bool ReadNvme(DiskHealth d)
    {
        IntPtr h = OpenDrive(d.Index);
        if (h == INVALID_HANDLE) return false;
        try
        {
            const int logSize = 512;
            int bufSize = 4096; // STORAGE_PROTOCOL_DATA_DESCRIPTOR + log
            IntPtr buf = Marshal.AllocHGlobal(bufSize);
            try
            {
                ZeroMemory(buf, bufSize);
                // STORAGE_PROPERTY_QUERY: PropertyId(4) + QueryType(4) + AdditionalParameters (STORAGE_PROTOCOL_SPECIFIC_DATA)
                Marshal.WriteInt32(buf, 0, (int)StorageDeviceProtocolSpecificProperty);
                Marshal.WriteInt32(buf, 4, (int)PropertyStandardQuery);
                var spec = new STORAGE_PROTOCOL_SPECIFIC_DATA
                {
                    ProtocolType = ProtocolTypeNvme,
                    DataType = NVMeDataTypeLogPage,
                    ProtocolDataRequestValue = NVME_LOG_PAGE_HEALTH_INFO,
                    ProtocolDataRequestSubValue = 0,
                    ProtocolDataOffset = (uint)Marshal.SizeOf<STORAGE_PROTOCOL_SPECIFIC_DATA>(),
                    ProtocolDataLength = logSize,
                };
                Marshal.StructureToPtr(spec, buf + 8, false);

                if (!DeviceIoControl(h, IOCTL_STORAGE_QUERY_PROPERTY, buf, (uint)bufSize, buf, (uint)bufSize, out _, IntPtr.Zero))
                    return false;

                // Returned: STORAGE_PROTOCOL_DATA_DESCRIPTOR { Version(4); Size(4); STORAGE_PROTOCOL_SPECIFIC_DATA }
                // The descriptor's ProtocolSpecificData starts at offset 8; the log follows at +ProtocolDataOffset.
                var outSpec = Marshal.PtrToStructure<STORAGE_PROTOCOL_SPECIFIC_DATA>(buf + 8);
                int logOffset = 8 + (int)outSpec.ProtocolDataOffset;
                if (outSpec.ProtocolDataLength == 0 || logOffset + 64 > bufSize) return false;

                var log = new byte[logSize];
                Marshal.Copy(buf + logOffset, log, 0, Math.Min(logSize, bufSize - logOffset));
                ParseNvmeHealthLog(d, log);
                d.SmartRead = true;
                d.ErrorEn = d.ErrorZh = null;
                return true;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        finally { CloseHandle(h); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROTOCOL_SPECIFIC_DATA
    {
        public uint ProtocolType;
        public uint DataType;
        public uint ProtocolDataRequestValue;
        public uint ProtocolDataRequestSubValue;
        public uint ProtocolDataOffset;
        public uint ProtocolDataLength;
        public uint FixedProtocolReturnData;
        public uint ProtocolDataRequestSubValue2;
        public uint ProtocolDataRequestSubValue3;
        public uint ProtocolDataRequestSubValue4;
    }

    private static void ParseNvmeHealthLog(DiskHealth d, byte[] log)
    {
        // NVMe SMART/Health Information Log (log page 02h):
        // byte 0: critical warning
        // bytes 1-2: composite temperature (Kelvin)
        // byte 5: available spare; byte 5? (we use percentage used at byte 5? ) -> see offsets below.
        byte criticalWarning = log[0];
        int tempKelvin = log[1] | (log[2] << 8);
        if (tempKelvin > 0) d.TemperatureC = tempKelvin - 273;

        int availableSpare = log[3];
        int percentUsed = log[5];
        d.PercentageUsed = percentUsed;

        // Data Units Read/Written: 128-bit LE counts at bytes 32..47 / 48..63.
        // Each unit == 1000 * 512 bytes. We keep the low 64 bits (ample for petabytes).
        const long bytesPerUnit = 512L * 1000L;
        d.DataUnitsRead = SafeMul(ReadLE(log, 32, 8), bytesPerUnit);
        d.DataUnitsWritten = SafeMul(ReadLE(log, 48, 8), bytesPerUnit);

        // Power Cycles: bytes 112..127; Power On Hours: bytes 128..143.
        d.PowerCycles = ReadLE(log, 112, 8);
        d.PowerOnHours = ReadLE(log, 128, 8);

        // Surface a few values as pseudo-attributes for the table view.
        d.Attributes.Clear();
        void Add(byte id, string en, string zh, long raw) =>
            d.Attributes.Add(new SmartAttribute { Id = id, NameEn = en, NameZh = zh, Current = 0, Worst = 0, Threshold = 0, Raw = raw });

        Add(0x01, "Critical Warning", "嚴重警告", criticalWarning);
        Add(0x02, "Composite Temperature (°C)", "綜合溫度（°C）", d.TemperatureC ?? 0);
        Add(0x03, "Available Spare (%)", "可用備援（%）", availableSpare);
        Add(0x05, "Percentage Used (%)", "已使用壽命（%）", percentUsed);
        Add(0x09, "Power-On Hours", "通電時數", d.PowerOnHours ?? 0);
        Add(0x0C, "Power Cycles", "通電次數", d.PowerCycles ?? 0);
        Add(0xF1, "Data Units Written", "寫入資料量（位元組）", d.DataUnitsWritten ?? 0);
        Add(0xF2, "Data Units Read", "讀取資料量（位元組）", d.DataUnitsRead ?? 0);
        Add(0xAE, "Unsafe Shutdowns", "不安全關機次數", ReadLE(log, 144, 8));
        Add(0xC2, "Media Errors", "媒體錯誤", ReadLE(log, 160, 8));
    }

    private static long SafeMul(long a, long b)
    {
        try { return checked(a * b); } catch (OverflowException) { return long.MaxValue; }
    }

    private static long ReadLE(byte[] b, int off, int len)
    {
        long v = 0;
        for (int i = 0; i < len && off + i < b.Length; i++) v |= (long)b[off + i] << (8 * i);
        return v;
    }

    // ---------------- Attribute name table (bilingual) ----------------

    private static (string En, string Zh) AttrName(byte id) => id switch
    {
        0x01 => ("Raw Read Error Rate", "原始讀取錯誤率"),
        0x02 => ("Throughput Performance", "輸送量效能"),
        0x03 => ("Spin-Up Time", "啟動時間"),
        0x04 => ("Start/Stop Count", "啟停次數"),
        0x05 => ("Reallocated Sectors Count", "重新分配磁區數"),
        0x07 => ("Seek Error Rate", "尋道錯誤率"),
        0x08 => ("Seek Time Performance", "尋道效能"),
        0x09 => ("Power-On Hours", "通電時數"),
        0x0A => ("Spin Retry Count", "啟動重試次數"),
        0x0B => ("Recalibration Retries", "重新校準重試"),
        0x0C => ("Power Cycle Count", "通電次數"),
        0x0D => ("Soft Read Error Rate", "軟讀取錯誤率"),
        0xAA => ("Available Reserved Space", "可用保留空間"),
        0xAB => ("Program Fail Count", "編程失敗次數"),
        0xAC => ("Erase Fail Count", "抹除失敗次數"),
        0xAE => ("Unexpected Power Loss", "異常斷電次數"),
        0xB1 => ("Wear Leveling Count", "磨損平衡次數"),
        0xB7 => ("SATA Downshift Error Count", "SATA 降速錯誤"),
        0xB8 => ("End-to-End Error", "端對端錯誤"),
        0xBB => ("Reported Uncorrectable Errors", "報告之不可修正錯誤"),
        0xBC => ("Command Timeout", "指令逾時"),
        0xBD => ("High Fly Writes", "高飛寫入"),
        0xBE => ("Airflow Temperature", "氣流溫度"),
        0xBF => ("G-Sense Error Rate", "震動錯誤率"),
        0xC0 => ("Power-Off Retract Count", "斷電收回次數"),
        0xC1 => ("Load/Unload Cycle Count", "載入／卸載循環"),
        0xC2 => ("Temperature", "溫度"),
        0xC3 => ("Hardware ECC Recovered", "硬體 ECC 修復"),
        0xC4 => ("Reallocation Event Count", "重新分配事件數"),
        0xC5 => ("Current Pending Sector Count", "待處理磁區數"),
        0xC6 => ("Uncorrectable Sector Count", "不可修正磁區數"),
        0xC7 => ("UltraDMA CRC Error Count", "UltraDMA CRC 錯誤"),
        0xC8 => ("Write Error Rate", "寫入錯誤率"),
        0xCA => ("Data Address Mark Errors", "資料位址標記錯誤"),
        0xCC => ("Soft ECC Correction", "軟 ECC 修正"),
        0xCD => ("Thermal Asperity Rate", "熱粗糙率"),
        0xDC => ("Disk Shift", "磁碟偏移"),
        0xDF => ("Load/Unload Retry Count", "載入／卸載重試"),
        0xE1 => ("Load Friction", "載入摩擦"),
        0xE7 => ("SSD Life Left / Temperature", "SSD 剩餘壽命／溫度"),
        0xE8 => ("Endurance Remaining", "剩餘耐用度"),
        0xE9 => ("Media Wearout Indicator", "媒體耗損指標"),
        0xF1 => ("Total LBAs Written", "累計寫入 LBA"),
        0xF2 => ("Total LBAs Read", "累計讀取 LBA"),
        0xFA => ("Read Error Retry Rate", "讀取錯誤重試率"),
        _ => ($"Attribute 0x{id:X2}", $"屬性 0x{id:X2}"),
    };

    // ---------------- Helpers ----------------

    private static IntPtr OpenDrive(int index) => CreateFile(
        $@"\\.\PhysicalDrive{index}",
        GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    private static int ToInt(object? o, int def) => o is null ? def : int.TryParse(o.ToString(), out var v) ? v : def;
    private static long ToLong(object? o, long def) => o is null ? def : long.TryParse(o.ToString(), out var v) ? v : def;
    private static ushort ToUShort(object? o) => o is null ? (ushort)0 : ushort.TryParse(o.ToString(), out var v) ? v : (ushort)0;

    private static void ZeroMemory(IntPtr ptr, int len)
    {
        for (int i = 0; i < len; i++) Marshal.WriteByte(ptr, i, 0);
    }

    // ---------------- PInvoke ----------------

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
