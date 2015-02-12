using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Wox.ShellContext
{
    public class ShellContextMenuManager
    {
        public void GetContextMenus(string path)
        {
            IntPtr desktopPtr;
            IShellFolder desktop = ShellAPI.GetDesktopFolder(out desktopPtr);

            IntPtr ownerHwnd = IntPtr.Zero;
            IShellFolder Root;
            string FolderPath = Directory.GetParent(path).FullName;
            IntPtr Pidl = IntPtr.Zero;
            IShellFolder parent;
            uint i, j = 0;
            desktop.ParseDisplayName(ownerHwnd, IntPtr.Zero, FolderPath, out i, out Pidl, ref j);
            desktop.BindToObject(Pidl, IntPtr.Zero, ref Guids.IID_IShellFolder, out Root);
            Marshal.ReleaseComObject(desktop);

            IEnumIDList fileEnum = null;
            IEnumIDList folderEnum = null;
            IntPtr fileEnumPtr = IntPtr.Zero;
            IntPtr folderEnumPtr = IntPtr.Zero;
            IntPtr pidlSub;
            int celtFetched;

            if (Root.EnumObjects(ownerHwnd, SHCONTF.FOLDERS | SHCONTF.INCLUDEHIDDEN, out fileEnumPtr) == ShellAPI.S_OK)
            {
                fileEnum = (IEnumIDList)Marshal.GetObjectForIUnknown(fileEnumPtr);
                while (fileEnum.Next(1, out pidlSub, out celtFetched) == 0 && celtFetched == ShellAPI.S_FALSE)
                {
                    string name = ShellAPI.GetNameByPIDL(pidlSub);
                }
            }

            if (Root.EnumObjects(ownerHwnd, SHCONTF.NONFOLDERS | SHCONTF.INCLUDEHIDDEN, out folderEnumPtr) == ShellAPI.S_OK)
            {
                folderEnum = (IEnumIDList)Marshal.GetObjectForIUnknown(folderEnumPtr);
                while (folderEnum.Next(1, out pidlSub, out celtFetched) == 0 && celtFetched == ShellAPI.S_FALSE)
                {
                    string name = ShellAPI.GetNameByPIDL(pidlSub);
                    if (Path.GetFileName(path) == name)
                    {
                        IntPtr PIDL = pidlSub;
                        IShellFolder IParent = Root;
                        IntPtr[] pidls = new IntPtr[1];
                        pidls[0] = PIDL;

                        //get IContextMenu interface
                        IntPtr iContextMenuPtr = IntPtr.Zero;
                        iContextMenuPtr = IParent.GetUIObjectOf(IntPtr.Zero, (uint)pidls.Length,
                            pidls, ref Guids.IID_IContextMenu, out iContextMenuPtr);
                        IContextMenu iContextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(iContextMenuPtr);

                        IntPtr contextMenu = ShellAPI.CreatePopupMenu();
                        iContextMenu.QueryContextMenu(contextMenu, 0, ShellAPI.CMD_FIRST, ShellAPI.CMD_LAST, CMF.NORMAL | CMF.EXPLORE);
                        ParseMenu(contextMenu);
                    }
                }
            }

            Marshal.ReleaseComObject(Root);
        }

        private void ParseMenu(IntPtr contextMenu)
        {
            var menuItemCount = ShellAPI.GetMenuItemCount(contextMenu);
            for (uint k = 0; k < menuItemCount - 1; k++)
            {
                StringBuilder menuName = new StringBuilder(320);
                ShellAPI.GetMenuString(contextMenu, k, menuName, 320, ShellAPI.MF_BYPOSITION);
                Debug.WriteLine(menuName.Replace("&", ""));

                //https://msdn.microsoft.com/en-us/library/windows/desktop/ms647578(v=vs.85).aspx
                ShellAPI.MENUITEMINFO menuiteminfo_t;
                int MIIM_SUBMENU = 0x00000004;
                int MIIM_STRING = 0x00000040;
                int MIIM_FTYPE = 0x00000100;
                menuiteminfo_t = new ShellAPI.MENUITEMINFO();
                menuiteminfo_t.fMask = MIIM_SUBMENU | MIIM_STRING | MIIM_FTYPE;
                menuiteminfo_t.dwTypeData = new string('\0', 320);
                menuiteminfo_t.cch = menuiteminfo_t.dwTypeData.Length - 1;
                bool result = ShellAPI.GetMenuItemInfo(new HandleRef(null, contextMenu), (int)k, true, menuiteminfo_t);
                if (menuiteminfo_t.hSubMenu != IntPtr.Zero)
                {
                    ParseMenu(menuiteminfo_t.hSubMenu);
                }
                ShellAPI.DeleteMenu(contextMenu, k, ShellAPI.MF_BYPOSITION);
            }
        }
    }
}
