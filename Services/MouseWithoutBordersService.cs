using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

// =====================================================================================
// 無界滑鼠 服務 · Mouse Without Borders service — the orchestrator.
//
// 職責 · Responsibilities:
//   • 本機身分：機器名、IP、安全密鑰（DPAPI 加密儲存）· local identity + DPAPI-stored key.
//   • TCP 監聽 + 主動撥號 · TCP listener for inbound peers + outbound dialer.
//   • 配對機器清單 + 1×4 版面 · paired-machine store + 1×4 left/right layout.
//   • 控制權交接（邊界過渡）· control hand-off across the screen edge.
//   • 輸入轉發 + 注入 · forward captured input to the controlled peer / inject inbound input.
//   • 剪貼簿文字同步 · text clipboard sync.
//
// 單例 · A process-wide singleton (one network stack), like the other WinForge services.
// =====================================================================================

/// <summary>無界滑鼠核心服務（單例）· Mouse Without Borders core service (singleton).</summary>
public sealed class MouseWithoutBordersService
{
    public static MouseWithoutBordersService Instance { get; } = new();

    private const string KeyMachineName = "mwb.machineName";
    private const string KeySecurityKey = "mwb.securityKey.dpapi"; // DPAPI blob (base64)
    private const string KeyPort = "mwb.port";
    private const string KeyMachines = "mwb.machines.json";
    private const string KeyEnabled = "mwb.enabled";
    private const string KeyClipboardShare = "mwb.clipboardShare";
    private const string KeyWrap = "mwb.wrap";

    private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("WinForge.MWB.Key.Entropy.v1");

    private readonly object _gate = new();
    private readonly List<MwbMachine> _machines = new();
    private readonly Dictionary<string, MwbConnection> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MwbLinkState> _states = new(StringComparer.OrdinalIgnoreCase);

    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private MwbInputCapture? _capture;

    /// <summary>控制權喺邊（"" = 本機；否則係對方機器名）· Who has control ("" = local; else peer name).</summary>
    public string ControlOwner { get; private set; } = "";

    /// <summary>狀態改變（連線、控制權等）· Raised when machines/links/control change (UI refresh).</summary>
    public event Action? StateChanged;

    /// <summary>收到對方剪貼簿文字 · Raised when a peer pushes clipboard text (UI applies it).</summary>
    public event Action<string>? ClipboardTextReceived;

    /// <summary>診斷訊息 · Diagnostic log line for the UI activity area.</summary>
    public event Action<string>? Log;

    private MouseWithoutBordersService() { Load(); }

    // ---- identity & settings ------------------------------------------------------

    /// <summary>本機機器名 · This PC's machine name (defaults to the computer name).</summary>
    public string MachineName
    {
        get
        {
            var v = SettingsStore.Get(KeyMachineName, "");
            return string.IsNullOrWhiteSpace(v) ? Environment.MachineName : v;
        }
        set => SettingsStore.Set(KeyMachineName, value ?? "");
    }

    /// <summary>控制埠 · TCP control port.</summary>
    public int Port
    {
        get => int.TryParse(SettingsStore.Get(KeyPort, ""), out var p) && p is > 0 and < 65536 ? p : MwbProtocol.DefaultPort;
        set => SettingsStore.Set(KeyPort, value.ToString());
    }

    public bool ClipboardShare
    {
        get => SettingsStore.Get(KeyClipboardShare, "1") == "1";
        set => SettingsStore.Set(KeyClipboardShare, value ? "1" : "0");
    }

    public bool WrapAround
    {
        get => SettingsStore.Get(KeyWrap, "0") == "1";
        set => SettingsStore.Set(KeyWrap, value ? "1" : "0");
    }

    public bool Enabled
    {
        get => SettingsStore.Get(KeyEnabled, "0") == "1";
        private set => SettingsStore.Set(KeyEnabled, value ? "1" : "0");
    }

