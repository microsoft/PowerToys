// PeekSymlinkResolver.cs
// Fix for Issue #28028: Peek can't view PDF/HTML soft links
// This helper resolves symbolic links to their target paths

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Peek.Common.Helpers
{
    /// <summary>
    /// Resolves symbolic links and junction points to their target paths.
    /// </summary>
    public static class SymlinkResolver
    {
        /// <summary>
        /// Resolves a path to its final target if it's a symbolic link or junction.
        /// </summary>
        /// <param name="path">The path to resolve.</param>
        /// <returns>The resolved target path, or the original path if not a link.</returns>
        public static string ResolveSymlink(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            try
            {
                var fileInfo = new FileInfo(path);
                
                // Check if it's a symbolic link
                if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // Get the target of the symbolic link
                    var target = fileInfo.LinkTarget;
                    if (!string.IsNullOrEmpty(target))
                    {
                        // If target is relative, make it absolute
                        if (!Path.IsPathRooted(target))
                        {
                            var directory = Path.GetDirectoryName(path);
                            target = Path.GetFullPath(Path.Combine(directory ?? string.Empty, target));
                        }
                        
                        return target;
                    }
                }
                
                return path;
            }
            catch (Exception)
            {
                // If resolution fails, return the original path
                return path;
            }
        }
        
        /// <summary>
        /// Checks if a path is a symbolic link or junction point.
        /// </summary>
        public static bool IsSymlink(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }
            
            try
            {
                var attributes = File.GetAttributes(path);
                return attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch
            {
                return false;
            }
        }
    }
}
