// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using ColorPicker.Models;

namespace ColorPicker.Common
{
    /// <summary>
    /// Responsible for the serialization of a collection of colors for the purpose of the export functionality.
    /// </summary>
    public static class ColorCollectionSerializer
    {
        private class ColorCollectionOutput
        {
            public ICollection<string> Colors { get; set; }
        }

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions();

        static ColorCollectionSerializer()
        {
            Options.WriteIndented = true;
        }

        /// <summary>
        /// Saves a collection of colors to a JSON file.  Will overwrite file if it already exists.  The structure of the JSON file is as follows:
        ///
        /// <code>
        /// {
        ///     "Colors": [
        ///         "#AABBCC",
        ///         "#001122",
        ///         ...
        ///     ]
        /// }
        /// </code>
        ///
        /// Where "#AABBCC" is the string representation of the color as determined by the ColorFormatModel.
        /// </summary>
        ///
        /// <param name="fileName">The name of the file to save to</param>
        /// <param name="colors">The collection of colors to export</param>
        /// <param name="formatModel">Model to format the exported colors in</param>
        public static void Save(string fileName, ICollection<Color> colors, ColorFormatModel formatModel)
        {
            if (colors == null)
            {
                throw new NullReferenceException("Color list to serialize must be non-null");
            }

            if (formatModel == null)
            {
                throw new NullReferenceException("ColorFormatModel must be non-null");
            }

            var output = new ColorCollectionOutput();
            output.Colors = new List<string>();

            foreach (Color color in colors)
            {
                output.Colors.Add(formatModel.Convert(color));
            }

            File.WriteAllText(fileName, JsonSerializer.Serialize(output, Options));
        }
    }
}
