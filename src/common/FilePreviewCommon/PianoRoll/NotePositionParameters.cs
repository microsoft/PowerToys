// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    internal sealed class NotePositionParameters
    {
        public required Tuple<double, double> Point1 { get; set; }

        public required Tuple<double, double> Point2 { get; set; }
    }
}
