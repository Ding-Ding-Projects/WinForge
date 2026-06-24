using System;
using System.Runtime.CompilerServices;

namespace WinForge.Services;

/// <summary>
/// 最早期診斷 · Earliest-possible startup diagnostics. A module initializer runs before Main, so it can
/// capture loader failures (TypeLoad / missing assembly / bad image) that fail-fast inside the WinUI/CsWinRT
/// activation path — before App's constructor or OnLaunched ever runs — which otherwise surface only as an
/// opaque native 0xC000027B stowed exception with no managed stack. This is exactly how the
/// LibVLCSharp VideoView TypeLoad startup crash was diagnosed. <see cref="Disarm"/> is called once the main
/// window is up so benign first-chance loader exceptions during normal operation don't spam the crash log.
/// </summary>
internal static class StartupDiagnostics
{
    private static EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs>? _handler;

    [ModuleInitializer]
    internal static void Init()
    {
        try
        {
            _handler = (_, e) =>
            {
                // Only the loader-class faults that turn into the startup stowed-exception crash.
                if (e.Exception is TypeLoadException
                    or System.IO.FileNotFoundException
                    or System.IO.FileLoadException
                    or MissingMethodException
                    or MissingMemberException
                    or BadImageFormatException)
                {
                    CrashLogger.Log("FirstChance:" + e.Exception.GetType().Name, e.Exception);
                }
            };
            AppDomain.CurrentDomain.FirstChanceException += _handler;
        }
        catch { /* diagnostics must never affect startup */ }
    }

    /// <summary>啟動完成後解除 · Detach once startup succeeded, so normal-operation probing doesn't log.</summary>
    internal static void Disarm()
    {
        try
        {
            if (_handler is not null)
            {
                AppDomain.CurrentDomain.FirstChanceException -= _handler;
                _handler = null;
            }
        }
        catch { }
    }
}
