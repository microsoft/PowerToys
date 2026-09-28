// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PowerToys.ContextMenuManager.MenuCapture
{
    // Builds the real Explorer context menu for a sample target and prints it as JSON:
    // [{ "text", "shortcut", "verb", "disabled", "separator", "icon": { "w", "h", "bgra" }, "children": [...] }]
    //
    // Runs as its own process on purpose: building the menu loads every registered shell
    // extension, and a misbehaving third-party DLL must not be able to take the Settings UI down.
    // A fresh process also has no cached handlers, so the result reflects the registry right now.
    internal static class Program
    {
        private const int MaxDepth = 4;
        private const uint FirstCommandId = 1;

        [STAThread]
        private static int Main(string[] args)
        {
            string target = args.Length == 2 && args[0] == "--target" ? args[1] : null;
            if (target == null)
            {
                Console.Error.WriteLine("Usage: --target desktop|background|folder|file|drive");
                return 2;
            }

            IntPtr owner = NativeMethods.CreateWindowEx(0, "Static", string.Empty, 0, 0, 0, 0, 0, NativeMethods.HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            IContextMenu menu = CreateContextMenu(target, owner);
            if (menu == null)
            {
                Console.Error.WriteLine($"Could not create a context menu for '{target}'.");
                return 1;
            }

            IntPtr hmenu = NativeMethods.CreatePopupMenu();
            try
            {
                int hr = menu.QueryContextMenu(hmenu, 0, FirstCommandId, 0x7FFF, NativeMethods.CMF_NORMAL);
                if (hr < 0)
                {
                    Console.Error.WriteLine($"QueryContextMenu failed: 0x{hr:X8}");
                    return 1;
                }

                using var stdout = Console.OpenStandardOutput();
                using var writer = new Utf8JsonWriter(stdout);
                WriteMenu(writer, hmenu, menu, 0);
                writer.Flush();
                return 0;
            }
            finally
            {
                NativeMethods.DestroyMenu(hmenu);
            }
        }

        private static IContextMenu CreateContextMenu(string target, IntPtr owner)
        {
            string sampleRoot = Path.Combine(Path.GetTempPath(), "PowerToys", "ContextMenuManagerPreview");
            string sampleFolder = Path.Combine(sampleRoot, "Sample folder");
            string sampleFile = Path.Combine(sampleRoot, "Sample file.txt");
            Directory.CreateDirectory(sampleFolder);
            if (!File.Exists(sampleFile))
            {
                File.WriteAllText(sampleFile, string.Empty);
            }

            switch (target)
            {
                case "desktop":
                    return BackgroundMenu(null, owner);
                case "background":
                    return BackgroundMenu(sampleRoot, owner);
                case "folder":
                    return ItemMenu(sampleFolder, owner);
                case "file":
                    return ItemMenu(sampleFile, owner);
                case "drive":
                    return ItemMenu(Path.GetPathRoot(Environment.SystemDirectory), owner);
                default:
                    return null;
            }
        }

        // Right-click on an item: the parent folder hands out the item's menu.
        private static IContextMenu ItemMenu(string path, IntPtr owner)
        {
            if (NativeMethods.SHParseDisplayName(path, IntPtr.Zero, out IntPtr pidl, 0, out _) < 0)
            {
                return null;
            }

            try
            {
                Guid shellFolderId = NativeMethods.IID_IShellFolder;
                Guid contextMenuId = NativeMethods.IID_IContextMenu;
                if (NativeMethods.SHBindToParent(pidl, ref shellFolderId, out IntPtr parentPtr, out IntPtr childPidl) < 0)
                {
                    return null;
                }

                var parent = (IShellFolder)Marshal.GetObjectForIUnknown(parentPtr);
                Marshal.Release(parentPtr);
                return parent.GetUIObjectOf(owner, 1, new[] { childPidl }, ref contextMenuId, IntPtr.Zero, out IntPtr menuPtr) < 0
                    ? null
                    : ToContextMenu(menuPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        // Right-click on empty space: the folder's own view object. null folder = the desktop.
        private static IContextMenu BackgroundMenu(string folderPath, IntPtr owner)
        {
            if (NativeMethods.SHGetDesktopFolder(out IntPtr desktopPtr) < 0)
            {
                return null;
            }

            var folder = (IShellFolder)Marshal.GetObjectForIUnknown(desktopPtr);
            Marshal.Release(desktopPtr);

            if (folderPath != null)
            {
                if (NativeMethods.SHParseDisplayName(folderPath, IntPtr.Zero, out IntPtr pidl, 0, out _) < 0)
                {
                    return null;
                }

                try
                {
                    Guid shellFolderId = NativeMethods.IID_IShellFolder;
                    if (folder.BindToObject(pidl, IntPtr.Zero, ref shellFolderId, out IntPtr folderPtr) < 0)
                    {
                        return null;
                    }

                    folder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                    Marshal.Release(folderPtr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
            }

            Guid contextMenuId = NativeMethods.IID_IContextMenu;
            return folder.CreateViewObject(owner, ref contextMenuId, out IntPtr menuPtr) < 0 ? null : ToContextMenu(menuPtr);
        }

        private static IContextMenu ToContextMenu(IntPtr ptr)
        {
            var menu = (IContextMenu)Marshal.GetObjectForIUnknown(ptr);
            Marshal.Release(ptr);
            return menu;
        }

        private static void WriteMenu(Utf8JsonWriter writer, IntPtr hmenu, IContextMenu menu, int depth)
        {
            writer.WriteStartArray();
            int count = NativeMethods.GetMenuItemCount(hmenu);
            for (uint i = 0; i < count; i++)
            {
                try
                {
                    WriteItem(writer, hmenu, i, menu, depth);
                }
                catch (Exception ex)
                {
                    // One broken item must not lose the rest of the menu.
                    Console.Error.WriteLine($"Item {i}: {ex.Message}");
                }
            }

            writer.WriteEndArray();
        }

        private static void WriteItem(Utf8JsonWriter writer, IntPtr hmenu, uint position, IContextMenu menu, int depth)
        {
            var info = new NativeMethods.MENUITEMINFO
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MENUITEMINFO>(),
                fMask = NativeMethods.MIIM_FTYPE | NativeMethods.MIIM_STATE | NativeMethods.MIIM_ID | NativeMethods.MIIM_SUBMENU
                    | NativeMethods.MIIM_CHECKMARKS | NativeMethods.MIIM_BITMAP | NativeMethods.MIIM_STRING,
            };

            // First call reports the text length, second one fills the buffer.
            if (!NativeMethods.GetMenuItemInfo(hmenu, position, true, ref info))
            {
                return;
            }

            if ((info.fType & NativeMethods.MFT_SEPARATOR) != 0)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("separator", true);
                writer.WriteEndObject();
                return;
            }

            string text = string.Empty;
            if (info.cch > 0)
            {
                info.cch++;
                info.dwTypeData = Marshal.AllocHGlobal((int)info.cch * 2);
                try
                {
                    if (NativeMethods.GetMenuItemInfo(hmenu, position, true, ref info))
                    {
                        text = Marshal.PtrToStringUni(info.dwTypeData) ?? string.Empty;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(info.dwTypeData);
                    info.dwTypeData = IntPtr.Zero;
                }
            }

            string shortcut = null;
            int tab = text.IndexOf('\t');
            if (tab >= 0)
            {
                shortcut = text.Substring(tab + 1);
                text = text.Substring(0, tab);
            }

            text = text.Replace("&&", "\u0000").Replace("&", string.Empty).Replace("\u0000", "&");
            string verb = info.hSubMenu == IntPtr.Zero ? GetVerb(menu, info.wID) : null;

            writer.WriteStartObject();
            writer.WriteString("text", string.IsNullOrEmpty(text) ? verb ?? string.Empty : text);
            if (shortcut != null)
            {
                writer.WriteString("shortcut", shortcut);
            }

            if (verb != null)
            {
                writer.WriteString("verb", verb);
            }

            if ((info.fState & NativeMethods.MFS_DISABLED) != 0)
            {
                writer.WriteBoolean("disabled", true);
            }

            // Modern menus put the icon in hbmpItem; older handlers still use the unchecked-state bitmap.
            WriteIcon(writer, info.hbmpItem);
            if (info.hbmpItem == IntPtr.Zero)
            {
                WriteIcon(writer, info.hbmpUnchecked);
            }

            if (info.hSubMenu != IntPtr.Zero && depth < MaxDepth)
            {
                // Many handlers (Send to, Open with, TortoiseGit) fill submenus only when they open.
                if (menu is IContextMenu3 menu3)
                {
                    menu3.HandleMenuMsg2(NativeMethods.WM_INITMENUPOPUP, info.hSubMenu, new IntPtr(position), out _);
                }
                else if (menu is IContextMenu2 menu2)
                {
                    menu2.HandleMenuMsg(NativeMethods.WM_INITMENUPOPUP, info.hSubMenu, new IntPtr(position));
                }

                writer.WritePropertyName("children");
                WriteMenu(writer, info.hSubMenu, menu, depth + 1);
            }

            writer.WriteEndObject();
        }

        private static string GetVerb(IContextMenu menu, uint id)
        {
            if (id < FirstCommandId || id > 0x7FFF)
            {
                return null;
            }

            const int chars = 256;
            IntPtr buffer = Marshal.AllocHGlobal(chars * 2);
            try
            {
                Marshal.WriteInt16(buffer, 0);
                return menu.GetCommandString(new UIntPtr(id - FirstCommandId), NativeMethods.GCS_VERBW, IntPtr.Zero, buffer, chars) >= 0
                    ? Marshal.PtrToStringUni(buffer) is { Length: > 0 } verb ? verb : null
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // Writes a 32bpp top-down BGRA copy of a menu bitmap. HBMMENU_* system glyphs (small
        // sentinel values, and -1 for owner-drawn) carry no pixels and are skipped.
        private static void WriteIcon(Utf8JsonWriter writer, IntPtr bitmap)
        {
            if (bitmap == IntPtr.Zero || (long)bitmap is >= -1 and <= 11)
            {
                return;
            }

            if (NativeMethods.GetObject(bitmap, Marshal.SizeOf<NativeMethods.BITMAP>(), out NativeMethods.BITMAP bm) == 0 || bm.bmWidth <= 0 || bm.bmHeight <= 0 || bm.bmWidth > 256 || bm.bmHeight > 256)
            {
                return;
            }

            int width = bm.bmWidth;
            int height = Math.Abs(bm.bmHeight);
            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
            };

            byte[] pixels = new byte[width * height * 4];
            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                if (NativeMethods.GetDIBits(hdc, bitmap, 0, (uint)height, pixels, ref header, NativeMethods.DIB_RGB_COLORS) == 0)
                {
                    return;
                }
            }
            finally
            {
                _ = NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
            }

            // Bitmaps without an alpha channel come back fully transparent; treat them as opaque.
            bool hasAlpha = false;
            for (int i = 3; i < pixels.Length && !hasAlpha; i += 4)
            {
                hasAlpha = pixels[i] != 0;
            }

            if (!hasAlpha)
            {
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    pixels[i] = 255;
                }
            }

            writer.WriteStartObject("icon");
            writer.WriteNumber("w", width);
            writer.WriteNumber("h", height);
            writer.WriteBase64String("bgra", pixels);
            writer.WriteEndObject();
        }
    }
}
