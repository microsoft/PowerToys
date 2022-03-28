using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static PeekUI.Helpers.NativeModels;

namespace PeekUI.Helpers
{
    
    public static class FileTypeHelper
    {
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
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            PerceivedType perceived;
            Perceived flag;
            bool isPerceivedType = false;

            try
            {
                if (NativeMethods.AssocGetPerceivedType(extension, out perceived, out flag, IntPtr.Zero) == HResult.Ok)
                {
                    isPerceivedType = perceived == perceivedType;
                }
            }
            catch (Exception)
            {
                // TODO: AssocGetPerceivedType throws on some file types (json, ps1, exe, etc.)
                // Properly handle these
                return false;
            }

            return isPerceivedType;
        }
    }
}
