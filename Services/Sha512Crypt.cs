using System;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// glibc 風格 SHA-512 crypt（$6$）· glibc-style SHA-512 crypt ("$6$") used by Raspberry Pi OS'
/// userconf.txt. Faithful port of Ulrich Drepper's reference sha512-crypt, so the generated hash is
/// accepted by Linux PAM with no external openssl/mkpasswd dependency.
/// </summary>
public static class Sha512Crypt
{
    private const string Itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int RoundsDefault = 5000;

    public static string Crypt(string password, string salt)
    {
        var pw = Encoding.UTF8.GetBytes(password ?? "");
        // salt may carry an optional "rounds=N$" prefix; Pi uses the default 5000.
        salt ??= "";
        if (salt.Length > 16) salt = salt.Substring(0, 16);
        var saltBytes = Encoding.UTF8.GetBytes(salt);

        using var sha = SHA512.Create();

        // Digest A
        byte[] altResult;
        {
            var b = new ByteBuf();
            b.Add(pw);
            b.Add(saltBytes);

            // Digest B = SHA512(password + salt + password)
            var bDigest = Hash(sha, Concat(pw, saltBytes, pw));

            // add B, repeated for the full length of password
            int cnt = pw.Length;
            while (cnt > sha.HashSize / 8) { b.Add(bDigest); cnt -= sha.HashSize / 8; }
            b.Add(Sub(bDigest, 0, cnt));

            // for each bit of password length, add B or password (MSB→LSB)
            for (int i = pw.Length; i > 0; i >>= 1)
                b.Add((i & 1) != 0 ? bDigest : pw);

            altResult = Hash(sha, b.ToArray());
        }

        // DP = SHA512(password * len(password)), then take first len(password) bytes
        byte[] pBytes;
        {
            var dp = new ByteBuf();
            for (int i = 0; i < pw.Length; i++) dp.Add(pw);
            var dpDigest = Hash(sha, dp.ToArray());
            pBytes = Produce(dpDigest, pw.Length);
        }

        // DS = SHA512(salt * (16 + altResult[0])), take first len(salt) bytes
        byte[] sBytes;
        {
            var ds = new ByteBuf();
            int times = 16 + (altResult[0] & 0xFF);
            for (int i = 0; i < times; i++) ds.Add(saltBytes);
            var dsDigest = Hash(sha, ds.ToArray());
            sBytes = Produce(dsDigest, saltBytes.Length);
        }

        // Rounds
        var cur = altResult;
        for (int round = 0; round < RoundsDefault; round++)
        {
            var c = new ByteBuf();
            if ((round & 1) != 0) c.Add(pBytes); else c.Add(cur);
            if (round % 3 != 0) c.Add(sBytes);
            if (round % 7 != 0) c.Add(pBytes);
            if ((round & 1) != 0) c.Add(cur); else c.Add(pBytes);
            cur = Hash(sha, c.ToArray());
        }

        // Base64 (custom ordering, per the reference implementation)
        var sb = new StringBuilder();
        sb.Append("$6$");
        sb.Append(salt);
        sb.Append('$');

        void B64From(int b2, int b1, int b0, int n)
        {
            int w = (b2 << 16) | (b1 << 8) | b0;
            while (n-- > 0) { sb.Append(Itoa64[w & 0x3f]); w >>= 6; }
        }

        B64From(cur[0], cur[21], cur[42], 4);
        B64From(cur[22], cur[43], cur[1], 4);
        B64From(cur[44], cur[2], cur[23], 4);
        B64From(cur[3], cur[24], cur[45], 4);
        B64From(cur[25], cur[46], cur[4], 4);
        B64From(cur[47], cur[5], cur[26], 4);
        B64From(cur[6], cur[27], cur[48], 4);
        B64From(cur[28], cur[49], cur[7], 4);
        B64From(cur[50], cur[8], cur[29], 4);
        B64From(cur[9], cur[30], cur[51], 4);
        B64From(cur[31], cur[52], cur[10], 4);
        B64From(cur[53], cur[11], cur[32], 4);
        B64From(cur[12], cur[33], cur[54], 4);
        B64From(cur[34], cur[55], cur[13], 4);
        B64From(cur[56], cur[14], cur[35], 4);
        B64From(cur[15], cur[36], cur[57], 4);
        B64From(cur[37], cur[58], cur[16], 4);
        B64From(cur[59], cur[17], cur[38], 4);
        B64From(cur[18], cur[39], cur[60], 4);
        B64From(cur[40], cur[61], cur[19], 4);
        B64From(cur[62], cur[20], cur[41], 4);
        B64From(0, 0, cur[63], 2);

        return sb.ToString();
    }

    private static byte[] Hash(SHA512 sha, byte[] data) => sha.ComputeHash(data);

    private static byte[] Concat(params byte[][] parts)
    {
        var b = new ByteBuf();
        foreach (var p in parts) b.Add(p);
        return b.ToArray();
    }

    private static byte[] Sub(byte[] src, int offset, int len)
    {
        var r = new byte[len];
        Array.Copy(src, offset, r, 0, len);
        return r;
    }

    /// <summary>Repeat a 64-byte digest to fill <paramref name="len"/> bytes.</summary>
    private static byte[] Produce(byte[] digest, int len)
    {
        var r = new byte[len];
        int filled = 0;
        while (filled < len)
        {
            int take = Math.Min(digest.Length, len - filled);
            Array.Copy(digest, 0, r, filled, take);
            filled += take;
        }
        return r;
    }

    private sealed class ByteBuf
    {
        private byte[] _buf = new byte[128];
        private int _len;
        public void Add(byte[] data)
        {
            if (_len + data.Length > _buf.Length)
                Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + data.Length));
            Array.Copy(data, 0, _buf, _len, data.Length);
            _len += data.Length;
        }
        public byte[] ToArray() => Sub(_buf, 0, _len);
    }
}
