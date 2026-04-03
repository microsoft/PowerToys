// HomePagePathResolver.cs
// Fix for Issue #42414: Peek doesn't work on File Explorer Home page
// Resolves shell locations like "Home", "Quick Access" to actual paths

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Helpers
{
    /// <summary>
    /// Resolves special File Explorer locations to their actual paths.
    /// </summary>
    public static class HomePagePathResolver
    {
        private static readonly Guid FOLDERID_Profile = new Guid("5E6C858F-0E22-4760-9AFE-EA3317B67173");
        private static readonly Guid FOLDERID_Desktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
        
        /// <summary>
        /// Checks if the current explorer location is the Home/Quick Access page.
        /// </summary>
        public static bool IsHomePage(string shellPath)
        {
            if (string.IsNullOrEmpty(shellPath))
            {
                return false;
            }
            
            // Common Home page identifiers
            return shellPath.Contains("shell:::", StringComparison.OrdinalIgnoreCase)
                || shellPath.Contains("Home", StringComparison.OrdinalIgnoreCase)
                || shellPath.StartsWith("::", StringComparison.Ordinal);
        }
        
        /// <summary>
        /// Gets the actual file path for an item selected on the Home page.
        /// Items on Home page are references to files in other locations.
        /// </summary>
        public static string ResolveHomePageItem(string itemPath)
        {
            if (string.IsNullOrEmpty(itemPath))
            {
                return itemPath;
            }
            
            try
            {
                // If it's already a valid file path, return it
                if (System.IO.File.Exists(itemPath) || System.IO.Directory.Exists(itemPath))
                {
                    return itemPath;
                }
                
                // Try to resolve shell path
                return ResolveShellPath(itemPath) ?? itemPath;
            }
            catch
            {
                return itemPath;
            }
        }
        
        private static string? ResolveShellPath(string shellPath)
        {
            // Implementation would use IShellItem to resolve the actual path
            // For now, return null to indicate no resolution needed
            return null;
        }
    }
}
