// XmlEncodingDetector.cs
// Fix for Issue #30515: Preview window doesn't render XML without BOM
// Detects XML encoding from declaration when BOM is absent

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    /// <summary>
    /// Detects encoding for XML files that may lack a BOM.
    /// </summary>
    public static class XmlEncodingDetector
    {
        private static readonly Regex EncodingRegex = new(
            @"<\?xml[^>]+encoding\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        /// <summary>
        /// Detects the encoding of an XML file.
        /// </summary>
        /// <param name="filePath">Path to the XML file.</param>
        /// <returns>The detected encoding, or UTF-8 as default.</returns>
        public static Encoding DetectEncoding(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Encoding.UTF8;
            }
            
            try
            {
                // Read first bytes to check for BOM
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bom = new byte[4];
                stream.Read(bom, 0, 4);
                
                // Check for BOM
                if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return Encoding.UTF8;
                if (bom[0] == 0xFF && bom[1] == 0xFE)
                    return Encoding.Unicode;
                if (bom[0] == 0xFE && bom[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
                
                // No BOM - try to detect from XML declaration
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var header = reader.ReadLine();
                
                if (!string.IsNullOrEmpty(header))
                {
                    var match = EncodingRegex.Match(header);
                    if (match.Success)
                    {
                        var encodingName = match.Groups[1].Value;
                        try
                        {
                            return Encoding.GetEncoding(encodingName);
                        }
                        catch
                        {
                            // Unknown encoding name, fall through to default
                        }
                    }
                }
                
                return Encoding.UTF8;
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }
}