    /// <summary>本機安全密鑰（明文，由 DPAPI 解密）· This PC's security key (plaintext, DPAPI-decrypted).</summary>
    public string SecurityKey
    {
        get
        {
            var blob = SettingsStore.Get(KeySecurityKey, "");
            if (string.IsNullOrEmpty(blob)) return "";
            try
            {
                var dec = ProtectedData.Unprotect(Convert.FromBase64String(blob), DpapiEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }
    }

    /// <summary>設定／產生安全密鑰，DPAPI 加密儲存 · Set the security key, DPAPI-encrypted at rest.</summary>
    public void SetSecurityKey(string key)
    {
        if (string.IsNullOrEmpty(key)) { SettingsStore.Set(KeySecurityKey, ""); return; }
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), DpapiEntropy, DataProtectionScope.CurrentUser);
            SettingsStore.Set(KeySecurityKey, Convert.ToBase64String(enc));
        }
        catch { /* best effort */ }
    }

    /// <summary>確保有密鑰（無就產生一個）· Ensure a key exists; generate one if missing. Returns it.</summary>
    public string EnsureSecurityKey()
    {
        var k = SecurityKey;
        if (string.IsNullOrEmpty(k)) { k = MwbProtocol.GenerateKey(); SetSecurityKey(k); }
        return k;
    }

    /// <summary>本機所有 IPv4 位址 · This PC's IPv4 addresses for the pairing UI.</summary>
    public List<string> LocalIPv4Addresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var s = ua.Address.ToString();
                        if (!s.StartsWith("169.254") && !list.Contains(s)) list.Add(s);
                    }
                }
            }
        }
        catch { }
        if (list.Count == 0) list.Add("127.0.0.1");
        return list;
    }

    // ---- machine store ------------------------------------------------------------

    /// <summary>配對機器嘅快照 · Snapshot copy of the paired-machine list.</summary>
    public List<MwbMachine> Machines { get { lock (_gate) return _machines.ToList(); } }

    /// <summary>取連線狀態 · Current link state for a machine name.</summary>
    public MwbLinkState StateOf(string name)
    {
        lock (_gate) return _states.TryGetValue(name, out var s) ? s : MwbLinkState.Disconnected;
    }

    /// <summary>加入／更新一部配對機器 · Add or update a paired machine; key is DPAPI-encrypted.</summary>
    public void AddOrUpdateMachine(string name, string host, int port, string securityKey, int slot = -1)
    {
        lock (_gate)
        {
            var m = _machines.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (m == null) { m = new MwbMachine { Name = name }; _machines.Add(m); }
            m.Host = host;
            m.Port = port;
            m.Slot = slot;
            if (!string.IsNullOrEmpty(securityKey)) m.KeyBlob = ProtectKey(securityKey);
            SaveLocked();
        }
        StateChanged?.Invoke();
    }

    /// <summary>移除一部機器 · Remove a paired machine and drop its link.</summary>
    public void RemoveMachine(string name)
    {
        Disconnect(name);
        lock (_gate)
        {
            _machines.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            SaveLocked();
        }
        StateChanged?.Invoke();
    }

    /// <summary>設定一部機器嘅版面槽位（0..3，左至右）· Set a machine's layout slot (0..3).</summary>
    public void SetSlot(string name, int slot)
    {
        lock (_gate)
        {
            var m = _machines.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (m != null) { m.Slot = slot; SaveLocked(); }
        }
        StateChanged?.Invoke();
    }

    private string MachineKey(MwbMachine m)
    {
        if (string.IsNullOrEmpty(m.KeyBlob)) return SecurityKey; // fall back to our own key
        try
        {
            var dec = ProtectedData.Unprotect(Convert.FromBase64String(m.KeyBlob), DpapiEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return SecurityKey; }
    }

    private static string ProtectKey(string key)
    {
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), DpapiEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    private void Load()
    {
        try
        {
            var json = SettingsStore.Get(KeyMachines, "");
            if (!string.IsNullOrEmpty(json))
            {
                var list = JsonSerializer.Deserialize<List<MwbMachine>>(json);
                if (list != null) { _machines.Clear(); _machines.AddRange(list.Where(m => !m.IsLocal)); }
            }
        }
        catch { }
    }

    private void SaveLocked()
    {
        try { SettingsStore.Set(KeyMachines, JsonSerializer.Serialize(_machines)); } catch { }
    }

    // ---- server / listener --------------------------------------------------------

    /// <summary>係咪正在監聽 · True while the control listener is running.</summary>
    public bool IsListening => _listener != null;

    /// <summary>啟動服務（監聽 + 裝鈎）· Start the service: listen for peers and install input hooks.</summary>
    public void Start()
    {
        if (string.IsNullOrEmpty(SecurityKey)) EnsureSecurityKey();
        StartListener();
        StartCapture();
        Enabled = true;
        Log?.Invoke($"Service started · listening on {Port}");
        StateChanged?.Invoke();
    }

    /// <summary>停止服務 · Stop listening, drop all links, remove hooks.</summary>
    public void Stop()
    {
        Enabled = false;
        StopCapture();
        foreach (var name in _links.Keys.ToList()) Disconnect(name);
        try { _serverCts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
        ControlOwner = "";
        Log?.Invoke("Service stopped");
        StateChanged?.Invoke();
    }

    private void StartListener()
    {
        if (_listener != null) return;
        _serverCts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        _ = AcceptLoopAsync(_listener, _serverCts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct); }
            catch { break; }

            // 用本機密鑰（配對嘅機器都用同一個共用密鑰）· Inbound peers authenticate with our key.
            var conn = new MwbConnection(client, SecurityKey, MachineName);
            WireConnection(conn);
            conn.Start(sendHello: false);
            Log?.Invoke($"Inbound connection from {conn.RemoteEndpoint}");
        }
    }

    // ---- outbound dial ------------------------------------------------------------

    /// <summary>主動連去一部配對機器 · Dial a paired machine and bring its link up.</summary>
    public async Task<bool> ConnectAsync(string name)
    {
        MwbMachine? m;
        lock (_gate) m = _machines.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (m == null) return false;

        SetState(name, MwbLinkState.Connecting);
        try
        {
            var client = new TcpClient();
            using var to = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            await client.ConnectAsync(m.Host, m.Port, to.Token);
            var conn = new MwbConnection(client, MachineKey(m), MachineName);
            WireConnection(conn);
            conn.Start(sendHello: true);
            Log?.Invoke($"Connecting to {name} at {m.Host}:{m.Port}");
            return true;
        }
        catch (Exception ex)
        {
            SetState(name, MwbLinkState.Error);
            Log?.Invoke($"Connect to {name} failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>斷開一部機器 · Disconnect a machine's link.</summary>
    public void Disconnect(string name)
    {
        MwbConnection? conn = null;
        lock (_gate) if (_links.TryGetValue(name, out conn)) _links.Remove(name);
        conn?.Close("user disconnect");
        SetState(name, MwbLinkState.Disconnected);
    }

    private void WireConnection(MwbConnection conn)
    {
        conn.OnAuthenticated += c =>
        {
            var key = string.IsNullOrEmpty(c.PeerName) ? c.RemoteEndpoint : c.PeerName;
            lock (_gate) _links[key] = c;
            SetState(key, MwbLinkState.Connected);
            Log?.Invoke($"Authenticated with {key}");
        };
        conn.OnPacket += HandlePacket;
        conn.OnClosed += (c, reason) =>
        {
            var key = string.IsNullOrEmpty(c.PeerName) ? c.RemoteEndpoint : c.PeerName;
            lock (_gate) if (_links.TryGetValue(key, out var existing) && existing == c) _links.Remove(key);
            SetState(key, MwbLinkState.Disconnected);
            if (string.Equals(ControlOwner, key, StringComparison.OrdinalIgnoreCase)) ReturnControlLocal();
            Log?.Invoke($"Closed {key}: {reason}");
        };
    }

    private void SetState(string name, MwbLinkState s)
    {
        lock (_gate) _states[name] = s;
        StateChanged?.Invoke();
    }

    // ---- packet handling (receiver) ----------------------------------------------

    private void HandlePacket(MwbConnection conn, MwbPacket p)
    {
        switch (p.Type)
        {
            case MwbPacketType.Hello:
            case MwbPacketType.HelloAck:
            case MwbPacketType.Heartbeat:
                break;

            case MwbPacketType.SwitchControl:
                // 對方將控制權交畀本機 · A peer handed control to us; we now inject its input.
                Log?.Invoke($"Receiving control from {conn.PeerName}");
                break;

            case MwbPacketType.ReleaseControl:
                Log?.Invoke($"{conn.PeerName} took control back");
                break;

            case MwbPacketType.Mouse:
                if (p.Payload.Length >= 16)
                {
                    var md = MwbProtocol.UnpackMouse(p.Payload);
                    if (((MwbMouseFlags)md.Flags).HasFlag(MwbMouseFlags.Move))
                        MwbInputInjector.MoveAbsolute(md.X, md.Y);
                    else
                        MwbInputInjector.InjectMouse(md);
                }
                break;

            case MwbPacketType.Keyboard:
                if (p.Payload.Length >= 12)
                    MwbInputInjector.InjectKey(MwbProtocol.UnpackKey(p.Payload));
                break;

            case MwbPacketType.ClipboardText:
                if (ClipboardShare)
                    ClipboardTextReceived?.Invoke(MwbProtocol.UnpackText(p.Payload));
                break;

            case MwbPacketType.ByeBye:
                conn.Close("peer said goodbye");
                break;
        }
    }

    // ---- capture & control transition (sender) -----------------------------------

    private void StartCapture()
    {
        if (_capture != null) return;
        _capture = new MwbInputCapture();
        _capture.EdgeHit += OnEdgeHit;
        _capture.MouseMoved += OnMouseMoved;
        _capture.MouseEvent += OnMouseEvent;
        _capture.KeyEvent += OnKeyEvent;
        _capture.Install();
    }

    private void StopCapture()
    {
        if (_capture == null) return;
        _capture.Uninstall();
        _capture.Dispose();
        _capture = null;
    }

    private void OnEdgeHit(int dir) // -1 left, +1 right
    {
        if (!string.IsNullOrEmpty(ControlOwner)) return; // already remote
        var neighbour = NeighbourInDirection(dir);
        if (neighbour == null) return;
        if (StateOf(neighbour.Name) != MwbLinkState.Connected) return;
        GiveControlTo(neighbour.Name);
    }

    /// <summary>本機版面槽位（IsLocal 嗰部，或 slot 0）· This PC's slot in the layout.</summary>
    private int LocalSlot()
    {
        // The local machine is implicitly slot 0 unless reassigned; paired machines fill 1..3.
        return 0;
    }

    private MwbMachine? NeighbourInDirection(int dir)
    {
        lock (_gate)
        {
            int local = LocalSlot();           // this PC is slot 0
            int target = local + dir;          // immediate neighbour
            var direct = _machines.FirstOrDefault(m => m.Slot == target);
            if (direct != null) return direct;

            if (!WrapAround) return null;

            // 環繞：去到最遠嗰部 · Wrap to the far end of the placed machines.
            var placed = _machines.Where(m => m.Slot >= 0).OrderBy(m => m.Slot).ToList();
            if (placed.Count == 0) return null;
            // Going off the right end wraps to the leftmost; off the left wraps to the rightmost.
            return dir > 0 ? placed.First() : placed.Last();
        }
    }

    /// <summary>將控制權交畀一部機器 · Hand control to a peer; start swallowing+forwarding local input.</summary>
    public void GiveControlTo(string name)
    {
        if (StateOf(name) != MwbLinkState.Connected) return;
        ControlOwner = name;
        if (_capture != null) _capture.Capturing = true;
        SendTo(name, new MwbPacket(MwbPacketType.SwitchControl));
        Log?.Invoke($"Control → {name}");
        StateChanged?.Invoke();
    }

    /// <summary>收返控制權 · Return control to the local machine; stop swallowing input.</summary>
    public void ReturnControlLocal()
    {
        if (string.IsNullOrEmpty(ControlOwner)) return;
        var prev = ControlOwner;
        SendTo(prev, new MwbPacket(MwbPacketType.ReleaseControl));
        ControlOwner = "";
        if (_capture != null) _capture.Capturing = false;
        Log?.Invoke("Control ← local");
        StateChanged?.Invoke();
    }

    private void OnMouseMoved(int dx, int dy, int nx, int ny)
    {
        if (string.IsNullOrEmpty(ControlOwner)) return;
        var md = new MwbMouseData { X = nx, Y = ny, Flags = (int)MwbMouseFlags.Move };
        SendTo(ControlOwner, new MwbPacket(MwbPacketType.Mouse, MwbProtocol.PackMouse(md)));
    }

    private void OnMouseEvent(MwbMouseData md)
    {
        if (string.IsNullOrEmpty(ControlOwner)) return;
        SendTo(ControlOwner, new MwbPacket(MwbPacketType.Mouse, MwbProtocol.PackMouse(md)));
    }

    private void OnKeyEvent(MwbKeyboardData kd)
    {
        if (string.IsNullOrEmpty(ControlOwner)) return;
        // 留意 Ctrl+Alt+Del 等系統鍵唔會經 hook · System keys (Ctrl+Alt+Del) never reach the hook.
        SendTo(ControlOwner, new MwbPacket(MwbPacketType.Keyboard, MwbProtocol.PackKey(kd)));
    }

    // ---- clipboard ----------------------------------------------------------------

    /// <summary>把剪貼簿文字推去所有連線機器 · Push clipboard text to every connected peer.</summary>
    public void BroadcastClipboardText(string text)
    {
        if (!ClipboardShare || string.IsNullOrEmpty(text)) return;
        var pkt = new MwbPacket(MwbPacketType.ClipboardText, MwbProtocol.PackText(text));
        lock (_gate)
            foreach (var conn in _links.Values) conn.Post(pkt);
    }

    // ---- send helpers -------------------------------------------------------------

    private void SendTo(string name, MwbPacket p)
    {
        MwbConnection? conn;
        lock (_gate) _links.TryGetValue(name, out conn);
        conn?.Post(p);
    }

    /// <summary>數一數活躍連線 · Count of currently connected links (for UI status).</summary>
    public int ConnectedCount
    {
        get { lock (_gate) return _states.Count(kv => kv.Value == MwbLinkState.Connected); }
    }
}
