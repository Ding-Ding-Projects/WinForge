using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

// =====================================================================================
// 無界滑鼠連線 · A single authenticated, AES-encrypted TCP control link to one peer.
//
// 線上框 · Stream framing per packet:  [4 cipherLen][16 IV][cipher...]
// 收到第一個 Hello 封包先驗證共用密鑰：如果解密成功（HMAC-less，但 PKCS7/結構正確 +
// 名一致）就當已驗證。一旦驗證，所有後續封包用 raise OnPacket 交畀上層。
// The shared key authenticates implicitly: a peer that doesn't share the key cannot produce a
// frame that decrypts to a well-formed packet, so the first Hello that deserializes cleanly
// proves possession of the key. After that, every packet is surfaced via OnPacket.
// =====================================================================================

/// <summary>
/// 一條配對機器嘅加密控制連線 · One encrypted control link to a peer machine.
/// </summary>
public sealed class MwbConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly MwbChannelCipher _cipher;
    private readonly string _localName;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _seq;
    private volatile bool _authed;
    private const int MaxFrame = 4 * 1024 * 1024; // 4 MB safety cap

    /// <summary>對方機器名（交握後填）· The peer's reported machine name (set after Hello).</summary>
    public string PeerName { get; private set; } = "";

    /// <summary>對方端點 · Remote endpoint string for display/logging.</summary>
    public string RemoteEndpoint { get; }

    /// <summary>收到一個封包（已驗證）· Raised on the read thread for each authenticated packet.</summary>
    public event Action<MwbConnection, MwbPacket>? OnPacket;

    /// <summary>連線結束 · Raised once when the link closes (graceful or errored).</summary>
    public event Action<MwbConnection, string>? OnClosed;

    /// <summary>交握完成、密鑰正確 · Raised once the peer proves it holds the shared key.</summary>
    public event Action<MwbConnection>? OnAuthenticated;

    public MwbConnection(TcpClient client, string securityKey, string localName)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _cipher = new MwbChannelCipher(securityKey);
        _localName = localName ?? "";
        RemoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "?";
    }

    /// <summary>開始收發迴圈 · Start the read loop and send our opening Hello.</summary>
    public void Start(bool sendHello)
    {
        _ = Task.Run(ReadLoopAsync);
        if (sendHello)
            _ = SendAsync(new MwbPacket(MwbPacketType.Hello, MwbProtocol.PackText(_localName)));
    }

    private async Task ReadLoopAsync()
    {
        var lenBuf = new byte[4];
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (!await ReadExactAsync(lenBuf, 4)) break;
                int cipherLen = BitConverter.ToInt32(lenBuf, 0);
                if (cipherLen is <= 0 or > MaxFrame) throw new IOException("Bad frame length.");

                var cipher = new byte[cipherLen];
                if (!await ReadExactAsync(cipher, cipherLen)) break;

                byte[] plain;
                try { plain = _cipher.Decrypt(cipher); }
                catch { throw new IOException("Decryption failed — wrong security key?"); }

                MwbPacket packet;
                try { packet = MwbProtocol.Deserialize(plain); }
                catch { throw new IOException("Malformed packet."); }

                HandlePacket(packet);
            }
            Close("closed");
        }
        catch (Exception ex)
        {
            Close(ex.Message);
        }
    }

    private void HandlePacket(MwbPacket packet)
    {
        if (!_authed)
        {
            // 第一個成功解密嘅封包就代表密鑰正確 · The first cleanly-decrypted packet proves the key.
            if (packet.Type is MwbPacketType.Hello or MwbPacketType.HelloAck)
            {
                PeerName = string.IsNullOrEmpty(packet.SenderName)
                    ? MwbProtocol.UnpackText(packet.Payload)
                    : packet.SenderName;
            }
            _authed = true;
            OnAuthenticated?.Invoke(this);

            if (packet.Type == MwbPacketType.Hello)
                _ = SendAsync(new MwbPacket(MwbPacketType.HelloAck, MwbProtocol.PackText(_localName)));
        }

        if (string.IsNullOrEmpty(PeerName) && !string.IsNullOrEmpty(packet.SenderName))
            PeerName = packet.SenderName;

        OnPacket?.Invoke(this, packet);
    }

    /// <summary>送一個封包（加密、長度框）· Send a packet, encrypted and length-prefixed.</summary>
    public async Task SendAsync(MwbPacket packet)
    {
        if (_cts.IsCancellationRequested) return;
        packet.Seq = Interlocked.Increment(ref _seq);
        if (string.IsNullOrEmpty(packet.SenderName)) packet.SenderName = _localName;

        var plain = MwbProtocol.Serialize(packet);
        var cipher = _cipher.Encrypt(plain);
        var frame = new byte[4 + cipher.Length];
        BitConverter.GetBytes(cipher.Length).CopyTo(frame, 0);
        Buffer.BlockCopy(cipher, 0, frame, 4, cipher.Length);

        await _writeLock.WaitAsync();
        try
        {
            await _stream.WriteAsync(frame, _cts.Token);
            await _stream.FlushAsync(_cts.Token);
        }
        catch (Exception ex) { Close(ex.Message); }
        finally { _writeLock.Release(); }
    }

    /// <summary>同步送（畀 hook callback 用，唔好 block）· Fire-and-forget send for hot hook paths.</summary>
    public void Post(MwbPacket packet) => _ = SendAsync(packet);

    private async Task<bool> ReadExactAsync(byte[] buf, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = await _stream.ReadAsync(buf.AsMemory(read, count - read), _cts.Token);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    private int _closed;

    /// <summary>關閉連線（只會 raise OnClosed 一次）· Close, raising OnClosed exactly once.</summary>
    public void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        try { _cts.Cancel(); } catch { }
        try { _stream.Dispose(); } catch { }
        try { _client.Close(); } catch { }
        OnClosed?.Invoke(this, reason);
    }

    public void Dispose()
    {
        Close("disposed");
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
