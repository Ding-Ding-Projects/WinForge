using System;

namespace WinForge.Services;

/// <summary>
/// 純受管理 ChaCha20（RFC 7539）· Pure managed ChaCha20 stream cipher (RFC 7539 / IETF variant:
/// 256-bit key, 96-bit nonce, 32-bit block counter). Used both for the KDBX 4 outer cipher and the
/// inner protected-field stream. .NET's System.Security.Cryptography.ChaCha20Poly1305 only exposes the
/// AEAD construction (with a tag), so the raw keystream cipher is implemented here. No native code.
/// </summary>
public sealed class ChaCha20Cipher
{
    private readonly uint[] _state = new uint[16];
    private readonly byte[] _keyStream = new byte[64];
    private int _ksPos = 64; // force a fresh block on first byte
    private uint _counter;

    private static readonly uint[] Sigma =
        { 0x61707865, 0x3320646e, 0x79622d32, 0x6b206574 };

    public ChaCha20Cipher(byte[] key, byte[] nonce, uint counter = 0)
    {
        if (key.Length != 32) throw new ArgumentException("ChaCha20 key must be 32 bytes");
        if (nonce.Length != 12) throw new ArgumentException("ChaCha20 nonce must be 12 bytes");
        _state[0] = Sigma[0]; _state[1] = Sigma[1]; _state[2] = Sigma[2]; _state[3] = Sigma[3];
        for (int i = 0; i < 8; i++) _state[4 + i] = U32(key, i * 4);
        _state[12] = counter;
        _state[13] = U32(nonce, 0);
        _state[14] = U32(nonce, 4);
        _state[15] = U32(nonce, 8);
        _counter = counter;
    }

    /// <summary>對位元組做 XOR（加解密相同）· XOR the data with the keystream (encrypt == decrypt).</summary>
    public byte[] Xor(byte[] data)
    {
        var output = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            if (_ksPos == 64) { GenerateBlock(); _ksPos = 0; }
            output[i] = (byte)(data[i] ^ _keyStream[_ksPos++]);
        }
        return output;
    }

    private void GenerateBlock()
    {
        _state[12] = _counter;
        var x = (uint[])_state.Clone();
        for (int i = 0; i < 10; i++) // 20 rounds = 10 double-rounds
        {
            QuarterRound(x, 0, 4, 8, 12);
            QuarterRound(x, 1, 5, 9, 13);
            QuarterRound(x, 2, 6, 10, 14);
            QuarterRound(x, 3, 7, 11, 15);
            QuarterRound(x, 0, 5, 10, 15);
            QuarterRound(x, 1, 6, 11, 12);
            QuarterRound(x, 2, 7, 8, 13);
            QuarterRound(x, 3, 4, 9, 14);
        }
        for (int i = 0; i < 16; i++)
        {
            uint v = x[i] + _state[i];
            _keyStream[i * 4 + 0] = (byte)v;
            _keyStream[i * 4 + 1] = (byte)(v >> 8);
            _keyStream[i * 4 + 2] = (byte)(v >> 16);
            _keyStream[i * 4 + 3] = (byte)(v >> 24);
        }
        _counter++;
    }

    private static void QuarterRound(uint[] x, int a, int b, int c, int d)
    {
        x[a] += x[b]; x[d] = Rotl(x[d] ^ x[a], 16);
        x[c] += x[d]; x[b] = Rotl(x[b] ^ x[c], 12);
        x[a] += x[b]; x[d] = Rotl(x[d] ^ x[a], 8);
        x[c] += x[d]; x[b] = Rotl(x[b] ^ x[c], 7);
    }

    private static uint Rotl(uint v, int c) => (v << c) | (v >> (32 - c));
    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}

/// <summary>ChaCha20 一次過加解密嘅靜態助手 · Static one-shot ChaCha20 helper for the outer cipher.</summary>
public static class ChaCha20
{
    public static byte[] Crypt(byte[] key, byte[] nonce, byte[] data)
        => new ChaCha20Cipher(key, nonce).Xor(data);
}

/// <summary>
/// 純受管理 Salsa20 · Pure managed Salsa20 stream cipher (256-bit key, 64-bit nonce), used as the
/// KDBX 3.1 inner random stream for protected field values. No native code.
/// </summary>
public sealed class Salsa20
{
    private readonly uint[] _state = new uint[16];
    private readonly byte[] _keyStream = new byte[64];
    private int _ksPos = 64;
    private ulong _counter;

    private static readonly uint[] Sigma =
        { 0x61707865, 0x3320646e, 0x79622d32, 0x6b206574 };

    public Salsa20(byte[] key, byte[] iv)
    {
        if (key.Length != 32) throw new ArgumentException("Salsa20 key must be 32 bytes");
        if (iv.Length != 8) throw new ArgumentException("Salsa20 IV must be 8 bytes");
        _state[0] = Sigma[0];
        _state[1] = U32(key, 0);
        _state[2] = U32(key, 4);
        _state[3] = U32(key, 8);
        _state[4] = U32(key, 12);
        _state[5] = Sigma[1];
        _state[6] = U32(iv, 0);
        _state[7] = U32(iv, 4);
        _state[8] = 0; // counter low
        _state[9] = 0; // counter high
        _state[10] = Sigma[2];
        _state[11] = U32(key, 16);
        _state[12] = U32(key, 20);
        _state[13] = U32(key, 24);
        _state[14] = U32(key, 28);
        _state[15] = Sigma[3];
    }

    public byte[] Xor(byte[] data)
    {
        var output = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            if (_ksPos == 64) { GenerateBlock(); _ksPos = 0; }
            output[i] = (byte)(data[i] ^ _keyStream[_ksPos++]);
        }
        return output;
    }

    private void GenerateBlock()
    {
        _state[8] = (uint)(_counter & 0xFFFFFFFF);
        _state[9] = (uint)(_counter >> 32);
        var x = (uint[])_state.Clone();
        for (int i = 0; i < 10; i++)
        {
            QuarterRound(x, 4, 0, 12, 8);
            QuarterRound(x, 9, 5, 1, 13);
            QuarterRound(x, 14, 10, 6, 2);
            QuarterRound(x, 3, 15, 11, 7);
            QuarterRound(x, 1, 0, 3, 2);
            QuarterRound(x, 6, 5, 4, 7);
            QuarterRound(x, 11, 10, 9, 8);
            QuarterRound(x, 12, 15, 14, 13);
        }
        for (int i = 0; i < 16; i++)
        {
            uint v = x[i] + _state[i];
            _keyStream[i * 4 + 0] = (byte)v;
            _keyStream[i * 4 + 1] = (byte)(v >> 8);
            _keyStream[i * 4 + 2] = (byte)(v >> 16);
            _keyStream[i * 4 + 3] = (byte)(v >> 24);
        }
        _counter++;
    }

    private static void QuarterRound(uint[] x, int a, int b, int c, int d)
    {
        x[a] ^= Rotl(x[b] + x[d], 7);
        x[c] ^= Rotl(x[a] + x[b], 9);
        x[d] ^= Rotl(x[c] + x[a], 13);
        x[b] ^= Rotl(x[d] + x[c], 18);
    }

    private static uint Rotl(uint v, int c) => (v << c) | (v >> (32 - c));
    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
