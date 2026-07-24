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

    /// <summary>一個輸出（render）裝置 · One active render (playback) endpoint.</summary>
    public sealed class AudioDeviceInfo
    {
        public string Id = "";
        public string FriendlyName = "";
        public bool IsDefault;
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
        private const int eMultimedia = 1;  // ERole
        private const int eCommunications = 2; // ERole

        // DEVICE_STATE_ACTIVE for IMMDeviceEnumerator.EnumAudioEndpoints
        private const int DEVICE_STATE_ACTIVE = 0x00000001;

        // STGM_READ for IPropertyStore
        private const int STGM_READ = 0x0;

        // PKEY_Device_FriendlyName
        private static readonly Guid PKEY_Device_FriendlyName_fmtid =
            new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");
        private const int PKEY_Device_FriendlyName_pid = 14;

        // Activation CLSCTX
        private const uint CLSCTX_ALL = 0x17;

        // ===============================================================
        // Public API
        // ===============================================================

        /// <summary>Master volume (0..1) + mute of the default render endpoint.</summary>
        public static (float level, bool muted) GetMaster()
        {
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
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
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
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
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
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

            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            object? mgrObj = null;
            IAudioSessionManager2? mgr = null;
            IAudioSessionEnumerator? sessEnum = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                mgrObj = ActivateSessionManager(device, out mgr);

                Check(mgr.GetSessionEnumerator(out sessEnum), "GetSessionEnumerator");
                Check(sessEnum.GetCount(out int count), "GetCount");

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl? ctrl = null;
                    IAudioSessionControl2? ctrl2 = null;
                    ISimpleAudioVolume? vol = null;
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

        // ---------------------------------------------------------------
        // Device enumeration / default-device switching (EarTrumpet port)
        // ---------------------------------------------------------------

        /// <summary>All active render (playback) endpoints, with the current default flagged.</summary>
        public static List<AudioDeviceInfo> GetRenderDevices()
        {
            var result = new List<AudioDeviceInfo>();

            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDeviceCollection? coll = null;
            IMMDevice? defDevice = null;
            string defaultId = "";
            try
            {
                enumObj = CreateEnumerator(out devEnum);

                // Best-effort: resolve the current default render device id.
                try
                {
                    if (devEnum.GetDefaultAudioEndpoint(eRender, eConsole, out defDevice) >= 0 && defDevice != null)
                        defDevice.GetId(out defaultId);
                }
                catch { defaultId = ""; }

                Check(devEnum.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out IntPtr pColl),
                      "EnumAudioEndpoints");
                coll = (IMMDeviceCollection)Marshal.GetObjectForIUnknown(pColl);
                Marshal.Release(pColl);

                Check(coll.GetCount(out int count), "IMMDeviceCollection.GetCount");
                for (int i = 0; i < count; i++)
                {
                    IMMDevice? dev = null;
                    try
                    {
                        if (coll.Item(i, out dev) < 0 || dev == null) continue;
                        string id = "";
                        try { dev.GetId(out id); } catch { }
                        if (string.IsNullOrEmpty(id)) continue;

                        result.Add(new AudioDeviceInfo
                        {
                            Id = id,
                            FriendlyName = ReadFriendlyName(dev, id),
                            IsDefault = !string.IsNullOrEmpty(defaultId) &&
                                        string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
                        });
                    }
                    finally { Release(dev); }
                }
            }
            finally
            {
                Release(defDevice);
                Release(coll);
                Release(devEnum);
                Release(enumObj);
            }

            return result;
        }

        /// <summary>Master volume + mute for a specific render endpoint (by device id).</summary>
        public static (float level, bool muted) GetMasterFor(string deviceId)
        {
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDeviceById(devEnum, deviceId);
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

        public static void SetMasterLevelFor(string deviceId, float v)
        {
            v = Clamp01(v);
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDeviceById(devEnum, deviceId);
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

        public static void SetMasterMuteFor(string deviceId, bool m)
        {
            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            IAudioEndpointVolume? epv = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDeviceById(devEnum, deviceId);
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

        /// <summary>One row per active session on the given render endpoint (by device id).</summary>
        public static List<AudioSession> GetSessionsForDevice(string deviceId)
        {
            var result = new List<AudioSession>();

            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            object? mgrObj = null;
            IAudioSessionManager2? mgr = null;
            IAudioSessionEnumerator? sessEnum = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDeviceById(devEnum, deviceId);
                mgrObj = ActivateSessionManager(device, out mgr);

                Check(mgr.GetSessionEnumerator(out sessEnum), "GetSessionEnumerator");
                Check(sessEnum.GetCount(out int count), "GetCount");

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl? ctrl = null;
                    IAudioSessionControl2? ctrl2 = null;
                    ISimpleAudioVolume? vol = null;
                    try
                    {
                        Check(sessEnum.GetSession(i, out ctrl), "GetSession");
                        if (ctrl == null) continue;

                        ctrl2 = (IAudioSessionControl2)ctrl;
                        vol = (ISimpleAudioVolume)ctrl;

                        bool isSystemSounds = ctrl2.IsSystemSoundsSession() == 0;
                        int pid = 0;
                        ctrl2.GetProcessId(out pid);

                        string sessionId = "";
                        try { ctrl2.GetSessionIdentifier(out sessionId); } catch { }
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            try { ctrl2.GetSessionInstanceIdentifier(out sessionId); } catch { }
                        }

                        string display = "";
                        try { ctrl2.GetDisplayName(out display); } catch { }
                        display = display?.Trim() ?? "";

                        if (isSystemSounds) display = "System sounds";
                        else if (string.IsNullOrEmpty(display) ||
                                 display.StartsWith("@", StringComparison.Ordinal))
                            display = ProcessNameForPid(pid);

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

        /// <summary>
        /// Make <paramref name="deviceId"/> the system default render endpoint.
        /// Sets all three roles (console / multimedia / communications), matching the
        /// Windows Sound control panel behaviour. Uses the undocumented IPolicyConfig.
        /// </summary>
        public static void SetDefaultEndpoint(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            object? client = null;
            IPolicyConfig? cfg = null;
            try
            {
                Type type = Type.GetTypeFromCLSID(CLSID_PolicyConfigClient, throwOnError: true)
                    ?? throw new COMException("Windows audio policy configuration is unavailable.");
                client = Activator.CreateInstance(type)
                    ?? throw new COMException("Windows audio policy configuration could not be activated.");
                cfg = client as IPolicyConfig
                    ?? throw new COMException("Windows audio policy configuration returned an incompatible interface.");

                Check(cfg.SetDefaultEndpoint(deviceId, eConsole), "SetDefaultEndpoint(eConsole)");
                Check(cfg.SetDefaultEndpoint(deviceId, eMultimedia), "SetDefaultEndpoint(eMultimedia)");
                Check(cfg.SetDefaultEndpoint(deviceId, eCommunications), "SetDefaultEndpoint(eCommunications)");
            }
            finally
            {
                Release(cfg);
                Release(client);
            }
        }

        // ===============================================================
        // Internal helpers
        // ===============================================================

        // CLSID_CPolicyConfigClient (undocumented; stable Win7..Win11).
        private static readonly Guid CLSID_PolicyConfigClient =
            new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

        private static IMMDevice GetDeviceById(IMMDeviceEnumerator devEnum, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return GetDefaultRenderDevice(devEnum);
            Check(devEnum.GetDevice(deviceId, out IMMDevice dev), "GetDevice");
            return dev;
        }

        private static string ReadFriendlyName(IMMDevice dev, string fallbackId)
        {
            IPropertyStore? store = null;
            try
            {
                if (dev.OpenPropertyStore(STGM_READ, out IntPtr pStore) < 0 || pStore == IntPtr.Zero)
                    return ShortId(fallbackId);
                store = (IPropertyStore)Marshal.GetObjectForIUnknown(pStore);
                Marshal.Release(pStore);

                var key = new PROPERTYKEY
                {
                    fmtid = PKEY_Device_FriendlyName_fmtid,
                    pid = (uint)PKEY_Device_FriendlyName_pid
                };
                var pv = new PROPVARIANT();
                try
                {
                    if (store.GetValue(ref key, out pv) >= 0)
                    {
                        string? name = pv.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) return name;
                    }
                }
                finally { try { PropVariantClear(ref pv); } catch { } }
            }
            catch { }
            finally { Release(store); }
            return ShortId(fallbackId);
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Unknown device";
            int dot = id.LastIndexOf('.');
            return dot >= 0 && dot < id.Length - 1 ? id.Substring(dot + 1) : id;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        private static void ForEachSession(string sessionId, Action<ISimpleAudioVolume> apply)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            object? enumObj = null;
            IMMDeviceEnumerator? devEnum = null;
            IMMDevice? device = null;
            object? mgrObj = null;
            IAudioSessionManager2? mgr = null;
            IAudioSessionEnumerator? sessEnum = null;
            try
            {
                enumObj = CreateEnumerator(out devEnum);
                device = GetDefaultRenderDevice(devEnum);
                mgrObj = ActivateSessionManager(device, out mgr);

                Check(mgr.GetSessionEnumerator(out sessEnum), "GetSessionEnumerator");
                Check(sessEnum.GetCount(out int count), "GetCount");

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl? ctrl = null;
                    IAudioSessionControl2? ctrl2 = null;
                    ISimpleAudioVolume? vol = null;
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
            Type type = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator, throwOnError: true)
                ?? throw new COMException("Windows audio device enumeration is unavailable.");
            object o = Activator.CreateInstance(type)
                ?? throw new COMException("Windows audio device enumeration could not be activated.");
            devEnum = o as IMMDeviceEnumerator
                ?? throw new COMException("Windows audio device enumeration returned an incompatible interface.");
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

        private static void Release(object? o)
        {
            if (o is not null && Marshal.IsComObject(o))
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

    // IMMDeviceCollection : IUnknown
    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        // 0: GetCount(UINT*)
        [PreserveSig] int GetCount(out int pcDevices);
        // 1: Item(UINT, IMMDevice**)
        [PreserveSig] int Item(int nDevice,
            [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
    }

    // PROPERTYKEY (REFPROPERTYKEY)
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // PROPVARIANT — minimal: enough to read VT_LPWSTR / VT_BSTR friendly names.
    [StructLayout(LayoutKind.Explicit)]
    internal struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;

        private const ushort VT_LPWSTR = 31;
        private const ushort VT_BSTR = 8;

        public string? GetString()
        {
            if (pointerValue == IntPtr.Zero) return null;
            if (vt == VT_LPWSTR) return Marshal.PtrToStringUni(pointerValue);
            if (vt == VT_BSTR) return Marshal.PtrToStringBSTR(pointerValue);
            return null;
        }
    }

    // IPropertyStore : IUnknown
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        // 0: GetCount(DWORD*)
        [PreserveSig] int GetCount(out uint cProps);
        // 1: GetAt(DWORD, PROPERTYKEY*)
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        // 2: GetValue(REFPROPERTYKEY, PROPVARIANT*)
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        // 3: SetValue(REFPROPERTYKEY, REFPROPVARIANT)
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        // 4: Commit()
        [PreserveSig] int Commit();
    }

    // IPolicyConfig (undocumented CPolicyConfigClient).
    // Vtable matches the Win7..Win11 layout used by EarTrumpet's IPolicyConfigWin7.
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        // 0..7: unused placeholders for correct vtable offsets
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        // 8: GetPropertyValue
        [PreserveSig] int GetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        // 9: SetPropertyValue
        [PreserveSig] int SetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        // 10: SetDefaultEndpoint(LPCWSTR wszDeviceId, ERole eRole)
        [PreserveSig] int SetDefaultEndpoint(
            [MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int eRole);
        // 11: SetEndpointVisibility(LPCWSTR, INT)
        [PreserveSig] int SetEndpointVisibility(
            [MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int isVisible);
    }
}
