// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    /// <summary>
    /// Gcode file helper class.
    /// </summary>
    public static class GcodeHelper
    {
        /// <summary>
        /// Gets any thumbnails found in a gcode file.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> instance to the gcode file.</param>
        /// <returns>The thumbnails found in a gcode file.</returns>
        public static IEnumerable<GcodeThumbnail> GetThumbnails(TextReader reader)
        {
            string? line;
            var format = GcodeThumbnailFormat.Unknown;
            StringBuilder? capturedText = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("; thumbnail", StringComparison.InvariantCulture))
                {
                    var parts = line[11..].Split(" ");

                    switch (parts[1])
                    {
                        case "begin":
                            format = parts[0].ToUpperInvariant() switch
                            {
                                "" => GcodeThumbnailFormat.PNG,
                                "_JPG" => GcodeThumbnailFormat.JPG,
                                "_QOI" => GcodeThumbnailFormat.QOI,
                                _ => GcodeThumbnailFormat.Unknown,
                            };
                            capturedText = new StringBuilder();

                            break;

                        case "end":
                            if (capturedText != null)
                            {
                                yield return new GcodeThumbnail(format, capturedText.ToString());

                                capturedText = null;
                            }

                            break;
                    }
                }
                else
                {
                    capturedText?.Append(line[2..]);
                }
            }
        }

        /// <summary>
        /// Gets the best thumbnail available in a gcode file.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> instance to the gcode file.</param>
        /// <returns>The best thumbnail available in the gcode file.</returns>
        public static GcodeThumbnail? GetBestThumbnail(TextReader reader)
        {
            return GetThumbnails(reader)
                .Where(x => x.Format != GcodeThumbnailFormat.Unknown)
                .OrderByDescending(x => (int)x.Format)
                .ThenByDescending(x => x.Data.Length)
                .FirstOrDefault();
        }
    }
}
