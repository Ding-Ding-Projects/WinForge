using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 檔案／資料夾對話框（Win32 COM）· File and folder pickers built on the Win32 common item dialog
/// (IFileOpenDialog / IFileSaveDialog). 用 COM 對話框而唔用 WinRT picker，因為 WinRT picker 喺
/// 以管理員身分執行嘅程序入面會默默失敗（開唔到）。This replaces the WinRT pickers, which fail
/// silently when the app runs elevated — the COM dialogs work both elevated and not, packaged or not.
/// Runs the modal dialog on a dedicated STA thread so the UI thread is never blocked.
/// Interfaces are flattened (no COM-inheritance) — the canonical, unambiguous vtable layout.
/// </summary>
public static class FileDialogs
{
    /// <summary>一對篩選器（顯示名 + 用「;」分隔嘅樣式，例如 "*.mp4;*.mkv"）· One filter pair.</summary>
    public readonly record struct Filter(string Name, string Spec);

    /// <summary>揀單一檔案 · Pick one file; returns its path, or null if cancelled.</summary>
    public static Task<string?> OpenFileAsync(params string[] extensions)
        => OpenFileAsync(BuildFilters(extensions), null);

    public static Task<string?> OpenFileAsync(IEnumerable<Filter>? filters, string? title = null)
        => RunStaAsync(() =>
        {
            var dlg = (IFileOpenDialog)new FileOpenDialogClass();
            dlg.GetOptions(out var opts);
            dlg.SetOptions(opts | FOS_FORCEFILESYSTEM | FOS_FILEMUSTEXIST | FOS_PATHMUSTEXIST);
            Apply(filters, dlg.SetFileTypes);
            if (title is not null) dlg.SetTitle(title);
            if (dlg.Show(OwnerHwnd()) != 0) return null;
            dlg.GetResult(out var item);
            return PathOf(item);
        });

    /// <summary>揀多個檔案 · Pick multiple files.</summary>
    public static Task<IReadOnlyList<string>> OpenFilesAsync(params string[] extensions)
        => OpenFilesAsync(BuildFilters(extensions), null);

    public static Task<IReadOnlyList<string>> OpenFilesAsync(IEnumerable<Filter>? filters, string? title = null)
        => RunStaAsync<IReadOnlyList<string>>(() =>
        {
            var dlg = (IFileOpenDialog)new FileOpenDialogClass();
            dlg.GetOptions(out var opts);
            dlg.SetOptions(opts | FOS_FORCEFILESYSTEM | FOS_FILEMUSTEXIST | FOS_PATHMUSTEXIST | FOS_ALLOWMULTISELECT);
            Apply(filters, dlg.SetFileTypes);
            if (title is not null) dlg.SetTitle(title);
            if (dlg.Show(OwnerHwnd()) != 0) return Array.Empty<string>();
            dlg.GetResults(out var array);
            var list = new List<string>();
            array.GetCount(out uint n);
            for (uint i = 0; i < n; i++)
            {
                array.GetItemAt(i, out var item);
                var p = PathOf(item);
                if (p is not null) list.Add(p);
            }
            return list;
        });

    /// <summary>揀一個資料夾 · Pick a folder; returns its path, or null if cancelled.</summary>
    public static Task<string?> OpenFolderAsync(string? title = null)
        => RunStaAsync(() =>
        {
            var dlg = (IFileOpenDialog)new FileOpenDialogClass();
            dlg.GetOptions(out var opts);
            dlg.SetOptions(opts | FOS_FORCEFILESYSTEM | FOS_PICKFOLDERS | FOS_PATHMUSTEXIST);
            if (title is not null) dlg.SetTitle(title);
            if (dlg.Show(OwnerHwnd()) != 0) return null;
            dlg.GetResult(out var item);
            return PathOf(item);
        });

