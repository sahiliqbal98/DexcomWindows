using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DexcomWindows.Helpers;

internal static class StartMenuShortcutHelper
{
    // PKEY_AppUserModel_ID
    private static PropertyKey PKEY_AppUserModel_ID = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    public static void EnsureStartMenuShortcut(string shortcutDisplayName, string appUserModelId, string exePath, string? iconPath = null)
    {
        if (string.IsNullOrWhiteSpace(shortcutDisplayName)) throw new ArgumentException("Shortcut name is required.", nameof(shortcutDisplayName));
        if (string.IsNullOrWhiteSpace(appUserModelId)) throw new ArgumentException("AppUserModelID is required.", nameof(appUserModelId));
        if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentException("Exe path is required.", nameof(exePath));
        if (!File.Exists(exePath)) throw new FileNotFoundException("Exe not found.", exePath);

        var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        Directory.CreateDirectory(programs);

        var shortcutPath = Path.Combine(programs, $"{shortcutDisplayName}.lnk");
        CreateOrUpdateShortcut(shortcutPath, appUserModelId, exePath, iconPath ?? exePath);
    }

    private static void CreateOrUpdateShortcut(string shortcutPath, string appUserModelId, string exePath, string iconPath)
    {
        IShellLinkW? link = null;
        IPropertyStore? store = null;
        IPersistFile? persist = null;

        try
        {
            link = (IShellLinkW)new CShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
            link.SetDescription("Steady Sugar - Dexcom glucose monitoring for Windows");
            link.SetIconLocation(iconPath, 0);

            store = (IPropertyStore)link;
            var key = PKEY_AppUserModel_ID;
            var pv = PropVariant.FromString(appUserModelId);
            try
            {
                var hr = store.SetValue(ref key, ref pv);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                hr = store.Commit();
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                pv.Dispose();
            }

            persist = (IPersistFile)link;
            persist.Save(shortcutPath, true);
        }
        finally
        {
            if (persist != null) Marshal.FinalReleaseComObject(persist);
            if (store != null) Marshal.FinalReleaseComObject(store);
            if (link != null) Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey
    {
        public readonly Guid fmtid;
        public readonly uint pid;

        public PropertyKey(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant : IDisposable
    {
        [FieldOffset(0)]
        public ushort vt;

        [FieldOffset(8)]
        public IntPtr pwszVal;

        public static PropVariant FromString(string value)
        {
            // VT_LPWSTR = 31
            return new PropVariant { vt = 31, pwszVal = Marshal.StringToCoTaskMemUni(value) };
        }

        public void Dispose()
        {
            if (pwszVal != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pwszVal);
                pwszVal = IntPtr.Zero;
            }
        }
    }
}

