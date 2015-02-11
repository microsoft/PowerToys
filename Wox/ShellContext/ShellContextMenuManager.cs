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

            IntPtr ownerHwnd =IntPtr.Zero;
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

                        var menuItemCount = ShellAPI.GetMenuItemCount(contextMenu);
                        for (int k = 0; k < menuItemCount - 1; k++)
                        {
                            StringBuilder menuName = new StringBuilder(0x20);
                            ShellAPI.GetMenuString(contextMenu, i, menuName, 0x20, ShellAPI.MF_BYPOSITION);
                            Debug.WriteLine(menuName.Replace("&",""));
                            ShellAPI.DeleteMenu(contextMenu, i, ShellAPI.MF_BYPOSITION);
                        }
                    }
                }
            }

            Marshal.ReleaseComObject(Root);
        }
    }
}
