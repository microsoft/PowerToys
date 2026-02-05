// DockerfileHandler.cs
// Fix for Issue #32686: Add support for Dockerfile preview
// Registers Dockerfile as a recognized format for Monaco previewer

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    /// <summary>
    /// Handler for Dockerfile preview support.
    /// </summary>
    public static class DockerfileHandler
    {
        /// <summary>
        /// Dockerfile-related file names (case-insensitive).
        /// </summary>
        public static readonly IReadOnlyList<string> DockerfileNames = new[]
        {
            "Dockerfile",
            "Dockerfile.dev",
            "Dockerfile.prod",
            "Dockerfile.test",
            "Containerfile"  // Podman equivalent
        };
        
        /// <summary>
        /// File extensions associated with Docker.
        /// </summary>
        public static readonly IReadOnlyList<string> DockerExtensions = new[]
        {
            ".dockerfile",
            ".containerfile"
        };
        
        /// <summary>
        /// Checks if a file is a Dockerfile.
        /// </summary>
        public static bool IsDockerfile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);
            
            // Check by name (Dockerfile, Dockerfile.dev, etc.)
            foreach (var name in DockerfileNames)
            {
                if (fileName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check by extension
            foreach (var ext in DockerExtensions)
            {
                if (extension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the Monaco language ID for Dockerfile syntax highlighting.
        /// </summary>
        public static string GetLanguageId() => "dockerfile";
    }
}
