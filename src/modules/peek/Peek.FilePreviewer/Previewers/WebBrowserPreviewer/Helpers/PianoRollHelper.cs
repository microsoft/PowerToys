// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi;
using Microsoft.PowerToys.FilePreviewCommon;

namespace Peek.FilePreviewer.Previewers
{
    public class PianoRollHelper
    {
        public static readonly HashSet<string> SupportedPianoRollFileTypes = new HashSet<string>()
        {
            ".mid",
            ".midi",
        };

        public static string PreviewTempFile(string filePath, string tempFolder)
        {
            string midiSvg = Microsoft.PowerToys.FilePreviewCommon.PianoRoll.PianoRollHelper.MidiSvg(filePath);

            string svgPath = tempFolder + "\\" + Guid.NewGuid().ToString() + ".svg";
            File.WriteAllText(svgPath, midiSvg);
            return svgPath;
        }
    }
}
