// AudioPolicyConfig.cs
// Per-application default audio endpoint ("move this app's stream to another device").
// Native port of EarTrumpet's AudioPolicyConfigService — raw COM, no third-party libs.
//
// This uses the UNDOCUMENTED WinRT runtime class "Windows.Media.Internal.AudioPolicyConfig"
// and its IAudioPolicyConfigFactory interface. The vtable layout differs across Windows
// builds (21H2+ vs downlevel use different IIDs but the same method shape), so every call
// is wrapped in try/catch and feature-detected — a failure degrades gracefully and never
// crashes the UI. Per-app routing may not take effect until the target app restarts.

using System;
using System.Runtime.InteropServices;

namespace WinForge.Services
{
    public static class AudioPolicyConfig
    {
        // EDataFlow.eRender
        private const int eRender = 0;
        // ERole
        private const int eConsole = 0;
        private const int eMultimedia = 1;

        // The render device-interface token wrapper Windows expects around a bare endpoint id.
        private const string DEVINTERFACE_AUDIO_RENDER = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        private const string MMDEVAPI_TOKEN = @"\\?\SWD#MMDEVAPI#";

        // 21H2+ (build 22000) factory IID; downlevel (Win10) uses a different IID.
        private static readonly Guid IID_Factory_21H2 =
            new Guid("ab3d4648-e242-459f-b02f-541c70306324");
        private static readonly Guid IID_Factory_Downlevel =
            new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258");

        private const string RUNTIMECLASS = "Windows.Media.Internal.AudioPolicyConfig";

        /// <summary>True if the undocumented policy-config factory could be activated on this build.</summary>
        public static bool IsSupported()
        {
            object? factory = null;
            try
            {
                factory = CreateFactory();
                return factory != null;
            }
            catch { return false; }
            finally { Release(factory); }
        }

        /// <summary>
        /// Route a specific process's playback to <paramref name="deviceId"/> (per-app default
        /// output device). Pass a null/empty deviceId to clear the override (back to system default).
        /// Returns true on success.
        /// </summary>
        public static bool SetAppDefaultDevice(int processId, string? deviceId)
        {
            object? factory = null;
            try
            {
                factory = CreateFactory();
                if (factory == null) return false;

                IntPtr hstring = IntPtr.Zero;
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    string packed = $"{MMDEVAPI_TOKEN}{deviceId}{DEVINTERFACE_AUDIO_RENDER}";
                    WindowsCreateString(packed, (uint)packed.Length, out hstring);
                }

                try
                {
                    bool ok;
                    if (factory is IAudioPolicyConfigFactory21H2 f1)
                    {
                        ok = f1.SetPersistedDefaultAudioEndpoint((uint)processId, eRender, eMultimedia, hstring) >= 0;
                        ok &= f1.SetPersistedDefaultAudioEndpoint((uint)processId, eRender, eConsole, hstring) >= 0;
                    }
                    else if (factory is IAudioPolicyConfigFactoryDownlevel f2)
                    {
                        ok = f2.SetPersistedDefaultAudioEndpoint((uint)processId, eRender, eMultimedia, hstring) >= 0;
                        ok &= f2.SetPersistedDefaultAudioEndpoint((uint)processId, eRender, eConsole, hstring) >= 0;
                    }
                    else ok = false;
                    return ok;
                }
                finally
                {
                    if (hstring != IntPtr.Zero) WindowsDeleteString(hstring);
                }
            }
            catch { return false; }
            finally { Release(factory); }
        }

        /// <summary>Clear an app's per-app output override (revert to following the system default).</summary>
        public static bool ClearAppDefaultDevice(int processId) => SetAppDefaultDevice(processId, null);

        // -----------------------------------------------------------------

        private static object? CreateFactory()
        {
            // Try the 21H2+ shape first on modern builds, then fall back to downlevel.
            bool atLeast21H2 = Environment.OSVersion.Version.Build >= 22000;
            Guid primary = atLeast21H2 ? IID_Factory_21H2 : IID_Factory_Downlevel;
            Guid secondary = atLeast21H2 ? IID_Factory_Downlevel : IID_Factory_21H2;

            try
            {
                Guid iid = primary;
                RoGetActivationFactory(RUNTIMECLASS, ref iid, out object f);
                return f;
            }
            catch
            {
                try
                {
                    Guid iid = secondary;
                    RoGetActivationFactory(RUNTIMECLASS, ref iid, out object f);
                    return f;
                }
                catch { return null; }
            }
        }

        private static void Release(object? o)
        {
            if (o is null) return;
            if (o != null && Marshal.IsComObject(o))
            {
                try { Marshal.ReleaseComObject(o); } catch { }
            }
        }

        // ---- combase interop ----
        [DllImport("combase.dll", PreserveSig = false)]
        private static extern void RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out, MarshalAs(UnmanagedType.IInspectable)] out object factory);

        [DllImport("combase.dll", PreserveSig = false)]
        private static extern void WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string src,
            [In] uint length,
            [Out] out IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = false)]
        private static extern void WindowsDeleteString([In] IntPtr hstring);
    }

    // IAudioPolicyConfigFactory — 21H2+ variant (IInspectable-based).
    [ComImport]
    [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    internal interface IAudioPolicyConfigFactory21H2
    {
        // 19 placeholder slots before the methods we need (matches EarTrumpet's port).
        [PreserveSig] int p00(); [PreserveSig] int p01(); [PreserveSig] int p02();
        [PreserveSig] int p03(); [PreserveSig] int p04(); [PreserveSig] int p05();
        [PreserveSig] int p06(); [PreserveSig] int p07(); [PreserveSig] int p08();
        [PreserveSig] int p09(); [PreserveSig] int p10(); [PreserveSig] int p11();
        [PreserveSig] int p12(); [PreserveSig] int p13(); [PreserveSig] int p14();
        [PreserveSig] int p15(); [PreserveSig] int p16(); [PreserveSig] int p17();
        [PreserveSig] int p18();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, IntPtr deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, int flow, int role,
            [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    // IAudioPolicyConfigFactory — downlevel (Win10) variant; same shape, different IID.
    [ComImport]
    [Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    internal interface IAudioPolicyConfigFactoryDownlevel
    {
        [PreserveSig] int p00(); [PreserveSig] int p01(); [PreserveSig] int p02();
        [PreserveSig] int p03(); [PreserveSig] int p04(); [PreserveSig] int p05();
        [PreserveSig] int p06(); [PreserveSig] int p07(); [PreserveSig] int p08();
        [PreserveSig] int p09(); [PreserveSig] int p10(); [PreserveSig] int p11();
        [PreserveSig] int p12(); [PreserveSig] int p13(); [PreserveSig] int p14();
        [PreserveSig] int p15(); [PreserveSig] int p16(); [PreserveSig] int p17();
        [PreserveSig] int p18();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, IntPtr deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, int flow, int role,
            [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }
}
