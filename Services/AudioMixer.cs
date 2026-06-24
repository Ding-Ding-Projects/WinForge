// AudioMixer.cs
// Self-contained Windows Core Audio (WASAPI) per-application volume mixer.
// Raw COM interop — no NAudio, no third-party libraries.
// Target: x64, .NET (any modern .NET / .NET 6+). namespace WinForge.Services.
//
// THREADING / LIFETIME NOTES (see also caveats field):
//   * Every public method creates a fresh MMDeviceEnumerator, resolves the
//     default render endpoint, does its work, and releases all COM objects
//     before returning. Nothing is cached, so there is no cross-thread COM
//     marshalling problem and no stale-handle problem after a device change.
//   * Call from the WinUI UI thread (already COM-initialized, STA). If you
//     call from a background thread, that thread must have called
//     CoInitializeEx first (MTA is fine for these apartment-neutral objects,
//     but the UI thread is simplest and recommended).
//   * All HRESULT-returning vtable methods are [PreserveSig] returning int and
//     are checked via Marshal.ThrowExceptionForHR, which throws a managed
//     exception carrying the HRESULT for any failure.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinForge.Services
{
    public sealed class AudioSession
    {
        public int Pid;
        public string DisplayName = "";
        public float Level;
        public bool Muted;
        public string SessionId = "";
    }

    public static class AudioMixer
    {
        // ---------------------------------------------------------------
        // CLSIDs
        // ---------------------------------------------------------------
        // CLSID_MMDeviceEnumerator
        private static readonly Guid CLSID_MMDeviceEnumerator =
            new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

        // Data-flow / role enums (passed as ints)
        private const int eRender = 0;      // EDataFlow
        private const int eConsole = 0;     // ERole

        // STGM_READ for IPropertyStore (not used here but documented)
        // private const int STGM_READ = 0x0;

        // Activation CLSCTX
        private const uint CLSCTX_ALL = 0x17;

        // ===============================================================
        // Public API
        // ===============================================================

        /// <summary>Master volume (0..1) + mute of the default render endpoint.</summary>
        public static (float level, bool muted) GetMaster()
        {
            object enumObj = null;
            IMMDeviceEnumerator devEnum = null;
            IMMDevice device = null;
            IAudioEndpointVolume epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                epv = ActivateEndpointVolume(device);

                Check(epv.GetMasterVolumeLevelScalar(out float level), "GetMasterVolumeLevelScalar");
                Check(epv.GetMute(out int mute), "GetMute");
                return (level, mute != 0);
            }
            finally
            {
                Release(epv);
                Release(device);
                Release(devEnum);
                Release(enumObj);
            }
        }

        public static void SetMasterLevel(float v)
        {
            v = Clamp01(v);
            object enumObj = null;
            IMMDeviceEnumerator devEnum = null;
            IMMDevice device = null;
            IAudioEndpointVolume epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                epv = ActivateEndpointVolume(device);

                Guid ctx = Guid.Empty;
                Check(epv.SetMasterVolumeLevelScalar(v, ref ctx), "SetMasterVolumeLevelScalar");
            }
            finally
            {
                Release(epv);
                Release(device);
                Release(devEnum);
                Release(enumObj);
            }
        }

        public static void SetMasterMute(bool m)
        {
            object enumObj = null;
            IMMDeviceEnumerator devEnum = null;
            IMMDevice device = null;
            IAudioEndpointVolume epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                epv = ActivateEndpointVolume(device);

                Guid ctx = Guid.Empty;
                Check(epv.SetMute(m ? 1 : 0, ref ctx), "SetMute");
            }
            finally
            {
                Release(epv);
                Release(device);
                Release(devEnum);
                Release(enumObj);
            }
        }

        /// <summary>One row per active audio session on the default render endpoint.</summary>
        public static List<AudioSession> GetSessions()
        {
            var result = new List<AudioSession>();

            object enumObj = null;
            IMMDeviceEnumerator devEnum = null;
            IMMDevice device = null;
            object mgrObj = null;
            IAudioSessionManager2 mgr = null;
            IAudioSessionEnumerator sessEnum = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                mgrObj = ActivateSessionManager(device, out mgr);

                Check(mgr.GetSessionEnumerator(out sessEnum), "GetSessionEnumerator");
                Check(sessEnum.GetCount(out int count), "GetCount");

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctrl = null;
                    IAudioSessionControl2 ctrl2 = null;
                    ISimpleAudioVolume vol = null;
                    try
                    {
                        Check(sessEnum.GetSession(i, out ctrl), "GetSession");
                        if (ctrl == null) continue;

                        // QI to IAudioSessionControl2 (pid / displayname / system-sounds)
                        ctrl2 = (IAudioSessionControl2)ctrl;
                        // QI the SAME control object to ISimpleAudioVolume (level / mute)
                        vol = (ISimpleAudioVolume)ctrl;

                        bool isSystemSounds = ctrl2.IsSystemSoundsSession() == 0; // S_OK == 0 => yes

                        int pid = 0;
                        ctrl2.GetProcessId(out pid); // 0 for system sounds; ignore HRESULT failure

                        string sessionId = "";
                        try { ctrl2.GetSessionIdentifier(out sessionId); } catch { /* optional */ }
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            // Fall back to the per-instance id so SetSession* can still find it.
                            try { ctrl2.GetSessionInstanceIdentifier(out sessionId); } catch { }
                        }

                        string display = "";
                        try { ctrl2.GetDisplayName(out display); } catch { }
                        display = display?.Trim() ?? "";

                        if (isSystemSounds)
                        {
                            display = "System sounds";
                        }
                        else if (string.IsNullOrEmpty(display) ||
                                 display.StartsWith("@%", StringComparison.Ordinal) ||
                                 display.StartsWith("@", StringComparison.Ordinal))
                        {
                            // Empty or resource-string (e.g. "@%SystemRoot%\\...,-1234"):
                            // fall back to the process name.
                            display = ProcessNameForPid(pid);
                        }

                        if (string.IsNullOrEmpty(display))
                            display = pid > 0 ? ("PID " + pid) : "Unknown";

                        Check(vol.GetMasterVolume(out float level), "GetMasterVolume");
                        Check(vol.GetMute(out int mute), "GetMute");

                        result.Add(new AudioSession
                        {
                            Pid = pid,
                            DisplayName = display,
                            Level = level,
                            Muted = mute != 0,
                            SessionId = sessionId
                        });
                    }
                    finally
                    {
                        Release(vol);
                        Release(ctrl2);
                        Release(ctrl);
                    }
                }
            }
            finally
            {
                Release(sessEnum);
                Release(mgr);
                Release(mgrObj);
                Release(device);
                Release(devEnum);
                Release(enumObj);
            }

            return result;
        }

        public static void SetSessionLevel(string sessionId, float v)
        {
            v = Clamp01(v);
            ForEachSession(sessionId, (vol) =>
            {
                Guid ctx = Guid.Empty;
                Check(vol.SetMasterVolume(v, ref ctx), "SetMasterVolume");
            });
        }

        public static void SetSessionMute(string sessionId, bool m)
        {
            ForEachSession(sessionId, (vol) =>
            {
                Guid ctx = Guid.Empty;
                Check(vol.SetMute(m ? 1 : 0, ref ctx), "SetMute");
            });
        }

        // ===============================================================
        // Internal helpers
        // ===============================================================

        private static void ForEachSession(string sessionId, Action<ISimpleAudioVolume> apply)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            object enumObj = null;
            IMMDeviceEnumerator devEnum = null;
            IMMDevice device = null;
            object mgrObj = null;
            IAudioSessionManager2 mgr = null;
            IAudioSessionEnumerator sessEnum = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                mgrObj = ActivateSessionManager(device, out mgr);

                Check(mgr.GetSessionEnumerator(out sessEnum), "GetSessionEnumerator");
                Check(sessEnum.GetCount(out int count), "GetCount");

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctrl = null;
                    IAudioSessionControl2 ctrl2 = null;
                    ISimpleAudioVolume vol = null;
                    try
                    {
                        Check(sessEnum.GetSession(i, out ctrl), "GetSession");
                        if (ctrl == null) continue;

                        ctrl2 = (IAudioSessionControl2)ctrl;

                        string id = "";
                        try { ctrl2.GetSessionIdentifier(out id); } catch { }
                        string instId = "";
                        try { ctrl2.GetSessionInstanceIdentifier(out instId); } catch { }

                        if (!string.Equals(id, sessionId, StringComparison.Ordinal) &&
                            !string.Equals(instId, sessionId, StringComparison.Ordinal))
                            continue;

                        vol = (ISimpleAudioVolume)ctrl;
                        apply(vol);
                    }
                    finally
                    {
                        Release(vol);
                        Release(ctrl2);
                        Release(ctrl);
                    }
                }
            }
            finally
            {
                Release(sessEnum);
                Release(mgr);
                Release(mgrObj);
                Release(device);
                Release(devEnum);
                Release(enumObj);
            }
        }

        private static object CreateEnumerator(out IMMDeviceEnumerator devEnum)
        {
            Type t = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator, throwOnError: true);
            object o = Activator.CreateInstance(t);
            devEnum = (IMMDeviceEnumerator)o;
            return o;
        }

        private static IMMDevice GetDefaultRenderDevice(IMMDeviceEnumerator devEnum)
        {
            Check(devEnum.GetDefaultAudioEndpoint(eRender, eConsole, out IMMDevice dev),
                  "GetDefaultAudioEndpoint");
            return dev;
        }

        private static IAudioEndpointVolume ActivateEndpointVolume(IMMDevice device)
        {
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            Check(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o), "Activate(IAudioEndpointVolume)");
            return (IAudioEndpointVolume)o;
        }

        private static object ActivateSessionManager(IMMDevice device, out IAudioSessionManager2 mgr)
        {
            Guid iid = typeof(IAudioSessionManager2).GUID;
            Check(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o), "Activate(IAudioSessionManager2)");
            mgr = (IAudioSessionManager2)o;
            return o;
        }

        private static string ProcessNameForPid(int pid)
        {
            if (pid <= 0) return "";
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return "";
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static void Check(int hr, string what)
        {
            if (hr < 0)
            {
                // Throws a COMException (or mapped subclass) carrying the HRESULT.
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        private static void Release(object o)
        {
            if (o != null && Marshal.IsComObject(o))
            {
                try { Marshal.ReleaseComObject(o); } catch { }
            }
        }
    }

    // ===================================================================
    // COM interop definitions
    // Every interface lists EVERY method in EXACT vtable order. Methods
    // that precede the ones we call are present as placeholders so the
    // vtable offsets are correct. HRESULT methods are [PreserveSig] int.
    // ===================================================================

    // coclass MMDeviceEnumerator
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class MMDeviceEnumeratorComObject { }

    // IMMDeviceEnumerator : IUnknown
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        // 0: HRESULT EnumAudioEndpoints(EDataFlow, DWORD dwStateMask, IMMDeviceCollection**)
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);

        // 1: HRESULT GetDefaultAudioEndpoint(EDataFlow, ERole, IMMDevice**)
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role,
            [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppEndpoint);

        // 2: HRESULT GetDevice(LPCWSTR, IMMDevice**)
        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
            [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);

        // 3: HRESULT RegisterEndpointNotificationCallback(IMMNotificationClient*)
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr pClient);

        // 4: HRESULT UnregisterEndpointNotificationCallback(IMMNotificationClient*)
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    // IMMDevice : IUnknown
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        // 0: HRESULT Activate(REFIID, DWORD dwClsCtx, PROPVARIANT*, void** ppInterface)
        [PreserveSig]
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        // 1: HRESULT OpenPropertyStore(DWORD stgmAccess, IPropertyStore**)
        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);

        // 2: HRESULT GetId(LPWSTR* ppstrId)
        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

        // 3: HRESULT GetState(DWORD* pdwState)
        [PreserveSig]
        int GetState(out int pdwState);
    }

    // IAudioEndpointVolume : IUnknown
    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        // 0: RegisterControlChangeNotify(IAudioEndpointVolumeCallback*)
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        // 1: UnregisterControlChangeNotify(IAudioEndpointVolumeCallback*)
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        // 2: GetChannelCount(UINT*)
        [PreserveSig] int GetChannelCount(out int pnChannelCount);
        // 3: SetMasterVolumeLevel(float fLevelDB, LPCGUID pguidEventContext)
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        // 4: SetMasterVolumeLevelScalar(float fLevel, LPCGUID)
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        // 5: GetMasterVolumeLevel(float*)
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        // 6: GetMasterVolumeLevelScalar(float*)
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        // 7: SetChannelVolumeLevel(UINT nChannel, float fLevelDB, LPCGUID)
        [PreserveSig] int SetChannelVolumeLevel(int nChannel, float fLevelDB, ref Guid pguidEventContext);
        // 8: SetChannelVolumeLevelScalar(UINT nChannel, float fLevel, LPCGUID)
        [PreserveSig] int SetChannelVolumeLevelScalar(int nChannel, float fLevel, ref Guid pguidEventContext);
        // 9: GetChannelVolumeLevel(UINT nChannel, float*)
        [PreserveSig] int GetChannelVolumeLevel(int nChannel, out float pfLevelDB);
        // 10: GetChannelVolumeLevelScalar(UINT nChannel, float*)
        [PreserveSig] int GetChannelVolumeLevelScalar(int nChannel, out float pfLevel);
        // 11: SetMute(BOOL bMute, LPCGUID)
        [PreserveSig] int SetMute(int bMute, ref Guid pguidEventContext);
        // 12: GetMute(BOOL*)
        [PreserveSig] int GetMute(out int pbMute);
        // 13: GetVolumeStepInfo(UINT* pnStep, UINT* pnStepCount)
        [PreserveSig] int GetVolumeStepInfo(out int pnStep, out int pnStepCount);
        // 14: VolumeStepUp(LPCGUID)
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        // 15: VolumeStepDown(LPCGUID)
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        // 16: QueryHardwareSupport(DWORD*)
        [PreserveSig] int QueryHardwareSupport(out int pdwHardwareSupportMask);
        // 17: GetVolumeRange(float* min, float* max, float* increment)
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    // IAudioSessionManager2 : IAudioSessionManager : IUnknown
    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        // ---- IAudioSessionManager ----
        // 0: GetAudioSessionControl(LPCGUID, DWORD, IAudioSessionControl**)
        [PreserveSig] int GetAudioSessionControl(IntPtr AudioSessionGuid, int StreamFlags, out IntPtr SessionControl);
        // 1: GetSimpleAudioVolume(LPCGUID, DWORD, ISimpleAudioVolume**)
        [PreserveSig] int GetSimpleAudioVolume(IntPtr AudioSessionGuid, int StreamFlags, out IntPtr AudioVolume);
        // ---- IAudioSessionManager2 ----
        // 2: GetSessionEnumerator(IAudioSessionEnumerator**)
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        // 3: RegisterSessionNotification(IAudioSessionNotification*)
        [PreserveSig] int RegisterSessionNotification(IntPtr SessionNotification);
        // 4: UnregisterSessionNotification(IAudioSessionNotification*)
        [PreserveSig] int UnregisterSessionNotification(IntPtr SessionNotification);
        // 5: RegisterDuckNotification(LPCWSTR, IAudioVolumeDuckNotification*)
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
        // 6: UnregisterDuckNotification(IAudioVolumeDuckNotification*)
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    // IAudioSessionEnumerator : IUnknown
    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        // 0: GetCount(int*)
        [PreserveSig] int GetCount(out int SessionCount);
        // 1: GetSession(int SessionCount, IAudioSessionControl**)
        [PreserveSig] int GetSession(int SessionCount,
            [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl Session);
    }

    // IAudioSessionControl : IUnknown
    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        // 0: GetState(AudioSessionState*)
        [PreserveSig] int GetState(out int pRetVal);
        // 1: GetDisplayName(LPWSTR*)
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 2: SetDisplayName(LPCWSTR, LPCGUID)
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        // 3: GetIconPath(LPWSTR*)
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 4: SetIconPath(LPCWSTR, LPCGUID)
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        // 5: GetGroupingParam(GUID*)
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        // 6: SetGroupingParam(LPCGUID, LPCGUID)
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        // 7: RegisterAudioSessionNotification(IAudioSessionEvents*)
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        // 8: UnregisterAudioSessionNotification(IAudioSessionEvents*)
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
    }

    // IAudioSessionControl2 : IAudioSessionControl : IUnknown
    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        // ---- IAudioSessionControl ----
        // 0: GetState
        [PreserveSig] int GetState(out int pRetVal);
        // 1: GetDisplayName
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 2: SetDisplayName
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        // 3: GetIconPath
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 4: SetIconPath
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        // 5: GetGroupingParam
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        // 6: SetGroupingParam
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        // 7: RegisterAudioSessionNotification
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        // 8: UnregisterAudioSessionNotification
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        // ---- IAudioSessionControl2 ----
        // 9: GetSessionIdentifier(LPWSTR*)
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 10: GetSessionInstanceIdentifier(LPWSTR*)
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        // 11: GetProcessId(DWORD*)
        [PreserveSig] int GetProcessId(out int pRetVal);
        // 12: IsSystemSoundsSession()  -> S_OK (0) if yes, S_FALSE (1) if no
        [PreserveSig] int IsSystemSoundsSession();
        // 13: SetDuckingPreference(BOOL)
        [PreserveSig] int SetDuckingPreference(int optOut);
    }

    // ISimpleAudioVolume : IUnknown
    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        // 0: SetMasterVolume(float fLevel, LPCGUID EventContext)
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        // 1: GetMasterVolume(float* pfLevel)
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        // 2: SetMute(BOOL bMute, LPCGUID EventContext)
        [PreserveSig] int SetMute(int bMute, ref Guid EventContext);
        // 3: GetMute(BOOL* pbMute)
        [PreserveSig] int GetMute(out int pbMute);
    }
}