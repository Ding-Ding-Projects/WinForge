using System;
using System.Runtime.InteropServices;

namespace WinForge.Models;

// =====================================================================================
// 無界滑鼠（鍵鼠共享）資料模型 · Mouse Without Borders (KVM) data models.
//
// 一套乾淨、會編譯嘅基礎：協定封包、機器資訊、版面（左右排列）。
// A clean, compiling foundation: the wire protocol packet, machine info and the
// left/right layout. Mirrors the shape of PowerToys MouseWithoutBorders but is an
// independent, native WinForge implementation (no shared code).
// =====================================================================================

/// <summary>
/// 封包類型 · Wire packet type. Values are stable on-the-wire identifiers; do not renumber
/// without bumping the protocol. They mirror the conceptual set used by Mouse Without Borders.
/// </summary>
public enum MwbPacketType : byte
{
    Invalid = 0,

    /// <summary>交握：附帶機器名 · Handshake / hello, carries the sender's machine name.</summary>
    Hello = 3,

    /// <summary>交握確認 · Handshake acknowledgement.</summary>
    HelloAck = 4,

    /// <summary>再見（優雅斷線）· Graceful disconnect.</summary>
    ByeBye = 5,

    /// <summary>心跳（保活）· Heartbeat / keep-alive.</summary>
    Heartbeat = 20,

    /// <summary>鍵盤事件 · Keyboard event (key down / up).</summary>
    Keyboard = 122,

    /// <summary>滑鼠事件 · Mouse event (move / click / wheel).</summary>
    Mouse = 123,

    /// <summary>剪貼簿文字 · Clipboard text sync.</summary>
    ClipboardText = 124,

    /// <summary>控制權交去呢部機 · Control handed over to the receiving machine.</summary>
    SwitchControl = 77,

    /// <summary>收返控制權（cursor 返本機）· Control returned to the local machine.</summary>
    ReleaseControl = 78,
}

/// <summary>
/// 鍵盤事件資料 · Keyboard event payload. Mirrors KBDLLHOOKSTRUCT essentials.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MwbKeyboardData
{
    /// <summary>虛擬鍵碼 · Virtual key code.</summary>
    public int VirtualKey;

    /// <summary>掃描碼 · Hardware scan code.</summary>
    public int ScanCode;

    /// <summary>旗標（按下／放開／延伸鍵）· Flags (up/down, extended).</summary>
    public int Flags;
}

/// <summary>
/// 滑鼠事件資料 · Mouse event payload. Coordinates are normalized 0..65535 across the
/// virtual desktop so they translate across machines with different resolutions.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MwbMouseData
{
    /// <summary>正規化 X（0..65535）· Normalized absolute X across the remote virtual desktop.</summary>
    public int X;

    /// <summary>正規化 Y（0..65535）· Normalized absolute Y across the remote virtual desktop.</summary>
    public int Y;

    /// <summary>滾輪量 · Wheel delta.</summary>
    public int WheelDelta;

    /// <summary>SendInput MOUSEEVENTF 旗標 · SendInput MOUSEEVENTF flags.</summary>
    public int Flags;
}

/// <summary>
/// 配對機器 · A paired machine known to this PC. Persisted (key encrypted via DPAPI).
/// </summary>
public sealed class MwbMachine
{
    /// <summary>機器名（通常 = 電腦名）· Machine display name (usually the computer name).</summary>
    public string Name { get; set; } = "";

    /// <summary>對方 IP 或主機名 · The peer's IPv4 / host to dial.</summary>
    public string Host { get; set; } = "";

    /// <summary>控制埠 · TCP control port (default mirrors MwbProtocol.DefaultPort).</summary>
    public int Port { get; set; } = 15100;

    /// <summary>
    /// DPAPI 加密嘅共用密鑰（base64）· The shared security key, DPAPI-encrypted to base64 at rest.
    /// Never store the plaintext key on disk.
    /// </summary>
    public string KeyBlob { get; set; } = "";

    /// <summary>版面位置（-1 = 未排）· Slot in the 1×4 layout, 0..3, or -1 if unplaced.</summary>
    public int Slot { get; set; } = -1;

    /// <summary>係咪本機 · True for the row that represents this PC itself.</summary>
    public bool IsLocal { get; set; }

    public override string ToString() => $"{Name} ({Host}:{Port})";
}

/// <summary>連線狀態 · Live connection state for a paired machine.</summary>
public enum MwbLinkState
{
    /// <summary>未連線 · Not connected.</summary>
    Disconnected,

    /// <summary>連線中 · Dialing / handshaking.</summary>
    Connecting,

    /// <summary>已連線 · Connected and authenticated.</summary>
    Connected,

    /// <summary>出錯 · Errored.</summary>
    Error,
}
