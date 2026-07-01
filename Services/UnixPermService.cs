using System;

namespace WinForge.Services;

/// <summary>
/// Unix 權限（chmod）計算 · Unix file-permission (chmod) helper. Pure managed, never throws.
/// A permission is the 9 rwx bits (owner/group/other) plus 3 special bits (setuid/setgid/sticky).
/// Encoded as a 4-digit octal (special digit first): e.g. 0755, 4755.
/// </summary>
public static class UnixPermService
{
    // Special-bit masks (leading octal digit).
    public const int SetUid = 0x800; // 04000
    public const int SetGid = 0x400; // 02000
    public const int Sticky = 0x200; // 01000

    // rwx masks by class.
    public const int OwnerR = 0x100, OwnerW = 0x80, OwnerX = 0x40;
    public const int GroupR = 0x20, GroupW = 0x10, GroupX = 0x08;
    public const int OtherR = 0x04, OtherW = 0x02, OtherX = 0x01;

    public const int PermMask = 0xFFF; // 12 meaningful bits.

    /// <summary>Clamp any int to the 12 meaningful permission bits.</summary>
    public static int Normalize(int mode) => mode & PermMask;

    /// <summary>4-digit octal string, e.g. "0755" or "4755".</summary>
    public static string ToOctal(int mode)
    {
        mode = Normalize(mode);
        int special = (mode >> 9) & 0x7;
        int perms = mode & 0x1FF;
        return $"{special}{Convert.ToString(perms, 8).PadLeft(3, '0')}";
    }

    /// <summary>Bare octal without the leading special digit unless special bits are set (chmod-friendly).</summary>
    public static string ToChmodOctal(int mode)
    {
        mode = Normalize(mode);
        int special = (mode >> 9) & 0x7;
        int perms = mode & 0x1FF;
        string p = Convert.ToString(perms, 8).PadLeft(3, '0');
        return special != 0 ? $"{special}{p}" : p;
    }

    /// <summary>9-char symbolic string with s/S/t/T for special bits, e.g. "rwxr-xr-x", "rwsr-sr-t".</summary>
    public static string ToSymbolic(int mode)
    {
        mode = Normalize(mode);
        Span<char> c = stackalloc char[9];

        c[0] = (mode & OwnerR) != 0 ? 'r' : '-';
        c[1] = (mode & OwnerW) != 0 ? 'w' : '-';
        c[2] = SpecialExec((mode & OwnerX) != 0, (mode & SetUid) != 0, 's');

        c[3] = (mode & GroupR) != 0 ? 'r' : '-';
        c[4] = (mode & GroupW) != 0 ? 'w' : '-';
        c[5] = SpecialExec((mode & GroupX) != 0, (mode & SetGid) != 0, 's');

        c[6] = (mode & OtherR) != 0 ? 'r' : '-';
        c[7] = (mode & OtherW) != 0 ? 'w' : '-';
        c[8] = SpecialExec((mode & OtherX) != 0, (mode & Sticky) != 0, 't');

        return new string(c);
    }

    // exec present + special => lower letter; special only => upper letter; exec only => x; none => -
    private static char SpecialExec(bool exec, bool special, char lower)
    {
        char upper = char.ToUpperInvariant(lower);
        if (special) return exec ? lower : upper;
        return exec ? 'x' : '-';
    }

    /// <summary>Parse an octal string ("755", "0755", "4755", "0o755") into mode bits. Returns false on bad input.</summary>
    public static bool TryParseOctal(string? text, out int mode)
    {
        mode = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        if (s.StartsWith("0o", StringComparison.OrdinalIgnoreCase) || s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return false; // hex not permitted
            s = s.Substring(2);
        }
        if (s.Length == 0 || s.Length > 4) return false;
        int value = 0;
        foreach (char ch in s)
        {
            if (ch < '0' || ch > '7') return false;
            value = (value << 3) | (ch - '0');
        }
        mode = Normalize(value);
        return true;
    }

    /// <summary>Parse a 9-char symbolic string ("rwxr-xr-x", with s/S/t/T) into mode bits. Returns false on bad input.</summary>
    public static bool TryParseSymbolic(string? text, out int mode)
    {
        mode = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        // Tolerate a leading file-type char (e.g. "-rwxr-xr-x", "drwx------").
        if (s.Length == 10) s = s.Substring(1);
        if (s.Length != 9) return false;

        int m = 0;
        if (!ReadRW(s[0], 'r', OwnerR, ref m)) return false;
        if (!ReadRW(s[1], 'w', OwnerW, ref m)) return false;
        if (!ReadExec(s[2], OwnerX, SetUid, 's', ref m)) return false;

        if (!ReadRW(s[3], 'r', GroupR, ref m)) return false;
        if (!ReadRW(s[4], 'w', GroupW, ref m)) return false;
        if (!ReadExec(s[5], GroupX, SetGid, 's', ref m)) return false;

        if (!ReadRW(s[6], 'r', OtherR, ref m)) return false;
        if (!ReadRW(s[7], 'w', OtherW, ref m)) return false;
        if (!ReadExec(s[8], OtherX, Sticky, 't', ref m)) return false;

        mode = m;
        return true;
    }

    private static bool ReadRW(char c, char set, int bit, ref int m)
    {
        if (c == set) { m |= bit; return true; }
        if (c == '-') return true;
        return false;
    }

    private static bool ReadExec(char c, int execBit, int specialBit, char lower, ref int m)
    {
        char upper = char.ToUpperInvariant(lower);
        if (c == 'x') { m |= execBit; return true; }
        if (c == '-') { return true; }
        if (c == lower) { m |= execBit | specialBit; return true; }
        if (c == upper) { m |= specialBit; return true; }
        return false;
    }
}
