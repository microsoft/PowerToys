// SupportedFormatsRegistry.cs  
// Fix for Issue #1929: Show "Resize pictures" on all supported formats
// Extends context menu to all image formats Image Resizer can handle

using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageResizer.Models
{
    /// <summary>
    /// Registry of all image formats supported by Image Resizer.
    /// </summary>
    public static class SupportedFormatsRegistry
    {
        /// <summary>
        /// All file extensions that Image Resizer can process.
        /// </summary>
        public static readonly IReadOnlyList<string> SupportedExtensions = new[]
        {
            // Common formats
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
            // Web formats
            ".webp", ".svg", ".ico",
            // RAW formats (read-only, converts to supported output)
            ".raw", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2",
            // HDR formats
            ".hdr", ".exr",
            // Other formats
            ".heic", ".heif", ".avif", ".jxl",
            // Less common but supported
            ".pbm", ".pgm", ".ppm", ".pnm", ".pcx", ".tga"
        };
        
        /// <summary>
        /// Checks if a file extension is supported for resizing.
        /// </summary>
        public static bool IsSupported(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            
            var ext = extension.StartsWith(".") ? extension : "." + extension;
            return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Gets the registry format string for shell integration.
        /// </summary>
        public static string GetShellAssociations()
        {
            return string.Join(";", SupportedExtensions.Select(e => $"*{e}"));
        }
    }
}
