using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.ShellContext
{
    public class ShellAPI
    {
        #region API 导入


        public const int MAX_PATH = 260;
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const uint CMD_FIRST = 1;
        public const uint CMD_LAST = 30000;

        public const Int32 MF_BYPOSITION = 0x400;
        [DllImport("user32.dll")]
        public static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);
        [DllImport("user32.dll")]
        public static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder lpString, int nMaxCount, uint uFlag);


        [DllImport("shell32.dll")]
        public static extern Int32 SHGetDesktopFolder(out IntPtr ppshf);

        [DllImport("Shlwapi.Dll", CharSet = CharSet.Auto)]
        public static extern Int32 StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder pszBuf, int cchBuf);

        [DllImport("shell32.dll")]
        public static extern int SHGetSpecialFolderLocation(IntPtr handle, CSIDL nFolder, out IntPtr ppidl);

        [DllImport("shell32",
            EntryPoint = "SHGetFileInfo",
            ExactSpelling = false,
            CharSet = CharSet.Auto,
            SetLastError = true)]
        public static extern IntPtr SHGetFileInfo(
            IntPtr ppidl,
            FILE_ATTRIBUTE dwFileAttributes,
            ref SHFILEINFO sfi,
            int cbFileInfo,
            SHGFI uFlags);

        [DllImport("user32",
            SetLastError = true,
            CharSet = CharSet.Auto)]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll",
            ExactSpelling = true,
            CharSet = CharSet.Auto)]
        public static extern uint TrackPopupMenuEx(
            IntPtr hmenu,
            TPM flags,
            int x,
            int y,
            IntPtr hwnd,
            IntPtr lptpm);

        #endregion

        /// <summary>
        /// 获得桌面 Shell
        /// </summary>
        public static IShellFolder GetDesktopFolder(out IntPtr ppshf)
        {
            SHGetDesktopFolder(out ppshf);
            Object obj = Marshal.GetObjectForIUnknown(ppshf);
            return (IShellFolder)obj;
        }

        /// <summary>
        /// 获取显示名称
        /// </summary>
        public static string GetNameByIShell(IShellFolder Root, IntPtr pidlSub)
        {
            IntPtr strr = Marshal.AllocCoTaskMem(MAX_PATH * 2 + 4);
            Marshal.WriteInt32(strr, 0, 0);
            StringBuilder buf = new StringBuilder(MAX_PATH);
            Root.GetDisplayNameOf(pidlSub, SHGNO.INFOLDER, strr);
            ShellAPI.StrRetToBuf(strr, pidlSub, buf, MAX_PATH);
            return buf.ToString();
        }

        /// <summary>
        /// 根据 PIDL 获取显示名称
        /// </summary>
        public static string GetNameByPIDL(IntPtr pidl)
        {
            SHFILEINFO info = new SHFILEINFO();
            ShellAPI.SHGetFileInfo(pidl, 0, ref info, Marshal.SizeOf(typeof(SHFILEINFO)),
                SHGFI.PIDL | SHGFI.DISPLAYNAME | SHGFI.TYPENAME);
            return info.szDisplayName;
        }

        public static PIDLShellFolder GetPIDLAndParentIShellFolder(string path)
        {

            if (Directory.Exists(path))
            {
                return GetPIDLAndParentIshellFolderForFolder(path);
            }
            else if (File.Exists(path))
            {
                return GetPIDLAndParentIshellFolderForFile(path);
            }

            return null;
        }

        private static PIDLShellFolder GetPIDLAndParentIshellFolderForFolder(string folderPath)
        {
            return null;
        }

        /// <summary>
        /// Get PIDL and parent shellfolder for given file path 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static PIDLShellFolder GetPIDLAndParentIshellFolderForFile(string filePath)
        {
            //get desktopPtr first
            IntPtr desktopPtr;
            IShellFolder desktop = GetDesktopFolder(out desktopPtr);

            string fileName = Path.GetFileName(filePath);
            IShellFolder parentShellFolder;
            string FolderPath = Directory.GetParent(filePath).FullName;
            IntPtr Pidl = IntPtr.Zero;
            uint i, j = 0;
            desktop.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, FolderPath, out i, out Pidl, ref j);
            desktop.BindToObject(Pidl, IntPtr.Zero, ref Guids.IID_IShellFolder, out parentShellFolder);
            Marshal.ReleaseComObject(desktop);

            IEnumIDList fileEnum = null;
            IEnumIDList folderEnum = null;
            IntPtr fileEnumPtr = IntPtr.Zero;
            IntPtr folderEnumPtr = IntPtr.Zero;
            IntPtr pidlSub;
            int celtFetched;

            if (parentShellFolder.EnumObjects(IntPtr.Zero, SHCONTF.NONFOLDERS | SHCONTF.INCLUDEHIDDEN, out folderEnumPtr) == ShellAPI.S_OK)
            {
                folderEnum = (IEnumIDList)Marshal.GetObjectForIUnknown(folderEnumPtr);
                while (folderEnum.Next(1, out pidlSub, out celtFetched) == 0 && celtFetched == ShellAPI.S_FALSE)
                {
                    string name = ShellAPI.GetNameByPIDL(pidlSub);
                    if (name == fileName)
                    {
                        PIDLShellFolder ps = new PIDLShellFolder { PIDL = pidlSub, ShellFolder = parentShellFolder };
                        Marshal.ReleaseComObject(parentShellFolder);
                        return ps;
                    }
                }
            }

            Marshal.ReleaseComObject(parentShellFolder);
            return null;
        }

    }

    public class PIDLShellFolder
    {
        public IShellFolder ShellFolder { get; set; }
        public IntPtr PIDL { get; set; }
    }
}
