using System;
using System.Collections.Generic;
using WinForge.Services;

namespace WinForge.Services;

/// <summary>
/// ASCII / Latin-1 表參考 · Pure-managed reference-table builder for character codes 0–127
/// (optionally extended to 255). No I/O, no redirect — just rows. Bilingual descriptions for
/// the C0 control codes (0–31), DEL (127) and the C1 range (128–159).
/// </summary>
public static class AsciiTableService
{
    /// <summary>One row of the table. Classic {Binding} target — plain public properties.</summary>
    public sealed class AsciiRow
    {
        public int Code { get; init; }
        public string Dec { get; init; } = "";
        public string Hex { get; init; } = "";
        public string Oct { get; init; } = "";
        public string Bin { get; init; } = "";
        public string Char { get; init; } = "";
        public string Name { get; init; } = "";

        /// <summary>What gets copied for the character; empty for non-printable rows.</summary>
        public string CopyChar { get; init; } = "";
        /// <summary>Lower-cased blob used for fast, never-throw filtering.</summary>
        public string Search { get; init; } = "";
    }

    // C0 control codes 0–31: mnemonic + bilingual description.
    private static readonly (string mn, string en, string zh)[] C0 =
    {
        ("NUL", "Null", "空字元"),
        ("SOH", "Start of Heading", "標題開始"),
        ("STX", "Start of Text", "文字開始"),
        ("ETX", "End of Text", "文字結束"),
        ("EOT", "End of Transmission", "傳輸結束"),
        ("ENQ", "Enquiry", "查詢"),
        ("ACK", "Acknowledge", "確認"),
        ("BEL", "Bell", "響鈴"),
        ("BS",  "Backspace", "退格"),
        ("HT",  "Horizontal Tab", "水平定位（Tab）"),
        ("LF",  "Line Feed", "換行"),
        ("VT",  "Vertical Tab", "垂直定位"),
        ("FF",  "Form Feed", "換頁"),
        ("CR",  "Carriage Return", "歸位"),
        ("SO",  "Shift Out", "移出"),
        ("SI",  "Shift In", "移入"),
        ("DLE", "Data Link Escape", "資料連結跳脫"),
        ("DC1", "Device Control 1 (XON)", "裝置控制 1（XON）"),
        ("DC2", "Device Control 2", "裝置控制 2"),
        ("DC3", "Device Control 3 (XOFF)", "裝置控制 3（XOFF）"),
        ("DC4", "Device Control 4", "裝置控制 4"),
        ("NAK", "Negative Acknowledge", "否定確認"),
        ("SYN", "Synchronous Idle", "同步閒置"),
        ("ETB", "End of Transmission Block", "傳輸區塊結束"),
        ("CAN", "Cancel", "取消"),
        ("EM",  "End of Medium", "媒體結束"),
        ("SUB", "Substitute", "替代"),
        ("ESC", "Escape", "跳脫"),
        ("FS",  "File Separator", "檔案分隔"),
        ("GS",  "Group Separator", "群組分隔"),
        ("RS",  "Record Separator", "記錄分隔"),
        ("US",  "Unit Separator", "單位分隔"),
    };

    private static string Bin8(int code)
    {
        try { return Convert.ToString(code & 0xFF, 2).PadLeft(8, '0'); }
        catch { return ""; }
    }

    private static string Oct(int code)
    {
        try { return "0o" + Convert.ToString(code, 8); }
        catch { return ""; }
    }

    /// <summary>Build every row for 0..127, or 0..255 when <paramref name="latin1"/> is true. Never throws.</summary>
    public static List<AsciiRow> Build(bool latin1, Func<string, string, string> pick)
    {
        var rows = new List<AsciiRow>(256);
        int max = latin1 ? 255 : 127;
        for (int i = 0; i <= max; i++)
        {
            try { rows.Add(Row(i, pick)); }
            catch { /* never let one bad code break the table */ }
        }
        return rows;
    }

    private static AsciiRow Row(int code, Func<string, string, string> pick)
    {
        string glyph, name, copy;

        if (code < C0.Length) // 0–31 control codes
        {
            var (mn, en, zh) = C0[code];
            glyph = mn;
            name = $"{mn} — {pick(en, zh)}";
            copy = ((char)code).ToString();
        }
        else if (code == 32)
        {
            glyph = "SP";
            name = $"SP — {pick("Space", "空格")}";
            copy = " ";
        }
        else if (code == 127)
        {
            glyph = "DEL";
            name = $"DEL — {pick("Delete", "刪除")}";
            copy = ((char)code).ToString();
        }
        else if (code >= 128 && code <= 160) // C1 controls + NBSP boundary
        {
            glyph = code == 160 ? "NBSP" : "CTRL";
            name = code == 160
                ? $"NBSP — {pick("No-Break Space", "不換行空格")}"
                : pick("C1 control", "C1 控制碼");
            copy = ((char)code).ToString();
        }
        else // printable
        {
            glyph = ((char)code).ToString();
            name = pick("Printable", "可列印字元");
            copy = glyph;
        }

        string dec = code.ToString();
        string hex = "0x" + code.ToString("X2");
        var row = new AsciiRow
        {
            Code = code,
            Dec = dec,
            Hex = hex,
            Oct = Oct(code),
            Bin = Bin8(code),
            Char = glyph,
            Name = name,
            CopyChar = copy,
        };
        // Build search blob after construction so it can reuse the fields.
        return new AsciiRow
        {
            Code = row.Code, Dec = row.Dec, Hex = row.Hex, Oct = row.Oct, Bin = row.Bin,
            Char = row.Char, Name = row.Name, CopyChar = row.CopyChar,
            Search = $"{dec} {hex} {row.Oct} {row.Bin} {row.Char} {row.Name}".ToLowerInvariant(),
        };
    }

    /// <summary>Filter rows by a free-text query over dec/hex/oct/bin/char/name. Never throws; empty query = all.</summary>
    public static List<AsciiRow> Filter(IReadOnlyList<AsciiRow> rows, string? query)
    {
        var result = new List<AsciiRow>(rows.Count);
        try
        {
            string q = (query ?? "").Trim().ToLowerInvariant();
            if (q.Length == 0) { result.AddRange(rows); return result; }
            foreach (var r in rows)
                if (r.Search.Contains(q, StringComparison.Ordinal)) result.Add(r);
        }
        catch
        {
            result.Clear();
            result.AddRange(rows);
        }
        return result;
    }
}