    /// <summary>另存新檔 · Save-as; returns the chosen path, or null if cancelled.</summary>
    public static Task<string?> SaveFileAsync(string? suggestedName = null, IEnumerable<Filter>? filters = null,
        string? defaultExt = null, string? title = null)
        => RunStaAsync(() =>
        {
            var dlg = (IFileSaveDialog)new FileSaveDialogClass();
            dlg.GetOptions(out var opts);
            dlg.SetOptions(opts | FOS_FORCEFILESYSTEM | FOS_OVERWRITEPROMPT);
            Apply(filters, dlg.SetFileTypes);
            if (!string.IsNullOrEmpty(suggestedName)) dlg.SetFileName(suggestedName);
            if (!string.IsNullOrEmpty(defaultExt)) dlg.SetDefaultExtension(defaultExt.TrimStart('.'));
            if (title is not null) dlg.SetTitle(title);
            if (dlg.Show(OwnerHwnd()) != 0) return null;
            dlg.GetResult(out var item);
            return PathOf(item);
        });

    public static Task<string?> SaveFileAsync(string suggestedName, params string[] extensions)
        => SaveFileAsync(suggestedName, BuildFilters(extensions),
            extensions.FirstOrDefault(e => e.StartsWith('.'))?.TrimStart('.'));

    /// <summary>由副檔名建構 filters · Build a "Supported"+"All files" filter set from extensions like ".mp4".</summary>
    public static IEnumerable<Filter>? BuildFilters(string[]? extensions)
    {
        if (extensions is null || extensions.Length == 0) return null;
        var exts = extensions.Where(e => !string.IsNullOrWhiteSpace(e) && e != "*" && e != "*.*").ToList();
        var list = new List<Filter>();
        if (exts.Count > 0)
        {
            var spec = string.Join(";", exts.Select(e => "*" + (e.StartsWith('.') ? e : "." + e)));
            list.Add(new Filter("Supported files", spec));
        }
        list.Add(new Filter("All files", "*.*"));
        return list;
    }

    // ===== internals =====

    private static void Apply(IEnumerable<Filter>? filters, Action<uint, COMDLG_FILTERSPEC[]> setFileTypes)
    {
        var arr = filters?.ToArray();
        if (arr is null || arr.Length == 0) return;
        var specs = arr.Select(f => new COMDLG_FILTERSPEC { pszName = f.Name, pszSpec = f.Spec }).ToArray();
        try { setFileTypes((uint)specs.Length, specs); } catch { /* ignore bad filter */ }
    }

    private static string? PathOf(IShellItem item)
    {
        if (item is null) return null;
        try
        {
            item.GetDisplayName(SIGDN_FILESYSPATH, out var ptr);
            if (ptr == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(ptr); }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }
        catch { return null; }
        finally { try { Marshal.ReleaseComObject(item); } catch { } }
    }

    private static IntPtr OwnerHwnd()
    {
        try
        {
            if (App.Shell is { } w) return WinRT.Interop.WindowNative.GetWindowHandle(w);
        }
        catch { }
        return IntPtr.Zero;
    }

    /// <summary>喺專用 STA 執行緒上彈對話框 · Run the modal dialog on a dedicated STA thread.</summary>
    private static Task<T> RunStaAsync<T>(Func<T> body)
    {
        var tcs = new TaskCompletionSource<T>();
        var t = new Thread(() =>
        {
            try { tcs.SetResult(body()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        return tcs.Task;
    }

    // ===== COM interop (flattened — every interface lists all its vtable methods in order) =====

    private const uint SIGDN_FILESYSPATH = 0x80058000;
    private const uint FOS_OVERWRITEPROMPT = 0x00000002;
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint FOS_ALLOWMULTISELECT = 0x00000200;
    private const uint FOS_PATHMUSTEXIST = 0x00000800;
    private const uint FOS_FILEMUSTEXIST = 0x00001000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogClass { }

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialogClass { }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, ref Guid rbhid, ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
        void GetAttributes(int dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    // The shared IFileDialog vtable (IModalWindow::Show first, then IFileDialog methods).
    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        // IFileOpenDialog
        void GetResults(out IShellItemArray ppenum);
        void GetSelectedItems(out IShellItemArray ppsai);
    }

    [ComImport, Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        // IFileSaveDialog (extra methods unused — declared for vtable completeness)
        void SetSaveAsItem(IShellItem psi);
        void SetProperties(IntPtr pStore);
        void SetCollectedProperties(IntPtr pList, int fAppendDefault);
        void GetProperties(out IntPtr ppStore);
        void ApplyProperties(IShellItem psi, IntPtr pStore, IntPtr hwnd, IntPtr pSink);
    }
}
