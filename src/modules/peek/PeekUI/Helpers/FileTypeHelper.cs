using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PeekUI.Helpers
{
    
    public static class FileTypeHelper
    {
        internal enum PerceivedType
        {
            Folder = -1,
            Unknown = 0,
            Image = 2,
            Video = 4,
            Document = 6,
        }

        internal enum PerceivedFlag
        {
            ///<summary>No perceived type was found (PERCEIVED_TYPE_UNSPECIFIED).</summary>
            Undefined = 0x0000,

            ///<summary>The perceived type was determined through an association in the registry.</summary>
            Softcoded = 0x0001,

            ///<summary>The perceived type is inherently known to Windows.</summary>
            Hardcoded = 0x0002,

            ///<summary>The perceived type was determined through a codec provided with Windows.</summary>
            NativeSupport = 0x0004,

            ///<summary>The perceived type is supported by the GDI+ library.</summary>
            GdiPlus = 0x0010,

            ///<summary>The perceived type is supported by the Windows Media SDK.</summary>
            WMSDK = 0x0020,

            ///<summary>The perceived type is supported by Windows compressed folders.</summary>
            ZipFolder = 0x0040
        }

        internal enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int)0x80070057),
            OutOfMemory = unchecked((int)0x8007000E),
            NoInterface = unchecked((int)0x80004002),
            Fail = unchecked((int)0x80004005),
            ExtractionFailed = unchecked((int)0x8004B200),
            ElementNotFound = unchecked((int)0x80070490),
            TypeElementNotFound = unchecked((int)0x8002802B),
            NoObject = unchecked((int)0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int)0x800704C7),
            ResourceInUse = unchecked((int)0x800700AA),
            AccessDenied = unchecked((int)0x80030005),
        }

        public static bool IsSupportedImage(string extension) => extension switch
        {
            ".bmp" => true,
            ".gif" => true,
            ".jpg" => true,
            ".jfif" => true,
            ".jfi" => true,
            ".jif" => true,
            ".jpeg" => true,
            ".jpe" => true,
            ".png" => true,
            ".tif" => true,
            ".tiff" => true,
            _ => false,
        };

        public static bool IsMedia(string extension)
        {
            return IsImage(extension) || IsVideo(extension);
        }

        public static bool IsImage(string extension)
        {
            return IsPerceivedType(extension, PerceivedType.Image);
        }

        public static bool IsVideo(string extension)
        {
            return IsPerceivedType(extension, PerceivedType.Video);
        }

        public static bool IsDocument(string extension)
        {
            return IsPerceivedType(extension, PerceivedType.Document);
        }

        internal static bool IsPerceivedType(string extension, PerceivedType perceivedType)
        {
            PerceivedType perceived;
            PerceivedFlag flag;
            bool isPerceivedType = false;

            if (AssocGetPerceivedType(extension, out perceived, out flag, IntPtr.Zero) == HResult.Ok)
            {
                isPerceivedType = perceived == perceivedType;
            };

            return isPerceivedType;
        }


        [DllImport("Shlwapi.dll", ExactSpelling = true, PreserveSig = false)]
        static extern HResult AssocGetPerceivedType(
            [MarshalAs(UnmanagedType.LPWStr)] string extension,
            out PerceivedType perceivedType,
            out PerceivedFlag perceivedFlags,
            IntPtr ptrType
        );
    }
}
