// PlaintextPreviewSettings.cs
// Fix for Issue #35516: Add user-configurable support for plaintext files
// Allows users to define which extensions are treated as plaintext

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Peek.Common.Models
{
    /// <summary>
    /// Settings for plaintext file preview in Peek.
    /// </summary>
    public class PlaintextPreviewSettings
    {
        /// <summary>
        /// Default extensions always treated as plaintext.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultExtensions = new[]
        {
            ".txt", ".md", ".log", ".ini", ".cfg", ".conf", ".config",
            ".json", ".xml", ".yaml", ".yml", ".toml",
            ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".psd1",
            ".bat", ".cmd",
            ".gitignore", ".gitattributes", ".editorconfig",
            ".env", ".properties"
        };
        
        /// <summary>
        /// User-defined additional extensions to preview as plaintext.
        /// </summary>
        [JsonPropertyName("additionalExtensions")]
        public List<string> AdditionalExtensions { get; set; } = new();
        
        /// <summary>
        /// Maximum file size in bytes to preview (default 5MB).
        /// </summary>
        [JsonPropertyName("maxFileSizeBytes")]
        public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
        
        /// <summary>
        /// Whether to enable syntax highlighting.
        /// </summary>
        [JsonPropertyName("enableSyntaxHighlighting")]
        public bool EnableSyntaxHighlighting { get; set; } = true;
        
        /// <summary>
        /// Checks if an extension should be previewed as plaintext.
        /// </summary>
        public bool ShouldPreviewAsPlaintext(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            
            var ext = extension.StartsWith(".") ? extension : "." + extension;
            
            return DefaultExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)
                || AdditionalExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
    }
}
