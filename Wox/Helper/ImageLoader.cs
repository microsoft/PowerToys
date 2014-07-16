using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wox.Helper
{
    public class ImageLoader
    {
        private static readonly Dictionary<string, ImageSource> imageCache = new Dictionary<string, ImageSource>();
        private static object locker = new object();

        private static readonly List<string> imageExts = new List<string>
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".tiff",
            ".ico"
        };

        private static readonly List<string> selfExts = new List<string>
        {
            ".exe",
            ".lnk",
            ".ani",
            ".cur",
            ".sln",
            ".appref-ms"
        };

        private static ImageSource GetIcon(string fileName)
        {
            Icon icon = GetFileIcon(fileName);
            if (icon == null) icon = Icon.ExtractAssociatedIcon(fileName);

            if (icon != null)
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                    new Int32Rect(0, 0, icon.Width, icon.Height), BitmapSizeOptions.FromEmptyOptions());
            }

            return null;
        }

        public static ImageSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (imageCache.ContainsKey(path))
            {
                return imageCache[path];
            }

            ImageSource img = null;
            string ext = Path.GetExtension(path).ToLower();

            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                img = new BitmapImage(new Uri(path));
            }
            else if (selfExts.Contains(ext))
            {
                img = GetIcon(path);
            }
            else if (!string.IsNullOrEmpty(path) && imageExts.Contains(ext) && File.Exists(path))
            {
                img = new BitmapImage(new Uri(path));
            }


            if (img != null)
            {
                if (!imageCache.ContainsKey(path))
                {
                    lock (locker)
                    {
                        if (!imageCache.ContainsKey(path))
                        {
                            imageCache.Add(path, img);
                        }
                    }
                }
            }

            return img;
        }

        // http://blogs.msdn.com/b/oldnewthing/archive/2011/01/27/10120844.aspx
        private static Icon GetFileIcon(string name)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;

            IntPtr himl = SHGetFileInfo(name,
                FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (himl != IntPtr.Zero)
            {
                IntPtr hIcon = ImageList_GetIcon(himl, shfi.iIcon, ILD_NORMAL);
                var icon = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return icon;
            }

            return null;
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        private const int MAX_PATH = 256;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHITEMID
        {
            public ushort cb;
            [MarshalAs(UnmanagedType.LPArray)]
            public byte[] abID;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ITEMIDLIST
        {
            public SHITEMID mkid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public int lParam;
            public IntPtr iImage;
        }

        // Browsing for directory.
        private const uint BIF_RETURNONLYFSDIRS = 0x0001;
        private const uint BIF_DONTGOBELOWDOMAIN = 0x0002;
        private const uint BIF_STATUSTEXT = 0x0004;
        private const uint BIF_RETURNFSANCESTORS = 0x0008;
        private const uint BIF_EDITBOX = 0x0010;
        private const uint BIF_VALIDATE = 0x0020;
        private const uint BIF_NEWDIALOGSTYLE = 0x0040;
        private const uint BIF_USENEWUI = (BIF_NEWDIALOGSTYLE | BIF_EDITBOX);
        private const uint BIF_BROWSEINCLUDEURLS = 0x0080;
        private const uint BIF_BROWSEFORCOMPUTER = 0x1000;
        private const uint BIF_BROWSEFORPRINTER = 0x2000;
        private const uint BIF_BROWSEINCLUDEFILES = 0x4000;
        private const uint BIF_SHAREABLE = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public const int NAMESIZE = 80;
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NAMESIZE)]
            public string szTypeName;
        };

        private const uint SHGFI_ICON = 0x000000100; // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200; // get display name
        private const uint SHGFI_TYPENAME = 0x000000400; // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800; // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000; // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000; // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000; // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000; // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000; // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000; // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000; // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001; // get small icon
        private const uint SHGFI_OPENICON = 0x000000002; // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004; // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008; // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010; // use passed dwFileAttribute
        private const uint SHGFI_ADDOVERLAYS = 0x000000020; // apply the appropriate overlays
        private const uint SHGFI_OVERLAYINDEX = 0x000000040; // Get the index of the overlay

        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint ILD_NORMAL = 0x00000000;

        [DllImport("Shell32.dll")]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags
            );

        [DllImport("User32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);
    }

}
