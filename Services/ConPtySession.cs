using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// ConPTY 偽終端機會話 · A pseudo-console (ConPTY) session that hosts a child shell (pwsh / cmd / wsl)
/// through the OS <c>CreatePseudoConsole</c> API and exposes its raw byte stream for an ANSI-rendering
/// terminal control. 由 SSH 模組（handoff 02）同 Windows Terminal 模組共用。
/// Shared by the SSH module (handoff 02) and the Windows Terminal module.
///
/// 設計 · Design: spawn the shell with <c>CreateProcess</c> + <c>STARTUPINFOEX</c> carrying the
/// <c>PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE</c> attribute, pipe its stdin/stdout, raise
/// <see cref="OutputReceived"/> as bytes arrive, and tear everything down deterministically so no
/// console handles or child processes leak.
/// </summary>
public sealed class ConPtySession : IDisposable
{
    /// <summary>由子程序輸出收到一批 UTF-8（含 ANSI）位元組 · Raised with output bytes (UTF-8 + ANSI) from the child.</summary>
    public event Action<string>? OutputReceived;

    /// <summary>子程序結束時觸發（帶結束代碼）· Raised when the child shell exits.</summary>
    public event Action<int>? Exited;

    public bool IsRunning => _hPC != IntPtr.Zero && !_disposed;

    private IntPtr _hPC = IntPtr.Zero;                 // HPCON
    private IntPtr _inputWrite = IntPtr.Zero;          // we write -> shell stdin
    private IntPtr _outputRead = IntPtr.Zero;          // we read  <- shell stdout
    private IntPtr _attrList = IntPtr.Zero;
    private PROCESS_INFORMATION _pi;
    private FileStream? _writer;
    private Thread? _readThread;
    private bool _disposed;
    private readonly object _gate = new();

    /// <summary>
    /// 啟動 ConPTY 並開動指定的命令列 · Start the ConPTY and spawn <paramref name="commandLine"/>
    /// (e.g. <c>powershell.exe</c>) at the given initial size and working directory.
    /// </summary>
    public void Start(string commandLine, short cols, short rows, string? workingDir = null)
    {
        if (cols < 1) cols = 80;
        if (rows < 1) rows = 25;

        // Two pipes: one carries OUR input to the shell, one carries the shell's output to US.
        if (!CreatePipe(out var inputRead, out _inputWrite, IntPtr.Zero, 0))
            throw new InvalidOperationException("CreatePipe (input) failed.");
        if (!CreatePipe(out _outputRead, out var outputWrite, IntPtr.Zero, 0))
            throw new InvalidOperationException("CreatePipe (output) failed.");

        var size = new COORD { X = cols, Y = rows };
        int hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out _hPC);
        if (hr != 0)
            throw new InvalidOperationException($"CreatePseudoConsole failed (HRESULT 0x{hr:X8}).");

        // The PTY now owns these ends; close our copies so EOF propagates correctly on teardown.
        CloseHandle(inputRead);
        CloseHandle(outputWrite);

        StartProcess(commandLine, workingDir);

        // Managed wrappers around the two pipe ends we keep.
        _writer = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(_inputWrite, ownsHandle: false),
            FileAccess.Write);

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ConPty-read" };
        _readThread.Start();
    }

    private unsafe void StartProcess(string commandLine, string? workingDir)
    {
        // Build the STARTUPINFOEX with a single attribute: the pseudo console handle.
        var startupInfo = new STARTUPINFOEX();
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        var attrSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
        _attrList = Marshal.AllocHGlobal(attrSize);
        startupInfo.lpAttributeList = _attrList;

        if (!InitializeProcThreadAttributeList(_attrList, 1, 0, ref attrSize))
            throw new InvalidOperationException("InitializeProcThreadAttributeList failed.");

        if (!UpdateProcThreadAttribute(_attrList, 0,
                (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _hPC, (IntPtr)IntPtr.Size,
                IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException("UpdateProcThreadAttribute failed.");

        var pSec = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };
        var tSec = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };

        bool ok = CreateProcess(
            null,
            new StringBuilder(commandLine),
            ref pSec, ref tSec,
            false,
            EXTENDED_STARTUPINFO_PRESENT,
            IntPtr.Zero,
            string.IsNullOrWhiteSpace(workingDir) ? null : workingDir,
            ref startupInfo,
            out _pi);

        if (!ok)
            throw new InvalidOperationException($"CreateProcess failed (Win32 {Marshal.GetLastWin32Error()}).");

        // Watch for child exit on a thread-pool callback.
        ThreadPool.RegisterWaitForSingleObject(
            new ThreadWaitHandle(_pi.hProcess),
            (_, _) =>
            {
                int code = 0;
                try { GetExitCodeProcess(_pi.hProcess, out code); } catch { }
                Exited?.Invoke(code);
            },
            null, Timeout.Infinite, executeOnlyOnce: true);
    }

    private void ReadLoop()
    {
        var buffer = new byte[4096];
        using var reader = new FileStream(
            new Microsoft.Win32.SafeHandles.SafeFileHandle(_outputRead, ownsHandle: false), FileAccess.Read);
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[8192];
        try
        {
            while (!_disposed)
            {
                int n = reader.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                int c = decoder.GetChars(buffer, 0, n, chars, 0);
                if (c > 0) OutputReceived?.Invoke(new string(chars, 0, c));
            }
        }
        catch { /* pipe closed on teardown */ }
    }

    /// <summary>寫入子程序 stdin（鍵盤輸入）· Send keystrokes / text to the child shell's stdin.</summary>
    public void Write(string text)
    {
        if (_writer is null || _disposed) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            lock (_gate)
            {
                _writer.Write(bytes, 0, bytes.Length);
                _writer.Flush();
            }
        }
        catch { }
    }

    /// <summary>改變偽終端機尺寸 · Resize the pseudo console (cols x rows).</summary>
    public void Resize(short cols, short rows)
    {
        if (_hPC == IntPtr.Zero || _disposed) return;
        if (cols < 1) cols = 1;
        if (rows < 1) rows = 1;
        try { ResizePseudoConsole(_hPC, new COORD { X = cols, Y = rows }); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { if (_hPC != IntPtr.Zero) ClosePseudoConsole(_hPC); } catch { }
        _hPC = IntPtr.Zero;

        try { _writer?.Dispose(); } catch { }
        try { if (_inputWrite != IntPtr.Zero) CloseHandle(_inputWrite); } catch { }
        try { if (_outputRead != IntPtr.Zero) CloseHandle(_outputRead); } catch { }

        try
        {
            if (_pi.hProcess != IntPtr.Zero)
            {
                // Best-effort: give the shell a moment to exit after the PTY closed, then kill.
                if (WaitForSingleObject(_pi.hProcess, 500) != 0)
                    TerminateProcess(_pi.hProcess, 0);
                CloseHandle(_pi.hThread);
                CloseHandle(_pi.hProcess);
            }
        }
        catch { }

        try
        {
            if (_attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(_attrList);
                Marshal.FreeHGlobal(_attrList);
                _attrList = IntPtr.Zero;
            }
        }
        catch { }
    }

    // A tiny WaitHandle wrapper so RegisterWaitForSingleObject can watch a raw HANDLE.
    private sealed class ThreadWaitHandle : WaitHandle
    {
        public ThreadWaitHandle(IntPtr h)
        {
            SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(h, ownsHandle: false);
        }
    }

    // ===================== P/Invoke =====================

    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X; public short Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);
}
