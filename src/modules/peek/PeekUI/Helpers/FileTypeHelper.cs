// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PeekUI.Native;
using static PeekUI.Native.NativeModels;

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
